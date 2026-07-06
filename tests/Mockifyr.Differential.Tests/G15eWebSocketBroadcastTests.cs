using System.Net;
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Mockifyr.Server;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// WebSocket broadcast + admin push (G15e), extending G15d. Like all WebSocket serving it has no stable
/// WireMock oracle, so it is validated by a self-test with <em>two</em> connected clients: the admin
/// <c>POST /__admin/channels/send</c> reaches both, and a message-mapping whose <c>send</c> action has a
/// non-originating <c>channelTarget</c> broadcasts to both. See docs/parity/g15-extras.md.
/// </summary>
public sealed class G15eWebSocketBroadcastTests
{
    [Fact]
    public async Task AdminChannelsSend_ReachesAllConnectedClients()
    {
        await using var host = MockifyrHost.Build(["--port", "0", "--https-port", "0"]);
        await host.StartAsync();
        var http = HttpAddress(host);

        using var a = await ConnectAsync(http, "/ch");
        using var b = await ConnectAsync(http, "/ch");

        using (var admin = new HttpClient { BaseAddress = http })
        using (var content = new StringContent("""{"message":{"body":{"data":"broadcast-hello"}}}""", Encoding.UTF8, "application/json"))
        {
            var response = await admin.PostAsync("/__admin/channels/send", content);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        Assert.Equal("broadcast-hello", await ReceiveAsync(a));
        Assert.Equal("broadcast-hello", await ReceiveAsync(b));
    }

    [Fact]
    public async Task BroadcastChannelTarget_ReachesAllClients()
    {
        await using var host = MockifyrHost.Build(["--port", "0", "--https-port", "0"]);
        await host.StartAsync();
        var http = HttpAddress(host);

        const string mapping =
            """
            {
              "trigger": { "type": "message", "message": { "body": { "equalTo": "shout" } } },
              "actions": [
                { "type": "send", "channelTarget": { "type": "broadcast" },
                  "message": { "body": { "data": "everyone: {{message.body}}" } } }
              ]
            }
            """;
        using (var admin = new HttpClient { BaseAddress = http })
        using (var content = new StringContent(mapping, Encoding.UTF8, "application/json"))
        {
            (await admin.PostAsync("/__admin/message-mappings", content)).EnsureSuccessStatusCode();
        }

        using var a = await ConnectAsync(http, "/room");
        using var b = await ConnectAsync(http, "/room");

        await SendAsync(a, "shout");

        // The broadcast reaches every connected client (including the originator).
        Assert.Equal("everyone: shout", await ReceiveAsync(a));
        Assert.Equal("everyone: shout", await ReceiveAsync(b));
    }

    private static Uri HttpAddress(WebApplication host)
    {
        var address = host.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses
            .First(a => a.StartsWith("http://", StringComparison.Ordinal))
            .Replace("[::]", "127.0.0.1").Replace("0.0.0.0", "127.0.0.1");
        return new Uri(address);
    }

    private static async Task<ClientWebSocket> ConnectAsync(Uri http, string path)
    {
        var client = new ClientWebSocket();
        await client.ConnectAsync(new Uri($"ws://{http.Host}:{http.Port}{path}"), CancellationToken.None);
        return client;
    }

    private static Task SendAsync(ClientWebSocket client, string text) =>
        client.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);

    private static async Task<string> ReceiveAsync(ClientWebSocket client)
    {
        var buffer = new byte[4096];
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var result = await client.ReceiveAsync(buffer, cts.Token);
        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }
}
