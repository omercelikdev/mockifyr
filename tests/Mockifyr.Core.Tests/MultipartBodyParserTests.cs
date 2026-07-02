using System.Text;
using Mockifyr.Core;

namespace Mockifyr.Core.Tests;

public class MultipartBodyParserTests
{
    private const string Boundary = "XABC";
    private const string ContentType = "multipart/form-data; boundary=" + Boundary;

    private static byte[] Body(params (string Name, string Value)[] parts)
    {
        var builder = new StringBuilder();
        foreach (var (name, value) in parts)
        {
            builder.Append("--").Append(Boundary).Append("\r\n")
                .Append("Content-Disposition: form-data; name=\"").Append(name).Append("\"\r\n\r\n")
                .Append(value).Append("\r\n");
        }

        builder.Append("--").Append(Boundary).Append("--\r\n");
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    [Fact]
    public void Parses_names_and_bodies()
    {
        var parts = MultipartBodyParser.Parse(Body(("f1", "hello world"), ("f2", "bye")), ContentType);

        Assert.Equal(2, parts.Count);
        Assert.Equal("f1", parts[0].Name);
        Assert.Equal("hello world", Encoding.UTF8.GetString(parts[0].Body));
        Assert.Equal("f2", parts[1].Name);
        Assert.Equal("bye", Encoding.UTF8.GetString(parts[1].Body));
    }

    [Fact]
    public void Non_multipart_content_type_yields_no_parts()
    {
        Assert.Empty(MultipartBodyParser.Parse(Encoding.UTF8.GetBytes("hello"), "text/plain"));
        Assert.Empty(MultipartBodyParser.Parse(Encoding.UTF8.GetBytes("hello"), contentType: null));
        Assert.Empty(MultipartBodyParser.Parse([], ContentType));
    }

    [Fact]
    public void Keeps_part_headers()
    {
        var parts = MultipartBodyParser.Parse(Body(("f1", "x")), ContentType);

        Assert.Single(parts);
        Assert.Equal("form-data; name=\"f1\"", parts[0].Headers["Content-Disposition"]);
    }
}
