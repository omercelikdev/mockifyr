using System.Text;
using Mockifyr.Core;

namespace Mockifyr.ServeEvents.Webhook;

/// <summary>
/// The <see cref="IServeEventListener"/> that performs the <c>webhook</c> post-serve action:
/// after a stub with webhook definitions matches, it fires each outbound HTTP call. This is the
/// engine's outbound-I/O edge — the pure core only records the <see cref="WebhookDefinition"/>; the
/// actual network call lives here (see docs/decisions/0001). Delivery is best-effort and never
/// throws back into serving. When a template renderer is supplied (G3b, verified by the differential
/// suite), the URL, header values, and body are rendered against the triggering request (exposed to
/// templates as <c>originalRequest</c>).
/// Each delivery is appended to the serve event as WEBHOOK_REQUEST / WEBHOOK_RESPONSE sub-events
/// (upstream 3.x journal shape), and a failed delivery — unreachable target or a template that does
/// not render — as an ERROR sub-event, so the journal shows what was actually sent and what came back.
/// </summary>
public sealed class WebhookServeEventListener : IServeEventListener
{
    // Content headers must be set on HttpContent, not HttpRequestMessage; this is the set that
    // webhook parameters commonly carry.
    private static readonly HashSet<string> ContentHeaders =
        new(StringComparer.OrdinalIgnoreCase) { "Content-Type", "Content-Length", "Content-Encoding", "Content-Language" };

    private readonly HttpClient _client;
    private readonly IServeEventTemplateRenderer? _renderer;

    public WebhookServeEventListener(HttpClient? client = null, IServeEventTemplateRenderer? renderer = null)
    {
        _client = client ?? new HttpClient();
        _renderer = renderer;
    }

    /// <inheritdoc />
    public async Task OnServeEventAsync(ServeEvent serveEvent, CancellationToken cancellationToken)
    {
        if (serveEvent.MatchedStub is not { Webhooks: { Count: > 0 } webhooks })
        {
            return;
        }

        foreach (var webhook in webhooks)
        {
            await SendAsync(webhook, serveEvent, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SendAsync(WebhookDefinition webhook, ServeEvent serveEvent, CancellationToken cancellationToken)
    {
        if (webhook.DelayMilliseconds > 0)
        {
            try { await Task.Delay(webhook.DelayMilliseconds, cancellationToken).ConfigureAwait(false); }
            catch (TaskCanceledException) { return; }
        }

        var originalRequest = serveEvent.Request;
        try
        {
            var url = Render(webhook.Url, originalRequest);
            using var request = new HttpRequestMessage(new HttpMethod(webhook.Method), url);

            byte[]? renderedBody = null;
            if (webhook.Body is { } body)
            {
                renderedBody = RenderBody(body, originalRequest);
                request.Content = new ByteArrayContent(renderedBody);
            }

            var renderedHeaders = new List<KeyValuePair<string, string>>();
            foreach (var (name, value) in webhook.Headers)
            {
                var rendered = Render(value, originalRequest);
                renderedHeaders.Add(new(name, rendered));
                if (ContentHeaders.Contains(name))
                {
                    // A content header needs an HttpContent to hang off of.
                    request.Content ??= new ByteArrayContent([]);
                    request.Content.Headers.TryAddWithoutValidation(name, rendered);
                }
                else
                {
                    request.Headers.TryAddWithoutValidation(name, rendered);
                }
            }

            Append(serveEvent, SubEvent.WebhookRequestType,
                new WebhookRequestData(webhook.Method, url, renderedHeaders, renderedBody));

            using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            var responseHeaders = response.Headers.Concat(response.Content.Headers)
                .SelectMany(h => h.Value.Select(v => new KeyValuePair<string, string>(h.Key, v)))
                .ToList();
            Append(serveEvent, SubEvent.WebhookResponseType,
                new WebhookResponseData((int)response.StatusCode, responseHeaders, responseBody.Length > 0 ? responseBody : null));
        }
        catch (Exception exception)
        {
            // Best-effort delivery: neither an unreachable target nor a template that fails to render
            // may surface into request serving — but both must show up in the journal.
            Append(serveEvent, SubEvent.ErrorType, new WebhookErrorData(exception.Message));
        }
    }

    private static void Append(ServeEvent serveEvent, string type, object data) =>
        serveEvent.AppendSubEvent(new SubEvent(
            type,
            (DateTimeOffset.UtcNow - serveEvent.Timestamp).Ticks * 100,
            data));

    // Templates a webhook field against originalRequest; a null renderer leaves it literal (G3a).
    private string Render(string value, CanonicalRequest originalRequest) =>
        _renderer is null ? value : _renderer.Render(value, originalRequest);

    private byte[] RenderBody(byte[] body, CanonicalRequest originalRequest) =>
        _renderer is null ? body : Encoding.UTF8.GetBytes(Render(Encoding.UTF8.GetString(body), originalRequest));
}
