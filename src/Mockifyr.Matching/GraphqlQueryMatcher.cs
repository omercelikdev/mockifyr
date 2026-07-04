using System.IO;
using System.Text;
using System.Text.Json;
using GraphQLParser;
using GraphQLParser.AST;
using GraphQLParser.Exceptions;
using GraphQLParser.Visitors;
using Mockifyr.Core;

namespace Mockifyr.Matching;

/// <summary>
/// Matches a GraphQL request body against an expected query the way WireMock's GraphQL extension does
/// (G14): it parses both queries to ASTs, canonicalizes them, and compares — so two semantically-equal
/// queries match regardless of <b>whitespace</b> and <b>field/argument order</b>. The reference
/// extension uses graphql-java's <c>AstSorter</c> + <c>AstComparator</c>; this reproduces the sort by
/// recursively ordering selections and arguments (bottom-up, by their printed form) and comparing the
/// canonical printed documents. The request body is the GraphQL POST JSON (<c>{"query": …}</c>).
///
/// <para>Covered: the <c>query</c>. Deferred: <c>variables</c> and <c>operationName</c> matching.</para>
/// </summary>
public sealed class GraphqlQueryMatcher(string expectedQuery) : IMatcher
{
    private readonly string? _expected = TryNormalize(expectedQuery);

    /// <inheritdoc />
    public MatchResult Match(MatchInput input)
    {
        if (_expected is null)
        {
            return MatchResult.NoMatch(1d);
        }

        var query = ExtractQuery(input.Request.Body);
        if (query is null || TryNormalize(query) is not { } actual)
        {
            return MatchResult.NoMatch(1d);
        }

        return actual == _expected ? MatchResult.Exact : MatchResult.NoMatch(1d);
    }

    // Pull the "query" string out of the GraphQL POST body ({"query": "...", "variables": {...}}).
    private static string? ExtractQuery(byte[] body)
    {
        if (body.Length == 0)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty("query", out var query) &&
                   query.ValueKind == JsonValueKind.String
                ? query.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Parse -> canonicalize (sort selections/arguments) -> print. Returns null on a syntax error.
    private static string? TryNormalize(string query)
    {
        try
        {
            var document = Parser.Parse(query);
            Sort(document);
            return Print(document);
        }
        catch (GraphQLParserException)
        {
            return null;
        }
    }

    private static void Sort(ASTNode node)
    {
        switch (node)
        {
            case GraphQLDocument document:
                foreach (var definition in document.Definitions)
                {
                    Sort(definition);
                }

                document.Definitions.Sort(ByPrintedForm);
                break;
            case GraphQLOperationDefinition { SelectionSet: { } operationSelections }:
                Sort(operationSelections);
                break;
            case GraphQLFragmentDefinition { SelectionSet: { } fragmentSelections }:
                Sort(fragmentSelections);
                break;
            case GraphQLInlineFragment { SelectionSet: { } inlineSelections }:
                Sort(inlineSelections);
                break;
            case GraphQLSelectionSet selectionSet:
                foreach (var selection in selectionSet.Selections)
                {
                    Sort(selection);
                }

                selectionSet.Selections.Sort(ByPrintedForm);
                break;
            case GraphQLField field:
                if (field.Arguments is { } arguments)
                {
                    arguments.Items.Sort(static (x, y) => string.CompareOrdinal(x.Name.StringValue, y.Name.StringValue));
                }

                if (field.SelectionSet is { } fieldSelections)
                {
                    Sort(fieldSelections);
                }

                break;
        }
    }

    private static int ByPrintedForm(ASTNode x, ASTNode y) => string.CompareOrdinal(Print(x), Print(y));

    private static string Print(ASTNode node)
    {
        using var writer = new StringWriter();
        new SDLPrinter().PrintAsync(node, writer).AsTask().GetAwaiter().GetResult();
        return writer.ToString();
    }
}
