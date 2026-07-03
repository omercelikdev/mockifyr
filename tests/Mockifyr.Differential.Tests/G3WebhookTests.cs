using Mockifyr.Differential.Generator;
using Mockifyr.Differential.Harness;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Differential validation of outbound webhooks (G3a): a host-side receiver captures the webhook
/// each side fires after a stub matches, and the two deliveries are diffed (method, path, body, and
/// the declared headers — the auto-added transport headers differ per client and are ignored).
/// Requires Docker; the oracle reaches the receiver via host.docker.internal.
/// </summary>
public sealed class G3WebhookTests : IAsyncLifetime
{
    private readonly DifferentialRunner _runner = new();
    private readonly WebhookReceiver _receiver = new();

    public Task InitializeAsync() => _runner.StartAsync();

    public async Task DisposeAsync()
    {
        _receiver.Dispose();
        await _runner.DisposeAsync();
    }

    [Fact]
    public async Task Webhook_Delivery()
    {
        var failures = new List<string>();

        foreach (var scenario in WebhookScenarios.All())
        {
            var outcome = await _runner.RunWebhookAsync(_receiver, scenario.MappingTemplate, scenario.Trigger);

            if (outcome.Oracle is not { } oracle)
            {
                failures.Add($"{scenario.Description}: the ORACLE fired no webhook");
                continue;
            }

            if (outcome.Mockifyr is not { } mockifyr)
            {
                failures.Add($"{scenario.Description}: mockifyr fired no webhook");
                continue;
            }

            if (oracle.Method != mockifyr.Method)
            {
                failures.Add($"{scenario.Description}: method — oracle={oracle.Method} mockifyr={mockifyr.Method}");
            }

            if (oracle.Path != mockifyr.Path)
            {
                failures.Add($"{scenario.Description}: path — oracle={oracle.Path} mockifyr={mockifyr.Path}");
            }

            if (oracle.Body != mockifyr.Body)
            {
                failures.Add($"{scenario.Description}: body — oracle=\"{oracle.Body}\" mockifyr=\"{mockifyr.Body}\"");
            }

            foreach (var header in scenario.ComparedHeaders)
            {
                var oracleValue = oracle.Headers.GetValueOrDefault(header);
                var mockifyrValue = mockifyr.Headers.GetValueOrDefault(header);
                if (!string.Equals(oracleValue, mockifyrValue, StringComparison.Ordinal))
                {
                    failures.Add($"{scenario.Description}: header[{header}] — oracle={oracleValue ?? "<absent>"} mockifyr={mockifyrValue ?? "<absent>"}");
                }
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} webhook divergence(s):\n{string.Join("\n", failures)}");
    }
}
