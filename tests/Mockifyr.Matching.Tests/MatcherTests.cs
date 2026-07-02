using Mockifyr.Core;

namespace Mockifyr.Matching.Tests;

public class MatcherTests
{
    private static MatchInput Request(string method, string url) =>
        new() { Request = CanonicalRequestBuilder.Build(method, url) };

    [Fact]
    public void UrlEqualTo_matches_identical_url()
    {
        var result = new UrlEqualToMatcher("/hello").Match(Request("GET", "/hello"));
        Assert.True(result.IsExactMatch);
    }

    [Fact]
    public void UrlEqualTo_rejects_different_url()
    {
        var result = new UrlEqualToMatcher("/hello").Match(Request("GET", "/world"));
        Assert.False(result.IsExactMatch);
    }

    [Fact]
    public void UrlEqualTo_is_query_sensitive()
    {
        var result = new UrlEqualToMatcher("/hello?a=1").Match(Request("GET", "/hello?a=2"));
        Assert.False(result.IsExactMatch);
    }

    [Fact]
    public void UrlPathEqualTo_ignores_query()
    {
        var result = new UrlPathEqualToMatcher("/hello").Match(Request("GET", "/hello?a=1"));
        Assert.True(result.IsExactMatch);
    }

    [Fact]
    public void UrlPattern_is_an_anchored_regex_over_the_full_url()
    {
        var matcher = new UrlPatternMatcher(@"/things/[0-9]+\?ok=true");
        Assert.True(matcher.Match(Request("GET", "/things/12?ok=true")).IsExactMatch);
        Assert.False(matcher.Match(Request("GET", "/things/12?ok=false")).IsExactMatch);
        Assert.False(matcher.Match(Request("GET", "/things/12")).IsExactMatch); // anchored
    }

    [Fact]
    public void UrlPathPattern_is_an_anchored_regex_over_the_path()
    {
        var matcher = new UrlPathPatternMatcher("/u/[a-z]+");
        Assert.True(matcher.Match(Request("GET", "/u/abc?x=1")).IsExactMatch); // query ignored
        Assert.False(matcher.Match(Request("GET", "/u/123")).IsExactMatch);
        Assert.False(matcher.Match(Request("GET", "/u/abc/def")).IsExactMatch); // whole-path anchored
    }

    [Fact]
    public void UrlPathTemplate_matches_one_segment_per_variable()
    {
        var matcher = new UrlPathTemplateMatcher("/users/{id}/orders/{oid}");
        Assert.True(matcher.Match(Request("GET", "/users/1/orders/9")).IsExactMatch);
        Assert.True(matcher.Match(Request("GET", "/users/1/orders/9?x=1")).IsExactMatch); // query ignored
        Assert.False(matcher.Match(Request("GET", "/users/1/orders")).IsExactMatch); // missing segment
        Assert.False(matcher.Match(Request("GET", "/users/1/orders/9/extra")).IsExactMatch); // extra segment
    }

    [Fact]
    public void Method_matches_case_insensitively()
    {
        var result = new MethodMatcher("get").Match(Request("GET", "/x"));
        Assert.True(result.IsExactMatch);
    }

    [Fact]
    public void Method_any_matches_every_method()
    {
        var result = new MethodMatcher("ANY").Match(Request("DELETE", "/x"));
        Assert.True(result.IsExactMatch);
    }
}
