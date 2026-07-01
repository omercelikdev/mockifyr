using Mockifyr.Core;

namespace Mockifyr.Matching.Tests;

public class ValueMatcherTests
{
    [Fact]
    public void EqualTo_is_case_sensitive_by_default()
    {
        Assert.True(new EqualToValueMatcher("bar").Match(true, ["bar"]).IsExactMatch);
        Assert.False(new EqualToValueMatcher("bar").Match(true, ["Bar"]).IsExactMatch);
        Assert.False(new EqualToValueMatcher("bar").Match(false, []).IsExactMatch);
    }

    [Fact]
    public void EqualTo_caseInsensitive_and_ignoreCase_match_regardless_of_case()
    {
        Assert.True(new EqualToValueMatcher("bar", caseInsensitive: true).Match(true, ["BAR"]).IsExactMatch);
        Assert.True(new EqualToIgnoreCaseValueMatcher("bar").Match(true, ["BAR"]).IsExactMatch);
    }

    [Fact]
    public void Contains_checks_substring()
    {
        Assert.True(new ContainsValueMatcher("ell").Match(true, ["hello"]).IsExactMatch);
        Assert.False(new ContainsValueMatcher("xyz").Match(true, ["hello"]).IsExactMatch);
    }

    [Fact]
    public void Matches_requires_full_match()
    {
        var matcher = new MatchesValueMatcher("[0-9]+");
        Assert.True(matcher.Match(true, ["123"]).IsExactMatch);
        Assert.False(matcher.Match(true, ["12a"]).IsExactMatch);
        Assert.False(matcher.Match(true, ["a12"]).IsExactMatch);
    }

    [Fact]
    public void DoesNotMatch_is_the_negation_when_present()
    {
        var matcher = new DoesNotMatchValueMatcher("[0-9]+");
        Assert.True(matcher.Match(true, ["abc"]).IsExactMatch);
        Assert.False(matcher.Match(true, ["123"]).IsExactMatch);
    }

    [Fact]
    public void Absent_matches_only_when_missing()
    {
        Assert.True(new AbsentValueMatcher().Match(false, []).IsExactMatch);
        Assert.False(new AbsentValueMatcher().Match(true, ["x"]).IsExactMatch);
    }

    [Fact]
    public void HeaderMatcher_reads_the_named_header()
    {
        var input = new MatchInput
        {
            Request = CanonicalRequestBuilder.Build("GET", "/x", [new("X-Env", "prod")]),
        };

        Assert.True(new HeaderMatcher("X-Env", new EqualToValueMatcher("prod")).Match(input).IsExactMatch);
        Assert.False(new HeaderMatcher("X-Env", new EqualToValueMatcher("dev")).Match(input).IsExactMatch);
        Assert.True(new HeaderMatcher("X-Missing", new AbsentValueMatcher()).Match(input).IsExactMatch);
    }

    [Fact]
    public void QueryMatcher_reads_the_named_parameter()
    {
        var input = new MatchInput { Request = CanonicalRequestBuilder.Build("GET", "/x?q=cats") };

        Assert.True(new QueryMatcher("q", new EqualToValueMatcher("cats")).Match(input).IsExactMatch);
        Assert.False(new QueryMatcher("q", new EqualToValueMatcher("dogs")).Match(input).IsExactMatch);
    }

    [Fact]
    public void CookieMatcher_reads_the_named_cookie_from_the_Cookie_header()
    {
        var input = new MatchInput
        {
            Request = CanonicalRequestBuilder.Build("GET", "/x", [new("Cookie", "sid=abc; theme=dark")]),
        };

        Assert.True(new CookieMatcher("sid", new EqualToValueMatcher("abc")).Match(input).IsExactMatch);
        Assert.True(new CookieMatcher("theme", new EqualToValueMatcher("dark")).Match(input).IsExactMatch);
        Assert.False(new CookieMatcher("sid", new EqualToValueMatcher("xyz")).Match(input).IsExactMatch);
        Assert.True(new CookieMatcher("missing", new AbsentValueMatcher()).Match(input).IsExactMatch);
    }
}
