using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Mockifyr.Adapters.WireMockJson;
using Mockifyr.Core;
using Mockifyr.Server;
using Testcontainers.PostgreSql;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Validation of multi-tenant change-feed reload (G16g). The change-feed reloader reconciles <em>every</em>
/// tenant, not just the default one: a stub persisted for a non-default tenant by another writer is
/// reloaded and served by a live host under that tenant, and a tenant emptied elsewhere is pruned while
/// other tenants are untouched. Driven over Postgres <c>LISTEN</c>/<c>NOTIFY</c> (G16f). Admin tenant
/// resolution is still a placeholder (default tenant), so the non-default tenants are written directly
/// through the persistence seam — simulating a tenant-aware peer instance — and served via the
/// <c>X-Mockifyr-Tenant</c> header the mock-serving facade honours. Coherence infrastructure, no oracle.
/// Requires Docker.
/// </summary>
public sealed class G16gMultiTenantReloadTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private static string Stub(string url, string body) =>
        "{\"request\":{\"method\":\"GET\",\"url\":\"" + url + "\"}," +
        "\"response\":{\"status\":200,\"body\":\"" + body + "\"}}";

    [Fact]
    public async Task Reload_Reconciles_All_Tenants_Independently()
    {
        var connectionString = _postgres.GetConnectionString();

        await using var host = await StartHostAsync(connectionString);
        using var client = Client(host);

        // A tenant-aware peer writer persisting into two non-default tenants (fires NOTIFY on each write).
        var peer = new PostgresStubPersistence(connectionString);
        var acme = new TenantId("acme");
        var globex = new TenantId("globex");
        var acmeStub = WireMockMappingReader.Read(Stub("/t", "acme-body"), acme)[0];
        var globexStub = WireMockMappingReader.Read(Stub("/t", "globex-body"), globex)[0];

        // Neither tenant is served yet.
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(client, "/t", "acme")).StatusCode);

        // Persist a stub for each tenant → the host reloads all tenants and serves each under its header.
        peer.Save(acmeStub, Stub("/t", "acme-body"));
        peer.Save(globexStub, Stub("/t", "globex-body"));

        Assert.Equal("acme-body", await PollBodyAsync(client, "/t", "acme", "acme-body"));
        Assert.Equal("globex-body", await PollBodyAsync(client, "/t", "globex", "globex-body"));

        // Empty the acme tenant elsewhere → acme is pruned, but globex is untouched (cross-tenant isolation).
        peer.Remove(acme, acmeStub.Id);

        var acmeGone = await PollStatusAsync(client, "/t", "acme", HttpStatusCode.NotFound);
        Assert.Equal(HttpStatusCode.NotFound, acmeGone.StatusCode);
        Assert.Equal("globex-body", await (await GetAsync(client, "/t", "globex")).Content.ReadAsStringAsync());
    }

    private static async Task<string> PollBodyAsync(HttpClient client, string path, string tenant, string expected)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            using var response = await GetAsync(client, path, tenant);
            if (response.StatusCode == HttpStatusCode.OK && await response.Content.ReadAsStringAsync() == expected)
            {
                return expected;
            }

            await Task.Delay(100);
        }

        using var last = await GetAsync(client, path, tenant);
        return await last.Content.ReadAsStringAsync();
    }

    private static async Task<HttpResponseMessage> PollStatusAsync(HttpClient client, string path, string tenant, HttpStatusCode expected)
    {
        HttpResponseMessage response = null!;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            response = await GetAsync(client, path, tenant);
            if (response.StatusCode == expected)
            {
                return response;
            }

            response.Dispose();
            await Task.Delay(100);
        }

        return await GetAsync(client, path, tenant);
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string path, string tenant)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("X-Mockifyr-Tenant", tenant);
        return await client.SendAsync(request);
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
