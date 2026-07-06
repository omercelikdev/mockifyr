using Mockifyr.Core;

namespace Mockifyr.Server;

/// <summary>
/// The reconcile step shared by the change-feed reloaders (G16e/G16f): on a change announced by another
/// instance, reload the persisted mappings and bring the in-memory store into line — upserting what's
/// persisted, then pruning what's gone — so a stub created (or deleted) elsewhere is served (or stopped)
/// here without a restart. Reconciliation spans <em>every</em> tenant (G16g): a loader that implements
/// <see cref="IMultiTenantMappingsLoader"/> contributes all tenants' stubs, others contribute the default
/// tenant; every tenant present in the reload <em>or</em> currently in the store is reconciled, so a
/// tenant whose last stub was deleted elsewhere is pruned too. Upsert precedes prune per tenant so there
/// is no empty window in which a live request could miss an existing match.
/// </summary>
internal static class ChangeFeedReconciler
{
    public static void Reload(IStubStore store, IEnumerable<IMappingsLoader> loaders)
    {
        // Collect the persisted stubs across all tenants: multi-tenant loaders enumerate every tenant;
        // single-tenant loaders (e.g. a mappings directory) contribute the default tenant only.
        var loaded = new List<StubMapping>();
        foreach (var loader in loaders)
        {
            loaded.AddRange(loader is IMultiTenantMappingsLoader multi
                ? multi.LoadAllTenants()
                : loader.Load(TenantId.Default));
        }

        var loadedByTenant = loaded.GroupBy(stub => stub.TenantId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<StubMapping>)[.. group]);

        // Reconcile every tenant that appears in the reload or currently exists in the store — the latter
        // so a tenant emptied elsewhere is pruned here rather than left stale.
        var tenants = new HashSet<TenantId>(loadedByTenant.Keys);
        tenants.UnionWith(store.GetTenants());

        foreach (var tenant in tenants)
        {
            var tenantStubs = loadedByTenant.GetValueOrDefault(tenant, []);
            var loadedIds = tenantStubs.Select(stub => stub.Id).ToHashSet();

            foreach (var stub in tenantStubs)
            {
                store.Put(stub);
            }

            foreach (var existing in store.GetStubs(tenant).ToList())
            {
                if (!loadedIds.Contains(existing.Id))
                {
                    store.Remove(tenant, existing.Id);
                }
            }
        }
    }
}
