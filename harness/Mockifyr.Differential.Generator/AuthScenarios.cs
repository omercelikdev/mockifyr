using System.Text;
using System.Text.Json;

namespace Mockifyr.Differential.Generator;

/// <summary>
/// Fuzzes <c>basicAuthCredentials</c> (G1k): a request-level shorthand that matches when the
/// <c>Authorization</c> header carries the expected HTTP Basic token. Pinned by the differential
/// suite — see docs/parity/g1-matching.md.
/// </summary>
public static class AuthScenarios
{
    public static IEnumerable<MatcherScenario> BasicAuth()
    {
        var correct = BasicToken("user", "pass");
        var wrongPassword = BasicToken("user", "wrong");
        var wrongUser = BasicToken("admin", "pass");

        var mapping = new Dictionary<string, object>
        {
            ["request"] = new Dictionary<string, object>
            {
                ["method"] = "GET",
                ["urlPath"] = "/p",
                ["basicAuthCredentials"] = new Dictionary<string, object> { ["username"] = "user", ["password"] = "pass" },
            },
            ["response"] = new Dictionary<string, object> { ["status"] = 200, ["body"] = "ok" },
        };

        var probes = new List<ProbeRequest>
        {
            new(WithAuth(correct), ExpectedMatch: true),
            new(WithAuth(wrongPassword), ExpectedMatch: false),
            new(WithAuth(wrongUser), ExpectedMatch: false),
            new(WithAuth("Basic not-base64"), ExpectedMatch: false),
            new(new RequestSpec { Method = "GET", Url = "/p" }, ExpectedMatch: false), // no Authorization header
        };

        yield return new MatcherScenario("basicAuth[user:pass]", JsonSerializer.Serialize(mapping), probes);
    }

    private static string BasicToken(string username, string password) =>
        "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));

    private static RequestSpec WithAuth(string authorization) =>
        new() { Method = "GET", Url = "/p", Headers = [new("Authorization", authorization)] };
}
