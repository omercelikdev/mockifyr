using Mockifyr.Differential.Generator;
using Mockifyr.Differential.Harness;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Differential validation of proxying (G8): a <c>proxyBaseUrl</c> stub forwards the matched request
/// to a shared upstream, and the proxied response (status, body, and the upstream's marker header)
/// must match between the oracle and Mockifyr. The upstream returns a deterministic response and
/// echoes the received path, so path/query forwarding is part of the diff. Requires Docker; the
/// oracle reaches the upstream via host.docker.internal.
/// </summary>
public sealed class G8ProxyTests : IAsyncLifetime
{
    private readonly DifferentialRunner _runner = new();
    private readonly UpstreamServer _upstream = new();

    public Task InitializeAsync() => _runner.StartAsync();

    public async Task DisposeAsync()
    {
        _upstream.Dispose();
        await _runner.DisposeAsync();
    }

    [Fact]
    public async Task Proxy_ReturnsUpstreamResponse()
    {
        var failures = new List<string>();

        foreach (var scenario in ProxyScenarios.All())
        {
            var outcome = await _runner.RunProxyAsync(_upstream, scenario.StubTemplate, scenario.Request);

            // Compare status + body + the upstream's marker header (transport headers are masked).
            var diff = ResponseDiffer.Compare(outcome.Oracle, outcome.Mockifyr, ["X-Upstream"]);
            if (!diff.IsMatch)
            {
                failures.Add($"{scenario.Description}: {diff.Report}");
            }

            // Sanity: the response actually came from the upstream (a real proxy, not a stub body).
            if (!outcome.Oracle.BodyAsText.Contains("upstream"))
            {
                failures.Add($"{scenario.Description}: oracle did not proxy (body=\"{outcome.Oracle.BodyAsText}\")");
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} proxy divergence(s):\n{string.Join("\n", failures)}");
    }
}
