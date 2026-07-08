using System.Net.Http;
using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Mockifyr.Differential.Harness;

/// <summary>
/// The gRPC test oracle (G13): the same Java WireMock image with the official gRPC extension loaded,
/// serving gRPC over HTTP/2. The extension jar (mounted into <c>/var/wiremock/extensions</c>), the
/// compiled proto descriptor (<c>/home/wiremock/grpc</c>), and the stub mapping
/// (<c>/home/wiremock/mappings</c>) are copied into the container. The extension converts protobuf to
/// JSON and matches with the ordinary stub engine — so a gRPC stub is just a POST-to-
/// <c>/service/method</c> stub with an <c>equalToJson</c> body and a <c>jsonBody</c> response.
/// </summary>
public sealed class WireMockGrpcOracle : IAsyncDisposable
{
    private const ushort WireMockPort = 8080;
    private const ushort WireMockHttpsPort = 8443;

    private const string ExtensionUrl =
        "https://repo1.maven.org/maven2/org/wiremock/wiremock-grpc-extension-standalone/0.11.0/" +
        "wiremock-grpc-extension-standalone-0.11.0.jar";

    private readonly IContainer _container;

    public WireMockGrpcOracle(byte[] descriptorSet, string mappingJson)
    {
        // gRPC needs HTTP/2; the plaintext h2c path is nondeterministic on WireMock (HTTP_1_1_REQUIRED),
        // so gRPC is driven over TLS (ALPN-negotiated h2), which is deterministic. See g11-tls-http2.md.
        _container = new ContainerBuilder(WireMockOracle.Image)
            .WithPortBinding(WireMockPort, assignRandomHostPort: true)
            .WithPortBinding(WireMockHttpsPort, assignRandomHostPort: true)
            .WithCommand("--https-port", "8443")
            .WithResourceMapping(GetExtensionJar(), "/var/wiremock/extensions/grpc.jar")
            .WithResourceMapping(descriptorSet, "/home/wiremock/grpc/service.dsc")
            .WithResourceMapping(Encoding.UTF8.GetBytes(mappingJson), "/home/wiremock/mappings/stub.json")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r.ForPort(WireMockPort).ForPath("/__admin/mappings")))
            .Build();
    }

    /// <summary>
    /// Starts the oracle container and waits until the HTTPS listener actually answers. The container
    /// wait strategy only gates on the plaintext admin port, but gRPC calls go over HTTPS (8443); the
    /// TLS/h2 listener can lag the admin surface, so the first gRPC call could race a not-yet-ready
    /// listener and see a transient status (intermittent G13d flake). Polling the HTTPS admin endpoint
    /// here closes that gap deterministically.
    /// </summary>
    public async Task StartAsync()
    {
        await _container.StartAsync();

        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        var httpsAdmin = new Uri($"{GrpcAddress}__admin/mappings");

        for (var attempt = 0; attempt < 40; attempt++)
        {
            try
            {
                using var response = await http.GetAsync(httpsAdmin);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // TLS listener not accepting yet — retry.
            }

            await Task.Delay(250);
        }
    }

    /// <summary>The base address for gRPC calls (HTTPS, ALPN-negotiated h2) against the oracle.</summary>
    public Uri GrpcAddress =>
        new($"https://{_container.Hostname}:{_container.GetMappedPublicPort(WireMockHttpsPort)}");

    /// <summary>A fresh HTTP client bound to the oracle's plaintext admin port (for reset/mappings — G13d).</summary>
    public HttpClient CreateAdminClient() => new()
    {
        BaseAddress = new Uri($"http://{_container.Hostname}:{_container.GetMappedPublicPort(WireMockPort)}"),
    };

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _container.DisposeAsync();

    // The extension jar is large (~30 MB); download once and cache in the temp dir so repeated test
    // runs (and multiple oracle instances) don't refetch it.
    private static byte[] GetExtensionJar()
    {
        var cache = Path.Combine(Path.GetTempPath(), "wiremock-grpc-extension-standalone-0.11.0.jar");
        if (!File.Exists(cache))
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            File.WriteAllBytes(cache, http.GetByteArrayAsync(ExtensionUrl).GetAwaiter().GetResult());
        }

        return File.ReadAllBytes(cache);
    }
}
