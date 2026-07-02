using System.Text;

namespace Mockifyr.Core;

/// <summary>
/// Parses a <c>multipart/form-data</c> request body into its parts. Pure string/byte work (no I/O,
/// no dependencies), so it lives with the other canonicalisation helpers; facades call it when
/// building the <see cref="CanonicalRequest"/>. Binary parts are decoded as UTF-8 text, which is
/// exact for the text corpus the differential suite exercises; non-text parts are out of scope.
/// </summary>
public static class MultipartBodyParser
{
    /// <summary>Parses the body into parts, or an empty list when it is not a parseable multipart body.</summary>
    public static IReadOnlyList<MultipartPart> Parse(byte[] body, string? contentType)
    {
        if (body.Length == 0 || ExtractBoundary(contentType) is not { } boundary)
        {
            return [];
        }

        var text = Encoding.UTF8.GetString(body);
        var delimiter = "--" + boundary;
        var parts = new List<MultipartPart>();

        foreach (var segment in text.Split(delimiter))
        {
            // Only true part segments carry a header block terminated by a blank line; the preamble,
            // the epilogue, and the closing "--" delimiter do not.
            var headerEnd = segment.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (headerEnd < 0)
            {
                continue;
            }

            var headers = ParseHeaders(segment[..headerEnd].Trim('\r', '\n'));
            var partBody = segment[(headerEnd + 4)..];

            // Drop the single CRLF that separates this part's body from the next delimiter.
            if (partBody.EndsWith("\r\n", StringComparison.Ordinal))
            {
                partBody = partBody[..^2];
            }

            parts.Add(new MultipartPart
            {
                Name = ExtractName(headers) ?? string.Empty,
                Headers = headers,
                Body = Encoding.UTF8.GetBytes(partBody),
            });
        }

        return parts;
    }

    private static string? ExtractBoundary(string? contentType)
    {
        if (contentType is null)
        {
            return null;
        }

        var index = contentType.IndexOf("boundary=", StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var boundary = contentType[(index + "boundary=".Length)..].Trim();
        if (boundary.StartsWith('"'))
        {
            var end = boundary.IndexOf('"', 1);
            boundary = end > 0 ? boundary[1..end] : boundary[1..];
        }
        else
        {
            var semicolon = boundary.IndexOf(';', StringComparison.Ordinal);
            if (semicolon >= 0)
            {
                boundary = boundary[..semicolon].Trim();
            }
        }

        return boundary.Length > 0 ? boundary : null;
    }

    private static IReadOnlyDictionary<string, string> ParseHeaders(string headerBlock)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in headerBlock.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0)
            {
                headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
            }
        }

        return headers;
    }

    private static string? ExtractName(IReadOnlyDictionary<string, string> headers)
    {
        if (!headers.TryGetValue("Content-Disposition", out var disposition))
        {
            return null;
        }

        var index = disposition.IndexOf("name=", StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var value = disposition[(index + "name=".Length)..].Trim();
        if (value.StartsWith('"'))
        {
            var end = value.IndexOf('"', 1);
            return end > 0 ? value[1..end] : value[1..];
        }

        var semicolon = value.IndexOf(';', StringComparison.Ordinal);
        return semicolon >= 0 ? value[..semicolon].Trim() : value;
    }
}
