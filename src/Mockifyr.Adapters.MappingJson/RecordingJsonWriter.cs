using System.Text;
using System.Text.Json;
using Mockifyr.Core;

namespace Mockifyr.Adapters.MappingJson;

/// <summary>
/// Generates a WireMock stub-mapping JSON from a captured request/response exchange (G9 record).
/// The recorder proxies a request to an upstream, then hands the pair here to produce a stub that
/// replays that response: an exact-URL + method request pattern (plus an <c>equalTo</c> body pattern
/// when the request had a body), and the captured status/body/headers as a static response. This is
/// the inverse of <see cref="MappingJsonReader"/>; a general model→JSON export is not needed.
/// </summary>
public static class RecordingJsonWriter
{
    // Recomputed by the serving side, so they are not baked into the generated stub.
    private static readonly HashSet<string> SkipResponseHeaders =
        new(StringComparer.OrdinalIgnoreCase) { "Content-Length", "Transfer-Encoding", "Connection" };

    public static string ToStubJson(CanonicalRequest request, CanonicalResponse response)
    {
        var requestPattern = new Dictionary<string, object>
        {
            ["method"] = request.Method,
            ["url"] = request.Url,
        };

        if (request.Body.Length > 0)
        {
            requestPattern["bodyPatterns"] = new object[]
            {
                new Dictionary<string, object> { ["equalTo"] = Encoding.UTF8.GetString(request.Body) },
            };
        }

        var responseDefinition = new Dictionary<string, object> { ["status"] = response.Status };

        if (response.Body.Length > 0)
        {
            responseDefinition["body"] = Encoding.UTF8.GetString(response.Body);
        }

        var headers = response.Headers
            .Where(group => !SkipResponseHeaders.Contains(group.Key))
            .ToDictionary(group => group.Key, group => (object)group.First());
        if (headers.Count > 0)
        {
            responseDefinition["headers"] = headers;
        }

        var mapping = new Dictionary<string, object>
        {
            ["request"] = requestPattern,
            ["response"] = responseDefinition,
        };

        return JsonSerializer.Serialize(mapping);
    }
}
