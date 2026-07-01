using Mockifyr.Core;

namespace Mockifyr.Matching;

/// <summary>
/// Matches the full request URL (path plus query string) for exact equality.
/// The WireMock JSON equivalent is <c>request.url</c>.
/// </summary>
public sealed class UrlEqualToMatcher(string expected) : IMatcher
{
    private readonly string _expected = expected;

    /// <inheritdoc />
    public MatchResult Match(MatchInput input) =>
        string.Equals(input.Request.Url, _expected, StringComparison.Ordinal)
            ? MatchResult.Exact
            : MatchResult.NoMatch(1d);
}

/// <summary>
/// Matches the request path (ignoring the query string) for exact equality.
/// The WireMock JSON equivalent is <c>request.urlPath</c>.
/// </summary>
public sealed class UrlPathEqualToMatcher(string expected) : IMatcher
{
    private readonly string _expected = expected;

    /// <inheritdoc />
    public MatchResult Match(MatchInput input) =>
        string.Equals(input.Request.Path, _expected, StringComparison.Ordinal)
            ? MatchResult.Exact
            : MatchResult.NoMatch(1d);
}
