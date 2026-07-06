using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Mockifyr.Server;
using Testcontainers.PostgreSql;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Validation of Postgres change-feed reload (G16f): the Redis change feed (G16e) using PostgreSQL
/// <c>LISTEN</c>/<c>NOTIFY</c> instead of Redis pub/sub. Two live Mockifyr hosts share one Postgres
/// backend with <c>--change-feed</c>; a stub created (or deleted) over the admin API on one instance is
/// served (or stopped) by the other <em>without a restart</em> — multi-instance coherence. Propagation is
/// asynchronous, so the assertions poll within a timeout. This is coherence infrastructure (no
/// WireMock-semantic novelty; served-response parity is covered by G16c), so no oracle is needed.
/// Requires Docker.
/// </summary>
public sealed class G16fPostgresChangeFeedTests : IAsyncLifetime
{
    private const string StubJson =
        """{"request":{"method":"GET","url":"/cf"},"response":{"status":200,"body":"coherent"}}""";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task Mutation_On_One_Instance_Propagates_To_Another()
    {
        var connectionString = _postgres.GetConnectionString();

        await using var hostA = await StartHostAsync(connectionString);
        await using var hostB = await StartHostAsync(connectionString);
        using var clientA = Client(hostA);
        using var clientB = Client(hostB);

        // Nothing served yet on B.
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.GetAsync("/cf")).StatusCode);

        // Create on A → B should start serving it without a restart.
        using var content = new StringContent(StubJson, Encoding.UTF8, "application/json");
        var created = await clientA.PostAsync("/__admin/mappings", content);
        var id = (await System.Text.Json.JsonDocument.ParseAsync(await created.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("id").GetGuid();

        var afterCreate = await PollAsync(clientB, "/cf", HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.OK, afterCreate.StatusCode);
        Assert.Equal("coherent", await afterCreate.Content.ReadAsStringAsync());

        // Delete on A → B should stop serving it.
        await clientA.DeleteAsync($"/__admin/mappings/{id}");
        var afterDelete = await PollAsync(clientB, "/cf", HttpStatusCode.NotFound);
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    // Polls until the endpoint returns the expected status, or a generous timeout elapses (LISTEN/NOTIFY
    // propagation is asynchronous). Returns the last response either way, so the caller asserts.
    private static async Task<HttpResponseMessage> PollAsync(HttpClient client, string path, HttpStatusCode expected)
    {
        HttpResponseMessage response = null!;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            response = await client.GetAsync(path);
            if (response.StatusCode == expected)
            {
                return response;
            }

            response.Dispose();
            await Task.Delay(100);
        }

        return await client.GetAsync(path);
    }

    private static async Task<WebApplication> StartHostAsync(string connectionString)
    {
        var app = MockifyrHost.Build(["--port", "0", "--postgres", connectionString, "--change-feed", "true"]);
        await app.StartAsync();
        return app;
    }

    private static HttpClient Client(WebApplication app)
    {
        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses
            .First(a => a.StartsWith("http://", StringComparison.Ordinal))
            .Replace("[::]", "127.0.0.1").Replace("0.0.0.0", "127.0.0.1");
        return new HttpClient { BaseAddress = new Uri(address) };
    }
}
