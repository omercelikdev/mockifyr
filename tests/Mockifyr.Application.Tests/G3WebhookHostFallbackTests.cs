using System.Net;
using System.Net.Sockets;
using System.Text;
using Mediant.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Mockifyr.Core;
using Mockifyr.Server;
using Mockifyr.ServeEvents.Webhook;
using Mockifyr.Templating;

namespace Mockifyr.Application.Tests;

/// <summary>
/// Coverage for the container-localhost callback fallback (#170): a webhook aimed at loopback that is
/// <em>refused</em> while Mockifyr runs in a container is retried once against the host gateway.
/// <para>
/// The bug is environmental, so the reproduction was done against the published Docker image (see
/// docs/parity/g3-webhook.md). These tests pin the decision logic and the retry behavior, which is
/// what can regress in code: <b>when</b> a retry happens, when it must not, and that a successful
/// first attempt never triggers one. Container detection itself is a property of the runtime, so the
/// tests that need "in a container" assert conditionally on it rather than pretending.
/// </para>
/// </summary>
public sealed class G3WebhookHostFallbackTests
{
    private const string Mapping = """
    {
      "request": { "method": "POST", "urlPath": "/order" },
      "response": { "status": 200 },
      "serveEventListeners": [
        { "name": "webhook", "parameters": {
            "method": "POST",
            "url": "http://localhost:59999/hook",
            "body": "{}"
        } }
      ]
    }
    """;

    // ---- the decision logic (pure, and the part that can regress) ------------------------------

    [Fact]
    public void A_refused_connection_is_recognised_through_the_exception_chain()
    {
        // The transport wraps the SocketException, so a naive type check on the outer exception
        // would miss every real refusal.
        var wrapped = new HttpRequestException(
            "Connection refused (localhost:5999)",
            new SocketException((int)SocketError.ConnectionRefused));

        Assert.True(ContainerHostFallback.IsConnectionRefused(wrapped));
    }

    [Theory]
    [InlineData(SocketError.TimedOut)]
    [InlineData(SocketError.HostNotFound)]
    [InlineData(SocketError.NetworkUnreachable)]
    public void Other_socket_failures_do_not_count_as_refused(SocketError error)
    {
        // Load-bearing: for a timeout or a DNS failure the target address is not the thing in
        // question, so retrying against a DIFFERENT host would be guessing, not fixing.
        var wrapped = new HttpRequestException("nope", new SocketException((int)error));

        Assert.False(ContainerHostFallback.IsConnectionRefused(wrapped));
    }

    [Fact]
    public void A_non_socket_failure_does_not_count_as_refused() =>
        Assert.False(ContainerHostFallback.IsConnectionRefused(new InvalidOperationException("template blew up")));

    [Theory]
    [InlineData("http://localhost:5004/hook")]
    [InlineData("http://127.0.0.1:5004/hook")]
    [InlineData("http://LOCALHOST:5004/hook")]
    [InlineData("https://127.0.0.1/hook")]
    public void Loopback_targets_are_eligible_for_the_retry(string url)
    {
        var retry = ContainerHostFallback.RetryTargetFor(url);

        if (ContainerHostFallback.IsInContainer)
        {
            Assert.NotNull(retry);
            Assert.Contains(ContainerHostFallback.HostGateway, retry);
            // Only the host changes — scheme, port and path must survive, or the retry would go
            // somewhere the operator never named.
            var original = new Uri(url);
            var rewritten = new Uri(retry!);
            Assert.Equal(original.Scheme, rewritten.Scheme);
            Assert.Equal(original.Port, rewritten.Port);
            Assert.Equal(original.AbsolutePath, rewritten.AbsolutePath);
        }
        else
        {
            // Outside a container the fallback must never engage: localhost already means the machine.
            Assert.Null(retry);
        }
    }

    [Theory]
    [InlineData("http://api.example.com/hook")]
    [InlineData("http://192.168.1.50:8080/hook")]
    [InlineData("http://host.docker.internal:5004/hook")]
    [InlineData("not a url")]
    public void Non_loopback_targets_are_never_rewritten(string url) =>
        Assert.Null(ContainerHostFallback.RetryTargetFor(url));

    [Fact]
    public void A_failed_retry_still_explains_the_container_cause_whatever_the_retrys_own_error_was()
    {
        // Regression guard, found in a container run: when host.docker.internal does not resolve, the
        // retry fails with "Network is unreachable" — not "refused". Judging the message on the RETRY's
        // error dropped the diagnosis at the exact moment it was most needed, leaving a message that
        // reads like the target being down.
        var retryFailure = new HttpRequestException(
            "Network is unreachable (host.docker.internal:5555)",
            new SocketException((int)SocketError.NetworkUnreachable));

        var explained = ContainerHostFallback.Explain(
            "http://localhost:5555/hook", retryFailure, fallbackAttempted: true);

        if (ContainerHostFallback.IsInContainer)
        {
            Assert.Contains("container", explained);
            Assert.Contains("Network is unreachable", explained); // the ground truth is kept
        }
        else
        {
            Assert.Equal(retryFailure.Message, explained);
        }
    }

    // ---- delivery behavior ---------------------------------------------------------------------

