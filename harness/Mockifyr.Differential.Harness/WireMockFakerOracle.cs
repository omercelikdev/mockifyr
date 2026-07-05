using System.Net.Http;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Mockifyr.Differential.Harness;

/// <summary>
/// The Faker test oracle (G15): the same Java WireMock image with the official faker extension loaded
/// (the <c>random</c> helper, backed by Datafaker). The extension jar auto-registers via ServiceLoader,
/// so it only needs to be copied into the extensions directory. Stubs are loaded over the admin API.
/// </summary>
public sealed class WireMockFakerOracle : IAsyncDisposable
{
    private const ushort WireMockPort = 8080;

    private const string ExtensionUrl =
        "https://repo1.maven.org/maven2/org/wiremock/extensions/wiremock-faker-extension-standalone/0.2.0/" +
        "wiremock-faker-extension-standalone-0.2.0.jar";

    private readonly IContainer _container;
    private HttpClient? _client;

    public WireMockFakerOracle()
    {
        _container = new ContainerBuilder(WireMockOracle.Image)
            .WithPortBinding(WireMockPort, assignRandomHostPort: true)
            .WithResourceMapping(GetExtensionJar(), "/var/wiremock/extensions/faker.jar")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r.ForPort(WireMockPort).ForPath("/__admin/mappings")))
            .Build();
    }

    /// <summary>Starts the oracle container.</summary>
    public Task StartAsync() => _container.StartAsync();

    /// <summary>An HTTP client bound to the oracle, for loading stubs and driving requests.</summary>
    public HttpClient Client => _client ??= new HttpClient
    {
        BaseAddress = new Uri($"http://{_container.Hostname}:{_container.GetMappedPublicPort(WireMockPort)}"),
    };

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        await _container.DisposeAsync();
    }

    // The extension jar (~20 MB, with dependencies); download once and cache in the temp dir.
    private static byte[] GetExtensionJar()
    {
        var cache = Path.Combine(Path.GetTempPath(), "wiremock-faker-extension-standalone-0.2.0.jar");
        if (!File.Exists(cache))
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            File.WriteAllBytes(cache, http.GetByteArrayAsync(ExtensionUrl).GetAwaiter().GetResult());
        }

        return File.ReadAllBytes(cache);
    }
}
