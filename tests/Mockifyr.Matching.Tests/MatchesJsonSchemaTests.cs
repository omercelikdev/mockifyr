namespace Mockifyr.Matching.Tests;

public class MatchesJsonSchemaTests
{
    private const string ObjectSchema =
        """{"type":"object","required":["n"],"properties":{"n":{"type":"integer","minimum":5}}}""";

    private static bool Match(string schema, string body, string? version = null) =>
        new MatchesJsonSchemaValueMatcher(schema, version).Match(present: true, [body]).IsExactMatch;

    [Fact]
    public void Valid_body_matches_and_invalid_does_not()
    {
        Assert.True(Match(ObjectSchema, """{"n":7}"""));
        Assert.True(Match(ObjectSchema, """{"n":5}""")); // minimum inclusive
        Assert.False(Match(ObjectSchema, """{"n":3}""")); // below minimum
        Assert.False(Match(ObjectSchema, """{"n":"7"}""")); // wrong type
        Assert.False(Match(ObjectSchema, """{"x":1}""")); // missing required
    }

    [Fact]
    public void Non_json_or_absent_body_does_not_match()
    {
        Assert.False(Match(ObjectSchema, "not json"));
        Assert.False(new MatchesJsonSchemaValueMatcher(ObjectSchema).Match(present: false, []).IsExactMatch);
    }

    [Fact]
    public void Invalid_schema_never_matches()
    {
        Assert.False(Match("{ this is not valid json", """{"n":7}"""));
    }

    [Fact]
    public void SchemaVersion_selects_the_draft_for_a_schema_without_dollar_schema()
    {
        // Basic keywords behave identically across drafts, so this asserts the versioned path runs.
        Assert.True(Match(ObjectSchema, """{"n":7}""", version: "V7"));
        Assert.False(Match(ObjectSchema, """{"n":3}""", version: "V7"));
    }
}
