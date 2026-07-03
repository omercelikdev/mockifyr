using Mockifyr.Core;

namespace Mockifyr.ServeEvents.Webhook;

/// <summary>
/// The <see cref="IServeEventListener"/> that performs WireMock's <c>webhook</c> post-serve action:
/// after a stub with webhook definitions matches, it fires each outbound HTTP call. This is the
/// engine's outbound-I/O edge — the pure core only records the <see cref="WebhookDefinition"/>; the
/// actual network call lives here (see docs/decisions/0001). Delivery is best-effort and never
/// throws back into serving. Body/URL/header templating arrives with G3b.
/// </summary>
public sealed class WebhookServeEventListener : IServeEventListener
{
    // Content headers must be set on HttpContent, not HttpRequestMessage; this is the set WireMock's
    // webhook parameters commonly carry.
    private static readonly HashSet<string> ContentHeaders =
        new(StringComparer.OrdinalIgnoreCase) { "Content-Type", "Content-Length", "Content-Encoding", "Content-Language" };

    private readonly HttpClient _client;

    public WebhookServeEventListener(HttpClient? client = null) => _client = client ?? new HttpClient();

    /// <inheritdoc />
    public async Task OnServeEventAsync(ServeEvent serveEvent, CancellationToken cancellationToken)
    {
        if (serveEvent.MatchedStub is not { Webhooks: { Count: > 0 } webhooks })
        {
            return;
        }

        foreach (var webhook in webhooks)
        {
            await SendAsync(webhook, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SendAsync(WebhookDefinition webhook, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(new HttpMethod(webhook.Method), webhook.Url);

        if (webhook.Body is { } body)
        {
            request.Content = new ByteArrayContent(body);
        }

        foreach (var (name, value) in webhook.Headers)
        {
            if (ContentHeaders.Contains(name))
            {
                // A content header needs an HttpContent to hang off of.
                request.Content ??= new ByteArrayContent([]);
                request.Content.Headers.TryAddWithoutValidation(name, value);
            }
            else
            {
                request.Headers.TryAddWithoutValidation(name, value);
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
}
