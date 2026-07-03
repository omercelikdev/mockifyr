using System.Linq;
using Mockifyr.Adapters.WireMockJson;
using Mockifyr.Core;
using Mockifyr.Differential.Generator;
using Mockifyr.Differential.Harness;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Differential validation of request verification (G6): after replaying traffic into both journals,
/// each pattern's match count and the unmatched-request count must agree. The admin JSON carries many
/// volatile fields (clientIp, loggedDate, …), so verification is compared semantically (counts), not
/// byte-for-byte. Requires Docker.
/// </summary>
public sealed class G6VerifyTests : IAsyncLifetime
{
    private readonly DifferentialRunner _runner = new();

    public Task InitializeAsync() => _runner.StartAsync();

    public async Task DisposeAsync() => await _runner.DisposeAsync();

    [Fact]
    public async Task Verify_CountAndUnmatched()
    {
        var failures = new List<string>();

        foreach (var scenario in VerifyScenarios.All())
        {
            var outcome = await _runner.RunVerifyAsync(scenario.MappingsJson, scenario.Traffic, scenario.CountPatterns);

            foreach (var (pattern, oracle, mockifyr) in outcome.Counts)
            {
                if (oracle != mockifyr)
                {
                    failures.Add($"{scenario.Description}: count for {pattern} — oracle={oracle} mockifyr={mockifyr}");
                }
            }

            if (outcome.OracleUnmatched != outcome.MockifyrUnmatched)
            {
                failures.Add($"{scenario.Description}: unmatched count — " +
                             $"oracle={outcome.OracleUnmatched} mockifyr={outcome.MockifyrUnmatched}");
            }

            // Guard against a degenerate case where every count is the same (e.g. all zero).
            if (outcome.Counts.Select(c => c.Oracle).Distinct().Count() < 2)
            {
                failures.Add($"{scenario.Description}: counts did not discriminate (all equal) — check the scenario");
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} verify divergence(s):\n{string.Join("\n", failures)}");
    }
}

/// <summary>
/// Near-miss diagnostics (G6), validated as pure logic: the closest stubs to an unmatched request are
/// ranked by ascending match distance, and an exact match has distance 0. Cross-engine near-miss
/// identity comparison is deferred (the oracle's near-miss JSON identifies stubs differently).
/// </summary>
public sealed class G6NearMissTests
{
    [Fact]
    public void NearMisses_AreRankedByAscendingDistance()
    {
        // Two stubs: one differs only by URL from the request, the other by method + URL — so the
        // first is the closer near-miss.
        const string mappings = """
            {"mappings":[
              {"request":{"method":"GET","url":"/close"},"response":{"status":200}},
              {"request":{"method":"POST","url":"/far/away"},"response":{"status":200}}
            ]}
            """;

        var server = new Mockifyr.Facade.Library.MockifyrServer();
        server.ImportWireMockJson(mappings);

        var request = CanonicalRequestBuilder.Build("GET", "/nope", [], null);
        var nearMisses = server.FindNearMisses(request);

        Assert.Equal(2, nearMisses.Count);
        // The URL-only mismatch is strictly closer than the method+URL mismatch, and ranks first.
        Assert.True(
            nearMisses[0].Distance < nearMisses[1].Distance,
            $"near-misses did not discriminate by closeness: {nearMisses[0].Distance} vs {nearMisses[1].Distance}");
    }
}
