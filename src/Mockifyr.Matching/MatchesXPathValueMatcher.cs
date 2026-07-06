using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Mockifyr.Core;

namespace Mockifyr.Matching;

/// <summary>
/// Matches an XPath expression against an XML body. Without a sub-matcher it matches when the
/// expression selects at least one node; with a sub-matcher the extracted value(s) must satisfy it.
/// Prefixes in the expression are bound via WireMock's <c>xPathNamespaces</c> map. WireMock treats an
/// <em>unprefixed</em> step as namespace-agnostic (so an unprefixed path matches a default-namespaced
/// document), while a prefixed step must match the bound namespace URI — reproduced here by retrying a
/// namespace-aware miss against a namespace-stripped copy. See docs/parity/g1-matching.md.
/// </summary>
public sealed class MatchesXPathValueMatcher(
    string expression,
    IValueMatcher? subMatcher = null,
    IReadOnlyDictionary<string, string>? namespaces = null) : IValueMatcher
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
        catch (XmlException)
        {
            return MatchResult.NoMatch(1d);
        }

        var resolver = BuildResolver();

        object result;
        try
        {
            result = Evaluate(document, resolver);
        }
        catch (XPathException)
        {
            return MatchResult.NoMatch(1d);
        }

        if (result is not IEnumerable<object> nodeSet)
        {
            // A scalar result from an XPath function (boolean/number/string). WireMock's presence form
            // matches whenever the expression yields a value — `count()==0` and `contains()==false`
            // both match — so presence is always a match; a sub-matcher compares the value's string form.
            return subMatcher is null ? MatchResult.Exact : subMatcher.Match(present: true, [ScalarText(result)]);
        }

        var nodes = nodeSet.OfType<XObject>().ToList();

        // The .NET engine is namespace-aware, so an unprefixed step won't match a namespaced element.
        // WireMock is lenient there, so on an empty result retry against a namespace-stripped document.
        if (nodes.Count == 0 && document.Root is not null)
        {
            try
            {
                if (Evaluate(new XDocument(Strip(document.Root)), resolver) is IEnumerable<object> retry)
                {
                    nodes = retry.OfType<XObject>().ToList();
                }
            }
            catch (XPathException)
            {
                // Keep the empty result.
            }
        }

        if (subMatcher is null)
        {
            return nodes.Count > 0 ? MatchResult.Exact : MatchResult.NoMatch(1d);
        }

        var extracted = nodes.Select(TextOf).ToList();
        return subMatcher.Match(extracted.Count > 0, extracted);
    }

    private object Evaluate(XNode node, IXmlNamespaceResolver? resolver) =>
        resolver is null ? node.XPathEvaluate(expression) : node.XPathEvaluate(expression, resolver);

    private IXmlNamespaceResolver? BuildResolver()
    {
        if (namespaces is not { Count: > 0 })
        {
            return null;
        }

        var manager = new XmlNamespaceManager(new NameTable());
        foreach (var (prefix, uri) in namespaces)
        {
            manager.AddNamespace(prefix, uri);
        }

        return manager;
    }

    // A namespace-free deep copy: element/attribute names keep only their local part, namespace
    // declarations are dropped, and text/other nodes pass through unchanged.
    private static XElement Strip(XElement element) => new(
        element.Name.LocalName,
        element.Attributes()
            .Where(a => !a.IsNamespaceDeclaration)
            .Select(a => new XAttribute(a.Name.LocalName, a.Value)),
        element.Nodes().Select(node => node is XElement child ? Strip(child) : node));

    // Renders an XPath scalar to the string a sub-matcher compares against: a whole number drops its
    // fractional part (`count()` → "2", not "2.0") and a boolean is lower-case — matching WireMock.
    private static string ScalarText(object value) => value switch
    {
        bool b => b ? "true" : "false",
        double d when !double.IsInfinity(d) && !double.IsNaN(d) && d == Math.Floor(d) =>
            ((long)d).ToString(CultureInfo.InvariantCulture),
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    private static string TextOf(XObject node) => node switch
    {
        XText text => text.Value,
        // An element node is passed to the sub-matcher as its serialized XML (so an `equalToXml`
        // sub-matcher works, and an element never text-equals its content) — matching WireMock.
        XElement element => element.ToString(SaveOptions.DisableFormatting),
        XAttribute attribute => attribute.Value,
        _ => node.ToString() ?? string.Empty,
    };
}
