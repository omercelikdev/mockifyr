using Mockifyr.Core;

namespace Mockifyr.Matching;

/// <summary>Applies a value matcher to a named request header.</summary>
public sealed class HeaderMatcher(string name, IValueMatcher value) : IMatcher
{
    /// <inheritdoc />
    public MatchResult Match(MatchInput input)
    {
        var present = input.Request.Headers.Contains(name);
        var values = present ? [.. input.Request.Headers[name]] : Array.Empty<string>();
        return value.Match(present, values);
    }
}

/// <summary>Applies a value matcher to a named query parameter.</summary>
public sealed class QueryMatcher(string name, IValueMatcher value) : IMatcher
{
    /// <inheritdoc />
    public MatchResult Match(MatchInput input)
    {
        var present = input.Request.Query.Contains(name);
        var values = present ? [.. input.Request.Query[name]] : Array.Empty<string>();
        return value.Match(present, values);
    }
}

/// <summary>Applies a value matcher to a named cookie.</summary>
public sealed class CookieMatcher(string name, IValueMatcher value) : IMatcher
{
    /// <inheritdoc />
    public MatchResult Match(MatchInput input)
    {
        var present = input.Request.Cookies.TryGetValue(name, out var cookie);
        var values = present ? new[] { cookie! } : Array.Empty<string>();
        return value.Match(present, values);
    }
}

/// <summary>Applies a value matcher to the request body.</summary>
/// <remarks>
/// WireMock treats an empty request body as absent for body matching (verified against the
/// oracle: <c>bodyPatterns equalTo ""</c> does not match an empty body). See
/// docs/parity/g1-matching.md.
/// </remarks>
public sealed class BodyMatcher(IValueMatcher value) : IMatcher
{
    /// <inheritdoc />
    public MatchResult Match(MatchInput input)
    {
        if (input.Request.Body.Length == 0)
        {
            return value.Match(present: false, Array.Empty<string>());
        }

        var text = System.Text.Encoding.UTF8.GetString(input.Request.Body);
        return value.Match(present: true, [text]);
    }
}
