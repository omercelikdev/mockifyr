using System.Text;
using System.Text.Json;

namespace Mockifyr.Differential.Generator;

/// <summary>
/// A proxy (G8) case: a <c>proxyBaseUrl</c> stub (with the upstream host as the <c>__PROXY_HOST__</c>
/// token, rewritten per side by the harness) and the request that triggers the proxy. The harness
/// diffs the proxied response (status, body, and the upstream's marker header) between the sides.
/// </summary>
public sealed record ProxyScenario(string Description, string StubTemplate, RequestSpec Request);

/// <summary>Proxy cases (G8): forward a matched request to an upstream and return its response.</summary>
public static class ProxyScenarios
{
    public static IEnumerable<ProxyScenario> All()
    {
        // GET proxy: the path + query must be preserved on the forwarded request.
        yield return Build(
            "get-with-query",
            method: "GET",
            urlPath: "/proxied",
            trigger: new RequestSpec { Method = "GET", Url = "/proxied?x=1&y=2" });

        // POST proxy: the body is forwarded; the upstream's response is returned.
        yield return Build(
            "post-body",
            method: "POST",
            urlPath: "/px",
            trigger: new RequestSpec { Method = "POST", Url = "/px", Body = Encoding.UTF8.GetBytes("client-body") });

        // additionalProxyRequestHeaders: the upstream receives (and echoes back) the added header.
        yield return Build(
            "additional-headers",
            method: "GET",
            urlPath: "/pah",
            trigger: new RequestSpec { Method = "GET", Url = "/pah" },
            additionalHeaders: new Dictionary<string, object> { ["X-Proxy-Added"] = "injected" });

        // proxyUrlPrefixToRemove: the leading /api is stripped, so the upstream receives /widgets — the
        // echoed path in the body must be /widgets (not /api/widgets) on both sides.
        yield return Build(
            "url-prefix-to-remove",
            method: "GET",
            urlPath: "/api/widgets",
            trigger: new RequestSpec { Method = "GET", Url = "/api/widgets" },
            urlPrefixToRemove: "/api");
    }

    private static ProxyScenario Build(
        string description, string method, string urlPath, RequestSpec trigger,
        Dictionary<string, object>? additionalHeaders = null, string? urlPrefixToRemove = null)
    {
        var response = new Dictionary<string, object> { ["proxyBaseUrl"] = "http://__PROXY_HOST__" };
        if (additionalHeaders is not null)
        {
            response["additionalProxyRequestHeaders"] = additionalHeaders;
        }
        if (urlPrefixToRemove is not null)
        {
            response["proxyUrlPrefixToRemove"] = urlPrefixToRemove;
        }

        var mapping = new Dictionary<string, object>
        {
            ["request"] = new Dictionary<string, object> { ["method"] = method, ["urlPath"] = urlPath },
            ["response"] = response,
        };

        return new ProxyScenario($"proxy[{description}]", JsonSerializer.Serialize(mapping), trigger);
    }
}
