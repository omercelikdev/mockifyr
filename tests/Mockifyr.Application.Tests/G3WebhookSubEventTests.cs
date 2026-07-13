using System.Text;
using Mediant.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Mockifyr.Application;
using Mockifyr.Core;
using Mockifyr.Server;
using Mockifyr.ServeEvents.Webhook;
using Mockifyr.Templating;

namespace Mockifyr.Application.Tests;

/// <summary>
/// Structural validation of webhook delivery recording: the listener appends WEBHOOK_REQUEST /
/// WEBHOOK_RESPONSE sub-events (upstream 3.x journal shape) carrying the <em>rendered</em> outbound
/// request and the target's actual response, and an ERROR sub-event when delivery fails. Timing of
/// the fire-and-forget dispatch is racy against a live oracle (documented in
/// docs/parity/g3-webhook.md), so this is a self-test with an injected handler.
/// </summary>
public sealed class G3WebhookSubEventTests
{
    private const string Mapping = """
    {
      "request": { "method": "POST", "urlPath": "/order" },
      "response": { "status": 200 },
      "serveEventListeners": [
        { "name": "webhook", "parameters": {
            "method": "POST",
            "url": "http://callback.test/hook",
            "headers": { "Content-Type": "application/json" },
            "body": "{ \"conversationId\": \"{{jsonPath originalRequest.body '$.Header.conversationID'}}\" }"
        } }
      ]
    }
    """;

    [Fact]
    public async Task Webhook_RecordsRenderedRequestAndResponseAsSubEvents()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.Accepted)
        {
            Content = new StringContent("""{"ack":true}""", Encoding.UTF8, "application/json"),
        });
        var serveEvent = await ServeAsync(handler);

        var subEvents = await WaitForSubEventsAsync(serveEvent, count: 2);
        var request = Assert.IsType<WebhookRequestData>(Assert.Single(subEvents, s => s.Type == SubEvent.WebhookRequestType).Data);
        Assert.Equal("http://callback.test/hook", request.Url);
        Assert.NotNull(request.Body);
        var renderedBody = Encoding.UTF8.GetString(request.Body!);
        Assert.Contains("conv-123", renderedBody); // templated against originalRequest, not the raw {{…}}
        Assert.DoesNotContain("{{", renderedBody);

        var response = Assert.IsType<WebhookResponseData>(Assert.Single(subEvents, s => s.Type == SubEvent.WebhookResponseType).Data);
        Assert.Equal(202, response.Status);
        Assert.Contains("\"ack\":true", Encoding.UTF8.GetString(response.Body!));
    }

    [Fact]
    public async Task Webhook_RecordsAnErrorSubEventWhenDeliveryFails()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("connection refused"));
        var serveEvent = await ServeAsync(handler);

        var subEvents = await WaitForSubEventsAsync(serveEvent, count: 2);
        Assert.Single(subEvents, s => s.Type == SubEvent.WebhookRequestType);
        var error = Assert.IsType<WebhookErrorData>(Assert.Single(subEvents, s => s.Type == SubEvent.ErrorType).Data);
        Assert.Contains("connection refused", error.Message);
    }

    private static async Task<ServeEvent> ServeAsync(StubHandler handler)
    {
        var listener = new WebhookServeEventListener(new HttpClient(handler), new WebhookTemplateRenderer());
        var provider = new ServiceCollection().AddMockifyr().BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // A dedicated engine over the same stores, wired with only the capturing listener — the
        // container's built-in webhook listener would otherwise also fire and pollute the sub-events.
        var engine = new StubEngine(
            provider.GetRequiredService<IStubStore>(),
            provider.GetRequiredService<IResponseRenderer>(),
            provider.GetRequiredService<IScenarioStateStore>(),
            provider.GetRequiredService<IRequestJournal>(),
            [listener],
            []);

        var created = await sender.Send(new CreateStubCommand(Mapping, TenantId.Default));
        Assert.True(created.IsSuccess);

        var body = Encoding.UTF8.GetBytes("""{ "Header": { "conversationID": "conv-123" } }""");
        engine.Handle(TenantId.Default, CanonicalRequestBuilder.Build("POST", "/order", [], body));

        var journal = provider.GetRequiredService<IRequestJournal>();
        return Assert.Single(journal.Query(TenantId.Default, new ServeEventQuery()));
    }

    // The dispatch is fire-and-forget; poll the journaled event until the sub-events land.
    private static async Task<IReadOnlyList<SubEvent>> WaitForSubEventsAsync(ServeEvent serveEvent, int count)
    {
        for (var i = 0; i < 100 && serveEvent.SubEvents.Count < count; i++)
        {
            await Task.Delay(20);
        }

        var subEvents = serveEvent.SubEvents;
        Assert.Equal(count, subEvents.Count);
        return subEvents;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
