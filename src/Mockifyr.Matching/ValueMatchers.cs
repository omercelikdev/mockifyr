using System.Text.RegularExpressions;
using Mockifyr.Core;

namespace Mockifyr.Matching;

/// <summary>
/// A leaf comparison applied to the value(s) of a matched target (a header, query parameter,
/// cookie, or the body). Reused by the target matchers so the standard matcher set is defined
/// once. The exact matching semantics are verified by the differential suite; see
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
/// Matches when a value fully matches the regular expression, following Java
/// <c>String.matches</c> semantics (the whole value must match), so the pattern is anchored.
/// Verified by the differential suite.
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

/// <summary>Matches when no value contains the expected substring (the <c>doesNotContain</c> predicate).</summary>
public sealed class DoesNotContainValueMatcher(string expected) : IValueMatcher
{
    /// <inheritdoc />
    public MatchResult Match(bool present, IReadOnlyList<string> values) =>
        present && values.All(v => !v.Contains(expected, StringComparison.Ordinal))
            ? MatchResult.Exact
            : MatchResult.NoMatch(1d);
}

/// <summary>Matches when every inner matcher matches the same target value(s) (the <c>and</c> predicate).</summary>
public sealed class AndValueMatcher(IReadOnlyList<IValueMatcher> matchers) : IValueMatcher
{
    /// <inheritdoc />
    public MatchResult Match(bool present, IReadOnlyList<string> values) =>
        matchers.All(m => m.Match(present, values).IsExactMatch) ? MatchResult.Exact : MatchResult.NoMatch(1d);
}

/// <summary>Matches when at least one inner matcher matches the target value(s) (the <c>or</c> predicate).</summary>
public sealed class OrValueMatcher(IReadOnlyList<IValueMatcher> matchers) : IValueMatcher
{
    /// <inheritdoc />
    public MatchResult Match(bool present, IReadOnlyList<string> values) =>
        matchers.Any(m => m.Match(present, values).IsExactMatch) ? MatchResult.Exact : MatchResult.NoMatch(1d);
}

/// <summary>Matches when the inner matcher does not match the target value(s) (the <c>not</c> predicate).</summary>
public sealed class NotValueMatcher(IValueMatcher inner) : IValueMatcher
{
    /// <inheritdoc />
    public MatchResult Match(bool present, IReadOnlyList<string> values) =>
        inner.Match(present, values).IsExactMatch ? MatchResult.NoMatch(1d) : MatchResult.Exact;
}

/// <summary>
/// Matches a multi-valued target when its values correspond exactly to the given matchers, in any
/// order (the <c>hasExactly</c> predicate): the counts are equal, every matcher matches some value, and
/// every value is matched by some matcher.
/// </summary>
public sealed class HasExactlyValueMatcher(IReadOnlyList<IValueMatcher> matchers) : IValueMatcher
{
    /// <inheritdoc />
    public MatchResult Match(bool present, IReadOnlyList<string> values)
    {
        if (!present || values.Count != matchers.Count)
        {
            return MatchResult.NoMatch(1d);
        }

        var everyMatcherHits = matchers.All(m => values.Any(v => m.Match(true, [v]).IsExactMatch));
        var everyValueHit = values.All(v => matchers.Any(m => m.Match(true, [v]).IsExactMatch));
        return everyMatcherHits && everyValueHit ? MatchResult.Exact : MatchResult.NoMatch(1d);
    }
}

/// <summary>
/// Matches a multi-valued target when every matcher matches at least one value, in any order and
/// allowing extra values (the <c>includes</c> predicate).
/// </summary>
public sealed class IncludesValueMatcher(IReadOnlyList<IValueMatcher> matchers) : IValueMatcher
{
    /// <inheritdoc />
    public MatchResult Match(bool present, IReadOnlyList<string> values) =>
        present && matchers.All(m => values.Any(v => m.Match(true, [v]).IsExactMatch))
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
