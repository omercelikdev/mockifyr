using System.Net.Http;
using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Mockifyr.Differential.Generator;

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
        // Lets the container reach a host-side webhook receiver (G3) via host.docker.internal on
        // Linux CI, where — unlike Docker Desktop — it is not resolvable by default.
        .WithExtraHost("host.docker.internal", "host-gateway")
        .WithWaitStrategy(Wait.ForUnixContainer()
            .UntilHttpRequestIsSucceeded(r => r.ForPort(WireMockPort).ForPath("/__admin/mappings")))
        .Build();

    private HttpClient? _client;

    // UseCookies is disabled so an explicit Cookie request header passes through unmodified
    // (otherwise the handler's cookie container would manage it).
    private HttpClient Client => _client ??= new HttpClient(new SocketsHttpHandler { UseCookies = false })
    {
        BaseAddress = new Uri($"http://{_container.Hostname}:{_container.GetMappedPublicPort(WireMockPort)}"),
    };

    /// <summary>Starts the oracle container.</summary>
    public Task StartAsync() => _container.StartAsync();

    /// <summary>A fresh HTTP client bound to the oracle, for driving its admin API directly (G7b).</summary>
    public HttpClient CreateAdminClient() => new()
    {
        BaseAddress = new Uri($"http://{_container.Hostname}:{_container.GetMappedPublicPort(WireMockPort)}"),
    };

    /// <summary>Clears all stubs and the request journal.</summary>
    public async Task ResetAsync()
    {
        using var response = await Client.PostAsync("/__admin/reset", content: null);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Loads WireMock stub mapping JSON. A single mapping goes to <c>/__admin/mappings</c>; a
    /// <c>{"mappings":[...]}</c> wrapper is loaded via <c>/__admin/mappings/import</c>, which
    /// preserves array order (array-first wins on equal priority).
    /// </summary>
    public async Task LoadMappingAsync(string wireMockJson)
    {
        var path = IsMappingsWrapper(wireMockJson) ? "/__admin/mappings/import" : "/__admin/mappings";
        using var content = new StringContent(wireMockJson, Encoding.UTF8, "application/json");
        using var response = await Client.PostAsync(path, content);
        response.EnsureSuccessStatusCode();
    }

    private static bool IsMappingsWrapper(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.ValueKind == JsonValueKind.Object &&
               doc.RootElement.TryGetProperty("mappings", out var mappings) &&
               mappings.ValueKind == JsonValueKind.Array;
    }

    /// <summary>Counts journaled requests matching a pattern via <c>/__admin/requests/count</c> (G6).</summary>
    public async Task<int> CountRequestsMatchingAsync(string patternJson)
    {
        using var content = new StringContent(patternJson, Encoding.UTF8, "application/json");
        using var response = await Client.PostAsync("/__admin/requests/count", content);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("count").GetInt32();
    }

    /// <summary>The count of unmatched journaled requests via <c>/__admin/requests/unmatched</c> (G6).</summary>
    public async Task<int> UnmatchedCountAsync()
    {
        using var response = await Client.GetAsync("/__admin/requests/unmatched");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("requests").GetArrayLength();
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

        // Force a fresh connection per request: on a reused keep-alive connection the oracle
        // receives a mangled (lowercased) Cookie header, which made cookie value matching diverge
        // spuriously. A fresh connection transmits the header verbatim. See docs/parity/g1-matching.md.
        request.Headers.ConnectionClose = true;
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
