using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Mockifyr.Differential.Harness;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Differential validation of GraphQL query matching (G14a): the same <c>graphql-body-matcher</c> stub
/// is loaded into the oracle (WireMock + the GraphQL extension) and Mockifyr, and a set of query
/// variants is POSTed to <c>/graphql</c> against each. The match decision (served response vs 404) must
/// agree for every variant — proving Mockifyr's parse + AST-sort normalization matches the reference
/// extension's: equal regardless of whitespace and field/argument order, unequal otherwise. Requires Docker.
/// </summary>
public sealed class G14aGraphqlTests : IAsyncLifetime
{
    private const string MappingJson =
        """
        {
          "request": {
            "method": "POST",
            "urlPath": "/graphql",
            "customMatcher": {
              "name": "graphql-body-matcher",
              "parameters": { "query": "{ hero { name id } friends(first: 2) { name } }" }
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

    private sealed record Case(string Description, string Query, bool ExpectMatch);

    private static IEnumerable<Case> Cases()
    {
        // Exact.
        yield return new Case("exact", "{ hero { name id } friends(first: 2) { name } }", true);
        // Whitespace / newlines differ.
        yield return new Case("reformatted", "{\n  hero {\n    name\n    id\n  }\n  friends(first: 2) {\n    name\n  }\n}", true);
        // Field order swapped at both levels.
        yield return new Case("reordered-fields", "{ friends(first: 2) { name } hero { id name } }", true);
        // A different query must not match.
        yield return new Case("different", "{ hero { name } }", false);
        // A syntactically invalid query must not match.
        yield return new Case("invalid", "{ hero { name ", false);
    }

    [Fact]
    public async Task QueryMatching_AgreesWithTheOracle()
    {
        using var mockifyrClient = _mockifyr.CreateClient();
        using (var load = new StringContent(MappingJson, Encoding.UTF8, "application/json"))
        {
            await mockifyrClient.PostAsync("/__admin/mappings", load);
        }

        var failures = new List<string>();
        foreach (var scenario in Cases())
        {
            var oracle = await PostAsync(_oracle.Client, scenario.Query);
            var mockifyr = await PostAsync(mockifyrClient, scenario.Query);

            var oracleMatched = oracle == 200;
            var mockifyrMatched = mockifyr == 200;

            if (oracleMatched != mockifyrMatched)
            {
                failures.Add($"{scenario.Description}: oracle={(oracleMatched ? "match" : "no-match")} mockifyr={(mockifyrMatched ? "match" : "no-match")}");
            }
            else if (oracleMatched != scenario.ExpectMatch)
            {
                failures.Add($"{scenario.Description}: both sides {(oracleMatched ? "matched" : "did not match")} but expected {(scenario.ExpectMatch ? "match" : "no-match")}");
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} GraphQL divergence(s):\n{string.Join("\n", failures)}");
    }

    private static async Task<int> PostAsync(HttpClient client, string query)
    {
        var body = System.Text.Json.JsonSerializer.Serialize(new { query });
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/graphql", content);
        return (int)response.StatusCode;
    }
}
