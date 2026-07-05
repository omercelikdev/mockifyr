using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Mockifyr.Differential.Harness;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Differential validation of GraphQL variables + operationName matching (G14b): a
/// <c>graphql-body-matcher</c> stub that also constrains <c>variables</c> and <c>operationName</c> is
/// loaded into the oracle (WireMock + the GraphQL extension) and Mockifyr, and request variants are
/// POSTed to <c>/graphql</c> against each. The match/no-match decision must agree for every variant —
/// proving Mockifyr aggregates the three the same way (query AST-equal AND variables JSON-equal AND
/// operationName equal; an unspecified expectation requires the request to omit it). Requires Docker.
/// </summary>
public sealed class G14bGraphqlVariablesTests : IAsyncLifetime
{
    // Stub constrains query + variables + operationName.
    private const string MappingJson =
        """
        {
          "request": {
            "method": "POST",
            "urlPath": "/graphql",
            "customMatcher": {
              "name": "graphql-body-matcher",
              "parameters": {
                "query": "query Hero($id: ID!) { hero(id: $id) { name } }",
                "variables": { "id": "1" },
                "operationName": "Hero"
              }
            }
          },
          "response": { "status": 200, "jsonBody": { "data": { "ok": true } } }
        }
        """;

    private readonly WireMockGraphqlOracle _oracle = new(MappingJson);
    private readonly WebApplicationFactory<Program> _mockifyr = new();

    public Task InitializeAsync() => _oracle.StartAsync();

    public async Task DisposeAsync()
    {
        await _mockifyr.DisposeAsync();
        await _oracle.DisposeAsync();
    }

    private sealed record Case(string Description, string Body, bool ExpectMatch);

    private static IEnumerable<Case> Cases()
    {
        const string query = "query Hero($id: ID!) { hero(id: $id) { name } }";

        // Everything matches (query reformatted too).
        yield return new Case("all-match",
            Body("query Hero($id: ID!) {\n  hero(id: $id) {\n    name\n  }\n}", new { id = "1" }, "Hero"), true);
        // Wrong variable value.
        yield return new Case("wrong-variable", Body(query, new { id = "2" }, "Hero"), false);
        // Missing variables (expected present).
        yield return new Case("missing-variables", BodyNoVars(query, "Hero"), false);
        // Wrong operation name.
        yield return new Case("wrong-operation", Body(query, new { id = "1" }, "Villain"), false);
        // Missing operation name (expected present).
        yield return new Case("missing-operation", BodyNoOp(query, new { id = "1" }), false);
    }

    [Fact]
    public async Task VariablesAndOperationName_AgreeWithTheOracle()
    {
        using var mockifyrClient = _mockifyr.CreateClient();
        using (var load = new StringContent(MappingJson, Encoding.UTF8, "application/json"))
        {
            await mockifyrClient.PostAsync("/__admin/mappings", load);
        }

        var failures = new List<string>();
        foreach (var scenario in Cases())
        {
            var oracle = await PostAsync(_oracle.Client, scenario.Body);
            var mockifyr = await PostAsync(mockifyrClient, scenario.Body);

            if ((oracle == 200) != (mockifyr == 200))
            {
                failures.Add($"{scenario.Description}: oracle={oracle} mockifyr={mockifyr}");
            }
            else if ((oracle == 200) != scenario.ExpectMatch)
            {
                failures.Add($"{scenario.Description}: both {(oracle == 200 ? "matched" : "no-match")} but expected {(scenario.ExpectMatch ? "match" : "no-match")}");
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} GraphQL variables divergence(s):\n{string.Join("\n", failures)}");
    }

    private static string Body(string query, object variables, string operationName) =>
        System.Text.Json.JsonSerializer.Serialize(new { query, variables, operationName });

    private static string BodyNoVars(string query, string operationName) =>
        System.Text.Json.JsonSerializer.Serialize(new { query, operationName });

    private static string BodyNoOp(string query, object variables) =>
        System.Text.Json.JsonSerializer.Serialize(new { query, variables });

    private static async Task<int> PostAsync(HttpClient client, string body)
    {
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/graphql", content);
        return (int)response.StatusCode;
    }
}
