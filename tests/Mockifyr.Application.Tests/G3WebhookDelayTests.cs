using System.Diagnostics;
using System.Text;
using Mediant.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Mockifyr.Application;
using Mockifyr.Core;
using Mockifyr.Server;
using Mockifyr.ServeEvents.Webhook;

namespace Mockifyr.Application.Tests;

/// <summary>
/// Structural validation of the webhook <c>delay</c> (WireMock <c>postServeActions</c> →
/// <c>delay: {type:fixed, milliseconds}</c>): the adapter parses it and the listener waits that long
/// before firing. Delay timing is racy against a live oracle (documented in docs/parity/g3-webhook.md),
/// so this is a self-test — an injected handler records when the outbound call actually happens.
/// </summary>
public sealed class G3WebhookDelayTests
{
    private const int DelayMs = 300;

    [Fact]
    public async Task Webhook_WaitsTheConfiguredDelayBeforeFiring()
    {
        var sw = new Stopwatch();
        long? firedAtMs = null;
        var handler = new CapturingHandler(() => firedAtMs = sw.ElapsedMilliseconds);
        var listener = new WebhookServeEventListener(new HttpClient(handler));

        var provider = new ServiceCollection().AddMockifyr(cfg => cfg.AddServeEventListener(listener)).BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();
        var engine = provider.GetRequiredService<StubEngine>();

        await sender.Send(new CreateStubCommand(
            $$"""
            {
              "request": { "method": "GET", "url": "/wh" },
              "response": { "status": 200 },
              "postServeActions": [
                { "name": "webhook", "parameters": { "method": "POST", "url": "http://localhost:1/hook",
                  "delay": { "type": "fixed", "milliseconds": {{DelayMs}} } } }
              ]
            }
            """, TenantId.Default));

        sw.Start();
        engine.Handle(TenantId.Default, CanonicalRequestBuilder.Build("GET", "/wh", [], null));

        // The dispatch is fire-and-forget; wait for the delayed outbound call to land.
        for (var i = 0; i < 60 && firedAtMs is null; i++)
        {
            await Task.Delay(50);
        }

        Assert.NotNull(firedAtMs);
        // Allow a little scheduler slack below the nominal delay.
        Assert.True(firedAtMs >= DelayMs - 50, $"webhook fired after {firedAtMs}ms, expected >= ~{DelayMs}ms");
    }

    private sealed class CapturingHandler(Action onSend) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            onSend();
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
