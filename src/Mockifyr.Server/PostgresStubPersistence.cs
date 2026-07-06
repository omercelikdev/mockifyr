using Microsoft.Extensions.Hosting;
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
    /// <summary>The <c>NOTIFY</c>/<c>LISTEN</c> channel a mutation announces on, so other instances reload (G16f).</summary>
    internal const string ChangeChannel = "mockifyr_changes";

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
        Announce(connection);
    }

    /// <inheritdoc />
    public void Remove(TenantId tenant, Guid id)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        using var command = new NpgsqlCommand("DELETE FROM stubs WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", id);
        command.ExecuteNonQuery();
        Announce(connection);
    }

    /// <inheritdoc />
    public void Clear(TenantId tenant)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        using var command = new NpgsqlCommand("DELETE FROM stubs WHERE tenant = @tenant", connection);
        command.Parameters.AddWithValue("tenant", tenant.Value);
        command.ExecuteNonQuery();
        Announce(connection);
    }

    // Announce a change on the open connection so LISTEN subscribers (G16f) reload. The channel is a
    // fixed identifier (not user input), so it is safe to interpolate; NOTIFY with no listener is a
    // cheap no-op, so this is always safe whether or not the change feed is enabled.
    private static void Announce(NpgsqlConnection connection)
    {
        using var notify = new NpgsqlCommand($"NOTIFY {ChangeChannel}", connection);
        notify.ExecuteNonQuery();
    }
}

/// <summary>
/// The Postgres change-feed reloader (G16f): the <see cref="RedisChangeFeedReloader"/> counterpart using
/// PostgreSQL <c>LISTEN</c>/<c>NOTIFY</c>. It holds a dedicated connection that <c>LISTEN</c>s on the
/// change channel and, on every notification another instance emits, reconciles the in-memory store from
/// the mappings loaders (<see cref="ChangeFeedReconciler"/>) — so a mutation on one instance is reflected
/// by the others without a restart. A background loop drives Npgsql's notification delivery.
/// </summary>
public sealed class PostgresChangeFeedReloader : IHostedService
{
    private readonly string _connectionString;
    private readonly IStubStore _store;
    private readonly IEnumerable<IMappingsLoader> _loaders;
    private readonly CancellationTokenSource _stopping = new();
    private NpgsqlConnection? _connection;
    private Task? _listenLoop;

    public PostgresChangeFeedReloader(string connectionString, IStubStore store, IEnumerable<IMappingsLoader> loaders)
    {
        _connectionString = connectionString;
        _store = store;
        _loaders = loaders;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _connection = new NpgsqlConnection(_connectionString);
        await _connection.OpenAsync(cancellationToken);
        _connection.Notification += (_, _) => ChangeFeedReconciler.Reload(_store, _loaders);

        using (var listen = new NpgsqlCommand($"LISTEN {PostgresStubPersistence.ChangeChannel}", _connection))
        {
            await listen.ExecuteNonQueryAsync(cancellationToken);
        }

        // Npgsql only delivers notifications while a wait is in flight, so drive it in a loop.
        _listenLoop = ListenLoopAsync(_stopping.Token);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stopping.CancelAsync();
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        if (_listenLoop is not null)
        {
            await Task.WhenAny(_listenLoop, Task.Delay(Timeout.Infinite, cancellationToken));
        }
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _connection!.WaitAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
        catch (Exception)
        {
            // The connection was disposed on shutdown; nothing to recover.
        }
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
