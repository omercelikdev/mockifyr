using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Validation of the admin health/status endpoint (G7c): <c>GET /__admin/health</c> reports the host's
/// name/version, the active persistence provider, and live tenant/stub counts — the data the dashboard's
/// Settings/Status screen shows. Mockifyr-specific (no WireMock oracle), so a self-test. No Docker.
/// </summary>
public sealed class G7cHealthTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _app = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task Health_ReportsProviderAndLiveCounts()
    {
        using var client = _app.CreateClient();

        var before = await client.GetFromJsonAsync<Health>("/__admin/health");
        Assert.NotNull(before);
        Assert.Equal("Mockifyr", before!.Name);
        // Default (ephemeral) host uses the no-op persistence provider.
        Assert.Equal("NullStubPersistence", before.Persistence);
        Assert.Equal(0, before.TotalStubs);

        // Create a stub under a tenant → the counts reflect it live.
        const string stub = """{"request":{"method":"GET","url":"/h"},"response":{"status":200}}""";
        using (var content = new StringContent(stub, Encoding.UTF8, "application/json"))
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/__admin/mappings") { Content = content };
            req.Headers.Add("X-Mockifyr-Tenant", "acme");
            (await client.SendAsync(req)).EnsureSuccessStatusCode();
        }

        var after = await client.GetFromJsonAsync<Health>("/__admin/health");
        Assert.Equal(1, after!.TotalStubs);
        Assert.True(after.Tenants >= 1);
    }

    private sealed record Health(string Name, string Version, string Persistence, int Tenants, int TotalStubs);
}
