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

    [Fact]
    public Task Templating_ResponseTemplate() => Verify(TemplatingScenarios.ResponseTemplate());

    [Fact]
    public Task Templating_PathVariables() => Verify(TemplatingScenarios.PathVariables());

    [Fact]
    public Task Templating_DataHelpers() => Verify(TemplatingScenarios.DataHelpers());

    [Fact]
    public Task Templating_DateHelpers() => Verify(TemplatingScenarios.DateHelpers());

    [Fact]
    public Task Templating_JsonHelpers() => Verify(TemplatingScenarios.JsonHelpers());

    [Fact]
    public Task Templating_FormatHelpers() => Verify(TemplatingScenarios.FormatHelpers());

    [Fact]
    public Task Templating_SystemHelpers() => Verify(TemplatingScenarios.SystemHelpers());

    [Fact]
    public Task Templating_MoreHelpers() => Verify(TemplatingScenarios.MoreHelpers());

    [Fact]
    public Task Templating_RequestModelFields() => Verify(TemplatingScenarios.RequestModelFields());

    /// <summary>
    /// Random helpers (G2e) can't be byte-diffed — their output is non-deterministic. Instead each
    /// case is probed many times: the oracle output must satisfy the case's structural contract
    /// (proving the contract is real WireMock behavior) and Mockifyr's output must satisfy the same
    /// contract. Both sides being independently valid under one contract is the parity claim.
    /// </summary>
    [Fact]
    public async Task Templating_RandomHelpers()
    {
        const int iterations = 25;
        var failures = new List<string>();

        foreach (var scenario in RandomScenarios.All())
        {
            await _runner.LoadAsync(scenario.WireMockJson);

            for (var i = 0; i < iterations; i++)
            {
                var outcome = await _runner.ProbeAsync(scenario.Request);

                if (!outcome.OracleMatched)
                {
                    failures.Add($"{scenario.Description}: oracle did not serve the stub (status {outcome.Oracle.Status})");
                }
                else if (!scenario.IsValid(outcome.Oracle.BodyAsText))
                {
                    failures.Add($"{scenario.Description}: ORACLE body violates the contract: \"{outcome.Oracle.BodyAsText}\"");
                }

                if (!scenario.IsValid(outcome.Mockifyr.BodyAsText))
                {
                    failures.Add($"{scenario.Description}: mockifyr body violates the contract: \"{outcome.Mockifyr.BodyAsText}\"");
                }
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} violation(s):\n{string.Join("\n", failures.Distinct().Take(25))}");
    }

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
