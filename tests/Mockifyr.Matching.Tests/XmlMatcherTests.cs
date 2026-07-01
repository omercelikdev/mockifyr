namespace Mockifyr.Matching.Tests;

public class XmlMatcherTests
{
    private static bool Xml(string expected, string actual) =>
        new EqualToXmlValueMatcher(expected).Match(present: true, [actual]).IsExactMatch;

    private static bool XPath(string expression, string body) =>
        new MatchesXPathValueMatcher(expression).Match(present: true, [body]).IsExactMatch;

    private static bool XPathSub(string expression, IValueMatcher sub, string body) =>
        new MatchesXPathValueMatcher(expression, sub).Match(present: true, [body]).IsExactMatch;

    [Fact]
    public void EqualToXml_ignores_whitespace_and_attribute_order()
    {
        Assert.True(Xml("""<a x="1" y="2"><b>t</b></a>""", "<a y=\"2\" x=\"1\">\n  <b>t</b>\n</a>"));
    }

    [Fact]
    public void EqualToXml_ignores_sibling_element_order()
    {
        Assert.True(Xml("""<a><b>1</b><c>2</c></a>""", """<a><c>2</c><b>1</b></a>"""));
    }

    [Fact]
    public void EqualToXml_is_sensitive_to_text_and_attributes()
    {
        Assert.False(Xml("""<a><b>t</b></a>""", """<a><b>u</b></a>"""));
        Assert.False(Xml("""<a x="1"/>""", """<a x="2"/>"""));
    }

    [Fact]
    public void EqualToXml_invalid_actual_does_not_match()
    {
        Assert.False(Xml("""<a/>""", "not xml"));
    }

    [Fact]
    public void MatchesXPath_presence()
    {
        Assert.True(XPath("/order/item", """<order><item>book</item></order>"""));
        Assert.True(XPath("//item", """<order><line><item>x</item></line></order>"""));
        Assert.True(XPath("/order/@id", """<order id="1"/>"""));
        Assert.False(XPath("/order/item", """<order><thing/></order>"""));
    }

    [Fact]
    public void MatchesXPath_subMatcher_applies_to_extracted_text_node()
    {
        Assert.True(XPathSub("/order/item/text()", new EqualToValueMatcher("book"), """<order><item>book</item></order>"""));
        Assert.False(XPathSub("/order/item/text()", new EqualToValueMatcher("book"), """<order><item>magazine</item></order>"""));
    }
}
