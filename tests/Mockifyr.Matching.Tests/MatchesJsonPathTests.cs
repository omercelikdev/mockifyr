namespace Mockifyr.Matching.Tests;

public class MatchesJsonPathTests
{
    private static bool Presence(string expression, string body) =>
        new MatchesJsonPathValueMatcher(expression).Match(present: true, [body]).IsExactMatch;

    private static bool WithSub(string expression, IValueMatcher sub, string body) =>
        new MatchesJsonPathValueMatcher(expression, sub).Match(present: true, [body]).IsExactMatch;

    [Fact]
    public void Presence_matches_when_the_path_selects_a_node()
    {
        Assert.True(Presence("$.name", """{"name":"x"}"""));
        Assert.True(Presence("$.a.b", """{"a":{"b":1}}"""));
        Assert.True(Presence("$..id", """{"x":{"id":1}}"""));
    }

    [Fact]
    public void Presence_does_not_match_when_the_path_is_absent()
    {
        Assert.False(Presence("$.name", """{"other":1}"""));
        Assert.False(Presence("$.items[0]", """{"items":[]}"""));
    }

    [Fact]
    public void Invalid_body_or_expression_does_not_match()
    {
        Assert.False(Presence("$.name", "not json"));
        Assert.False(Presence("$[", """{"name":"x"}"""));
    }

    [Fact]
    public void Numeric_filter_selects_elements_by_value()
    {
        // G1j: numeric matching in WireMock is reached through JSONPath filter expressions.
        Assert.True(Presence("$.items[?(@.price > 10)]", """{"items":[{"price":20}]}"""));
        Assert.False(Presence("$.items[?(@.price > 10)]", """{"items":[{"price":10}]}""")); // strict
        Assert.True(Presence("$.items[?(@.price <= 10)]", """{"items":[{"price":10}]}"""));
        Assert.True(Presence("$.items[?(@.qty == 3)]", """{"items":[{"qty":3.0}]}""")); // scale-insensitive
        Assert.False(Presence("$.items[?(@.rate > 1.5)]", """{"items":[{"rate":1.5}]}"""));
    }

    [Fact]
    public void SubMatcher_applies_to_the_extracted_value()
    {
        Assert.True(WithSub("$.name", new EqualToValueMatcher("tom"), """{"name":"tom"}"""));
        Assert.False(WithSub("$.name", new EqualToValueMatcher("tom"), """{"name":"bob"}"""));
        Assert.True(WithSub("$.n", new EqualToValueMatcher("30"), """{"n":30}"""));
    }
}
