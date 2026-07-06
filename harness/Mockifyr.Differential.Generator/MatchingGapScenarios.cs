using System.Text;
using System.Text.Json;

namespace Mockifyr.Differential.Generator;

/// <summary>
/// Differential scenarios for matcher gaps surfaced by the WireMock feature audit: the
/// <c>doesNotContain</c> value matcher and <c>formParameters</c> (matching a form-encoded body's
/// parameters). Validated against the oracle. See docs/parity/g1-matching.md.
/// </summary>
public static class MatchingGapScenarios
{
    public static IEnumerable<MatcherScenario> DoesNotContain()
    {
        var matcher = new Dictionary<string, object>
        {
            ["method"] = "POST",
            ["urlPath"] = "/p",
            ["bodyPatterns"] = new object[] { new Dictionary<string, object> { ["doesNotContain"] = "bar" } },
        };

        yield return Compose("doesNotContain", matcher,
            ("baz", true),      // does not contain "bar"
            ("hello", true),
            ("foobar", false),  // contains "bar"
            ("bar", false));
    }

    public static IEnumerable<MatcherScenario> FormParameters()
    {
        yield return Compose("formParameters/equalTo",
            Form(new Dictionary<string, object> { ["a"] = new Dictionary<string, object> { ["equalTo"] = "1" } }),
            ("a=1&b=2", true),
            ("a=9", false),
            ("b=2", false));    // `a` absent

        yield return Compose("formParameters/matches",
            Form(new Dictionary<string, object> { ["a"] = new Dictionary<string, object> { ["matches"] = "[0-9]+" } }),
            ("a=42", true),
            ("a=x", false));
    }

    private static Dictionary<string, object> Form(Dictionary<string, object> formParameters) => new()
    {
        ["method"] = "POST",
        ["urlPath"] = "/p",
        ["formParameters"] = formParameters,
    };

    private static MatcherScenario Compose(string description, Dictionary<string, object> requestPattern, params (string Body, bool Match)[] bodies)
    {
        var mapping = new Dictionary<string, object>
        {
            ["request"] = requestPattern,
            ["response"] = new Dictionary<string, object> { ["status"] = 200, ["body"] = "ok" },
        };

        var isForm = requestPattern.ContainsKey("formParameters");
        var probes = bodies
            .Select(b => new ProbeRequest(
                new RequestSpec
                {
                    Method = "POST",
                    Url = "/p",
                    Headers = isForm ? [new("Content-Type", "application/x-www-form-urlencoded")] : [],
                    Body = Encoding.UTF8.GetBytes(b.Body),
                },
                b.Match))
            .ToList();

        return new MatcherScenario($"matching-gap[{description}]", JsonSerializer.Serialize(mapping), probes);
    }
}
