using System.Xml.Linq;
using System.Xml.XPath;
using Mockifyr.Core;

namespace Mockifyr.Matching;

/// <summary>
/// Matches an XPath expression against an XML body. Without a sub-matcher it matches when the
/// expression selects at least one node; with a sub-matcher the extracted value(s) must satisfy
/// it. Namespaces and XPath functions are pinned/deferred by the differential suite — see
/// docs/parity/g1-matching.md.
/// </summary>
public sealed class MatchesXPathValueMatcher(string expression, IValueMatcher? subMatcher = null) : IValueMatcher
{
    /// <inheritdoc />
    public MatchResult Match(bool present, IReadOnlyList<string> values)
    {
        if (!present || values.Count == 0)
        {
            return MatchResult.NoMatch(1d);
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(values[0]);
        }
        catch (System.Xml.XmlException)
        {
            return MatchResult.NoMatch(1d);
        }

        object result;
        try
        {
            result = document.XPathEvaluate(expression);
        }
        catch (XPathException)
        {
            return MatchResult.NoMatch(1d);
        }

        if (result is not IEnumerable<object> nodeSet)
        {
            // Scalar XPath result (boolean/number/string): treat a true boolean as a match.
            return result is bool b && b ? MatchResult.Exact : MatchResult.NoMatch(1d);
        }

        var nodes = nodeSet.OfType<XObject>().ToList();

        if (subMatcher is null)
        {
            return nodes.Count > 0 ? MatchResult.Exact : MatchResult.NoMatch(1d);
        }

        var extracted = nodes.Select(TextOf).ToList();
        return subMatcher.Match(extracted.Count > 0, extracted);
    }

    private static string TextOf(XObject node) => node switch
    {
        XText text => text.Value,
        XElement element => element.Value,
        XAttribute attribute => attribute.Value,
        _ => node.ToString() ?? string.Empty,
    };
}
