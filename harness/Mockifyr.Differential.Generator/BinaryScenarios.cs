using System.Text;
using System.Text.Json;

namespace Mockifyr.Differential.Generator;

/// <summary>
/// Fuzzes <c>binaryEqualTo</c> (G1d): an exact byte-for-byte body comparison against a base64-encoded
/// expected value, including non-text bytes. Pinned by the differential suite — see
/// docs/parity/g1-matching.md.
/// </summary>
public static class BinaryScenarios
{
    public static IEnumerable<MatcherScenario> BinaryEqualTo()
    {
        yield return Build("text bytes", Encoding.UTF8.GetBytes("hello"),
            (Encoding.UTF8.GetBytes("hello"), true),
            (Encoding.UTF8.GetBytes("world"), false),
            (Encoding.UTF8.GetBytes("hello!"), false));

        var binary = new byte[] { 0x00, 0x01, 0xFF, 0x10 };
        yield return Build("raw bytes", binary,
            (binary, true),
            (new byte[] { 0x00, 0x01, 0xFE, 0x10 }, false),
            (new byte[] { 0x00, 0x01, 0xFF }, false));
    }

    private static MatcherScenario Build(string description, byte[] expected, params (byte[] Body, bool Match)[] cases)
    {
        var mapping = new Dictionary<string, object>
        {
            ["request"] = new Dictionary<string, object>
            {
                ["method"] = "POST",
                ["urlPath"] = "/b",
                ["bodyPatterns"] = new object[]
                {
                    new Dictionary<string, object> { ["binaryEqualTo"] = Convert.ToBase64String(expected) },
                },
            },
            ["response"] = new Dictionary<string, object> { ["status"] = 200, ["body"] = "ok" },
        };

        var probes = cases
            .Select(c => new ProbeRequest(new RequestSpec { Method = "POST", Url = "/b", Body = c.Body }, c.Match))
            .ToList();

        return new MatcherScenario($"binaryEqualTo[{description}]", JsonSerializer.Serialize(mapping), probes);
    }
}
