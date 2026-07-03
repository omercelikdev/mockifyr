using System.Text;
using System.Text.Json;

namespace Mockifyr.Differential.Generator;

/// <summary>
/// Fuzzes response templating (G2b): the <c>response-template</c> transformer with the request
/// model (<c>method</c>, <c>url</c>, <c>path</c>, <c>pathSegments</c>, <c>query</c>, <c>headers</c>,
/// <c>body</c>), non-escaping output, and templated response headers. The harness diffs the rendered
/// response against the oracle. See docs/parity/g2-response.md.
/// </summary>
public static class TemplatingScenarios
{
    public static IEnumerable<MatcherScenario> ResponseTemplate()
    {
        // Echo the request model back through the template.
        yield return Build(
            "request-model",
            new Dictionary<string, object> { ["method"] = "POST", ["urlPathPattern"] = "/t/.*" },
            new Dictionary<string, object>
            {
                ["status"] = 200,
                ["transformers"] = new object[] { "response-template" },
                ["body"] = "m={{request.method}} url={{request.url}} path={{request.path}} " +
                           "q={{request.query.foo}} h={{request.headers.X}} seg1={{request.pathSegments.[1]}} " +
                           "body={{request.body}} miss=[{{request.query.none}}]",
            },
            new RequestSpec
            {
                Method = "POST",
                Url = "/t/abc?foo=bar",
                Headers = [new("X", "hval")],
                Body = Encoding.UTF8.GetBytes("reqbody"),
            },
            unmatchedUrl: "/other");

        // Output is not HTML-escaped.
        yield return Build(
            "no-escape",
            new Dictionary<string, object> { ["method"] = "POST", ["urlPath"] = "/e" },
            new Dictionary<string, object>
            {
                ["status"] = 200,
                ["transformers"] = new object[] { "response-template" },
                ["body"] = "[{{request.body}}]",
            },
            new RequestSpec { Method = "POST", Url = "/e", Body = Encoding.UTF8.GetBytes("<a>&\"x") });

        // Response headers are templated too.
        yield return Build(
            "templated-header",
            new Dictionary<string, object> { ["method"] = "GET", ["urlPath"] = "/h" },
            new Dictionary<string, object>
            {
                ["status"] = 200,
                ["transformers"] = new object[] { "response-template" },
                ["headers"] = new Dictionary<string, object> { ["X-Echo"] = "{{request.query.v}}" },
                ["body"] = "ok",
            },
            new RequestSpec { Method = "GET", Url = "/h?v=hello" });

        // A stub without the transformer must NOT template (verbatim body).
        yield return Build(
            "no-transformer",
            new Dictionary<string, object> { ["method"] = "GET", ["urlPath"] = "/raw" },
            new Dictionary<string, object> { ["status"] = 200, ["body"] = "raw {{request.path}}" },
            new RequestSpec { Method = "GET", Url = "/raw" });
    }

    private static MatcherScenario Build(
        string description,
        Dictionary<string, object> requestPattern,
        Dictionary<string, object> response,
        RequestSpec matching,
        string? unmatchedUrl = null)
    {
        var mapping = new Dictionary<string, object> { ["request"] = requestPattern, ["response"] = response };

        var probes = new List<ProbeRequest> { new(matching, ExpectedMatch: true) };
        if (unmatchedUrl is not null)
        {
            probes.Add(new(new RequestSpec { Method = matching.Method, Url = unmatchedUrl }, ExpectedMatch: false));
        }

        return new MatcherScenario($"templating[{description}]", JsonSerializer.Serialize(mapping), probes);
    }
}
