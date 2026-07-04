using System.Net.Http;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Mockifyr.Differential.Generator;
using Mockifyr.Differential.Harness;
using Mockifyr.Server;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Differential validation of HTTPS/TLS serving (G11a): the same stub is served over a <em>real TLS
/// connection</em> by both the oracle (its <c>--https-port</c> listener) and a Kestrel-hosted Mockifyr
/// (bound via <c>--https-port</c> with a self-signed cert), and the wire responses — status, body,
/// declared headers — must match. TLS is transport encryption, so the HTTP response is identical to
/// the plaintext one; this proves the handshake succeeds and nothing diverges over TLS. Both clients
/// accept the self-signed certificates. Requires Docker.
/// </summary>
public sealed class G11aHttpsTests : IAsyncLifetime
{
    private readonly WireMockOracle _oracle = new();
    private WebApplication? _mockifyr;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        await _oracle.StartAsync();

        // A real Kestrel host with HTTPS on an ephemeral port.
        _mockifyr = MockifyrHost.Build(["--port", "0", "--https-port", "0"]);
        await _mockifyr.StartAsync();

        var httpsAddress = _mockifyr.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses
            .First(a => a.StartsWith("https://", StringComparison.Ordinal))
            .Replace("[::]", "127.0.0.1").Replace("0.0.0.0", "127.0.0.1");

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };
        _client = new HttpClient(handler) { BaseAddress = new Uri(httpsAddress) };
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_mockifyr is not null)
        {
            await _mockifyr.DisposeAsync();
        }

        await _oracle.DisposeAsync();
    }

    private sealed record Case(string Description, string StubJson, RequestSpec Request, string[] Headers);

    private static IEnumerable<Case> Cases()
    {
        yield return new Case(
            "static-body",
            """{"request":{"method":"GET","url":"/tls"},"response":{"status":200,"body":"secure hello"}}""",
            new RequestSpec { Method = "GET", Url = "/tls" },
            []);

        yield return new Case(
            "json-body-header",
            """{"request":{"method":"POST","url":"/tls/j"},"response":{"status":201,"headers":{"X-Marker":"m"},"jsonBody":{"a":1,"b":[2,3]}}}""",
            new RequestSpec { Method = "POST", Url = "/tls/j", Body = Encoding.UTF8.GetBytes("x") },
            ["X-Marker", "Content-Type"]);
    }

    [Fact]
    public async Task Serves_OverTls_MatchingTheOracle()
    {
        var failures = new List<string>();

        foreach (var scenario in Cases())
        {
            // Oracle: load over its HTTP admin, serve over its HTTPS listener.
            await _oracle.ResetAsync();
            await _oracle.LoadMappingAsync(scenario.StubJson);
            var oracle = await _oracle.SendHttpsAsync(scenario.Request);

            // Mockifyr: load + serve entirely over TLS.
            var mockifyr = await DriveOverTls(scenario.StubJson, scenario.Request);

            if (oracle.Status != mockifyr.Status)
            {
                failures.Add($"{scenario.Description}: status oracle={oracle.Status} mockifyr={mockifyr.Status}");
            }

            if (!oracle.Body.AsSpan().SequenceEqual(mockifyr.Body))
            {
                failures.Add($"{scenario.Description}: body oracle=\"{Text(oracle.Body)}\" mockifyr=\"{Text(mockifyr.Body)}\"");
            }

            foreach (var header in scenario.Headers)
            {
                var o = string.Join(",", oracle.Headers.GetValueOrDefault(header) ?? []);
                var m = mockifyr.Headers.GetValueOrDefault(header) ?? "<absent>";
                if (!string.Equals(o.Length == 0 ? "<absent>" : o, m, StringComparison.Ordinal))
                {
                    failures.Add($"{scenario.Description}: header[{header}] oracle={o} mockifyr={m}");
                }
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} HTTPS divergence(s):\n{string.Join("\n", failures)}");
    }

    private async Task<(int Status, byte[] Body, IReadOnlyDictionary<string, string> Headers)> DriveOverTls(
        string stubJson, RequestSpec request)
    {
        await _client!.PostAsync("/__admin/mappings/reset", content: null);
        using (var load = new StringContent(stubJson, Encoding.UTF8, "application/json"))
        {
            await _client.PostAsync("/__admin/mappings", load);
        }

        using var message = new HttpRequestMessage(new HttpMethod(request.Method), request.Url);
        if (request.Body is { } body)
        {
            message.Content = new ByteArrayContent(body);
        }

        using var response = await _client.SendAsync(message);
        var headers = response.Headers
            .Concat(response.Content.Headers)
            .ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);

        return ((int)response.StatusCode, await response.Content.ReadAsByteArrayAsync(), headers);
    }

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);
}
