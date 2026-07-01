using System.Net.Http;
using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Mockifyr.Differential.Harness;

/// <summary>
/// The test oracle: a real Java WireMock running in a container. Its responses are the ground
/// truth Mockifyr is diffed against. See docs/decisions/0002.
/// </summary>
public sealed class WireMockOracle : IAsyncDisposable
{
    /// <summary>The pinned oracle image.</summary>
    public const string Image = "wiremock/wiremock:3.10.0";

    private const ushort WireMockPort = 8080;

    private readonly IContainer _container = new ContainerBuilder(Image)
        .WithPortBinding(WireMockPort, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer()
            .UntilHttpRequestIsSucceeded(r => r.ForPort(WireMockPort).ForPath("/__admin/mappings")))
        .Build();

    private HttpClient? _client;

    private HttpClient Client => _client ??= new HttpClient
    {
        BaseAddress = new Uri($"http://{_container.Hostname}:{_container.GetMappedPublicPort(WireMockPort)}"),
    };

    /// <summary>Starts the oracle container.</summary>
    public Task StartAsync() => _container.StartAsync();

    /// <summary>Clears all stubs and the request journal.</summary>
    public async Task ResetAsync()
    {
        using var response = await Client.PostAsync("/__admin/reset", content: null);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Loads a single WireMock stub mapping.</summary>
    public async Task LoadMappingAsync(string wireMockJson)
    {
        using var content = new StringContent(wireMockJson, Encoding.UTF8, "application/json");
        using var response = await Client.PostAsync("/__admin/mappings", content);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Replays a request against the oracle and snapshots the response.</summary>
    public async Task<HttpResponseSnapshot> SendAsync(RequestSpec spec)
    {
        using var request = new HttpRequestMessage(new HttpMethod(spec.Method), spec.Url);
        if (spec.Body is { } body)
        {
            request.Content = new ByteArrayContent(body);
        }

        foreach (var header in spec.Headers)
        {
            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        using var response = await Client.SendAsync(request);

        var headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in response.Headers)
        {
            headers[header.Key] = [.. header.Value];
        }

        foreach (var header in response.Content.Headers)
        {
            headers[header.Key] = [.. header.Value];
        }

        return new HttpResponseSnapshot
        {
            Status = (int)response.StatusCode,
            Headers = headers,
            Body = await response.Content.ReadAsByteArrayAsync(),
        };
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        await _container.DisposeAsync();
    }
}
