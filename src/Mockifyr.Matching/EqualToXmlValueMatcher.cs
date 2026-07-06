using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Mockifyr.Core;

namespace Mockifyr.Matching;

/// <summary>
/// Matches an XML value semantically: insignificant whitespace and attribute order are ignored,
/// element order is significant, and leaf text is whitespace-normalized. WireMock uses XMLUnit.
///
/// <para>When <c>enablePlaceholders</c> is set, a leaf text or attribute value in the <em>expected</em>
/// XML that is <b>entirely</b> an XMLUnit placeholder relaxes the comparison for that node:
/// <c>${xmlunit.ignore}</c> matches anything, <c>${xmlunit.isNumber}</c> a number,
/// <c>${xmlunit.isDateTime}</c> a date-time, and <c>${xmlunit.matchesRegex(…)}</c> a regex. The
/// delimiters default to <c>${</c>/<c>}</c> and are overridable via the placeholder delimiter regexes.
/// Pinned by the differential suite — see docs/parity/g1-matching.md.</para>
/// </summary>
public sealed class EqualToXmlValueMatcher : IValueMatcher
{
    private readonly XElement? _expected;
    private readonly Regex? _placeholder;

    /// <summary>Creates the matcher from the expected XML text and optional placeholder configuration.</summary>
    public EqualToXmlValueMatcher(
        string expectedXml,
        bool enablePlaceholders = false,
        string? openingDelimiterRegex = null,
        string? closingDelimiterRegex = null)
    {
        _expected = TryParse(expectedXml);
        if (enablePlaceholders)
        {
            var open = string.IsNullOrEmpty(openingDelimiterRegex) ? @"\$\{" : openingDelimiterRegex;
            var close = string.IsNullOrEmpty(closingDelimiterRegex) ? @"\}" : closingDelimiterRegex;
            _placeholder = new Regex("^" + open + "(.*)" + close + "$", RegexOptions.Singleline);
        }
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

    private bool ElementsEqual(XElement expected, XElement actual)
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
            return LeafMatch(expected.Value, actual.Value);
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

    private bool AttributesEqual(XElement expected, XElement actual)
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
            if (!actualAttributes.TryGetValue(attribute.Name, out var value) || !LeafMatch(attribute.Value, value))
            {
                return false;
            }
        }

        return true;
    }

    // Compares a leaf value, honouring an XMLUnit placeholder in the expected text when enabled.
    private bool LeafMatch(string expectedText, string actualText)
    {
        var expected = Normalize(expectedText);
        var actual = Normalize(actualText);

        if (_placeholder is not null && _placeholder.Match(expected) is { Success: true } m)
        {
            var keyword = m.Groups[1].Value.Trim();
            if (keyword == "xmlunit.ignore")
            {
                return true;
            }

            if (keyword == "xmlunit.isNumber")
            {
                return double.TryParse(actual, NumberStyles.Any, CultureInfo.InvariantCulture, out _);
            }

            if (keyword == "xmlunit.isDateTime")
            {
                return DateTimeOffset.TryParse(actual, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
            }

            if (keyword.StartsWith("xmlunit.matchesRegex(", StringComparison.Ordinal) &&
                keyword.EndsWith(")", StringComparison.Ordinal))
            {
                var pattern = keyword["xmlunit.matchesRegex(".Length..^1];
                try
                {
                    // XMLUnit's matchesRegex is a partial (find) match, not anchored — verified against
                    // the oracle (`[0-9]+` matched `12a3`).
                    return Regex.IsMatch(actual, pattern);
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }

            // A delimiter-shaped value that is not a recognised placeholder falls through to literal.
        }

        return string.Equals(expected, actual, StringComparison.Ordinal);
    }

    private static string Normalize(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
