namespace Mockifyr.Core;

/// <summary>
/// Builds a <see cref="CanonicalRequest"/> from simple inputs. Used by tests and the harness to
/// drive the engine directly. Real facades (HTTP/gRPC/...) build the canonical request from
/// their own transport instead.
/// </summary>
public static class CanonicalRequestBuilder
{
    /// <summary>Builds a canonical request from a method, a URL (path + optional query), headers, and a body.</summary>
    public static CanonicalRequest Build(
        string method,
        string url,
        IEnumerable<KeyValuePair<string, string>>? headers = null,
        byte[]? body = null)
    {
        var queryIndex = url.IndexOf('?', StringComparison.Ordinal);
        var path = queryIndex >= 0 ? url[..queryIndex] : url;
        var queryString = queryIndex >= 0 ? url[(queryIndex + 1)..] : string.Empty;

        return new CanonicalRequest
        {
            Method = method,
            Url = url,
            Path = path,
            PathSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries),
            PathVariables = new Dictionary<string, string>(),
            Query = ParseQuery(queryString),
            Headers = (headers ?? []).ToLookup(h => h.Key, h => h.Value, StringComparer.OrdinalIgnoreCase),
            Cookies = new Dictionary<string, string>(),
            Body = body ?? [],
            Parts = [],
            ClientIp = null,
        };
    }

    private static ILookup<string, string> ParseQuery(string queryString)
    {
        if (string.IsNullOrEmpty(queryString))
        {
            return Array.Empty<KeyValuePair<string, string>>().ToLookup(x => x.Key, x => x.Value);
        }

        return queryString
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair =>
            {
                var eq = pair.IndexOf('=', StringComparison.Ordinal);
                return eq >= 0
                    ? new KeyValuePair<string, string>(Uri.UnescapeDataString(pair[..eq]), Uri.UnescapeDataString(pair[(eq + 1)..]))
                    : new KeyValuePair<string, string>(Uri.UnescapeDataString(pair), string.Empty);
            })
            .ToLookup(x => x.Key, x => x.Value, StringComparer.Ordinal);
    }
}
