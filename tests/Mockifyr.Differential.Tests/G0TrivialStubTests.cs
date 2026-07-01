using Mockifyr.Differential.Generator;
using Mockifyr.Differential.Harness;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// G0 gate: the harness diffs a trivial stub (exact URL + static response) against the real
/// WireMock oracle. Requires Docker. See docs/decisions/0002.
/// </summary>
public sealed class G0TrivialStubTests : IAsyncLifetime
{
    private readonly DifferentialRunner _runner = new();

    public Task InitializeAsync() => _runner.StartAsync();

    public async Task DisposeAsync() => await _runner.DisposeAsync();

    [Fact]
    public async Task ExactUrl_StaticResponse_MatchesOracle()
    {
        const string mapping =
            """
            {
              "request": { "method": "GET", "url": "/hello" },
              "response": { "status": 200, "body": "world" }
            }
            """;

        var outcome = await _runner.RunAsync(mapping, new RequestSpec { Method = "GET", Url = "/hello" });

        // The oracle is the source of truth; assert we reproduce it exactly.
        Assert.Equal(200, outcome.Oracle.Status);
        Assert.Equal("world", outcome.Oracle.BodyAsText);
        Assert.True(outcome.Diff.IsMatch, outcome.Diff.Report);
    }

    [Fact]
    public async Task UnknownPath_YieldsNotFoundOnBothSides()
    {
        const string mapping =
            """
            {
              "request": { "method": "GET", "url": "/hello" },
              "response": { "status": 200, "body": "world" }
            }
            """;

        var outcome = await _runner.RunAsync(mapping, new RequestSpec { Method = "GET", Url = "/nope" });

        // Both must treat an unmatched request as 404 (body diff for no-match arrives with G6).
        Assert.Equal(404, outcome.Oracle.Status);
        Assert.Equal(404, outcome.Mockifyr.Status);
    }
}
