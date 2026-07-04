using LiteDB;
using Mockifyr.Adapters.WireMockJson;
using Mockifyr.Core;

namespace Mockifyr.Server;

/// <summary>
/// A persisted stub row in LiteDB: the id (document key), owning tenant, and the stub's id-stamped
/// WireMock JSON. Storing the raw JSON keeps persistence faithful without a domain → JSON serializer.
/// </summary>
internal sealed class StoredStub
{
    public Guid Id { get; set; }

    public string Tenant { get; set; } = string.Empty;

    public string Json { get; set; } = string.Empty;
}

/// <summary>
/// LiteDB-backed stub persistence (G16b): the second provider behind the <see cref="IStubPersistence"/>
/// seam, proving the multi-provider design. Each stub is one document (id-stamped JSON, keyed by id and
/// tagged with its tenant) in a single embedded database file; <see cref="LiteDbMappingsLoader"/>
/// reloads them on startup. The <see cref="LiteDatabase"/> is shared (DI singleton) and thread-safe.
/// </summary>
public sealed class LiteDbStubPersistence(LiteDatabase database) : IStubPersistence
{
    internal const string Collection = "stubs";

    private readonly ILiteCollection<StoredStub> _stubs = database.GetCollection<StoredStub>(Collection);

    /// <inheritdoc />
    public void Save(StubMapping stub, string mappingJson) =>
        _stubs.Upsert(new StoredStub
        {
            Id = stub.Id,
            Tenant = stub.TenantId.Value,
            Json = PersistableJson.WithId(mappingJson, stub.Id),
        });

    /// <inheritdoc />
    public void Remove(TenantId tenant, Guid id) => _stubs.Delete(id);

    /// <inheritdoc />
    public void Clear(TenantId tenant) => _stubs.DeleteMany(stored => stored.Tenant == tenant.Value);
}

/// <summary>
/// Reloads the stubs a <see cref="LiteDbStubPersistence"/> saved (G16b) — the <see cref="IMappingsLoader"/>
/// counterpart, run at startup. Reads the tenant's documents and parses each stored JSON back into a
/// <see cref="StubMapping"/> (ids are preserved because the JSON was id-stamped when saved).
/// </summary>
public sealed class LiteDbMappingsLoader(LiteDatabase database, IMatcherRegistry? matchers = null) : IMappingsLoader
{
    private readonly ILiteCollection<StoredStub> _stubs =
        database.GetCollection<StoredStub>(LiteDbStubPersistence.Collection);

    /// <inheritdoc />
    public IReadOnlyList<StubMapping> Load(TenantId tenant)
    {
        var stubs = new List<StubMapping>();
        foreach (var stored in _stubs.Find(row => row.Tenant == tenant.Value))
        {
            stubs.AddRange(WireMockMappingReader.Read(stored.Json, tenant, matchers));
        }

        return stubs;
    }
}
