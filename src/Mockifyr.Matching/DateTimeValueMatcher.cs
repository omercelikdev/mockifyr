using System.Globalization;
using Mockifyr.Core;

namespace Mockifyr.Matching;

/// <summary>The temporal relation a <see cref="DateTimeValueMatcher"/> asserts.</summary>
public enum DateTimeComparison
{
    /// <summary>The actual instant is strictly before the expected instant (<c>before</c>).</summary>
    Before,

    /// <summary>The actual instant is strictly after the expected instant (<c>after</c>).</summary>
    After,

    /// <summary>The actual instant equals the expected instant (<c>equalToDateTime</c>).</summary>
    Equal,
}

/// <summary>
/// Matches a request value parsed as a date/time against an <b>absolute</b> ISO-8601 expected
/// instant. Corresponds to WireMock's <c>before</c> / <c>after</c> / <c>equalToDateTime</c> over
/// the deterministic subset; <c>now</c>-relative expected values, offsets, and truncation are
/// deferred (see docs/parity/g1-matching.md). Comparison is on the instant: a value without an
/// explicit zone is read as UTC, matching how the corpus is pinned by the differential suite.
/// </summary>
public sealed class DateTimeValueMatcher(DateTimeComparison comparison, string expected, string? actualFormat = null)
    : IValueMatcher
{
    private const DateTimeStyles Styles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

    private readonly DateTimeOffset? _expected =
        DateTimeOffset.TryParse(expected, CultureInfo.InvariantCulture, Styles, out var e) ? e : null;

    /// <inheritdoc />
    public MatchResult Match(bool present, IReadOnlyList<string> values)
    {
        if (!present || _expected is null)
        {
            return MatchResult.NoMatch(1d);
        }

        return values.Any(v => TryParseActual(v, out var actual) && Compare(actual))
            ? MatchResult.Exact
            : MatchResult.NoMatch(1d);
    }

    private bool Compare(DateTimeOffset actual) => comparison switch
    {
        DateTimeComparison.Before => actual < _expected!.Value,
        DateTimeComparison.After => actual > _expected!.Value,
        DateTimeComparison.Equal => actual == _expected!.Value,
        _ => false,
    };

    private bool TryParseActual(string value, out DateTimeOffset actual) =>
        actualFormat is null
            ? DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, Styles, out actual)
            : DateTimeOffset.TryParseExact(value, actualFormat, CultureInfo.InvariantCulture, Styles, out actual);
}
