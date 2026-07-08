using Mockifyr.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mockifyr.Matching;

/// <summary>
/// Matches a JSONPath expression against a JSON body. Without a sub-matcher it matches when the
/// path selects at least one node (presence). With a sub-matcher, the extracted value(s) must
/// satisfy it. The engine targets the Jayway JsonPath dialect; Newtonsoft's dialect is the
/// closest .NET proxy (it accepts Jayway-style filters). Divergences are pinned and verified by
/// the differential suite — see docs/parity/g1-matching.md.
/// </summary>
public sealed class MatchesJsonPathValueMatcher(string expression, IValueMatcher? subMatcher = null) : IValueMatcher
{
    /// <inheritdoc />
    public MatchResult Match(bool present, IReadOnlyList<string> values)
    {
        if (!present || values.Count == 0)
        {
            return MatchResult.NoMatch(1d);
        }

        JToken root;
        try
        {
            root = JToken.Parse(values[0]);
        }
        catch (JsonException)
        {
            return MatchResult.NoMatch(1d);
        }

        List<JToken> selected;
        try
        {
            selected = root.SelectTokens(expression).ToList();
        }
        catch (JsonException)
        {
            // Invalid path expression: treat as a non-match rather than throwing.
            return MatchResult.NoMatch(1d);
        }

        if (subMatcher is null)
        {
            return selected.Count > 0 ? MatchResult.Exact : MatchResult.NoMatch(1d);
        }

        var extracted = selected.Select(Represent).ToList();
        return subMatcher.Match(extracted.Count > 0, extracted);
    }

    private static string Represent(JToken token) => token.Type switch
    {
        JTokenType.String => token.Value<string>() ?? string.Empty,
        JTokenType.Boolean => token.Value<bool>() ? "true" : "false",
        JTokenType.Null => "null",
        _ => token.ToString(Formatting.None),
    };
}
