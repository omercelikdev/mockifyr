using System.Xml.Linq;
using Mockifyr.Core;

namespace Mockifyr.Matching;

/// <summary>
/// Matches an XML value semantically: insignificant whitespace and attribute order are ignored,
/// element order is significant, and leaf text is whitespace-normalized. WireMock uses XMLUnit;
/// the exact semantics (and the advanced placeholders / exemptedComparisons / namespaceAwareness
/// options) are pinned by the differential suite — see docs/parity/g1-matching.md.
/// </summary>
public sealed class EqualToXmlValueMatcher : IValueMatcher
{
    private readonly XElement? _expected;

    /// <summary>Creates the matcher from the expected XML text.</summary>
    public EqualToXmlValueMatcher(string expectedXml)
    {
        _expected = TryParse(expectedXml);
    }

    /// <inheritdoc />
    public MatchResult Match(bool present, IReadOnlyList<string> values)
    {
        if (_expected is null || !present || values.Count == 0)
        {
            return MatchResult.NoMatch(1d);
        }

        var actual = TryParse(values[0]);
        if (actual is null)
        {
            return MatchResult.NoMatch(1d);
        }

        return ElementsEqual(_expected, actual) ? MatchResult.Exact : MatchResult.NoMatch(1d);
    }

    private static XElement? TryParse(string xml)
    {
        try
        {
            return XDocument.Parse(xml).Root;
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    private static bool ElementsEqual(XElement expected, XElement actual)
    {
        if (expected.Name != actual.Name)
        {
            return false;
        }

        if (!AttributesEqual(expected, actual))
        {
            return false;
        }

        var expectedChildren = expected.Elements().ToList();
        var actualChildren = actual.Elements().ToList();
        if (expectedChildren.Count != actualChildren.Count)
        {
            return false;
        }

        if (expectedChildren.Count == 0)
        {
            return string.Equals(Normalize(expected.Value), Normalize(actual.Value), StringComparison.Ordinal);
        }

        // Sibling element order is not significant (WireMock/XMLUnit treats a reorder as "similar",
        // verified against the oracle), so children are matched as a multiset.
        var consumed = new bool[actualChildren.Count];
        foreach (var expectedChild in expectedChildren)
        {
            var matched = -1;
            for (var i = 0; i < actualChildren.Count; i++)
            {
                if (!consumed[i] && ElementsEqual(expectedChild, actualChildren[i]))
                {
                    matched = i;
                    break;
                }
            }

            if (matched < 0)
            {
                return false;
            }

            consumed[matched] = true;
        }

        return true;
    }

    private static bool AttributesEqual(XElement expected, XElement actual)
    {
        var expectedAttributes = expected.Attributes().Where(a => !a.IsNamespaceDeclaration).ToList();
        var actualAttributes = actual.Attributes()
            .Where(a => !a.IsNamespaceDeclaration)
            .ToDictionary(a => a.Name, a => a.Value);

        if (expectedAttributes.Count != actualAttributes.Count)
        {
            return false;
        }

        foreach (var attribute in expectedAttributes)
        {
            if (!actualAttributes.TryGetValue(attribute.Name, out var value) ||
                !string.Equals(value, attribute.Value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static string Normalize(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
