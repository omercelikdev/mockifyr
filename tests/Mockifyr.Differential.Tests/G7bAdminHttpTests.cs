using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Mockifyr.Differential.Harness;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Differential validation of the admin HTTP surface (G7b). The same admin scenario is driven over
/// HTTP against both the oracle and Mockifyr's in-memory admin host, and the observation sequence
/// (status codes + mapping counts) must match. The admin JSON carries per-engine ids, so the
/// comparison is <em>semantic</em> (effects), not byte-for-byte. Requires Docker.
/// </summary>
public sealed class G7bAdminHttpTests : IAsyncLifetime
{
    private readonly WireMockOracle _oracle = new();
    private readonly WebApplicationFactory<Program> _mockifyr = new();

    public Task InitializeAsync() => _oracle.StartAsync();

    public async Task DisposeAsync()
    {
        await _mockifyr.DisposeAsync();
        await _oracle.DisposeAsync();
    }

    [Fact]
    public async Task Admin_Crud_MatchesTheOracle()
    {
        using var oracleClient = _oracle.CreateAdminClient();
        using var mockifyrClient = _mockifyr.CreateClient();

        var oracle = await DriveAdminScenario(oracleClient);
        var mockifyr = await DriveAdminScenario(mockifyrClient);

        Assert.Equal(oracle, mockifyr);
    }

    /// <summary>
    /// Exercises the admin CRUD/import/reset surface and records a comparable sequence of
    /// observations — each side uses its own stub ids, so only the effects (status codes + mapping
    /// counts) are recorded.
    /// </summary>
    private static async Task<List<string>> DriveAdminScenario(HttpClient client)
    {
        var log = new List<string>();

        // Start clean.
        log.Add($"reset:{(int)(await client.PostAsync("/__admin/mappings/reset", null)).StatusCode}");
        log.Add($"count0:{await MappingCount(client)}");

        // Create a stub; capture its id for the later get/delete.
        var (createStatus, id) = await CreateStub(client, Stub("GET", "/a", "one"));
        log.Add($"create:{createStatus}");
        log.Add($"count1:{await MappingCount(client)}");

        // Get it, and get a definitely-missing one.
        log.Add($"get:{(int)(await client.GetAsync($"/__admin/mappings/{id}")).StatusCode}");
        log.Add($"getMissing:{(int)(await client.GetAsync($"/__admin/mappings/{Guid.NewGuid()}")).StatusCode}");

        // Delete it.
        log.Add($"delete:{(int)(await client.DeleteAsync($"/__admin/mappings/{id}")).StatusCode}");
        log.Add($"count2:{await MappingCount(client)}");

        // Import a bundle of two.
        var bundle = """
            {"mappings":[
              {"request":{"method":"GET","url":"/b"},"response":{"status":200}},
              {"request":{"method":"GET","url":"/c"},"response":{"status":200}}
            ]}
            """;
        log.Add($"import:{(int)(await client.PostAsync("/__admin/mappings/import", Json(bundle))).StatusCode}");
        log.Add($"count3:{await MappingCount(client)}");

        // Malformed stub JSON is rejected.
        log.Add($"badJson:{(int)(await client.PostAsync("/__admin/mappings", Json("not-json"))).StatusCode}");

        // Reset clears everything.
        log.Add($"reset2:{(int)(await client.PostAsync("/__admin/mappings/reset", null)).StatusCode}");
        log.Add($"count4:{await MappingCount(client)}");

        return log;
    }

    private static async Task<(int Status, string Id)> CreateStub(HttpClient client, string stubJson)
    {
        using var response = await client.PostAsync("/__admin/mappings", Json(stubJson));
        var id = string.Empty;
        if (response.IsSuccessStatusCode)
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            id = doc.RootElement.GetProperty("id").GetString() ?? string.Empty;
        }

        return ((int)response.StatusCode, id);
    }

    private static async Task<int> MappingCount(HttpClient client)
    {
        using var response = await client.GetAsync("/__admin/mappings");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("mappings").GetArrayLength();
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private static string Stub(string method, string url, string responseBody) =>
        "{\"request\":{\"method\":\"" + method + "\",\"url\":\"" + url + "\"}," +
        "\"response\":{\"status\":200,\"body\":\"" + responseBody + "\"}}";
}
