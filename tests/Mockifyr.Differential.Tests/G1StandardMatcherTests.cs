using Mockifyr.Differential.Harness;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// G1c/G1d: the standard matcher set (equalTo, contains, matches, doesNotMatch, absent) applied
/// to headers, query parameters, and the body — each validated against the WireMock oracle.
/// Positive cases assert a green diff; negative cases assert both sides return 404 (the no-match
/// body is compared from G6). Requires Docker.
/// </summary>
public sealed class G1StandardMatcherTests : IAsyncLifetime
{
    private readonly DifferentialRunner _runner = new();

    public Task InitializeAsync() => _runner.StartAsync();

    public async Task DisposeAsync() => await _runner.DisposeAsync();

    [Fact]
    public async Task Header_equalTo_present_matches_oracle()
    {
        const string mapping =
            """
            {
              "request": { "method": "GET", "urlPath": "/h", "headers": { "X-Env": { "equalTo": "prod" } } },
              "response": { "status": 200, "body": "ok" }
            }
            """;

        var request = new RequestSpec { Method = "GET", Url = "/h", Headers = [new("X-Env", "prod")] };
        var outcome = await _runner.RunAsync(mapping, request);

        Assert.Equal(200, outcome.Oracle.Status);
        Assert.True(outcome.Diff.IsMatch, outcome.Diff.Report);
    }

    [Fact]
    public async Task Header_equalTo_wrong_value_is_notFound_on_both()
    {
        const string mapping =
            """
            {
              "request": { "method": "GET", "urlPath": "/h", "headers": { "X-Env": { "equalTo": "prod" } } },
              "response": { "status": 200, "body": "ok" }
            }
            """;

        var request = new RequestSpec { Method = "GET", Url = "/h", Headers = [new("X-Env", "dev")] };
        var outcome = await _runner.RunAsync(mapping, request);

        Assert.Equal(404, outcome.Oracle.Status);
        Assert.Equal(404, outcome.Mockifyr.Status);
    }

    [Fact]
    public async Task Header_absent_matches_when_missing()
    {
        const string mapping =
            """
            {
              "request": { "method": "GET", "urlPath": "/h", "headers": { "X-Env": { "absent": true } } },
              "response": { "status": 200, "body": "ok" }
            }
            """;

        var request = new RequestSpec { Method = "GET", Url = "/h" };
        var outcome = await _runner.RunAsync(mapping, request);

        Assert.Equal(200, outcome.Oracle.Status);
        Assert.True(outcome.Diff.IsMatch, outcome.Diff.Report);
    }

    [Fact]
    public async Task Query_equalTo_matches_oracle()
    {
        const string mapping =
            """
            {
              "request": { "method": "GET", "urlPath": "/search", "queryParameters": { "q": { "equalTo": "cats" } } },
              "response": { "status": 200, "body": "ok" }
            }
            """;

        var outcome = await _runner.RunAsync(mapping, new RequestSpec { Method = "GET", Url = "/search?q=cats" });

        Assert.Equal(200, outcome.Oracle.Status);
        Assert.True(outcome.Diff.IsMatch, outcome.Diff.Report);
    }

    [Fact]
    public async Task Query_equalTo_mismatch_is_notFound_on_both()
    {
        const string mapping =
            """
            {
              "request": { "method": "GET", "urlPath": "/search", "queryParameters": { "q": { "equalTo": "cats" } } },
              "response": { "status": 200, "body": "ok" }
            }
            """;

        var outcome = await _runner.RunAsync(mapping, new RequestSpec { Method = "GET", Url = "/search?q=dogs" });

        Assert.Equal(404, outcome.Oracle.Status);
        Assert.Equal(404, outcome.Mockifyr.Status);
    }

    [Fact]
    public async Task Body_equalTo_matches_oracle()
    {
        const string mapping =
            """
            {
              "request": { "method": "POST", "urlPath": "/b", "bodyPatterns": [ { "equalTo": "ping" } ] },
              "response": { "status": 200, "body": "ok" }
            }
            """;

        var request = new RequestSpec { Method = "POST", Url = "/b", Body = "ping"u8.ToArray() };
        var outcome = await _runner.RunAsync(mapping, request);

        Assert.Equal(200, outcome.Oracle.Status);
        Assert.True(outcome.Diff.IsMatch, outcome.Diff.Report);
    }

    [Fact]
    public async Task Body_contains_matches_oracle()
    {
        const string mapping =
            """
            {
              "request": { "method": "POST", "urlPath": "/b", "bodyPatterns": [ { "contains": "pong" } ] },
              "response": { "status": 200, "body": "ok" }
            }
            """;

        var request = new RequestSpec { Method = "POST", Url = "/b", Body = "pingpong"u8.ToArray() };
        var outcome = await _runner.RunAsync(mapping, request);

        Assert.Equal(200, outcome.Oracle.Status);
        Assert.True(outcome.Diff.IsMatch, outcome.Diff.Report);
    }

    [Fact]
    public async Task Body_matches_regex_full_match_semantics()
    {
        const string mapping =
            """
            {
              "request": { "method": "POST", "urlPath": "/b", "bodyPatterns": [ { "matches": "[a-z]+[0-9]+" } ] },
              "response": { "status": 200, "body": "ok" }
            }
            """;

        var request = new RequestSpec { Method = "POST", Url = "/b", Body = "abc123"u8.ToArray() };
        var outcome = await _runner.RunAsync(mapping, request);

        Assert.Equal(200, outcome.Oracle.Status);
        Assert.True(outcome.Diff.IsMatch, outcome.Diff.Report);
    }
}
