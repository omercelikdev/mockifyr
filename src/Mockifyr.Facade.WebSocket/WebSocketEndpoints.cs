using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Mockifyr.Core;
using Mockifyr.Templating;

namespace Mockifyr.Facade.WebSocket;

/// <summary>
/// The WebSocket message-serving facade (G15d), mirroring WireMock 4's message framework: register
/// message stubs via <c>POST /__admin/message-mappings</c>, then a WebSocket client's inbound message is
/// matched against each stub's trigger and the matching stubs' templated responses are sent back to the
/// originating channel. WebSocket serving has no stable WireMock oracle (still beta), so it is validated
/// by a self-test round-trip rather than differentially — see docs/parity/g15-extras.md.
/// </summary>
public static class WebSocketEndpoints
{
    private const string TenantHeader = "X-Mockifyr-Tenant";

    /// <summary>
    /// Adds WebSocket message serving to the app: the <c>/__admin/message-mappings</c> registration
    /// endpoint plus a front-of-pipeline middleware that accepts WebSocket upgrades on any path and
    /// serves matched, templated responses. Call this early so upgrades are intercepted before the
    /// mock-serving fallback.
    /// </summary>
    public static WebApplication UseMockifyrWebSockets(this WebApplication app, string? filesDirectory = null)
    {
        var store = new MessageMappingStore();
        var registry = new WebSocketChannelRegistry();
        var renderer = new MessageTemplateRenderer();

        app.UseWebSockets();

        app.Use(async (context, next) =>
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                var tenant = TenantOf(context.Request);
                using var socket = await context.WebSockets.AcceptWebSocketAsync();
                var channelId = registry.Add(socket, tenant);
                try
                {
                    // Connect-time mappings (G15g) fire once, unsolicited, before the receive loop.
                    await SendConnectMessagesAsync(socket, store, registry, renderer, tenant, context.RequestAborted);
                    await ServeAsync(socket, store, registry, renderer, tenant, context.RequestAborted);
                }
                finally
                {
                    registry.Remove(channelId);
                }

                return;
            }

            await next(context);
        });

        app.MapPost("/__admin/message-mappings", async (HttpRequest request) =>
        {
            using var reader = new StreamReader(request.Body);
            var json = await reader.ReadToEndAsync();
            try
            {
                var mapping = MessageMappingReader.Read(json, TenantOf(request), filesDirectory);
                store.Add(mapping);
                return Results.Json(new { id = mapping.Id }, statusCode: StatusCodes.Status201Created);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                // JsonException = malformed JSON; InvalidOperationException = a well-formed but wrong-typed
                // field (e.g. a string where the send body object is expected). Both are client input errors.
                return Results.StatusCode(StatusCodes.Status422UnprocessableEntity);
            }
        });

        // Admin push (WireMock 4's POST /__admin/channels/send): dispatch a message to connected clients.
        app.MapPost("/__admin/channels/send", async (HttpRequest request) =>
        {
            using var reader = new StreamReader(request.Body);
            var json = await reader.ReadToEndAsync();
            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("body", out var body) &&
                    body.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.String)
                {
                    await registry.BroadcastAsync(TenantOf(request), data.GetString()!, CancellationToken.None);
                    return Results.Ok();
                }

                return Results.StatusCode(StatusCodes.Status422UnprocessableEntity);
            }
            catch (JsonException)
            {
                return Results.StatusCode(StatusCodes.Status422UnprocessableEntity);
            }
        });

        return app;
    }

    // Connect-time serving (G15g): when a client connects, every connection-triggered mapping's actions
    // are sent once, unsolicited. There is no inbound message, so templates render against an empty body.
    private static async Task SendConnectMessagesAsync(
        System.Net.WebSockets.WebSocket socket,
        MessageMappingStore store,
        WebSocketChannelRegistry registry,
        MessageTemplateRenderer renderer,
        TenantId tenant,
        CancellationToken cancellationToken)
    {
        foreach (var mapping in store.For(tenant))
        {
            if (!mapping.OnConnect)
            {
                continue;
            }

            foreach (var action in mapping.Responses)
            {
                var rendered = renderer.Render(action.Data, string.Empty);
                if (action.Broadcast)
                {
                    await registry.BroadcastAsync(tenant, rendered, cancellationToken);
                }
                else
                {
                    await socket.SendAsync(
                        Encoding.UTF8.GetBytes(rendered), WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
                }
            }
        }
    }

    private static async Task ServeAsync(
        System.Net.WebSockets.WebSocket socket,
        MessageMappingStore store,
        WebSocketChannelRegistry registry,
        MessageTemplateRenderer renderer,
        TenantId tenant,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        while (socket.State == WebSocketState.Open)
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, statusDescription: null, cancellationToken);
                    return;
                }

                message.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            var text = Encoding.UTF8.GetString(message.ToArray());
            foreach (var mapping in store.For(tenant))
            {
                // Connect-time mappings fire only on connect (SendConnectMessagesAsync), never per message.
                if (mapping.OnConnect || !mapping.Matches(text))
                {
                    continue;
                }

                foreach (var action in mapping.Responses)
                {
                    var rendered = renderer.Render(action.Data, text);
                    if (action.Broadcast)
                    {
                        await registry.BroadcastAsync(tenant, rendered, cancellationToken);
                    }
                    else
                    {
                        await socket.SendAsync(
                            Encoding.UTF8.GetBytes(rendered), WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
                    }
                }
            }
        }
    }

    private static TenantId TenantOf(HttpRequest request) =>
        request.Headers.TryGetValue(TenantHeader, out var value) && !string.IsNullOrEmpty(value)
            ? new TenantId(value!)
            : TenantId.Default;
}
