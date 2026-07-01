using System.Text;
using System.Text.Json;

namespace Mockifyr.Differential.Generator;

/// <summary>
/// Fuzzes <c>equalToXml</c> and <c>matchesXPath</c> (body) over the common subset: semantic XML
/// equality (whitespace / attribute order insignificant, element order significant) and XPath
/// presence / sub-matcher on element, attribute, and descendant selection. Advanced options
/// (placeholders, namespaceAwareness, namespaced XPath, XPath functions) are validated later; see
/// docs/parity/g1-matching.md.
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
