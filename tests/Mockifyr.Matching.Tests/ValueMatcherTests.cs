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
    public void And_requires_every_inner_matcher()
    {
        var matcher = new AndValueMatcher([new ContainsValueMatcher("a"), new ContainsValueMatcher("b")]);
        Assert.True(matcher.Match(true, ["cab"]).IsExactMatch);
        Assert.False(matcher.Match(true, ["a"]).IsExactMatch);
    }

    [Fact]
    public void Or_requires_any_inner_matcher()
    {
        var matcher = new OrValueMatcher([new EqualToValueMatcher("x"), new EqualToValueMatcher("y")]);
        Assert.True(matcher.Match(true, ["y"]).IsExactMatch);
        Assert.False(matcher.Match(true, ["z"]).IsExactMatch);
    }

    [Fact]
    public void Not_negates_the_inner_matcher_including_absence()
    {
        var matcher = new NotValueMatcher(new EqualToValueMatcher("x"));
        Assert.True(matcher.Match(true, ["y"]).IsExactMatch);
        Assert.False(matcher.Match(true, ["x"]).IsExactMatch);
        Assert.True(matcher.Match(false, []).IsExactMatch); // absent target: inner fails, so not matches
    }

    [Fact]
    public void DateTime_before_after_equal_compare_the_instant()
    {
        const string expected = "2021-06-15T12:00:00Z";
        Assert.True(new DateTimeValueMatcher(DateTimeComparison.Before, expected).Match(true, ["2021-06-15T11:00:00Z"]).IsExactMatch);
        Assert.False(new DateTimeValueMatcher(DateTimeComparison.Before, expected).Match(true, ["2021-06-15T12:00:00Z"]).IsExactMatch);
        Assert.True(new DateTimeValueMatcher(DateTimeComparison.After, expected).Match(true, ["2021-06-15T13:00:00Z"]).IsExactMatch);
        Assert.True(new DateTimeValueMatcher(DateTimeComparison.Equal, expected).Match(true, ["2021-06-15T12:00:00Z"]).IsExactMatch);
    }

    [Fact]
    public void DateTime_equality_is_zone_normalized()
    {
        // Same instant expressed in a different offset must compare equal.
        var matcher = new DateTimeValueMatcher(DateTimeComparison.Equal, "2021-06-15T12:00:00Z");
        Assert.True(matcher.Match(true, ["2021-06-15T14:00:00+02:00"]).IsExactMatch);
    }

    [Fact]
    public void DateTime_unparseable_actual_or_expected_does_not_match()
    {
        Assert.False(new DateTimeValueMatcher(DateTimeComparison.Before, "2021-06-15T12:00:00Z").Match(true, ["nope"]).IsExactMatch);
        Assert.False(new DateTimeValueMatcher(DateTimeComparison.Before, "not-a-date").Match(true, ["2021-06-15T11:00:00Z"]).IsExactMatch);
        Assert.False(new DateTimeValueMatcher(DateTimeComparison.Before, "2021-06-15T12:00:00Z").Match(false, []).IsExactMatch);
    }

    [Fact]
    public void DateTime_actualFormat_parses_the_incoming_value()
    {
        var matcher = new DateTimeValueMatcher(DateTimeComparison.After, "2021-06-15T00:00:00Z", "dd/MM/yyyy");
        Assert.True(matcher.Match(true, ["16/06/2021"]).IsExactMatch);
        Assert.False(matcher.Match(true, ["14/06/2021"]).IsExactMatch);
        Assert.False(matcher.Match(true, ["2021-06-16"]).IsExactMatch); // wrong format → no parse → no match
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
