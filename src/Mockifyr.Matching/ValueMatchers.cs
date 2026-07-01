using System.Text.RegularExpressions;
using Mockifyr.Core;

namespace Mockifyr.Matching;

/// <summary>
/// A leaf comparison applied to the value(s) of a matched target (a header, query parameter,
/// cookie, or the body). Reused by the target matchers so the standard matcher set is defined
/// once. WireMock's underlying semantics are pinned by the differential suite; see
/// docs/parity/g1-matching.md.
/// </summary>
public interface IValueMatcher
{
    /// <summary>Evaluates the target's presence and value(s).</summary>
    MatchResult Match(bool present, IReadOnlyList<string> values);
}

/// <summary>Matches when a value equals the expected string (optionally case-insensitively).</summary>
public sealed class EqualToValueMatcher(string expected, bool caseInsensitive = false) : IValueMatcher
{
    private readonly StringComparison _comparison =
        caseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <inheritdoc />
    public MatchResult Match(bool present, IReadOnlyList<string> values) =>
        present && values.Any(v => string.Equals(v, expected, _comparison))
            ? MatchResult.Exact
            : MatchResult.NoMatch(1d);
}

/// <summary>Matches when a value equals the expected string ignoring case.</summary>
public sealed class EqualToIgnoreCaseValueMatcher(string expected) : IValueMatcher
{
    private readonly EqualToValueMatcher _inner = new(expected, caseInsensitive: true);

    /// <inheritdoc />
    public MatchResult Match(bool present, IReadOnlyList<string> values) => _inner.Match(present, values);
}

/// <summary>Matches when a value contains the expected substring.</summary>
public sealed class ContainsValueMatcher(string expected) : IValueMatcher
{
    /// <inheritdoc />
    public MatchResult Match(bool present, IReadOnlyList<string> values) =>
        present && values.Any(v => v.Contains(expected, StringComparison.Ordinal))
            ? MatchResult.Exact
            : MatchResult.NoMatch(1d);
}

/// <summary>
/// Matches when a value fully matches the regular expression. WireMock uses Java
/// <c>String.matches</c> semantics (the whole value must match), so the pattern is anchored.
/// </summary>
public sealed class MatchesValueMatcher(string pattern) : IValueMatcher
{
    private readonly Regex _regex = new($@"\A(?:{pattern})\z", RegexOptions.None);

    /// <inheritdoc />
    public MatchResult Match(bool present, IReadOnlyList<string> values) =>
        present && values.Any(v => _regex.IsMatch(v))
            ? MatchResult.Exact
            : MatchResult.NoMatch(1d);
}

/// <summary>Matches when a value is present and does not fully match the regular expression.</summary>
public sealed class DoesNotMatchValueMatcher(string pattern) : IValueMatcher
{
    private readonly Regex _regex = new($@"\A(?:{pattern})\z", RegexOptions.None);

    /// <inheritdoc />
    public MatchResult Match(bool present, IReadOnlyList<string> values) =>
        present && values.All(v => !_regex.IsMatch(v))
            ? MatchResult.Exact
            : MatchResult.NoMatch(1d);
}

/// <summary>Matches when the target is absent.</summary>
public sealed class AbsentValueMatcher : IValueMatcher
{
    /// <inheritdoc />
    public MatchResult Match(bool present, IReadOnlyList<string> values) =>
        present ? MatchResult.NoMatch(1d) : MatchResult.Exact;
}
