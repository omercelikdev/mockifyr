using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Mockifyr.Differential.Generator;
using Mockifyr.Differential.Harness;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Regression for a <c>jsonBody</c> that contains Handlebars template expressions with
/// <em>single-quoted</em> helper arguments — the common WireMock pattern
/// <c>{{jsonPath request.body '$.field'}}</c>. The response body is produced by serializing
/// <c>jsonBody</c>; the default System.Text.Json encoder escapes <c>'</c> to <c>'</c>, which both
/// diverges from the oracle (Jackson keeps it literal) and breaks the template (the helper argument no
/// longer parses, so the value renders empty). The extracted value is deterministic, so the whole body
/// is byte-diffed against the oracle. Requires Docker.
/// </summary>
public sealed class G2jTemplatedJsonBodyTests : IAsyncLifetime
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
    public async Task TemplatedJsonBody_WithSingleQuotedHelperArgs_MatchesTheOracle()
    {
        const string stub =
            """
            {"request":{"method":"POST","url":"/tpl"},
             "response":{"status":200,"transformers":["response-template"],
               "headers":{"Content-Type":"application/json"},
               "jsonBody":{"echo":"{{jsonPath request.body '$.name'}}","greeting":"Hi {{jsonPath request.body '$.name'}}!"}}}
            """;
        var request = new RequestSpec { Method = "POST", Url = "/tpl", Body = Encoding.UTF8.GetBytes("""{"name":"Ada"}""") };

        using var oracleClient = _oracle.CreateAdminClient();
        using var mockifyrClient = _mockifyr.CreateClient();

        var oracle = await Drive(oracleClient, stub, request);
        var mockifyr = await Drive(mockifyrClient, stub, request);

        Assert.True(
            oracle.AsSpan().SequenceEqual(mockifyr),
            $"body diverged:\n oracle  =\"{Encoding.UTF8.GetString(oracle)}\"\n mockifyr=\"{Encoding.UTF8.GetString(mockifyr)}\"");
    }

    private static async Task<byte[]> Drive(HttpClient client, string stubJson, RequestSpec request)
    {
        await client.PostAsync("/__admin/mappings/reset", content: null);
        using (var load = new StringContent(stubJson, Encoding.UTF8, "application/json"))
        {
            await client.PostAsync("/__admin/mappings", load);
        }

        using var message = new HttpRequestMessage(new HttpMethod(request.Method), request.Url);
        if (request.Body is { } body)
        {
            message.Content = new ByteArrayContent(body);
        }

        using var response = await client.SendAsync(message);
        return await response.Content.ReadAsByteArrayAsync();
    }
}
