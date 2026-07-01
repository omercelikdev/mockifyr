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
