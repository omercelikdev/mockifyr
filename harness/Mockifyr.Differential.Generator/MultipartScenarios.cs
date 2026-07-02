using System.Text;
using System.Text.Json;

namespace Mockifyr.Differential.Generator;

/// <summary>
/// Fuzzes <c>multipartPatterns</c> (G1k). Verified oracle semantics: a part satisfies a pattern
/// when all of its <c>bodyPatterns</c> match that part, <c>matchingType</c> chooses ANY (default)
/// or ALL across parts, the per-pattern <c>name</c> is a no-op, and a non-multipart request never
/// matches. See docs/parity/g1-matching.md.
/// </summary>
public static class MultipartScenarios
{
    private const string Boundary = "XABC";
    private const string ContentType = "multipart/form-data; boundary=" + Boundary;

    public static IEnumerable<MatcherScenario> Multipart()
    {
        // Default matchingType (ANY): at least one part must contain "hello".
        yield return Build(
            "any[contains hello]",
            MatchingType: null,
            [new Dictionary<string, object> { ["contains"] = "hello" }],
            (Parts(("f1", "hello"), ("f2", "bye")), true),
            (Parts(("f1", "hello"), ("f2", "hello")), true),
            (Parts(("f1", "bye"), ("f2", "other")), false),
            (NonMultipart("hello there"), false)); // multipart required

        // ALL: every part must contain "hello".
        yield return Build(
            "all[contains hello]",
            MatchingType: "ALL",
            [new Dictionary<string, object> { ["contains"] = "hello" }],
            (Parts(("f1", "hello"), ("f2", "hello")), true),
            (Parts(("f1", "hello"), ("f2", "bye")), false));

        // Two body patterns must both match the SAME part.
        yield return Build(
            "any[contains a, contains b]",
            MatchingType: "ANY",
            [
                new Dictionary<string, object> { ["contains"] = "a" },
                new Dictionary<string, object> { ["contains"] = "b" },
            ],
            (Parts(("f1", "ab"), ("f2", "zzz")), true),
            (Parts(("f1", "a"), ("f2", "b")), false)); // split across parts
    }

    private static MatcherScenario Build(
        string description,
        string? MatchingType,
        IReadOnlyList<Dictionary<string, object>> bodyPatterns,
        params (RequestSpec Request, bool Match)[] probes)
    {
        var pattern = new Dictionary<string, object> { ["bodyPatterns"] = bodyPatterns };
        if (MatchingType is not null)
        {
            pattern["matchingType"] = MatchingType;
        }

        var mapping = new Dictionary<string, object>
        {
            ["request"] = new Dictionary<string, object>
            {
                ["method"] = "POST",
                ["urlPath"] = "/m",
                ["multipartPatterns"] = new object[] { pattern },
            },
            ["response"] = new Dictionary<string, object> { ["status"] = 200, ["body"] = "ok" },
        };

        var probeList = probes.Select(p => new ProbeRequest(p.Request, p.Match)).ToList();
        return new MatcherScenario($"multipart[{description}]", JsonSerializer.Serialize(mapping), probeList);
    }

    private static RequestSpec Parts(params (string Name, string Value)[] parts)
    {
        var builder = new StringBuilder();
        foreach (var (name, value) in parts)
        {
            builder.Append("--").Append(Boundary).Append("\r\n")
                .Append("Content-Disposition: form-data; name=\"").Append(name).Append("\"\r\n\r\n")
                .Append(value).Append("\r\n");
        }

        builder.Append("--").Append(Boundary).Append("--\r\n");

        return new RequestSpec
        {
            Method = "POST",
            Url = "/m",
            Headers = [new("Content-Type", ContentType)],
            Body = Encoding.UTF8.GetBytes(builder.ToString()),
        };
    }

    private static RequestSpec NonMultipart(string body) => new()
    {
        Method = "POST",
        Url = "/m",
        Headers = [new("Content-Type", "text/plain")],
        Body = Encoding.UTF8.GetBytes(body),
    };
}
