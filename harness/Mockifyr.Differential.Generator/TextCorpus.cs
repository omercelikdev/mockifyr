namespace Mockifyr.Differential.Generator;

/// <summary>
/// Curated "interesting" values used to fuzz matchers across their input space. Separate corpora
/// avoid transport ambiguities: header/query values stay to safe printable ASCII (no leading or
/// trailing whitespace, no control characters), while the body may hold anything.
/// </summary>
/// <remarks>
/// Deliberately excluded for now (they carry transport-level ambiguity we validate later):
/// empty and whitespace-only header values, control characters and non-ASCII in headers.
/// Tracked in docs/parity/g1-matching.md.
/// </remarks>
public static class TextCorpus
{
    /// <summary>Values safe to send as a header or query value.</summary>
    public static readonly IReadOnlyList<string> HeaderSafe =
    [
        "a", "A", "abc", "ABC", "hello world", "a b",
        "123", "0", "-1", "1.0", "true", "false", "null",
        "{}", "[]", "q\"q", "a/b", "a\\b", "x=y", "key-val",
        new string('x', 300),
    ];

    /// <summary>Values safe to send as a request body (a superset of <see cref="HeaderSafe"/>).</summary>
    public static readonly IReadOnlyList<string> Body =
    [
        .. HeaderSafe,
        "", "  ", " leading", "trailing ",
        "héllo", "naïve", "Ω≈ç√", "日本語", "😀",
        "line1\nline2", "tab\tsep", "carriage\r\nreturn",
    ];
}
