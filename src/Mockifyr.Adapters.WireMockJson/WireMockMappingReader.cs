using System.Text;
using System.Text.Json;
using Mockifyr.Core;
using Mockifyr.Matching;

namespace Mockifyr.Adapters.WireMockJson;

/// <summary>
/// Reads WireMock stub mapping JSON (the canonical format the differential harness loads into
/// both the oracle and Mockifyr) into the internal domain model. This adapter is itself under
/// differential test. See docs/decisions/0004.
/// </summary>
/// <remarks>
/// G0 scope: <c>request.method</c>, <c>request.url</c>/<c>request.urlPath</c>, and
/// <c>response.status</c>/<c>body</c>/<c>headers</c>/<c>priority</c>. The surface grows with the
/// matching (G1) and response (G2) roadmap items.
/// </remarks>
public static class WireMockMappingReader
{
    /// <summary>Reads one or more mappings (a single object or a <c>{"mappings":[...]}</c> wrapper).</summary>
    public static IReadOnlyList<StubMapping> Read(string json, TenantId tenant)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("mappings", out var mappings) &&
            mappings.ValueKind == JsonValueKind.Array)
        {
            return [.. mappings.EnumerateArray().Select(m => ReadOne(m, tenant))];
        }

        return [ReadOne(root, tenant)];
    }

    private static StubMapping ReadOne(JsonElement mapping, TenantId tenant)
    {
        var request = mapping.TryGetProperty("request", out var r) ? r : default;
        var response = mapping.TryGetProperty("response", out var rsp) ? rsp : default;

        return new StubMapping
        {
            Id = Guid.NewGuid(),
            TenantId = tenant,
            Priority = mapping.TryGetProperty("priority", out var p) && p.TryGetInt32(out var pri) ? pri : 5,
            Request = ReadRequest(request),
            Response = ReadResponse(response),
        };
    }

    private static RequestPattern ReadRequest(JsonElement request)
    {
        IMatcher? url = null;
        if (request.ValueKind == JsonValueKind.Object)
        {
            if (request.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String)
            {
                url = new UrlEqualToMatcher(u.GetString()!);
            }
            else if (request.TryGetProperty("urlPath", out var up) && up.ValueKind == JsonValueKind.String)
            {
                url = new UrlPathEqualToMatcher(up.GetString()!);
            }
        }

        var method = request.ValueKind == JsonValueKind.Object &&
                     request.TryGetProperty("method", out var m) && m.ValueKind == JsonValueKind.String
            ? m.GetString()!
            : "ANY";

        return new RequestPattern
        {
            Url = url,
            Method = new MethodMatcher(method),
            Headers = [],
            Query = [],
            Cookies = [],
            Body = [],
        };
    }

    private static ResponseDefinition ReadResponse(JsonElement response)
    {
        var status = response.ValueKind == JsonValueKind.Object &&
                     response.TryGetProperty("status", out var s) && s.TryGetInt32(out var code)
            ? code
            : 200;

        byte[]? body = null;
        if (response.ValueKind == JsonValueKind.Object &&
            response.TryGetProperty("body", out var b) && b.ValueKind == JsonValueKind.String)
        {
            body = Encoding.UTF8.GetBytes(b.GetString()!);
        }

        var headerPairs = new List<KeyValuePair<string, string>>();
        if (response.ValueKind == JsonValueKind.Object &&
            response.TryGetProperty("headers", out var h) && h.ValueKind == JsonValueKind.Object)
        {
            foreach (var header in h.EnumerateObject())
            {
                if (header.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var v in header.Value.EnumerateArray())
                    {
                        headerPairs.Add(new(header.Name, v.GetString() ?? string.Empty));
                    }
                }
                else
                {
                    headerPairs.Add(new(header.Name, header.Value.GetString() ?? string.Empty));
                }
            }
        }

        return new ResponseDefinition
        {
            Status = status,
            Headers = headerPairs.ToLookup(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
            Body = body,
            Transformers = [],
        };
    }
}
