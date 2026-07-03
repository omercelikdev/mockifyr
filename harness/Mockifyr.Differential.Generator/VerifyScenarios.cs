using System.Text;
using System.Text.Json;

namespace Mockifyr.Differential.Generator;

/// <summary>
/// A verification (G6) case: stubs to load, request traffic to replay into the journal, and the
/// request patterns to count. The harness compares each pattern's match count and the unmatched
/// count between the oracle and Mockifyr.
/// </summary>
public sealed record VerifyScenario(
    string Description,
    string MappingsJson,
    IReadOnlyList<RequestSpec> Traffic,
    IReadOnlyList<string> CountPatterns);

/// <summary>Verification / near-miss cases (G6): count/find/unmatched over the request journal.</summary>
public static class VerifyScenarios
{
    public static IEnumerable<VerifyScenario> All()
    {
        // One body-gated stub. Traffic: 2 match the body, 1 is same URL but wrong body, 1 is a
        // different URL (unmatched). The count patterns exercise: method+url, method+url+body,
        // a non-matching method, and the match-everything empty pattern.
        var mappings = Mappings(
            Stub("POST", "/api", body: "hello", response: "ok"));

        var traffic = new List<RequestSpec>
        {
            Post("/api", "say hello world"),
            Post("/api", "hello again"),
            Post("/api", "no greeting here"),
            new() { Method = "GET", Url = "/somewhere-else" },
        };

        var patterns = new List<string>
        {
            Pattern(("method", "POST"), ("url", "/api")),
            PatternWithBody("POST", "/api", "hello"),
            Pattern(("method", "DELETE"), ("url", "/api")),
            "{}", // matches everything
        };

        yield return new VerifyScenario("count-find-unmatched", mappings, traffic, patterns);
    }

    private static RequestSpec Post(string url, string body) =>
        new() { Method = "POST", Url = url, Body = Encoding.UTF8.GetBytes(body) };

    private static Dictionary<string, object> Stub(string method, string url, string body, string response) => new()
    {
        ["request"] = new Dictionary<string, object>
        {
            ["method"] = method,
            ["url"] = url,
            ["bodyPatterns"] = new object[] { new Dictionary<string, object> { ["contains"] = body } },
        },
        ["response"] = new Dictionary<string, object> { ["status"] = 200, ["body"] = response },
    };

    private static string Pattern(params (string Key, string Value)[] fields) =>
        JsonSerializer.Serialize(fields.ToDictionary(f => f.Key, f => (object)f.Value));

    private static string PatternWithBody(string method, string url, string contains) =>
        JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["method"] = method,
            ["url"] = url,
            ["bodyPatterns"] = new object[] { new Dictionary<string, object> { ["contains"] = contains } },
        });

    private static string Mappings(params Dictionary<string, object>[] stubs) =>
        JsonSerializer.Serialize(new Dictionary<string, object> { ["mappings"] = stubs });
}
