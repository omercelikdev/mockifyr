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

    /// <summary>
    /// G2c built-in data helpers: <c>jsonPath</c>, <c>xPath</c>, <c>regexExtract</c>,
    /// <c>formData</c>, <c>parseJson</c>. Each stub echoes the helper output through the body so the
    /// harness diffs it against the oracle.
    /// </summary>
    public static IEnumerable<MatcherScenario> DataHelpers()
    {
        // jsonPath: scalar string/number/boolean, missing → empty, compact array.
        yield return Build(
            "jsonPath-scalars",
            new Dictionary<string, object> { ["method"] = "POST", ["urlPath"] = "/jp" },
            Templated("name={{jsonPath request.body '$.name'}}|n={{jsonPath request.body '$.n'}}|" +
                      "flag={{jsonPath request.body '$.flag'}}|miss=[{{jsonPath request.body '$.nope'}}]|" +
                      "arr={{jsonPath request.body '$.arr'}}"),
            Json("/jp", "{\"name\":\"neo\",\"n\":42,\"flag\":true,\"arr\":[1,2,3]}"),
            unmatchedUrl: "/nope");

        // jsonPath: object result is Jackson-pretty-printed.
        yield return Build(
            "jsonPath-object",
            new Dictionary<string, object> { ["method"] = "POST", ["urlPath"] = "/jo" },
            Templated("obj={{jsonPath request.body '$.obj'}}"),
            Json("/jo", "{\"obj\":{\"a\":1,\"b\":[2,3],\"c\":\"x\"}}"));

        // xPath: text(), attribute, string(), count(), missing → empty.
        yield return Build(
            "xPath-values",
            new Dictionary<string, object> { ["method"] = "POST", ["urlPath"] = "/xv" },
            Templated("t={{xPath request.body '/r/a/text()'}}|id={{xPath request.body '/r/a/@id'}}|" +
                      "s={{xPath request.body 'string(/r/a)'}}|c={{xPath request.body 'count(/r/a)'}}|" +
                      "miss=[{{xPath request.body '/r/z/text()'}}]"),
            Xml("/xv", "<r><a id=\"7\">one</a><a>two</a></r>"));

        // xPath: element node is serialized as XML (leaf inline, tree indented).
        yield return Build(
            "xPath-element",
            new Dictionary<string, object> { ["method"] = "POST", ["urlPath"] = "/xe" },
            Templated("leaf={{xPath request.body '/r/a'}}|tree={{xPath request.body '/r'}}"),
            Xml("/xe", "<r><a>one</a></r>"));

        // regexExtract: whole-match extraction plus capture groups into an indexable variable.
        yield return Build(
            "regexExtract-groups",
            new Dictionary<string, object> { ["method"] = "POST", ["urlPath"] = "/re" },
            Templated("m={{regexExtract request.body '[0-9]+'}}|" +
                      "{{regexExtract request.body '([a-z]+)-([0-9]+)' 'p'}}g1={{p.0}}|g2={{p.1}}"),
            Text("/re", "ab-99"));

        // regexExtract: default= fallback on no match.
        yield return Build(
            "regexExtract-default",
            new Dictionary<string, object> { ["method"] = "POST", ["urlPath"] = "/rd" },
            Templated("v={{regexExtract request.body '[0-9]+' default='NONE'}}"),
            Text("/rd", "abc"));

        // regexExtract: no match and no default → WireMock's error string.
        yield return Build(
            "regexExtract-error",
            new Dictionary<string, object> { ["method"] = "POST", ["urlPath"] = "/rr" },
            Templated("v={{regexExtract request.body '[0-9]+'}}"),
            Text("/rr", "abc"));

        // formData: parse form-encoded body; first value, missing → empty, no url-decoding.
        yield return Build(
            "formData",
            new Dictionary<string, object> { ["method"] = "POST", ["urlPath"] = "/fd" },
            Templated("{{formData request.body 'form'}}a={{form.a}}|b={{form.b}}|miss=[{{form.z}}]"),
            Form("/fd", "a=1&b=hello%20world"));

        // formData: urlDecode=true decodes %XX and '+'.
        yield return Build(
            "formData-decode",
            new Dictionary<string, object> { ["method"] = "POST", ["urlPath"] = "/fdd" },
            Templated("{{formData request.body 'form' urlDecode=true}}b={{form.b}}|p={{form.p}}"),
            Form("/fdd", "b=hello%20world&p=a+b"));

        // parseJson: parse a JSON string into a navigable variable.
        yield return Build(
            "parseJson",
            new Dictionary<string, object> { ["method"] = "POST", ["urlPath"] = "/pj" },
            Templated("{{parseJson request.body 'o'}}name={{o.name}}|deep={{o.a.b}}|arr0={{o.arr.0}}"),
            Json("/pj", "{\"name\":\"neo\",\"a\":{\"b\":5},\"arr\":[7,8]}"));
    }

    private static Dictionary<string, object> Templated(string body) => new()
    {
        ["status"] = 200,
        ["transformers"] = new object[] { "response-template" },
        ["body"] = body,
    };

    private static RequestSpec Json(string url, string body) => Request(url, body, "application/json");

    private static RequestSpec Xml(string url, string body) => Request(url, body, "text/xml");

    private static RequestSpec Text(string url, string body) => Request(url, body, "text/plain");

    private static RequestSpec Form(string url, string body) =>
        Request(url, body, "application/x-www-form-urlencoded");

    private static RequestSpec Request(string url, string body, string contentType) => new()
    {
        Method = "POST",
        Url = url,
        Headers = [new("Content-Type", contentType)],
        Body = Encoding.UTF8.GetBytes(body),
    };

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
