using System.Text;
using Mockifyr.Differential.Generator;
using Mockifyr.Differential.Harness;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Differential validation of record &amp; playback (G9): Mockifyr proxies requests to an upstream,
/// captures the responses, and generates stubs. Those generated stubs are loaded into <em>both</em>
/// the real oracle and a fresh Mockifyr and the requests replayed — so a stub Mockifyr recorded is
/// proven WireMock-valid and to replay the captured response on the real WireMock. Comparison is
/// semantic (status + body + stable headers); volatile transport headers are masked. Requires Docker.
/// </summary>
public sealed class G9RecordPlaybackTests : IAsyncLifetime
{
    private static readonly string[] StableHeaders = ["X-Upstream", "Content-Type"];

    private readonly DifferentialRunner _runner = new();
    private readonly UpstreamServer _upstream = new();

    public Task InitializeAsync() => _runner.StartAsync();

    public async Task DisposeAsync()
    {
        _upstream.Dispose();
        await _runner.DisposeAsync();
    }

    [Fact]
    public async Task Recorded_Stubs_ReplayTheCapturedResponse()
    {
        var requests = new List<RequestSpec>
        {
            new() { Method = "GET", Url = "/api/users/7?full=true" },
            new() { Method = "POST", Url = "/api/orders", Body = Encoding.UTF8.GetBytes("{\"item\":\"book\"}") },
            new() { Method = "GET", Url = "/health" },
        };

        var outcome = await _runner.RunRecordPlaybackAsync(_upstream, requests);

        var failures = new List<string>();
        for (var i = 0; i < requests.Count; i++)
        {
            var captured = outcome.Captured[i];
            var oracle = outcome.OracleReplay[i];
            var mockifyr = outcome.MockifyrReplay[i];

            // The real WireMock replays Mockifyr's generated stub to the captured response...
            var oracleDiff = ResponseDiffer.Compare(captured, oracle, StableHeaders);
            if (!oracleDiff.IsMatch)
            {
                failures.Add($"{requests[i].Method} {requests[i].Url}: oracle replay != captured — {oracleDiff.Report}");
            }

            // ...and Mockifyr replays it identically.
            var mockifyrDiff = ResponseDiffer.Compare(oracle, mockifyr, StableHeaders);
            if (!mockifyrDiff.IsMatch)
            {
                failures.Add($"{requests[i].Method} {requests[i].Url}: mockifyr replay != oracle — {mockifyrDiff.Report}");
            }

            // Sanity: the captured response really came from the upstream.
            if (!captured.BodyAsText.Contains("upstream"))
            {
                failures.Add($"{requests[i].Method} {requests[i].Url}: capture did not reach the upstream (\"{captured.BodyAsText}\")");
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} record/playback divergence(s):\n{string.Join("\n", failures)}");
    }
}
