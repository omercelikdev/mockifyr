using System.Text.Json;

namespace Mockifyr.Differential.Generator;

/// <summary>
/// Fuzzes the multi-value matchers (G1c) on a query parameter: <c>hasExactly</c> (the values must
/// correspond exactly to the matchers, in any order) and <c>includes</c> (every matcher must match
/// some value, extras allowed). Query parameters (not headers) are used because they carry
/// multi-value unambiguously over the wire. Pinned by the differential suite — see
/// docs/parity/g1-matching.md.
/// </summary>
public static class MultiValueScenarios
{
    private const string Param = "p";

    public static IEnumerable<MatcherScenario> HasExactly()
    {
        yield return Build("hasExactly[equalTo a, equalTo b]", "hasExactly",
            [Eq("a"), Eq("b")],
            (["a", "b"], true),
            (["b", "a"], true),         // order-insensitive
            (["a"], false),             // missing b
            (["a", "b", "c"], false),   // extra value
            (["a", "x"], false));       // x matches nothing
    }

    public static IEnumerable<MatcherScenario> Includes()
    {
        yield return Build("includes[equalTo a, equalTo b]", "includes",
            [Eq("a"), Eq("b")],
            (["a", "b"], true),
            (["b", "a"], true),          // order-insensitive
            (["a", "b", "c"], true),     // extras allowed
            (["a"], false));             // missing b
    }

    private static Dictionary<string, object> Eq(string value) => new() { ["equalTo"] = value };

    private static MatcherScenario Build(
        string description, string key, Dictionary<string, object>[] subMatchers, params (string[] Values, bool Match)[] cases)
    {
        var mapping = new Dictionary<string, object>
        {
            ["request"] = new Dictionary<string, object>
            {
                ["method"] = "GET",
                ["urlPath"] = "/m",
                ["queryParameters"] = new Dictionary<string, object> { [Param] = new Dictionary<string, object> { [key] = subMatchers } },
            },
            ["response"] = new Dictionary<string, object> { ["status"] = 200, ["body"] = "ok" },
        };

        var probes = cases
            .Select(c => new ProbeRequest(
                new RequestSpec
                {
                    Method = "GET",
                    Url = "/m?" + string.Join('&', c.Values.Select(v => $"{Param}={Uri.EscapeDataString(v)}")),
                },
                c.Match))
            .ToList();

        return new MatcherScenario($"multivalue[{description}]", JsonSerializer.Serialize(mapping), probes);
    }
}
