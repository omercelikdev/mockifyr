using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Mockifyr.Differential.Harness;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Differential validation of GraphQL <b>directive and fragment ordering</b> normalization (G14d),
/// extending G14a beyond field/argument order. The same <c>graphql-body-matcher</c> stub — a query
/// carrying directives and a named fragment — is loaded into the oracle and Mockifyr, and query variants
/// that reorder directives, directive arguments, and fragment definitions are POSTed to each. The match
/// decision must agree with the oracle for every variant (the oracle is the truth for whether the
/// extension normalizes that ordering). Requires Docker.
/// </summary>
public sealed class G14dGraphqlOrderingTests : IAsyncLifetime
{
    // A query with two directives (each with an argument) on a field, plus a named fragment spread.
    private const string StubQuery =
        "query Hero { hero @include(if: true) @skip(if: false) { ...fields } } fragment fields on Hero { name id }";

    private static readonly string MappingJson =
        "{\"request\":{\"method\":\"POST\",\"urlPath\":\"/graphql\",\"customMatcher\":{\"name\":\"graphql-body-matcher\"," +
        "\"parameters\":{\"query\":\"" + StubQuery + "\"}}},\"response\":{\"status\":200,\"jsonBody\":{\"data\":{\"ok\":true}}}}";

    private readonly WireMockGraphqlOracle _oracle = new(MappingJson);
    private readonly WebApplicationFactory<Program> _mockifyr = new();

    public Task InitializeAsync() => _oracle.StartAsync();

    public async Task DisposeAsync()
    {
        await _mockifyr.DisposeAsync();
        await _oracle.DisposeAsync();
    }

    private sealed record Case(string Description, string Query);

    private static IEnumerable<Case> Cases()
    {
        yield return new Case("exact", StubQuery);
        // Directives on the field swapped.
        yield return new Case("reordered-directives",
            "query Hero { hero @skip(if: false) @include(if: true) { ...fields } } fragment fields on Hero { name id }");
        // Fragment fields reordered (already covered by G14a selection sort, kept for the fragment body).
        yield return new Case("reordered-fragment-fields",
            "query Hero { hero @include(if: true) @skip(if: false) { ...fields } } fragment fields on Hero { id name }");
        // A genuinely different query (extra directive) must not match.
        yield return new Case("different",
            "query Hero { hero @include(if: true) { ...fields } } fragment fields on Hero { name id }");
    }

    [Fact]
    public async Task DirectiveAndFragmentOrdering_AgreesWithTheOracle()
    {
        using var mockifyrClient = _mockifyr.CreateClient();
        using (var load = new StringContent(MappingJson, Encoding.UTF8, "application/json"))
        {
            await mockifyrClient.PostAsync("/__admin/mappings", load);
        }

        var failures = new List<string>();
        var oracleMatches = 0;
        var oracleNonMatches = 0;

        foreach (var scenario in Cases())
        {
            var oracleMatched = await PostAsync(_oracle.Client, scenario.Query) == 200;
            var mockifyrMatched = await PostAsync(mockifyrClient, scenario.Query) == 200;

            if (oracleMatched)
            {
                oracleMatches++;
            }
            else
            {
                oracleNonMatches++;
            }

            if (oracleMatched != mockifyrMatched)
            {
                failures.Add($"{scenario.Description}: oracle={(oracleMatched ? "match" : "no-match")} mockifyr={(mockifyrMatched ? "match" : "no-match")}");
            }
        }

        // Non-degenerate: the oracle must both match and reject across the cases, else the test proves nothing.
        Assert.True(oracleMatches > 0 && oracleNonMatches > 0,
            $"degenerate coverage: oracle matched {oracleMatches}, rejected {oracleNonMatches}");
        Assert.True(failures.Count == 0, $"{failures.Count} GraphQL ordering divergence(s):\n{string.Join("\n", failures)}");
    }

    private static async Task<int> PostAsync(HttpClient client, string query)
    {
        var body = System.Text.Json.JsonSerializer.Serialize(new { query });
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/graphql", content);
        return (int)response.StatusCode;
    }
}
