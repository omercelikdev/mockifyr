using Mockifyr.Core;

namespace Mockifyr.Outbound;

/// <summary>
/// Applies a <see cref="ProxyDirective"/> (G8): forwards the matched request to the upstream at
/// <c>proxyBaseUrl</c> (preserving the path + query, method, headers, and body) and returns the
/// upstream's response. This is outbound I/O, so it lives at the facade edge — the pure engine only
/// records the directive. The transport HTTP facade (G12) reuses this same responder over the wire.
/// </summary>
public sealed class ProxyResponder(HttpClient? client = null)
{
    // The Host header must track the upstream URL (set by HttpClient), not the original mock host.
    private static readonly HashSet<string> DropForwardHeaders =
        new(StringComparer.OrdinalIgnoreCase) { "Host", "Content-Length" };

    private readonly HttpClient _client = client ?? new HttpClient();

    public async Task<CanonicalResponse> ProxyAsync(
        ProxyDirective proxy, CanonicalRequest request, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(new HttpMethod(request.Method), proxy.BaseUrl.TrimEnd('/') + request.Url);

        if (request.Body is { Length: > 0 } body)
        {
            message.Content = new ByteArrayContent(body);
        }

        foreach (var group in request.Headers)
        {
            if (DropForwardHeaders.Contains(group.Key))
            {
                continue;
            }

            foreach (var value in group)
            {
                if (!message.Headers.TryAddWithoutValidation(group.Key, value))
                {
                    message.Content?.Headers.TryAddWithoutValidation(group.Key, value);
                }
            }
        }

        // WireMock's additionalProxyRequestHeaders are added to the forwarded request.
        foreach (var (name, value) in proxy.AdditionalHeaders)
        {
            if (!message.Headers.TryAddWithoutValidation(name, value))
            {
                message.Content?.Headers.TryAddWithoutValidation(name, value);
            }
        }

        using var response = await _client.SendAsync(message, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        var headers = response.Headers
            .Concat(response.Content.Headers)
            .SelectMany(header => header.Value.Select(value => new KeyValuePair<string, string>(header.Key, value)))
            .ToLookup(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        return new CanonicalResponse
        {
            Status = (int)response.StatusCode,
            Headers = headers,
            Body = responseBody,
        };
    }
}
