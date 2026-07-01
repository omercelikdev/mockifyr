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

/// <summary>Applies a value matcher to the request body (always present, as a single value).</summary>
public sealed class BodyMatcher(IValueMatcher value) : IMatcher
{
    /// <inheritdoc />
    public MatchResult Match(MatchInput input)
    {
        var text = System.Text.Encoding.UTF8.GetString(input.Request.Body);
        return value.Match(present: true, [text]);
    }
}
