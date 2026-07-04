using Mockifyr.Adapters.WireMockJson;
using Mockifyr.Core;
using Npgsql;

namespace Mockifyr.Server;

/// <summary>
/// Creates the persistence table if it is absent — shared by the Postgres provider and loader so
/// whichever runs first (the loader runs at startup, before any mutation) finds the schema in place.
/// </summary>
internal static class PostgresSchema
{
    private const string CreateTable =
        "CREATE TABLE IF NOT EXISTS stubs (id uuid PRIMARY KEY, tenant text NOT NULL, json text NOT NULL)";

    public static void Ensure(string connectionString)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(CreateTable, connection);
        command.ExecuteNonQuery();
    }
}

/// <summary>
/// PostgreSQL-backed stub persistence (G16c): the third provider behind the <see cref="IStubPersistence"/>
/// seam — a real SQL backend. Each stub is a row <c>(id, tenant, json)</c>; the stored JSON is
/// id-stamped (shared <see cref="PersistableJson"/>) so ids round-trip identically to the file/LiteDB
/// backends. Connections are opened per operation from Npgsql's pool (thread-safe). The counterpart
/// <see cref="PostgresMappingsLoader"/> reloads rows on startup.
/// </summary>
public sealed class PostgresStubPersistence : IStubPersistence
{
    private readonly string _connectionString;

    public PostgresStubPersistence(string connectionString)
    {
        _connectionString = connectionString;
        PostgresSchema.Ensure(connectionString);
    }

    /// <inheritdoc />
    public void Save(StubMapping stub, string mappingJson)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(
            "INSERT INTO stubs (id, tenant, json) VALUES (@id, @tenant, @json) " +
            "ON CONFLICT (id) DO UPDATE SET tenant = @tenant, json = @json",
            connection);
        command.Parameters.AddWithValue("id", stub.Id);
        command.Parameters.AddWithValue("tenant", stub.TenantId.Value);
        command.Parameters.AddWithValue("json", PersistableJson.WithId(mappingJson, stub.Id));
        command.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public void Remove(TenantId tenant, Guid id)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        using var command = new NpgsqlCommand("DELETE FROM stubs WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", id);
        command.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public void Clear(TenantId tenant)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        using var command = new NpgsqlCommand("DELETE FROM stubs WHERE tenant = @tenant", connection);
        command.Parameters.AddWithValue("tenant", tenant.Value);
        command.ExecuteNonQuery();
    }
}

/// <summary>
/// Reloads the stubs a <see cref="PostgresStubPersistence"/> saved (G16c) — the
/// <see cref="IMappingsLoader"/> counterpart, run at startup. Reads the tenant's rows and parses each
/// stored JSON back into a <see cref="StubMapping"/> (ids preserved via the id-stamped JSON).
/// </summary>
public sealed class PostgresMappingsLoader : IMappingsLoader
{
    private readonly string _connectionString;
    private readonly IMatcherRegistry? _matchers;

    public PostgresMappingsLoader(string connectionString, IMatcherRegistry? matchers = null)
    {
        _connectionString = connectionString;
        _matchers = matchers;
        PostgresSchema.Ensure(connectionString);
    }

    /// <inheritdoc />
    public IReadOnlyList<StubMapping> Load(TenantId tenant)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        using var command = new NpgsqlCommand("SELECT json FROM stubs WHERE tenant = @tenant", connection);
        command.Parameters.AddWithValue("tenant", tenant.Value);

        var stubs = new List<StubMapping>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            stubs.AddRange(WireMockMappingReader.Read(reader.GetString(0), tenant, _matchers));
        }

        return stubs;
    }
}
