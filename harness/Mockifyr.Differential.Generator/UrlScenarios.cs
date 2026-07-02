using System.Text.Json;

namespace Mockifyr.Differential.Generator;

/// <summary>
/// Fuzzes the advanced URL matchers (G1b): <c>urlPattern</c> (regex over the full URL),
/// <c>urlPathPattern</c> (regex over the path), and <c>urlPathTemplate</c> (segment template). All
/// three are whole-value/whole-path anchored; the path forms ignore the query string. Pinned by the
/// differential suite — see docs/parity/g1-matching.md.
/// </summary>
public static class UrlScenarios
{
    public static IEnumerable<MatcherScenario> UrlPattern()
    {
        yield return Build("urlPattern", "urlPattern", """/things/[0-9]+\?ok=true""",
            ("/things/12?ok=true", true),
            ("/things/7?ok=true", true),
            ("/things/12?ok=false", false),
            ("/things/x?ok=true", false),
            ("/things/12", false)); // anchored: needs the query too
    }

    public static IEnumerable<MatcherScenario> UrlPathPattern()
    {
        yield return Build("urlPathPattern", "urlPathPattern", "/u/[a-z]+",
            ("/u/abc", true),
            ("/u/abc?x=1", true),   // query ignored
            ("/u/123", false),
            ("/u/abc/def", false)); // anchored to the whole path
    }

    public static IEnumerable<MatcherScenario> UrlPathTemplate()
    {
        yield return Build("urlPathTemplate", "urlPathTemplate", "/users/{id}/orders/{oid}",
            ("/users/1/orders/9", true),
            ("/users/1/orders/9?x=1", true),  // query ignored
            ("/users/abc/orders/xyz", true),
            ("/users/1/orders", false),        // missing segment
            ("/users/1/orders/9/extra", false)); // extra segment
    }

    private static MatcherScenario Build(string description, string urlKey, string urlValue, params (string Url, bool Match)[] cases)
    {
        var mapping = new Dictionary<string, object>
        {
            ["request"] = new Dictionary<string, object> { ["method"] = "GET", [urlKey] = urlValue },
            ["response"] = new Dictionary<string, object> { ["status"] = 200, ["body"] = "ok" },
        };

        var probes = cases
            .Select(c => new ProbeRequest(new RequestSpec { Method = "GET", Url = c.Url }, c.Match))
            .ToList();

        return new MatcherScenario($"url[{description}] {urlValue}", JsonSerializer.Serialize(mapping), probes);
    }
}
