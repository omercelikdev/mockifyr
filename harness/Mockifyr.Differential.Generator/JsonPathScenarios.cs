using System.Text;
using System.Text.Json;

namespace Mockifyr.Differential.Generator;

/// <summary>
/// Fuzzes <c>matchesJsonPath</c> (body) over the common JSONPath subset: property access, array
/// index, wildcards, and recursive descent — both the presence form and the expression +
/// sub-matcher form. Jayway-specific dialect edges (filters, functions) are validated later; see
/// docs/parity/g1-matching.md.
/// </summary>
public static class JsonPathScenarios
{
    /// <summary>Presence form: the stub matches when the path selects at least one node.</summary>
    public static IEnumerable<MatcherScenario> Presence()
    {
        yield return Presence("$.name",
            ("""{"name":"x"}""", true), ("""{"other":1}""", false), ("not json", false));
        yield return Presence("$.a.b",
            ("""{"a":{"b":1}}""", true), ("""{"a":{}}""", false), ("""{"a":1}""", false));
        yield return Presence("$.items[0]",
            ("""{"items":[9]}""", true), ("""{"items":[]}""", false), ("""{"items":1}""", false));
        yield return Presence("$..id",
            ("""{"x":{"id":1}}""", true), ("""{"a":[{"id":7}]}""", true), ("""{"x":{"y":2}}""", false));
        yield return Presence("$.store.book[*].author",
            ("""{"store":{"book":[{"author":"a"}]}}""", true), ("""{"store":{"book":[]}}""", false));
    }

    /// <summary>Expression + sub-matcher form: the extracted value must satisfy the sub-matcher.</summary>
    public static IEnumerable<MatcherScenario> SubMatcher()
    {
        yield return Sub("$.name", "equalTo", "tom",
            ("""{"name":"tom"}""", true), ("""{"name":"bob"}""", false), ("""{"other":1}""", false));
        yield return Sub("$.color", "contains", "ed",
            ("""{"color":"red"}""", true), ("""{"color":"blue"}""", false));
        yield return Sub("$.a.b", "equalTo", "deep",
            ("""{"a":{"b":"deep"}}""", true), ("""{"a":{"b":"shallow"}}""", false));
    }

    /// <summary>
    /// Numeric filter form (G1j): <c>[?(@.field &gt; n)]</c> selects array elements by numeric
    /// comparison, and the stub matches when at least one element passes the filter. This is the
    /// oracle-validatable route to numeric matching in open-source WireMock (there is no standalone
    /// <c>equalToNumber</c> matcher — see docs/parity/g1-matching.md).
    /// </summary>
    public static IEnumerable<MatcherScenario> NumericFilters()
    {
        yield return Presence("$.items[?(@.price > 10)]",
            ("""{"items":[{"price":20}]}""", true),
            ("""{"items":[{"price":5},{"price":15}]}""", true),
            ("""{"items":[{"price":10}]}""", false),   // strict >
            ("""{"items":[{"price":5}]}""", false),
            ("""{"items":[]}""", false),
            ("not json", false));

        yield return Presence("$.items[?(@.price >= 10)]",
            ("""{"items":[{"price":10}]}""", true),
            ("""{"items":[{"price":11}]}""", true),
            ("""{"items":[{"price":9}]}""", false));

        yield return Presence("$.items[?(@.price < 10)]",
            ("""{"items":[{"price":5}]}""", true),
            ("""{"items":[{"price":10}]}""", false),
            ("""{"items":[{"price":11}]}""", false));

        yield return Presence("$.items[?(@.price <= 10)]",
            ("""{"items":[{"price":10}]}""", true),
            ("""{"items":[{"price":9}]}""", true),
            ("""{"items":[{"price":11}]}""", false));

        yield return Presence("$.items[?(@.qty == 3)]",
            ("""{"items":[{"qty":3}]}""", true),
            ("""{"items":[{"qty":3.0}]}""", true),      // numeric equality ignores scale
            ("""{"items":[{"qty":4}]}""", false),
            ("""{"items":[{"qty":2},{"qty":3}]}""", true));

        // Decimal comparison.
        yield return Presence("$.items[?(@.rate > 1.5)]",
            ("""{"items":[{"rate":1.6}]}""", true),
            ("""{"items":[{"rate":1.5}]}""", false),
            ("""{"items":[{"rate":1.4}]}""", false));
    }

    /// <summary>
    /// String-equality filter form: <c>[?(@.field == 'value')]</c> selects array elements whose field
    /// equals the string, and the stub matches when at least one passes. Confirms Newtonsoft's filter
    /// dialect agrees with WireMock's Jayway engine on string comparisons (the numeric side is G1j).
    /// </summary>
    public static IEnumerable<MatcherScenario> StringFilters()
    {
        yield return Presence("$.items[?(@.name == 'neo')]",
            ("""{"items":[{"name":"neo"}]}""", true),
            ("""{"items":[{"name":"morpheus"},{"name":"neo"}]}""", true),
            ("""{"items":[{"name":"trinity"}]}""", false),
            ("""{"items":[{"name":"Neo"}]}""", false),          // case-sensitive
            ("""{"items":[]}""", false),
            ("not json", false));

        yield return Presence("$.users[?(@.role == 'admin')]",
            ("""{"users":[{"role":"admin"}]}""", true),
            ("""{"users":[{"role":"user"},{"role":"admin"}]}""", true),
            ("""{"users":[{"role":"user"}]}""", false));

        // Inequality filter.
        yield return Presence("$.users[?(@.role != 'admin')]",
            ("""{"users":[{"role":"user"}]}""", true),
            ("""{"users":[{"role":"admin"}]}""", false),
            ("""{"users":[{"role":"admin"},{"role":"user"}]}""", true));
    }

    private static MatcherScenario Presence(string path, params (string Body, bool Match)[] bodies) =>
        Build($"matchesJsonPath[presence] {path}", new Dictionary<string, object> { ["matchesJsonPath"] = path }, bodies);

    private static MatcherScenario Sub(string path, string key, string value, params (string Body, bool Match)[] bodies) =>
        Build(
            $"matchesJsonPath[{key}] {path}",
            new Dictionary<string, object> { ["matchesJsonPath"] = new Dictionary<string, object> { ["expression"] = path, [key] = value } },
            bodies);

    private static MatcherScenario Build(string description, Dictionary<string, object> matcher, (string Body, bool Match)[] bodies)
    {
        var mapping = new Dictionary<string, object>
        {
            ["request"] = new Dictionary<string, object>
            {
                ["method"] = "POST",
                ["urlPath"] = "/p",
                ["bodyPatterns"] = new object[] { matcher },
            },
            ["response"] = new Dictionary<string, object> { ["status"] = 200, ["body"] = "ok" },
        };

        var probes = bodies
            .Select(b => new ProbeRequest(
                new RequestSpec { Method = "POST", Url = "/p", Body = Encoding.UTF8.GetBytes(b.Body) },
                b.Match))
            .ToList();

        return new MatcherScenario(description, JsonSerializer.Serialize(mapping), probes);
    }
}
