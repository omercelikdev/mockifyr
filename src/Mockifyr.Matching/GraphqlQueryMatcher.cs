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
/// Matches a GraphQL request body against a stubbed GraphQL query (G14, verified by the differential suite). The
/// <see cref="MatchInput"/> body is the GraphQL POST JSON (<c>{"query", "variables", "operationName"}</c>);
/// a match requires <b>all three</b> to agree (the extension aggregates them):
/// <list type="bullet">
/// <item><b>query</b> — both queries are parsed to ASTs, canonicalized, and compared, so equal queries
/// match regardless of <b>whitespace</b> and <b>field/argument order</b> (graphql-java's
/// <c>AstSorter</c> + <c>AstComparator</c>, reproduced here via GraphQL-Parser).</item>
/// <item><b>variables</b> (G14b) — when the stub specifies them, semantic JSON equality
/// (<c>equalToJson</c>, no array-order/extra-element leniency); when it does not, the request must have
/// <em>no</em> variables.</item>
/// <item><b>operationName</b> (G14b) — when specified, exact string equality; else the request must have
/// none.</item>
/// </list>
/// </summary>
public sealed class GraphqlQueryMatcher(string expectedQuery, string? expectedVariablesJson = null, string? expectedOperationName = null) : IMatcher
{
    private readonly string? _expectedQuery = TryNormalize(expectedQuery);
    private readonly EqualToJsonValueMatcher? _expectedVariables =
        expectedVariablesJson is null ? null : new EqualToJsonValueMatcher(expectedVariablesJson, ignoreArrayOrder: false, ignoreExtraElements: false);

    /// <inheritdoc />
    public MatchResult Match(MatchInput input)
    {
        if (_expectedQuery is null)
        {
            return MatchResult.NoMatch(1d);
        }

        var (query, variables, operationName) = ExtractBody(input.Request.Body);

        // Query: parse + canonicalize both, compare.
        if (query is null || TryNormalize(query) is not { } actualQuery || actualQuery != _expectedQuery)
        {
            return MatchResult.NoMatch(1d);
        }

        // Variables: an unspecified expectation requires the request to have none; otherwise JSON-equal.
        if (_expectedVariables is null)
        {
            if (variables is not null)
            {
                return MatchResult.NoMatch(1d);
            }
        }
        else if (variables is null || !_expectedVariables.Match(present: true, [variables]).IsExactMatch)
        {
            return MatchResult.NoMatch(1d);
        }

        // Operation name: unspecified requires the request to have none; otherwise exact string equality.
        if (!string.Equals(operationName, expectedOperationName, StringComparison.Ordinal))
        {
            return MatchResult.NoMatch(1d);
        }

        return MatchResult.Exact;
    }

    // Pull query / variables / operationName out of the GraphQL POST body. A "variables"/"operationName"
    // that is absent or JSON null reads as null (matching the reference extension, which treats both the
    // same way). Variables come back as their raw JSON text for semantic comparison.
    private static (string? Query, string? Variables, string? OperationName) ExtractBody(byte[] body)
    {
        if (body.Length == 0)
        {
            return (null, null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return (null, null, null);
            }

            var query = root.TryGetProperty("query", out var q) && q.ValueKind == JsonValueKind.String ? q.GetString() : null;
            var variables = root.TryGetProperty("variables", out var v) && v.ValueKind is not JsonValueKind.Null ? v.GetRawText() : null;
            var operationName = root.TryGetProperty("operationName", out var o) && o.ValueKind == JsonValueKind.String ? o.GetString() : null;
            return (query, variables, operationName);
        }
        catch (JsonException)
        {
            return (null, null, null);
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
        // Directives can appear on any node (fields, the operation, fragment definitions/spreads, inline
        // fragments); the reference extension normalizes their order and each directive's argument order,
        // so sort them for every visited node — independent of the structural recursion below.
        if (node is IHasDirectivesNode { Directives: { } directives })
        {
            foreach (var directive in directives.Items)
            {
                directive.Arguments?.Items.Sort(static (x, y) => string.CompareOrdinal(x.Name.StringValue, y.Name.StringValue));
            }

            directives.Items.Sort(ByPrintedForm);
        }

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
