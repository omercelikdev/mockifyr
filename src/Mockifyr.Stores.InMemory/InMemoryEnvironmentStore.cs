using System.Collections.Concurrent;
using Mockifyr.Core;

namespace Mockifyr.Stores.InMemory;

/// <summary>
/// Tenant-scoped in-memory environment store (G17). Doubles as the serve-path
/// <see cref="IEnvironmentResolver"/>, so resolution reads the same live state the admin API writes —
/// changing a key's active value takes effect on the next request with no stub re-save, which is the
/// whole point of issue #165.
/// <para>
/// Keys are held per tenant in separate dictionaries. There is no shared/global bucket and no
/// fallback to the default tenant: a tenant that has defined nothing resolves nothing (issue #166).
/// </para>
/// </summary>
public sealed class InMemoryEnvironmentStore : IEnvironmentStore, IEnvironmentResolver
{
    private readonly ConcurrentDictionary<TenantId, ConcurrentDictionary<string, EnvironmentKey>> _byTenant = new();

    /// <inheritdoc />
    public IReadOnlyList<EnvironmentKey> GetKeys(TenantId tenant) =>
        _byTenant.TryGetValue(tenant, out var keys)
            ? [.. keys.Values.OrderBy(k => k.Key, StringComparer.Ordinal)]
            : [];

    /// <inheritdoc />
    public IReadOnlyCollection<TenantId> GetTenants() =>
        [.. _byTenant.Where(entry => !entry.Value.IsEmpty).Select(entry => entry.Key)];

    /// <inheritdoc />
    public void Put(TenantId tenant, EnvironmentKey key) =>
        _byTenant.GetOrAdd(tenant, static _ => new ConcurrentDictionary<string, EnvironmentKey>(StringComparer.Ordinal))[key.Key] = key;

    /// <inheritdoc />
    public bool Remove(TenantId tenant, string key) =>
        _byTenant.TryGetValue(tenant, out var keys) && keys.TryRemove(key, out _);

    /// <inheritdoc />
    public void Clear(TenantId tenant) => _byTenant.TryRemove(tenant, out _);

    /// <inheritdoc />
    public bool HasKeys(TenantId tenant) => _byTenant.TryGetValue(tenant, out var keys) && !keys.IsEmpty;

    /// <inheritdoc />
    public bool TryResolve(TenantId tenant, string key, out string value)
    {
        if (_byTenant.TryGetValue(tenant, out var keys)
            && keys.TryGetValue(key, out var entry)
            && entry.Resolve() is { } resolved)
        {
            value = resolved;
            return true;
        }

        value = string.Empty;
        return false;
    }
}
