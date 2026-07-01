namespace Mockifyr.Matching.Tests;

public class EqualToJsonTests
{
    private static bool Matches(string expected, string actual, bool ignoreArrayOrder = false, bool ignoreExtraElements = false) =>
        new EqualToJsonValueMatcher(expected, ignoreArrayOrder, ignoreExtraElements)
            .Match(present: true, [actual]).IsExactMatch;

    [Fact]
    public void Key_order_and_whitespace_are_irrelevant()
    {
        Assert.True(Matches("""{"a":1,"b":2}""", """{ "b": 2, "a": 1 }"""));
    }

    [Fact]
    public void Numbers_compare_by_value()
    {
        Assert.True(Matches("""{"n":1}""", """{"n":1.0}"""));
        Assert.True(Matches("[1,2]", "[1.0,2.0]"));
    }

    [Fact]
    public void Types_are_significant()
    {
        Assert.False(Matches("""{"n":1}""", """{"n":"1"}"""));
        Assert.False(Matches("""{"b":true}""", """{"b":"true"}"""));
    }

    [Fact]
    public void Strict_rejects_extra_fields_and_reordered_arrays()
    {
        Assert.False(Matches("""{"a":1}""", """{"a":1,"b":2}"""));
        Assert.False(Matches("[1,2,3]", "[3,2,1]"));
    }

    [Fact]
    public void IgnoreExtraElements_allows_extra_fields_but_not_missing()
    {
        Assert.True(Matches("""{"a":1}""", """{"a":1,"b":2}""", ignoreExtraElements: true));
        Assert.False(Matches("""{"a":1,"b":2}""", """{"a":1}""", ignoreExtraElements: true));
    }

    [Fact]
    public void IgnoreArrayOrder_treats_arrays_as_multisets()
    {
        Assert.True(Matches("[1,2,3]", "[3,1,2]", ignoreArrayOrder: true));
        Assert.False(Matches("[1,2,3]", "[1,2]", ignoreArrayOrder: true));
        Assert.False(Matches("[1,2,2]", "[1,2,3]", ignoreArrayOrder: true));
    }

    [Fact]
    public void Invalid_actual_json_does_not_match()
    {
        Assert.False(Matches("""{"a":1}""", "not json"));
    }
}
