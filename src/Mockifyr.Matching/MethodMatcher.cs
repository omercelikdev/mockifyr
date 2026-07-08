using Mockifyr.Core;

namespace Mockifyr.Matching;

/// <summary>
/// Matches the HTTP method. A method of <c>ANY</c> matches every request (verified by the differential suite).
/// </summary>
public sealed class MethodMatcher(string expected) : IMatcher
{
    private readonly string _expected = expected;

    /// <inheritdoc />
    public MatchResult Match(MatchInput input)
    {
        if (string.Equals(_expected, "ANY", StringComparison.OrdinalIgnoreCase))
        {
            return MatchResult.Exact;
        }

        return string.Equals(input.Request.Method, _expected, StringComparison.OrdinalIgnoreCase)
            ? MatchResult.Exact
            : MatchResult.NoMatch(1d);
    }
}
