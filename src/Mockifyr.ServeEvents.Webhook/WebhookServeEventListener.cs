using System.Text;
using Mockifyr.Core;

namespace Mockifyr.ServeEvents.Webhook;

/// <summary>
/// The <see cref="IServeEventListener"/> that performs WireMock's <c>webhook</c> post-serve action:
/// after a stub with webhook definitions matches, it fires each outbound HTTP call. This is the
/// engine's outbound-I/O edge — the pure core only records the <see cref="WebhookDefinition"/>; the
/// actual network call lives here (see docs/decisions/0001). Delivery is best-effort and never
/// throws back into serving. When a template renderer is supplied (G3b), the URL, header values, and
/// body are rendered against the triggering request (WireMock's <c>originalRequest</c>).
/// </summary>
public sealed class WebhookServeEventListener : IServeEventListener
{
    // Content headers must be set on HttpContent, not HttpRequestMessage; this is the set WireMock's
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
            await SendAsync(webhook, serveEvent.Request, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SendAsync(WebhookDefinition webhook, CanonicalRequest originalRequest, CancellationToken cancellationToken)
    {
        var url = Render(webhook.Url, originalRequest);
        using var request = new HttpRequestMessage(new HttpMethod(webhook.Method), url);

        if (webhook.Body is { } body)
        {
            request.Content = new ByteArrayContent(RenderBody(body, originalRequest));
        }

        foreach (var (name, value) in webhook.Headers)
        {
            var rendered = Render(value, originalRequest);
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

        try
        {
            using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            // Best-effort delivery: an unreachable target must not surface into request serving.
        }
    }

    // Templates a webhook field against originalRequest; a null renderer leaves it literal (G3a).
    private string Render(string value, CanonicalRequest originalRequest) =>
        _renderer is null ? value : _renderer.Render(value, originalRequest);

    private byte[] RenderBody(byte[] body, CanonicalRequest originalRequest) =>
        _renderer is null ? body : Encoding.UTF8.GetBytes(Render(Encoding.UTF8.GetString(body), originalRequest));
}
