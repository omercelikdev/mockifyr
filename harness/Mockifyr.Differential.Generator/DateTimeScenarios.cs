using System.Text.Json;

namespace Mockifyr.Differential.Generator;

/// <summary>
/// Fuzzes the date/time matchers (<c>before</c> / <c>after</c> / <c>equalToDateTime</c>) over the
/// deterministic subset: <b>absolute</b> ISO-8601 expected values, so the diff is race-free.
/// <c>now</c>-relative expected values are evaluated at request time by WireMock and cannot be
/// diffed deterministically against a second clock, so they are deferred; see
/// docs/parity/g1-matching.md.
/// </summary>
public static class DateTimeScenarios
{
    private const string Q = "d";

    /// <summary>before / after / equalToDateTime against an absolute UTC instant.</summary>
    public static IEnumerable<MatcherScenario> Comparisons()
    {
        const string expected = "2021-06-15T12:00:00Z";

        yield return Build("before", "before", expected,
            ("2021-06-15T11:00:00Z", true),
            ("2020-01-01T00:00:00Z", true),
            ("2021-06-15T12:00:00Z", false),
            ("2021-06-15T13:00:00Z", false),
            ("2022-01-01T00:00:00Z", false),
            ("not-a-date", false));

        yield return Build("after", "after", expected,
            ("2021-06-15T13:00:00Z", true),
            ("2022-01-01T00:00:00Z", true),
            ("2021-06-15T12:00:00Z", false),
            ("2021-06-15T11:00:00Z", false),
            ("2020-01-01T00:00:00Z", false),
            ("not-a-date", false));

        yield return Build("equalToDateTime", "equalToDateTime", expected,
            ("2021-06-15T12:00:00Z", true),
            ("2021-06-15T11:00:00Z", false),
            ("2021-06-15T13:00:00Z", false),
            ("2020-01-01T00:00:00Z", false),
            ("not-a-date", false));
    }

    /// <summary>
    /// <c>actualFormat</c> parses the incoming value with a custom (Java/​.NET-overlapping) pattern
    /// before comparing. The expected side stays ISO-8601.
    /// </summary>
    public static IEnumerable<MatcherScenario> ActualFormat()
    {
        const string expected = "2021-06-15T00:00:00Z";

        yield return BuildWithFormat("after[dd/MM/yyyy]", "after", expected, "dd/MM/yyyy",
            ("16/06/2021", true),
            ("01/07/2021", true),
            ("14/06/2021", false),
            ("15/06/2021", false),
            ("not-a-date", false));

        yield return BuildWithFormat("before[dd/MM/yyyy]", "before", expected, "dd/MM/yyyy",
            ("14/06/2021", true),
            ("01/01/2020", true),
            ("15/06/2021", false),
            ("16/06/2021", false));
    }

    private static MatcherScenario Build(
        string description, string key, string expected, params (string Value, bool Match)[] cases) =>
        Compose(description, new Dictionary<string, object> { [key] = expected }, cases);

    private static MatcherScenario BuildWithFormat(
        string description, string key, string expected, string actualFormat, params (string Value, bool Match)[] cases) =>
        Compose(
            description,
            new Dictionary<string, object> { [key] = expected, ["actualFormat"] = actualFormat },
            cases);

    private static MatcherScenario Compose(
        string description, Dictionary<string, object> matcher, (string Value, bool Match)[] cases)
    {
        var mapping = new Dictionary<string, object>
        {
            ["request"] = new Dictionary<string, object>
            {
                ["method"] = "GET",
                ["urlPath"] = "/p",
                ["queryParameters"] = new Dictionary<string, object> { [Q] = matcher },
            },
            ["response"] = new Dictionary<string, object> { ["status"] = 200, ["body"] = "ok" },
        };

        var probes = cases
            .Select(c => new ProbeRequest(
                new RequestSpec { Method = "GET", Url = $"/p?{Q}={Uri.EscapeDataString(c.Value)}" },
                c.Match))
            .ToList();

        return new MatcherScenario($"datetime[{description}]", JsonSerializer.Serialize(mapping), probes);
    }
}
