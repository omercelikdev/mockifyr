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
    public static WebApplication UseMockifyrWebSockets(this WebApplication app)
    {
        var store = new MessageMappingStore();
        var renderer = new MessageTemplateRenderer();

        app.UseWebSockets();

        app.Use(async (context, next) =>
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                var tenant = TenantOf(context.Request);
                using var socket = await context.WebSockets.AcceptWebSocketAsync();
                await ServeAsync(socket, store, renderer, tenant, context.RequestAborted);
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
                var mapping = MessageMappingReader.Read(json, TenantOf(request));
                store.Add(mapping);
                return Results.Json(new { id = mapping.Id }, statusCode: StatusCodes.Status201Created);
            }
            catch (JsonException)
            {
                return Results.StatusCode(StatusCodes.Status422UnprocessableEntity);
            }
        });

        return app;
    }

    private static async Task ServeAsync(
        System.Net.WebSockets.WebSocket socket,
        MessageMappingStore store,
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
                if (!mapping.Matches(text))
                {
                    continue;
                }

                foreach (var template in mapping.Responses)
                {
                    var response = Encoding.UTF8.GetBytes(renderer.Render(template, text));
                    await socket.SendAsync(response, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
                }
            }
        }
    }

    private static TenantId TenantOf(HttpRequest request) =>
        request.Headers.TryGetValue(TenantHeader, out var value) && !string.IsNullOrEmpty(value)
            ? new TenantId(value!)
            : TenantId.Default;
}
