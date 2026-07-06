using System.Text;
using System.Text.Json;

namespace Mockifyr.Differential.Generator;

/// <summary>
/// Fuzzes <c>equalToXml</c> and <c>matchesXPath</c> (body) over the common subset: semantic XML
/// equality (whitespace / attribute order insignificant, element order significant) and XPath
/// presence / sub-matcher on element, attribute, and descendant selection, plus **namespaced XPath**
/// via <c>xPathNamespaces</c>. Advanced options (placeholders, explicit namespaceAwareness modes,
/// XPath functions) are validated later; see docs/parity/g1-matching.md.
/// </summary>
public static class XmlScenarios
{
    public static IEnumerable<MatcherScenario> EqualToXml()
    {
        const string baseXml = """<order id="1" ref="x"><item>book</item><qty>2</qty></order>""";

        yield return Build(
            "equalToXml basic",
            new Dictionary<string, object> { ["equalToXml"] = baseXml },
            (baseXml, true),
            ("""<order id="1" ref="x">   <item>book</item>   <qty>2</qty>   </order>""", true), // whitespace
            ("""<order ref="x" id="1"><item>book</item><qty>2</qty></order>""", true),           // attribute order
            ("""<order id="1" ref="x"><item>magazine</item><qty>2</qty></order>""", false),      // changed text
            ("""<order id="2" ref="x"><item>book</item><qty>2</qty></order>""", false),          // changed attribute
            ("""<order id="1" ref="x"><qty>2</qty><item>book</item></order>""", true),           // reordered children: order-insensitive
            ("not xml", false));
    }

    public static IEnumerable<MatcherScenario> MatchesXPath()
    {
        yield return Build(
            "matchesXPath[presence] /order/item",
            new Dictionary<string, object> { ["matchesXPath"] = "/order/item" },
            ("""<order><item>book</item></order>""", true),
            ("""<order><thing>x</thing></order>""", false),
            ("not xml", false));

        yield return Build(
            "matchesXPath[presence] //qty",
            new Dictionary<string, object> { ["matchesXPath"] = "//qty" },
            ("""<order><line><qty>2</qty></line></order>""", true),
            ("""<order><item>x</item></order>""", false));

        yield return Build(
            "matchesXPath[presence] /order/@id",
            new Dictionary<string, object> { ["matchesXPath"] = "/order/@id" },
            ("""<order id="1"/>""", true),
            ("""<order/>""", false));

        yield return Build(
            "matchesXPath[equalTo] /order/item/text()",
            new Dictionary<string, object>
            {
                ["matchesXPath"] = new Dictionary<string, object> { ["expression"] = "/order/item/text()", ["equalTo"] = "book" },
            },
            ("""<order><item>book</item></order>""", true),
            ("""<order><item>magazine</item></order>""", false));
    }

    /// <summary>
    /// Namespaced <c>matchesXPath</c> via <c>xPathNamespaces</c> (prefix → URI). A prefixed step must
    /// match the bound namespace URI, an unprefixed step is namespace-agnostic (matches a
    /// default-namespaced document), and an unbound/wrong-bound prefix selects nothing. The object form
    /// requires a sub-matcher (WireMock rejects an expression-only object form), so every case pairs the
    /// path with <c>equalTo</c>. See docs/parity/g1-matching.md.
    /// </summary>
    public static IEnumerable<MatcherScenario> NamespacedXPath()
    {
        const string prefixedXml = """<r xmlns:a="http://x"><a:item>hi</a:item></r>""";
        const string otherValueXml = """<r xmlns:a="http://x"><a:item>bye</a:item></r>""";
        const string defaultXml = """<r xmlns="http://x"><item>hi</item></r>""";
        const string twoNsXml = """<a:r xmlns:a="http://x"><b:item xmlns:b="http://y">hi</b:item></a:r>""";

        // Prefix bound correctly: node selected, value compared.
        yield return Build("xpath-ns[prefix bound]",
            Ns("/r/a:item/text()", new() { ["a"] = "http://x" }, "hi"),
            (prefixedXml, true),
            (otherValueXml, false)); // node found, value "bye" != "hi"

        // A prefix with no binding selects nothing → no match.
        yield return Build("xpath-ns[unbound prefix]",
            Ns("/r/a:item/text()", null, "hi"),
            (prefixedXml, false));

        // A prefix bound to the wrong URI selects nothing → no match.
        yield return Build("xpath-ns[wrong uri]",
            Ns("/r/a:item/text()", new() { ["a"] = "http://WRONG" }, "hi"),
            (prefixedXml, false));

        // Default namespace bound to a prefix.
        yield return Build("xpath-ns[default via prefix]",
            Ns("/d:r/d:item/text()", new() { ["d"] = "http://x" }, "hi"),
            (defaultXml, true));

        // Unprefixed path is namespace-agnostic — it matches a default-namespaced document.
        yield return Build("xpath-ns[unprefixed vs default]",
            Ns("/r/item/text()", null, "hi"),
            (defaultXml, true));

        // Two distinct namespaces bound at once.
        yield return Build("xpath-ns[two namespaces]",
            Ns("/a:r/b:item/text()", new() { ["a"] = "http://x", ["b"] = "http://y" }, "hi"),
            (twoNsXml, true));
    }

    // Builds a `{ "matchesXPath": { expression, [xPathNamespaces], equalTo } }` matcher dict.
    private static Dictionary<string, object> Ns(string expression, Dictionary<string, object>? namespaces, string equalTo)
    {
        var inner = new Dictionary<string, object> { ["expression"] = expression, ["equalTo"] = equalTo };
        if (namespaces is { Count: > 0 })
        {
            inner["xPathNamespaces"] = namespaces;
        }

        return new Dictionary<string, object> { ["matchesXPath"] = inner };
    }

    private static MatcherScenario Build(string description, Dictionary<string, object> matcher, params (string Body, bool Match)[] bodies)
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
