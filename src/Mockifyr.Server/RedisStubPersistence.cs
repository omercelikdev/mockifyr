using Mockifyr.Adapters.WireMockJson;
using Mockifyr.Core;
using StackExchange.Redis;

namespace Mockifyr.Server;

/// <summary>
/// Redis-backed stub persistence (G16d): the fourth provider behind the <see cref="IStubPersistence"/>
/// seam — a key-value backend. Each tenant's stubs live in one Redis hash (<c>mockifyr:stubs:{tenant}</c>)
/// keyed by stub id, the value being the id-stamped WireMock JSON (shared <see cref="PersistableJson"/>)
/// so ids round-trip identically to the other backends. The <see cref="IConnectionMultiplexer"/> is
/// shared (DI singleton) and thread-safe. <see cref="RedisMappingsLoader"/> reloads on startup.
/// </summary>
public sealed class RedisStubPersistence(IConnectionMultiplexer redis) : IStubPersistence
{
    internal static string HashKey(TenantId tenant) => $"mockifyr:stubs:{tenant.Value}";

    /// <inheritdoc />
    public void Save(StubMapping stub, string mappingJson) =>
        redis.GetDatabase().HashSet(
            HashKey(stub.TenantId), stub.Id.ToString(), PersistableJson.WithId(mappingJson, stub.Id));

    /// <inheritdoc />
    public void Remove(TenantId tenant, Guid id) =>
        redis.GetDatabase().HashDelete(HashKey(tenant), id.ToString());

    /// <inheritdoc />
    public void Clear(TenantId tenant) => redis.GetDatabase().KeyDelete(HashKey(tenant));
}

/// <summary>
/// Reloads the stubs a <see cref="RedisStubPersistence"/> saved (G16d) — the <see cref="IMappingsLoader"/>
/// counterpart, run at startup. Reads the tenant's hash and parses each stored JSON back into a
/// <see cref="StubMapping"/> (ids preserved via the id-stamped JSON).
/// </summary>
public sealed class RedisMappingsLoader(IConnectionMultiplexer redis, IMatcherRegistry? matchers = null) : IMappingsLoader
{
    /// <inheritdoc />
    public IReadOnlyList<StubMapping> Load(TenantId tenant)
    {
        var stubs = new List<StubMapping>();
        foreach (var entry in redis.GetDatabase().HashGetAll(RedisStubPersistence.HashKey(tenant)))
        {
            stubs.AddRange(WireMockMappingReader.Read(entry.Value!, tenant, matchers));
        }

        return stubs;
    }
}
