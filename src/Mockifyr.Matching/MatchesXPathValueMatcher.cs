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
            // Scalar XPath result (boolean/number/string): treat a true boolean as a match.
            return result is bool b && b ? MatchResult.Exact : MatchResult.NoMatch(1d);
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

    private static string TextOf(XObject node) => node switch
    {
        XText text => text.Value,
        XElement element => element.Value,
        XAttribute attribute => attribute.Value,
        _ => node.ToString() ?? string.Empty,
    };
}
