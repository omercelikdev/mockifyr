using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Validation of the admin request-journal listing (G6b): <c>GET /__admin/requests</c> returns the
/// tenant's serve events (matched + unmatched), and <c>?unmatched=true</c> narrows to the misses. The
/// listing projection + its tenant scoping are Mockifyr-specific (WireMock is single-tenant), so this is
/// a self-test: two requests are driven through the mock-serving path (one matched, one not) under one
/// tenant, and the journal reflects exactly those, isolated from other tenants. No Docker required.
/// </summary>
public sealed class G6bJournalTests : IAsyncLifetime
{
    private const string Stub = """{"request":{"method":"GET","url":"/hit"},"response":{"status":200}}""";

    private readonly WebApplicationFactory<Program> _app = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task Journal_ListsTenantServeEvents_AndFiltersUnmatched()
    {
        using var client = _app.CreateClient();

        // Register a stub under tenant "acme".
        using (var content = new StringContent(Stub, Encoding.UTF8, "application/json"))
        {
            var create = new HttpRequestMessage(HttpMethod.Post, "/__admin/mappings") { Content = content };
            create.Headers.Add("X-Mockifyr-Tenant", "acme");
            (await client.SendAsync(create)).EnsureSuccessStatusCode();
        }

        // Drive one matched and one unmatched request through the mock-serving path (records serve events).
        await ServeAsync(client, "/hit", "acme");     // matched
        await ServeAsync(client, "/missing", "acme"); // unmatched

        var all = await RequestsAsync(client, "acme", unmatchedOnly: false);
        Assert.Equal(2, all.Count);
        Assert.Contains(all, r => r.WasMatched && r.Url == "/hit");
        Assert.Contains(all, r => !r.WasMatched && r.Url == "/missing");

        var unmatched = await RequestsAsync(client, "acme", unmatchedOnly: true);
        Assert.Single(unmatched);
        Assert.False(unmatched[0].WasMatched);

        // Another tenant's journal is empty — isolation.
        Assert.Empty(await RequestsAsync(client, "globex", unmatchedOnly: false));
    }

    private static async Task ServeAsync(HttpClient client, string path, string tenant)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Add("X-Mockifyr-Tenant", tenant);
        using var _ = await client.SendAsync(req);
    }

    private static async Task<IReadOnlyList<JournalRow>> RequestsAsync(HttpClient client, string tenant, bool unmatchedOnly)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, unmatchedOnly ? "/__admin/requests?unmatched=true" : "/__admin/requests");
        req.Headers.Add("X-Mockifyr-Tenant", tenant);
        using var response = await client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JournalEnvelope>();
        return body?.Requests ?? [];
    }

    private sealed record JournalEnvelope(IReadOnlyList<JournalRow> Requests);
    private sealed record JournalRow(Guid Id, string Method, string Url, bool WasMatched);
}
