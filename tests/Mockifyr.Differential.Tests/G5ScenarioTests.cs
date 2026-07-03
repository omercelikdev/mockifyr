using Mockifyr.Differential.Generator;
using Mockifyr.Differential.Harness;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Differential validation of stateful scenarios (G5): a set of state-bound stubs is loaded into both
/// sides, then a request sequence is driven in order. Each step's response is diffed — the scenario
/// state must advance identically on both sides. Requires Docker.
/// </summary>
public sealed class G5ScenarioTests : IAsyncLifetime
{
    private readonly DifferentialRunner _runner = new();

    public Task InitializeAsync() => _runner.StartAsync();

    public async Task DisposeAsync() => await _runner.DisposeAsync();

    [Fact]
    public async Task Scenario_StateMachine()
    {
        var failures = new List<string>();

        foreach (var scenario in StateScenarios.All())
        {
            await _runner.LoadAsync(scenario.MappingsJson);

            var distinctBodies = new HashSet<string>();
            for (var step = 0; step < scenario.Steps.Count; step++)
            {
                var outcome = await _runner.ProbeAsync(scenario.Steps[step]);

                if (!outcome.DecisionAgrees)
                {
                    failures.Add($"{scenario.Description} step {step}: decision mismatch — " +
                                 $"oracle matched={outcome.OracleMatched}, mockifyr matched={outcome.MockifyrMatched}");
                }
                else if (outcome.OracleMatched && !outcome.Diff.IsMatch)
                {
                    failures.Add($"{scenario.Description} step {step}: response diff — {outcome.Diff.Report}");
                }

                if (outcome.OracleMatched)
                {
                    distinctBodies.Add(outcome.Oracle.BodyAsText);
                }
            }

            // A real state machine returns different responses as it advances; guard against a
            // degenerate scenario where every step happened to return the same body.
            if (distinctBodies.Count < 2)
            {
                failures.Add($"{scenario.Description}: expected the response to change across states, " +
                             $"saw only {distinctBodies.Count} distinct body/bodies");
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} scenario divergence(s):\n{string.Join("\n", failures)}");
    }
}
