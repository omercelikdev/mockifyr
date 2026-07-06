using Mockifyr.Core;

namespace Mockifyr.Server;

/// <summary>
/// The reconcile step shared by the change-feed reloaders (G16e/G16f): on a change announced by another
/// instance, reload a tenant from the mappings loaders and bring the in-memory store into line —
/// upserting what's persisted, then pruning what's gone — so a stub created (or deleted) elsewhere is
/// served (or stopped) here without a restart. Upsert precedes prune so there is no empty window in
/// which a live request could miss an existing match.
/// </summary>
internal static class ChangeFeedReconciler
{
    public static void Reload(IStubStore store, IEnumerable<IMappingsLoader> loaders)
    {
        var tenant = TenantId.Default;
        var loaded = loaders.SelectMany(loader => loader.Load(tenant)).ToList();
        var loadedIds = loaded.Select(stub => stub.Id).ToHashSet();

        foreach (var stub in loaded)
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
