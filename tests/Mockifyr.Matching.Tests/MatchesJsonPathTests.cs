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
    public void SubMatcher_applies_to_the_extracted_value()
    {
        Assert.True(WithSub("$.name", new EqualToValueMatcher("tom"), """{"name":"tom"}"""));
        Assert.False(WithSub("$.name", new EqualToValueMatcher("tom"), """{"name":"bob"}"""));
        Assert.True(WithSub("$.n", new EqualToValueMatcher("30"), """{"n":30}"""));
    }
}
