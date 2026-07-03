using System.Text.Json;

namespace Mockifyr.Differential.Generator;

/// <summary>
/// Fuzzes static response rendering (G2a): <c>jsonBody</c> (inline JSON, emitted compact),
/// <c>base64Body</c> (decoded bytes), and multi-value response headers. Each scenario matches a
/// simple request so the differential harness compares the rendered body/headers. See
/// docs/parity/g2-response.md.
/// </summary>
public static class ResponseScenarios
{
    public static IEnumerable<MatcherScenario> Bodies()
    {
        yield return One(
            "jsonBody",
            new Dictionary<string, object>
            {
                ["status"] = 200,
                ["jsonBody"] = new Dictionary<string, object> { ["a"] = 1, ["b"] = new object[] { 2, 3 }, ["c"] = "x" },
            },
            "/j",
            withUnmatched: true);

        yield return One(
            "base64Body",
            new Dictionary<string, object>
            {
                ["status"] = 200,
                ["base64Body"] = Convert.ToBase64String([0x68, 0x69, 0x00, 0x21]), // "hi\0!"
            },
            "/b");

        yield return One(
            "multi-value header",
            new Dictionary<string, object>
            {
                ["status"] = 200,
                ["body"] = "ok",
                ["headers"] = new Dictionary<string, object> { ["X-Multi"] = new object[] { "a", "b" } },
            },
            "/h");

        yield return One(
            "custom status code",
            new Dictionary<string, object> { ["status"] = 418, ["body"] = "teapot" },
            "/t");
    }

    private static MatcherScenario One(string description, Dictionary<string, object> response, string url, bool withUnmatched = false)
    {
        var mapping = new Dictionary<string, object>
        {
            ["request"] = new Dictionary<string, object> { ["method"] = "GET", ["urlPath"] = url },
            ["response"] = response,
        };

        var probes = new List<ProbeRequest> { new(new RequestSpec { Method = "GET", Url = url }, ExpectedMatch: true) };
        if (withUnmatched)
        {
            probes.Add(new(new RequestSpec { Method = "GET", Url = "/unmatched" }, ExpectedMatch: false));
        }

        return new MatcherScenario($"response[{description}]", JsonSerializer.Serialize(mapping), probes);
    }
}
