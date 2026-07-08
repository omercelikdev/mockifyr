using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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

    [Fact]
    public async Task Tenants_ListsTheTenantsThatHaveStubs()
    {
        using var client = _app.CreateClient();

        // No stubs yet → no tenants.
        var empty = await client.GetFromJsonAsync<TenantsResponse>("/__admin/tenants");
        Assert.Empty(empty!.Tenants);

        // Create a stub under two tenants → both are listed, sorted.
        foreach (var tenant in new[] { "globex", "acme" })
        {
            using var content = new StringContent(
                """{"request":{"method":"GET","url":"/t"},"response":{"status":200}}""", Encoding.UTF8, "application/json");
            var req = new HttpRequestMessage(HttpMethod.Post, "/__admin/mappings") { Content = content };
            req.Headers.Add("X-Mockifyr-Tenant", tenant);
            (await client.SendAsync(req)).EnsureSuccessStatusCode();
        }

        var listed = await client.GetFromJsonAsync<TenantsResponse>("/__admin/tenants");
        Assert.Equal(["acme", "globex"], listed!.Tenants);
    }

    [Fact]
    public async Task Mappings_ReturnTheFullSourceNotJustTheId()
    {
        using var client = _app.CreateClient();

        const string stub = """{"request":{"method":"GET","urlPath":"/orders/42"},"response":{"status":201,"body":"created"}}""";
        using var content = new StringContent(stub, Encoding.UTF8, "application/json");
        (await client.PostAsync("/__admin/mappings", content)).EnsureSuccessStatusCode();

        // GET /__admin/mappings must return the whole mapping (request + response), not just an id, so
        // the dashboard can display it and round-trip an edit without losing the fields.
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/__admin/mappings"));
        var mapping = doc.RootElement.GetProperty("mappings")[0];

        Assert.True(mapping.TryGetProperty("id", out _));
        Assert.Equal("/orders/42", mapping.GetProperty("request").GetProperty("urlPath").GetString());
        Assert.Equal(201, mapping.GetProperty("response").GetProperty("status").GetInt32());
    }

    private sealed record Health(string Name, string Version, string Persistence, int Tenants, int TotalStubs);
    private sealed record TenantsResponse(string[] Tenants);
}
