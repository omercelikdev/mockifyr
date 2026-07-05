using System.Globalization;
using System.Text.RegularExpressions;
using Mockifyr.Differential.Generator;
using Mockifyr.Differential.Harness;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Validation of the <c>now</c> templating helper (backfill). Its output is clock-dependent, so it
/// can't be byte-diffed against the oracle — instead it is validated <em>structurally</em> (the same
/// method the random helpers use, G2e): the <b>oracle's</b> output must satisfy a contract — correct
/// format and a value inside the request's time window — which proves the contract is real WireMock
/// behavior, and <b>Mockifyr's</b> output must satisfy the same contract. Both being independently
/// valid under one oracle-derived contract is the parity claim. Requires Docker.
/// </summary>
public sealed class G2iNowHelperTests : IAsyncLifetime
{
    // Generous window to absorb container-clock skew and load latency while still proving "roughly now".
    private static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(15);

    private static readonly Regex Iso = new(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$", RegexOptions.Compiled);
    private static readonly Regex Day = new(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled);
    private static readonly Regex EpochMillis = new(@"^\d{13}$", RegexOptions.Compiled);

    private readonly DifferentialRunner _runner = new();

    public Task InitializeAsync() => _runner.StartAsync();

    public async Task DisposeAsync() => await _runner.DisposeAsync();

    private sealed record NowCase(string Description, string Template, Func<string, DateTimeOffset, DateTimeOffset, bool> IsValid);

    private static IEnumerable<NowCase> Cases()
    {
        // Default ISO instant, ~now.
        yield return new NowCase("iso-default", "{{now}}", (body, lo, hi) => IsoWithin(body, lo, hi));
        // epoch millis, ~now.
        yield return new NowCase("epoch", "{{now format='epoch'}}", (body, lo, hi) => EpochWithin(body, lo, hi));
        // ISO instant ~10 years ahead (offset= applied deterministically).
        yield return new NowCase("offset-10-years", "{{now offset='10 years'}}", (body, lo, hi) => IsoWithin(body, lo.AddYears(10), hi.AddYears(10)));
        // Day-granularity format — today's date.
        yield return new NowCase("day-format", "{{now format='yyyy-MM-dd'}}", (body, lo, hi) => DayWithin(body, lo, hi));
    }

    [Fact]
    public async Task NowHelper_StructurallyMatchesTheOracle()
    {
        var failures = new List<string>();

        foreach (var scenario in Cases())
        {
            var json = "{\"request\":{\"method\":\"GET\",\"urlPath\":\"/now\"}," +
                       "\"response\":{\"status\":200,\"transformers\":[\"response-template\"],\"body\":\"" + scenario.Template + "\"}}";

            var before = DateTimeOffset.UtcNow;
            var outcome = await _runner.RunAsync(json, new RequestSpec { Method = "GET", Url = "/now" });
            var after = DateTimeOffset.UtcNow;
            var lo = before - Tolerance;
            var hi = after + Tolerance;

            // The oracle output must satisfy the contract — this proves the contract is real WireMock behavior.
            if (!scenario.IsValid(outcome.Oracle.BodyAsText, lo, hi))
            {
                failures.Add($"{scenario.Description}: ORACLE body violates the contract: \"{outcome.Oracle.BodyAsText}\"");
            }

            // ...and Mockifyr's output must satisfy the same contract.
            if (!scenario.IsValid(outcome.Mockifyr.BodyAsText, lo, hi))
            {
                failures.Add($"{scenario.Description}: mockifyr body violates the contract: \"{outcome.Mockifyr.BodyAsText}\"");
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} now-helper divergence(s):\n{string.Join("\n", failures)}");
    }

    private static bool IsoWithin(string body, DateTimeOffset lo, DateTimeOffset hi) =>
        Iso.IsMatch(body) &&
        DateTimeOffset.TryParse(body, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var instant) &&
        instant >= lo && instant <= hi;

    private static bool EpochWithin(string body, DateTimeOffset lo, DateTimeOffset hi) =>
        EpochMillis.IsMatch(body) &&
        long.TryParse(body, out var millis) &&
        millis >= lo.ToUnixTimeMilliseconds() && millis <= hi.ToUnixTimeMilliseconds();

    private static bool DayWithin(string body, DateTimeOffset lo, DateTimeOffset hi) =>
        Day.IsMatch(body) &&
        DateOnly.TryParseExact(body, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) &&
        date >= DateOnly.FromDateTime(lo.UtcDateTime) && date <= DateOnly.FromDateTime(hi.UtcDateTime);
}
