using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Mockifyr.Differential.Harness;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Differential validation of response templating over a GraphQL-matched stub (G14): a
/// <c>graphql-body-matcher</c> stub whose response declares the <c>response-template</c> transformer
/// and extracts the request's GraphQL <c>variables</c>/<c>operationName</c> (via <c>jsonPath</c> over
/// <c>request.body</c>). The GraphQL extension is only a matcher, so templating is the standard
/// transformer applied post-match; the oracle (WireMock + the extension) and Mockifyr must render the
/// <em>same</em> response body. Requires Docker.
/// </summary>
public sealed class G14cGraphqlResponseTemplatingTests : IAsyncLifetime
{
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
                "variables": { "id": "42" },
                "operationName": "Hero"
              }
            }
          },
          "response": {
            "status": 200,
            "transformers": ["response-template"],
            "body": "{\"id\":\"{{jsonPath request.body '$.variables.id'}}\",\"op\":\"{{jsonPath request.body '$.operationName'}}\"}"
          }
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

    [Fact]
    public async Task ResponseTemplate_OverGraphqlMatch_MatchesTheOracle()
    {
        using var mockifyrClient = _mockifyr.CreateClient();
        using (var load = new StringContent(MappingJson, Encoding.UTF8, "application/json"))
        {
            await mockifyrClient.PostAsync("/__admin/mappings", load);
        }

        const string requestBody =
            """{"query":"query Hero($id: ID!) { hero(id: $id) { name } }","variables":{"id":"42"},"operationName":"Hero"}""";

        var (oracleStatus, oracleBody) = await PostAsync(_oracle.Client, requestBody);
        var (mockifyrStatus, mockifyrBody) = await PostAsync(mockifyrClient, requestBody);

        Assert.Equal(200, oracleStatus);
        Assert.Equal(oracleStatus, mockifyrStatus);
        // The response was templated from the GraphQL request's variables/operationName on both sides.
        Assert.Equal("""{"id":"42","op":"Hero"}""", oracleBody);
        Assert.Equal(oracleBody, mockifyrBody);
    }

    private static async Task<(int Status, string Body)> PostAsync(HttpClient client, string body)
    {
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/graphql", content);
        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }
}
