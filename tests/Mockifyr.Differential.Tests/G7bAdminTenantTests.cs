using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Validation of admin tenant scoping (G7b): the admin API resolves the tenant from the
/// <c>X-Mockifyr-Tenant</c> header (the same header the mock-serving facade honours). Multi-tenancy is a
/// Mockifyr-specific invariant with no WireMock oracle — WireMock is single-tenant — so it is a self-test
/// of isolation: a stub created under one tenant is visible only to that tenant, and an absent header
/// resolves to the default tenant (so single-tenant callers are unchanged). No Docker required.
/// </summary>
public sealed class G7bAdminTenantTests : IAsyncLifetime
{
    private const string Stub = """{"request":{"method":"GET","url":"/t"},"response":{"status":200}}""";

    private readonly WebApplicationFactory<Program> _app = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task Stubs_AreIsolatedByTheTenantHeader()
    {
        using var client = _app.CreateClient();

        // Create a stub under tenant "acme".
        using (var content = new StringContent(Stub, Encoding.UTF8, "application/json"))
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/__admin/mappings") { Content = content };
            req.Headers.Add("X-Mockifyr-Tenant", "acme");
            (await client.SendAsync(req)).EnsureSuccessStatusCode();
        }

        // acme sees it; globex and the default tenant (no header) do not — isolation.
        Assert.Equal(1, await CountAsync(client, "acme"));
        Assert.Equal(0, await CountAsync(client, "globex"));
        Assert.Equal(0, await CountAsync(client, tenant: null));

        // A create with no header lands in the default tenant (backward compatible with single-tenant use).
        using (var content = new StringContent(Stub, Encoding.UTF8, "application/json"))
        {
            (await client.PostAsync("/__admin/mappings", content)).EnsureSuccessStatusCode();
        }

        Assert.Equal(1, await CountAsync(client, tenant: null));
        Assert.Equal(1, await CountAsync(client, "acme")); // unchanged — still isolated
    }

    private static async Task<int> CountAsync(HttpClient client, string? tenant)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/__admin/mappings");
        if (tenant is not null)
        {
            req.Headers.Add("X-Mockifyr-Tenant", tenant);
        }

        using var response = await client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<MappingsEnvelope>();
        return body?.Mappings.Count ?? 0;
    }

    private sealed record MappingsEnvelope(IReadOnlyList<MappingId> Mappings);
    private sealed record MappingId(Guid Id);
}
