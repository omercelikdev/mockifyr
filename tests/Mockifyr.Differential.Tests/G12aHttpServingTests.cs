using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Mockifyr.Differential.Generator;
using Mockifyr.Differential.Harness;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Differential validation of the mock-serving HTTP facade (G12a): the same stub is loaded and driven
/// over <em>real HTTP</em> against both the oracle and a hosted Mockifyr, and the wire responses —
/// status, reason phrase (<c>statusMessage</c>), body, and declared headers — must match. This closes
/// the deferred over-the-wire behaviors (mock serving, reason phrase, multi-value headers on the
/// wire). Requires Docker.
/// </summary>
public sealed class G12aHttpServingTests : IAsyncLifetime
{
    private readonly WireMockOracle _oracle = new();
    private readonly WebApplicationFactory<Program> _mockifyr = new();

    public Task InitializeAsync() => _oracle.StartAsync();

    public async Task DisposeAsync()
    {
        await _mockifyr.DisposeAsync();
        await _oracle.DisposeAsync();
    }

    private sealed record Case(string Description, string StubJson, RequestSpec Request, string[] ComparedHeaders);

    private sealed record WireResult(int Status, string? ReasonPhrase, byte[] Body, IReadOnlyDictionary<string, string> Headers);

    private static IEnumerable<Case> Cases()
    {
        yield return new Case(
            "static-body",
            """{"request":{"method":"GET","url":"/a"},"response":{"status":200,"body":"hello"}}""",
            new RequestSpec { Method = "GET", Url = "/a" },
            []);

        yield return new Case(
            "status-message",
            """{"request":{"method":"GET","url":"/teapot"},"response":{"status":418,"statusMessage":"I am a teapot","body":"tea"}}""",
            new RequestSpec { Method = "GET", Url = "/teapot" },
            []);

        yield return new Case(
            "multi-value-headers",
            """{"request":{"method":"GET","url":"/mv"},"response":{"status":200,"headers":{"X-Multi":["a","b"],"Content-Type":"text/plain"},"body":"ok"}}""",
            new RequestSpec { Method = "GET", Url = "/mv" },
            ["X-Multi", "Content-Type"]);

        yield return new Case(
            "json-body",
            """{"request":{"method":"POST","url":"/j"},"response":{"status":201,"jsonBody":{"a":1,"b":[2,3]}}}""",
            new RequestSpec { Method = "POST", Url = "/j", Body = Encoding.UTF8.GetBytes("x") },
            []);
    }

    [Fact]
    public async Task Serves_OverTheWire_MatchingTheOracle()
    {
        using var oracleClient = _oracle.CreateAdminClient();
        using var mockifyrClient = _mockifyr.CreateClient();
        var failures = new List<string>();

        foreach (var scenario in Cases())
        {
            var oracle = await DriveOverWire(oracleClient, scenario.StubJson, scenario.Request);
            var mockifyr = await DriveOverWire(mockifyrClient, scenario.StubJson, scenario.Request);

            if (oracle.Status != mockifyr.Status)
            {
                failures.Add($"{scenario.Description}: status oracle={oracle.Status} mockifyr={mockifyr.Status}");
            }

            if (oracle.ReasonPhrase != mockifyr.ReasonPhrase)
            {
                failures.Add($"{scenario.Description}: reason oracle=\"{oracle.ReasonPhrase}\" mockifyr=\"{mockifyr.ReasonPhrase}\"");
            }

            if (!oracle.Body.AsSpan().SequenceEqual(mockifyr.Body))
            {
                failures.Add($"{scenario.Description}: body oracle=\"{Text(oracle.Body)}\" mockifyr=\"{Text(mockifyr.Body)}\"");
            }

            foreach (var header in scenario.ComparedHeaders)
            {
                var o = oracle.Headers.GetValueOrDefault(header);
                var m = mockifyr.Headers.GetValueOrDefault(header);
                if (!string.Equals(o, m, StringComparison.Ordinal))
                {
                    failures.Add($"{scenario.Description}: header[{header}] oracle={o ?? "<absent>"} mockifyr={m ?? "<absent>"}");
                }
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} over-the-wire divergence(s):\n{string.Join("\n", failures)}");
    }

    private static async Task<WireResult> DriveOverWire(HttpClient client, string stubJson, RequestSpec request)
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
        var headers = response.Headers
            .Concat(response.Content.Headers)
            .ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);

        return new WireResult(
            (int)response.StatusCode,
            response.ReasonPhrase,
            await response.Content.ReadAsByteArrayAsync(),
            headers);
    }

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);
}
