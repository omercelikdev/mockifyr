using System.Text;
using System.Text.Json;

namespace Mockifyr.Differential.Generator;

/// <summary>Which part of the request a matcher targets.</summary>
public enum Target
{
    /// <summary>A request header.</summary>
    Header,

    /// <summary>A query parameter.</summary>
    Query,

    /// <summary>The request body.</summary>
    Body,
}

/// <summary>A single request probe against a scenario's stub, with the expected match decision.</summary>
public sealed record ProbeRequest(RequestSpec Request, bool ExpectedMatch);

/// <summary>One stub plus many request probes; the harness loads the stub once and sends every probe.</summary>
public sealed record MatcherScenario(string Description, string WireMockJson, IReadOnlyList<ProbeRequest> Probes);

/// <summary>
/// Deterministic, seed-driven generators that fuzz the standard matchers across the text corpus.
/// Each scenario pairs a stub with a matching probe and several non-matching probes, so the
/// differential harness can assert the match/no-match decision agrees with the oracle over
/// hundreds of cases rather than a handful of hand-picked ones. See docs/decisions/0002.
/// </summary>
public static class MatcherScenarios
{
    private const string ProbeHeader = "X-Probe";
    private const string ProbeQuery = "p";

    /// <summary>equalTo across the corpus for a target: one matching probe, a few non-matching.</summary>
    public static IEnumerable<MatcherScenario> EqualTo(Target target, int seed)
    {
        var rng = new Random(seed);
        var corpus = CorpusFor(target);

        foreach (var expected in corpus)
        {
            var probes = new List<ProbeRequest> { new(Request(target, expected), ExpectedMatch: true) };
            foreach (var other in PickDistinct(corpus, expected, 2, rng))
            {
                probes.Add(new(Request(target, other), ExpectedMatch: false));
            }

            yield return new MatcherScenario(
                $"equalTo[{target}] expected={Describe(expected)}",
                Stub(target, "equalTo", expected),
                probes);
        }
    }

    /// <summary>contains across a set of needles for a target.</summary>
    public static IEnumerable<MatcherScenario> Contains(Target target, int seed)
    {
        _ = seed;
        string[] needles = ["a", "abc", "x y", "{}", "123", "\"", "A"];
        string[] pool = ["zzz", "different", "QWERTY", "000", "-", "path/to"];

        foreach (var needle in needles)
        {
            var matching = "pre" + needle + "post";
            var probes = new List<ProbeRequest> { new(Request(target, matching), ExpectedMatch: true) };
            foreach (var candidate in pool.Where(v => !v.Contains(needle, StringComparison.Ordinal)).Take(2))
            {
                probes.Add(new(Request(target, candidate), ExpectedMatch: false));
            }

            yield return new MatcherScenario(
                $"contains[{target}] needle={Describe(needle)}",
                Stub(target, "contains", needle),
                probes);
        }
    }

    /// <summary>matches (regex) on the body, exercising WireMock's full-match semantics.</summary>
    public static IEnumerable<MatcherScenario> Matches(int seed)
    {
        _ = seed;
        (string Pattern, string[] Matching, string[] NonMatching)[] cases =
        [
            ("[0-9]+", ["0", "123", "9876543210"], ["", "12a", "a12", "1 2", "12.3"]),
            ("[a-z]+[0-9]+", ["abc123", "z9"], ["abc", "123", "abc123x", "ABC123"]),
            ("a.c", ["abc", "axc", "a.c"], ["ac", "abbc", "abcd", "xabc"]),
            (@"\d{3}", ["123", "007"], ["12", "1234", "12a"]),
            ("hello|world", ["hello", "world"], ["hell", "helloworld", "HELLO"]),
        ];

        foreach (var (pattern, matching, nonMatching) in cases)
        {
            var probes = new List<ProbeRequest>();
            probes.AddRange(matching.Select(v => new ProbeRequest(Request(Target.Body, v), ExpectedMatch: true)));
            probes.AddRange(nonMatching.Select(v => new ProbeRequest(Request(Target.Body, v), ExpectedMatch: false)));

            yield return new MatcherScenario(
                $"matches[Body] pattern={Describe(pattern)}",
                Stub(Target.Body, "matches", pattern),
                probes);
        }
    }

    /// <summary>absent on a header or query target: matches when the target is missing.</summary>
    public static IEnumerable<MatcherScenario> Absent(Target target)
    {
        var probes = new List<ProbeRequest>
        {
            new(RequestWithoutTarget(target), ExpectedMatch: true),
            new(Request(target, "present"), ExpectedMatch: false),
            new(Request(target, "123"), ExpectedMatch: false),
        };

        yield return new MatcherScenario($"absent[{target}]", AbsentStub(target), probes);
    }

    private static IReadOnlyList<string> CorpusFor(Target target) =>
        target == Target.Body ? TextCorpus.Body : TextCorpus.HeaderSafe;

    private static IEnumerable<string> PickDistinct(IReadOnlyList<string> corpus, string exclude, int count, Random rng)
    {
        var candidates = corpus.Where(v => !string.Equals(v, exclude, StringComparison.Ordinal)).ToList();
        for (var i = 0; i < count && candidates.Count > 0; i++)
        {
            var index = rng.Next(candidates.Count);
            yield return candidates[index];
            candidates.RemoveAt(index);
        }
    }

    private static string Stub(Target target, string matcherKey, string expected)
    {
        var matcherSpec = new Dictionary<string, object> { [matcherKey] = expected };
        return SerializeStub(target, matcherSpec);
    }

    private static string AbsentStub(Target target)
    {
        var matcherSpec = new Dictionary<string, object> { ["absent"] = true };
        return SerializeStub(target, matcherSpec);
    }

    private static string SerializeStub(Target target, Dictionary<string, object> matcherSpec)
    {
        var request = new Dictionary<string, object>
        {
            ["method"] = target == Target.Body ? "POST" : "GET",
            ["urlPath"] = "/p",
        };

        switch (target)
        {
            case Target.Header:
                request["headers"] = new Dictionary<string, object> { [ProbeHeader] = matcherSpec };
                break;
            case Target.Query:
                request["queryParameters"] = new Dictionary<string, object> { [ProbeQuery] = matcherSpec };
                break;
            case Target.Body:
                request["bodyPatterns"] = new object[] { matcherSpec };
                break;
        }

        var mapping = new Dictionary<string, object>
        {
            ["request"] = request,
            ["response"] = new Dictionary<string, object> { ["status"] = 200, ["body"] = "ok" },
        };

        return JsonSerializer.Serialize(mapping);
    }

    private static RequestSpec Request(Target target, string value) => target switch
    {
        Target.Header => new RequestSpec { Method = "GET", Url = "/p", Headers = [new(ProbeHeader, value)] },
        Target.Query => new RequestSpec { Method = "GET", Url = $"/p?{ProbeQuery}={Uri.EscapeDataString(value)}" },
        Target.Body => new RequestSpec { Method = "POST", Url = "/p", Body = Encoding.UTF8.GetBytes(value) },
        _ => throw new ArgumentOutOfRangeException(nameof(target)),
    };

    private static RequestSpec RequestWithoutTarget(Target target) => target switch
    {
        Target.Header => new RequestSpec { Method = "GET", Url = "/p" },
        Target.Query => new RequestSpec { Method = "GET", Url = "/p" },
        _ => throw new ArgumentOutOfRangeException(nameof(target)),
    };

    private static string Describe(string value)
    {
        var trimmed = value.Length > 24 ? value[..24] + "…" : value;
        return "\"" + trimmed.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "\"";
    }
}
