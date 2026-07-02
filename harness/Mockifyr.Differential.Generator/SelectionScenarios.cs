using System.Text.Json;

namespace Mockifyr.Differential.Generator;

/// <summary>
/// Fuzzes stub priority &amp; selection (G1k): when several stubs match, the lowest priority number
/// wins and an unset priority defaults to 5. Uses <b>distinct</b> priorities so the outcome is
/// independent of insertion order (WireMock's equal-priority tie-break is load-path dependent and
/// deferred). Each scenario is a multi-stub mappings wrapper; the probe asserts both engines serve
/// the same winning stub. See docs/parity/g1-matching.md.
/// </summary>
public static class SelectionScenarios
{
    public static IEnumerable<MatcherScenario> Priority()
    {
        // Three overlapping stubs; priority 1 must win over 2 and 3.
        yield return Build(
            "distinct priorities 1<2<3",
            [(1, "/p", "P1"), (2, "/p", "P2"), (3, "/p", "P3")],
            (Get("/p"), Match: true),
            (Get("/none"), Match: false)); // coverage: an unmatched probe

        // An explicit priority below the default (5) wins over an unset stub.
        yield return Build(
            "explicit 3 beats default",
            [(3, "/d", "explicit3"), (null, "/d", "defaultUnset")],
            (Get("/d"), Match: true));

        // The default (5) wins over an explicit priority above it, confirming default == 5.
        yield return Build(
            "default beats explicit 8",
            [(8, "/e", "explicit8"), (null, "/e", "defaultUnset")],
            (Get("/e"), Match: true));
    }

    private static RequestSpec Get(string url) => new() { Method = "GET", Url = url };

    private static MatcherScenario Build(
        string description,
        (int? Priority, string Path, string Body)[] stubs,
        params (RequestSpec Request, bool Match)[] probes)
    {
        var mappings = stubs.Select(s =>
        {
            var mapping = new Dictionary<string, object>
            {
                ["request"] = new Dictionary<string, object> { ["method"] = "GET", ["urlPath"] = s.Path },
                ["response"] = new Dictionary<string, object> { ["status"] = 200, ["body"] = s.Body },
            };
            if (s.Priority is { } priority)
            {
                mapping["priority"] = priority;
            }

            return mapping;
        }).ToArray();

        var wrapper = new Dictionary<string, object> { ["mappings"] = mappings };
        var json = JsonSerializer.Serialize(wrapper);

        var probeList = probes.Select(p => new ProbeRequest(p.Request, p.Match)).ToList();
        return new MatcherScenario($"selection[{description}]", json, probeList);
    }
}