    [Fact]
    public async Task A_successful_delivery_never_triggers_a_retry()
    {
        var attempts = new List<string>();
        var serveEvent = await ServeAsync(new StubHandler(request =>
        {
            attempts.Add(request.RequestUri!.Host);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        await WaitAsync(serveEvent, expected: 2);

        // The whole safety argument rests on this: something listening on the container's own
        // loopback is reached on the first attempt, so the fallback cannot displace it.
        Assert.Equal(["localhost"], attempts);
        Assert.Single(serveEvent.SubEvents, s => s.Type == SubEvent.WebhookRequestType);
        Assert.DoesNotContain(serveEvent.SubEvents, s => s.Type == SubEvent.ErrorType);
    }

    [Fact]
    public async Task A_timeout_is_reported_without_retrying_a_different_host()
    {
        var attempts = new List<string>();
        var serveEvent = await ServeAsync(new StubHandler(request =>
        {
            attempts.Add(request.RequestUri!.Host);
            throw new HttpRequestException("timed out", new SocketException((int)SocketError.TimedOut));
        }));

        await WaitAsync(serveEvent, expected: 2);

        Assert.Equal(["localhost"], attempts);
        var error = Assert.IsType<WebhookErrorData>(
            Assert.Single(serveEvent.SubEvents, s => s.Type == SubEvent.ErrorType).Data);
        Assert.Contains("timed out", error.Message);
        Assert.DoesNotContain(ContainerHostFallback.HostGateway, error.Message);
    }

    [Fact]
    public async Task A_refused_loopback_callback_retries_via_the_host_gateway_when_containerised()
    {
        var attempts = new List<string>();
        var serveEvent = await ServeAsync(new StubHandler(request =>
        {
            attempts.Add(request.RequestUri!.Host);
            if (request.RequestUri!.Host == ContainerHostFallback.HostGateway)
            {
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            throw new HttpRequestException("Connection refused", new SocketException((int)SocketError.ConnectionRefused));
        }));

        if (!ContainerHostFallback.IsInContainer)
        {
            // Outside a container there is nothing to fall back to; the single attempt must simply
            // report the refusal. Asserting that here keeps the test honest on a developer machine.
            await WaitAsync(serveEvent, expected: 2);
            Assert.Equal(["localhost"], attempts);
            return;
        }

        await WaitAsync(serveEvent, expected: 4);
        Assert.Equal(["localhost", ContainerHostFallback.HostGateway], attempts);
        // Both attempts are journaled, so the retry is visible rather than magic.
        Assert.Equal(2, serveEvent.SubEvents.Count(s => s.Type == SubEvent.WebhookRequestType));
        Assert.Contains(serveEvent.SubEvents, s => s.Type == SubEvent.WebhookResponseType);
    }

    [Fact]
    public async Task The_fallback_can_be_switched_off()
    {
        var attempts = new List<string>();
        var serveEvent = await ServeAsync(
            new StubHandler(request =>
            {
                attempts.Add(request.RequestUri!.Host);
                throw new HttpRequestException("Connection refused", new SocketException((int)SocketError.ConnectionRefused));
            }),
            hostFallback: false);

        await WaitAsync(serveEvent, expected: 2);

        Assert.Equal(["localhost"], attempts);
    }

    [Fact]
    public async Task A_refused_callback_explains_the_container_cause_rather_than_implying_the_target_is_down()
    {
        var serveEvent = await ServeAsync(new StubHandler(_ =>
            throw new HttpRequestException("Connection refused (localhost:59999)",
                new SocketException((int)SocketError.ConnectionRefused))));

        await WaitAsync(serveEvent, expected: ContainerHostFallback.IsInContainer ? 4 : 2);

        var messages = serveEvent.SubEvents
            .Where(s => s.Type == SubEvent.ErrorType)
            .Select(s => ((WebhookErrorData)s.Data!).Message)
            .ToList();

        Assert.NotEmpty(messages);
        // The raw transport text is kept — it is the ground truth — and the diagnosis is added to it.
        Assert.Contains(messages, m => m.Contains("Connection refused"));
        if (ContainerHostFallback.IsInContainer)
        {
            Assert.Contains(messages, m => m.Contains("container") && m.Contains(ContainerHostFallback.HostGateway));
        }
    }

    // ---- harness -------------------------------------------------------------------------------

    private static async Task<ServeEvent> ServeAsync(StubHandler handler, bool hostFallback = true)
    {
        var listener = new WebhookServeEventListener(
            new HttpClient(handler), new WebhookTemplateRenderer(), hostFallback);
        var provider = new ServiceCollection().AddMockifyr().BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // A dedicated engine wired with only the capturing listener — the container's built-in
        // webhook listener would otherwise also fire and pollute the sub-events.
        var engine = new StubEngine(
            provider.GetRequiredService<IStubStore>(),
            provider.GetRequiredService<IResponseRenderer>(),
            provider.GetRequiredService<IScenarioStateStore>(),
            provider.GetRequiredService<IRequestJournal>(),
            [listener],
            []);

        Assert.True((await sender.Send(new CreateStubCommand(Mapping, TenantId.Default))).IsSuccess);
        engine.Handle(TenantId.Default, CanonicalRequestBuilder.Build("POST", "/order", [], Encoding.UTF8.GetBytes("{}")));

        return Assert.Single(provider.GetRequiredService<IRequestJournal>().Query(TenantId.Default, new ServeEventQuery()));
    }

    // Delivery is fire-and-forget; poll until the sub-events settle. `expected` is the count that
    // proves the path finished, so a slow machine waits rather than asserting on a half-written event.
    private static async Task WaitAsync(ServeEvent serveEvent, int expected)
    {
        for (var i = 0; i < 150 && serveEvent.SubEvents.Count < expected; i++)
        {
            await Task.Delay(20);
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
