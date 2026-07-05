using Mockifyr.Core;

namespace Mockifyr.Matching;

/// <summary>
/// Applies a value matcher to the request host — the hostname from the <c>Host</c> header. WireMock's
/// <c>request.host</c> is a <c>StringValuePattern</c>, so any standard value matcher (equalTo, matches,
/// contains, …) applies. Enables multi-domain mocking (G15c): one instance serving many hosts.
/// </summary>
public sealed class HostMatcher(IValueMatcher value) : IMatcher
{
    /// <inheritdoc />
    public MatchResult Match(MatchInput input)
    {
        var host = input.Request.Host;
        return host is null
            ? value.Match(present: false, Array.Empty<string>())
            : value.Match(present: true, [host]);
    }
}

/// <summary>
/// Matches the request scheme (WireMock's <c>request.scheme</c>). WireMock's scheme is a plain string
/// (not a value pattern); comparison is case-insensitive since schemes are canonically lower-case.
/// </summary>
public sealed class SchemeMatcher(string expected) : IMatcher
{
    private readonly string _expected = expected;

    /// <inheritdoc />
    public MatchResult Match(MatchInput input) =>
        input.Request.Scheme is { } scheme && string.Equals(scheme, _expected, StringComparison.OrdinalIgnoreCase)
            ? MatchResult.Exact
            : MatchResult.NoMatch(1d);
}

/// <summary>Matches the request port for exact equality (WireMock's <c>request.port</c>).</summary>
public sealed class PortMatcher(int expected) : IMatcher
{
    private readonly int _expected = expected;

    /// <inheritdoc />
    public MatchResult Match(MatchInput input) =>
        input.Request.Port == _expected ? MatchResult.Exact : MatchResult.NoMatch(1d);
}
