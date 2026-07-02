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
        var headerLookup = (headers ?? []).ToLookup(h => h.Key, h => h.Value, StringComparer.OrdinalIgnoreCase);

        return new CanonicalRequest
        {
            Method = method,
            Url = url,
            Path = path,
            PathSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries),
            PathVariables = new Dictionary<string, string>(),
            Query = ParseQuery(queryString),
            Headers = headerLookup,
            Cookies = ParseCookies(headerLookup),
            Body = body ?? [],
            Parts = MultipartBodyParser.Parse(body ?? [], headerLookup["Content-Type"].FirstOrDefault()),
            ClientIp = null,
        };
    }

    private static IReadOnlyDictionary<string, string> ParseCookies(ILookup<string, string> headers)
    {
        var cookies = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var header in headers["Cookie"])
        {
            foreach (var pair in header.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var eq = pair.IndexOf('=', StringComparison.Ordinal);
                if (eq >= 0)
                {
                    cookies[pair[..eq].Trim()] = pair[(eq + 1)..].Trim();
                }
            }
        }

        return cookies;
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
