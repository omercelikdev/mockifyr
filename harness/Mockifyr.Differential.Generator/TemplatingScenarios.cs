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

    /// <summary>
    /// G2d date/time helpers: <c>parseDate</c> + <c>date</c>. Every case parses a <em>fixed</em>
    /// instant (so the output is deterministic and clock-independent) and renders it through the
    /// oracle for a byte diff. The clock-dependent surface (<c>now</c>, unparseable-date fallback)
    /// is deliberately excluded — it is racy against a second clock.
    /// </summary>
    public static IEnumerable<MatcherScenario> DateHelpers()
    {
        // Format patterns (shared Java/.NET letters) plus the default ISO-8601 rendering.
        yield return Get(
            "date-format", "/df",
            "d={{date (parseDate '2021-05-15T10:30:00Z') format='yyyy-MM-dd'}}|" +
            "t={{date (parseDate '2021-05-15T10:30:00Z') format='HH:mm:ss'}}|" +
            "def={{date (parseDate '2021-05-15T10:30:00Z')}}",
            unmatchedUrl: "/nope-date");

        // offset= across every supported (plural) unit, forwards and backwards.
        yield return Get(
            "date-offset", "/dof",
            "dd={{date (parseDate '2021-05-15T10:30:00Z') offset='3 days' format='yyyy-MM-dd'}}|" +
            "hh={{date (parseDate '2021-05-15T10:30:00Z') offset='-1 hours' format='HH:mm'}}|" +
            "mo={{date (parseDate '2021-05-15T10:30:00Z') offset='2 months' format='yyyy-MM-dd'}}|" +
            "yy={{date (parseDate '2021-05-15T10:30:00Z') offset='1 years' format='yyyy-MM-dd'}}|" +
            "mi={{date (parseDate '2021-05-15T10:30:00Z') offset='90 minutes' format='HH:mm'}}|" +
            "se={{date (parseDate '2021-05-15T10:30:00Z') offset='-30 seconds' format='HH:mm:ss'}}");

        // Java SimpleDateFormat letters that differ from .NET: E (day name), a (AM/PM), S (millis).
        yield return Get(
            "date-java-patterns", "/djp",
            "rfc={{date (parseDate '2021-05-15T10:30:00Z') format='EEE, dd MMM yyyy HH:mm:ss'}}|" +
            "ap={{date (parseDate '2021-05-15T22:30:00Z') format='hh:mm a'}}|" +
            "ms={{date (parseDate '2021-05-15T10:30:00Z') format='yyyy-MM-dd HH:mm:ss.SSS'}}");

        // The special epoch (milliseconds) and unix (seconds) format tokens.
        yield return Get(
            "date-epoch-unix", "/deu",
            "epoch={{date (parseDate '2021-05-15T10:30:00Z') format='epoch'}}|" +
            "unix={{date (parseDate '2021-05-15T10:30:00Z') format='unix'}}");

        // parseDate with an explicit (non-ISO) input format.
        yield return Get(
            "date-parse-custom", "/dpc",
            "custom={{date (parseDate '15/05/2021' format='dd/MM/yyyy') format='yyyy-MM-dd'}}");

        // timezone= is ignored on a parsed instant (oracle applies no shift) — pin that.
        yield return Get(
            "date-timezone-ignored", "/dtz",
            "utc={{date (parseDate '2021-05-15T10:30:00Z') format='HH:mm'}}|" +
            "syd={{date (parseDate '2021-05-15T10:30:00Z') timezone='Australia/Sydney' format='HH:mm'}}");
    }

    /// <summary>
    /// G2f JSON-manipulation helpers: <c>jsonArrayAdd</c>, <c>jsonMerge</c>, <c>jsonRemove</c>
    /// (compact output) and <c>toJson</c> (Jackson-pretty). Each stub echoes the helper output for a
    /// byte diff against the oracle.
    /// </summary>
    public static IEnumerable<MatcherScenario> JsonHelpers()
    {
        // jsonArrayAdd: append a parsed item; the object form; maxItems drops from the front.
        yield return Get(
            "jsonArrayAdd", "/ja",
            "add={{jsonArrayAdd '[1,2,3]' '4'}}|" +
            "obj={{jsonArrayAdd '[1,2]' '{\"k\":9}'}}|" +
            "cap={{jsonArrayAdd '[1,2,3]' '4' maxItems=3}}",
            unmatchedUrl: "/nope-json");

        // jsonMerge: B overrides A (A keeps order, new keys appended); deep merge of nested objects.
        yield return Get(
            "jsonMerge", "/jm",
            "flat={{jsonMerge '{\"x\":1,\"z\":0}' '{\"x\":9,\"y\":2}'}}|" +
            "deep={{jsonMerge '{\"a\":{\"x\":1}}' '{\"a\":{\"y\":2}}'}}");

        // jsonRemove: top-level and nested paths.
        yield return Get(
            "jsonRemove", "/jr",
            "top={{jsonRemove '{\"a\":1,\"b\":2}' '$.b'}}|" +
            "nested={{jsonRemove '{\"a\":{\"b\":1,\"c\":2}}' '$.a.b'}}");

        // toJson: Jackson-pretty rendering of an object and of an array (spaced `[ 1, 2, 3 ]`).
        yield return Get(
            "toJson", "/tj",
            "obj={{toJson (jsonPath request.body '$.obj')}}|arr={{toJson (jsonPath request.body '$.arr')}}",
            request: Json("/tj", "{\"obj\":{\"k\":1},\"arr\":[1,2,3]}"));
    }

    private static MatcherScenario Get(
        string description, string url, string body, RequestSpec? request = null, string? unmatchedUrl = null)
    {
        var matching = request ?? new RequestSpec { Method = "GET", Url = url };
        return Build(
            description,
            new Dictionary<string, object> { ["method"] = matching.Method, ["urlPath"] = url },
            Templated(body),
            matching,
            unmatchedUrl);
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
