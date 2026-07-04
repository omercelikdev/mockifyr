using System.Net;
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
/// Differential validation of HTTP/2 (G11b): the same stub is fetched with an HTTP/2-forcing client
/// from both the oracle and a Kestrel-hosted Mockifyr over TLS. Both must ALPN-negotiate HTTP/2
/// (<c>response.Version</c> == 2.0) and return the same body — so Mockifyr speaks h2 exactly where
/// WireMock does. (Plaintext prior-knowledge h2c is <em>not</em> asserted: the oracle's plaintext port
/// answers it nondeterministically — sometimes h2, sometimes <c>HTTP_1_1_REQUIRED</c> — so it is not a
/// stable parity behavior. Mockifyr's plaintext listener is left h2c-capable to match. See
/// docs/parity/g11-tls-http2.md.) Requires Docker.
/// </summary>
public sealed class G11bHttp2Tests : IAsyncLifetime
{
    private const string StubJson =
        """{"request":{"method":"GET","url":"/h2"},"response":{"status":200,"body":"over-http2"}}""";

    private static readonly RequestSpec Request = new() { Method = "GET", Url = "/h2" };

    private readonly WireMockOracle _oracle = new();
    private WebApplication? _mockifyr;
    private Uri _mockifyrHttp = null!;
    private Uri _mockifyrHttps = null!;

    public async Task InitializeAsync()
    {
        await _oracle.StartAsync();
        await _oracle.ResetAsync();
        await _oracle.LoadMappingAsync(StubJson);

        _mockifyr = MockifyrHost.Build(["--port", "0", "--https-port", "0"]);
        await _mockifyr.StartAsync();

        var addresses = _mockifyr.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses
            .Select(a => a.Replace("[::]", "127.0.0.1").Replace("0.0.0.0", "127.0.0.1"))
            .ToList();
        _mockifyrHttp = new Uri(addresses.First(a => a.StartsWith("http://", StringComparison.Ordinal)));
        _mockifyrHttps = new Uri(addresses.First(a => a.StartsWith("https://", StringComparison.Ordinal)));

        // Load the same stub into Mockifyr (over its plaintext admin).
        using var admin = new HttpClient { BaseAddress = _mockifyrHttp };
        using var load = new StringContent(StubJson, Encoding.UTF8, "application/json");
        await admin.PostAsync("/__admin/mappings", load);
    }

    public async Task DisposeAsync()
    {
        if (_mockifyr is not null)
        {
            await _mockifyr.DisposeAsync();
        }

        await _oracle.DisposeAsync();
    }

    [Fact]
    public async Task Http2_OverTls_NegotiatedByBothSides()
    {
        var oracle = await FetchExactVersionAsync(_oracle.HttpsBaseAddress, HttpVersion.Version20);
        var mockifyr = await FetchExactVersionAsync(_mockifyrHttps, HttpVersion.Version20);

        Assert.Equal(2, oracle.Version.Major);   // the oracle really spoke h2 (guards the test itself)
        Assert.Equal(2, mockifyr.Version.Major);  // ...and so did Mockifyr
        Assert.Equal(oracle.Status, mockifyr.Status);
        Assert.True(oracle.Body.AsSpan().SequenceEqual(mockifyr.Body),
            $"body oracle=\"{Text(oracle.Body)}\" mockifyr=\"{Text(mockifyr.Body)}\"");
    }

    // Forces the exact HTTP version (h2 prior-knowledge): the request fails rather than downgrading,
    // so a passing assertion on response.Version is a real negotiation, not a 1.1 fallback.
    private static async Task<(Version Version, int Status, byte[] Body)> FetchExactVersionAsync(Uri baseAddress, Version version)
    {
        using var handler = new SocketsHttpHandler
        {
            SslOptions = { RemoteCertificateValidationCallback = static (_, _, _, _) => true },
        };
        using var client = new HttpClient(handler) { BaseAddress = baseAddress };
        using var message = new HttpRequestMessage(HttpMethod.Get, Request.Url)
        {
            Version = version,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
        };

        using var response = await client.SendAsync(message);
        return (response.Version, (int)response.StatusCode, await response.Content.ReadAsByteArrayAsync());
    }

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);
}
