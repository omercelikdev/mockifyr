using System.Net.Http;
using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Mockifyr.Differential.Harness;

/// <summary>
/// The GraphQL test oracle (G14): the same Java WireMock image with the community GraphQL extension
/// loaded (<c>graphql-body-matcher</c>). GraphQL rides plain HTTP (a POST to <c>/graphql</c> with a
/// <c>{"query": …}</c> body), so — unlike gRPC — there is no HTTP/2 or descriptor plumbing; the
/// extension jar and the stub are copied in and the matcher is loaded via <c>--extensions</c>.
/// </summary>
public sealed class WireMockGraphqlOracle : IAsyncDisposable
{
    private const ushort WireMockPort = 8080;
    private const string MatcherClass = "io.github.nilwurtz.GraphqlBodyMatcher";

    private const string ExtensionUrl =
        "https://repo1.maven.org/maven2/io/github/nilwurtz/wiremock-graphql-extension/0.9.0/" +
        "wiremock-graphql-extension-0.9.0-jar-with-dependencies.jar";

    private readonly IContainer _container;
    private HttpClient? _client;

    public WireMockGraphqlOracle(string mappingJson)
    {
        _container = new ContainerBuilder(WireMockOracle.Image)
            .WithPortBinding(WireMockPort, assignRandomHostPort: true)
            .WithResourceMapping(GetExtensionJar(), "/var/wiremock/extensions/graphql.jar")
            .WithResourceMapping(Encoding.UTF8.GetBytes(mappingJson), "/home/wiremock/mappings/stub.json")
            .WithCommand("--extensions", MatcherClass)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r.ForPort(WireMockPort).ForPath("/__admin/mappings")))
            .Build();
    }

    /// <summary>Starts the oracle container.</summary>
    public Task StartAsync() => _container.StartAsync();

    /// <summary>An HTTP client bound to the oracle, for POSTing GraphQL requests.</summary>
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

    // The extension jar (~22 MB, with dependencies); download once and cache in the temp dir.
    private static byte[] GetExtensionJar()
    {
        var cache = Path.Combine(Path.GetTempPath(), "wiremock-graphql-extension-0.9.0-jar-with-dependencies.jar");
        if (!File.Exists(cache))
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            File.WriteAllBytes(cache, http.GetByteArrayAsync(ExtensionUrl).GetAwaiter().GetResult());
        }

        return File.ReadAllBytes(cache);
    }
}
