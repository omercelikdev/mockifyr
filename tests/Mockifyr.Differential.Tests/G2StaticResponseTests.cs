using Mockifyr.Differential.Generator;
using Mockifyr.Differential.Harness;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Differential validation of static response rendering (G2a): the harness renders each stub's
/// response and diffs the body, status, and declared headers against the WireMock oracle. Requires
/// Docker.
/// </summary>
public sealed class G2StaticResponseTests : IAsyncLifetime
{
    private readonly DifferentialRunner _runner = new();

    public Task InitializeAsync() => _runner.StartAsync();

    public async Task DisposeAsync() => await _runner.DisposeAsync();

    [Fact]
    public Task StaticResponse_Bodies() => Verify(ResponseScenarios.Bodies());

    private async Task Verify(IEnumerable<MatcherScenario> scenarios)
    {
        var failures = new List<string>();
        var matched = 0;
        var unmatched = 0;

        foreach (var scenario in scenarios)
        {
            await _runner.LoadAsync(scenario.WireMockJson);

            foreach (var probe in scenario.Probes)
            {
                var outcome = await _runner.ProbeAsync(probe.Request);

                if (outcome.OracleMatched)
                {
                    matched++;
                }
                else
                {
                    unmatched++;
                }

                if (!outcome.DecisionAgrees)
                {
                    failures.Add(
                        $"{scenario.Description}: decision mismatch — oracle matched={outcome.OracleMatched}, " +
                        $"mockifyr matched={outcome.MockifyrMatched} (statuses {outcome.Oracle.Status}/{outcome.Mockifyr.Status})");
                }
                else if (outcome.OracleMatched && !outcome.Diff.IsMatch)
                {
                    failures.Add($"{scenario.Description}: response diff — {outcome.Diff.Report}");
                }
            }
        }

        Assert.True(matched > 0 && unmatched > 0, $"degenerate coverage: matched={matched}, unmatched={unmatched}");
        Assert.True(failures.Count == 0, $"{failures.Count} divergence(s):\n{string.Join("\n", failures.Take(25))}");
    }
}
