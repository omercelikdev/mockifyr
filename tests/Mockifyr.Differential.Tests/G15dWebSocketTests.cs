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
/// WebSocket message serving (G15d). WireMock's WebSocket support is beta with no stable oracle, so
/// this is validated by a <em>self-test</em> round-trip (not differentially): a real WebSocket client
/// connects to a live Mockifyr host, and messages are matched against admin-registered message-mappings
/// and answered with the templated response. See docs/parity/g15-extras.md.
/// </summary>
public sealed class G15dWebSocketTests
{
    [Fact]
    public async Task EchoStub_RoundTripsTemplatedResponse()
    {
        await using var host = MockifyrHost.Build(["--port", "0", "--https-port", "0"]);
        await host.StartAsync();

        var http = HttpAddress(host);
        const string echo =
            """
            {
              "trigger": { "type": "message", "message": { "body": { "matches": ".*" } } },
              "actions": [
                { "type": "send", "channelTarget": { "type": "originating" },
                  "message": { "body": { "data": "Echo: {{message.body}}" } } }
              ]
            }
            """;
        await RegisterAsync(http, echo);

        using var client = new ClientWebSocket();
        await client.ConnectAsync(WebSocketUri(http, "/socket"), CancellationToken.None);

        await SendAsync(client, "ping");
        Assert.Equal("Echo: ping", await ReceiveAsync(client));
        await SendAsync(client, "hello world");
        Assert.Equal("Echo: hello world", await ReceiveAsync(client));

        await client.CloseAsync(WebSocketCloseStatus.NormalClosure, statusDescription: null, CancellationToken.None);
    }

    [Fact]
    public async Task EqualToTriggers_RouteToTheirOwnResponse()
    {
        await using var host = MockifyrHost.Build(["--port", "0", "--https-port", "0"]);
        await host.StartAsync();

        var http = HttpAddress(host);
        await RegisterAsync(http, Stub("ping", "pong"));
        await RegisterAsync(http, Stub("marco", "polo"));

        using var client = new ClientWebSocket();
        await client.ConnectAsync(WebSocketUri(http, "/ws"), CancellationToken.None);

        await SendAsync(client, "ping");
        Assert.Equal("pong", await ReceiveAsync(client));
        await SendAsync(client, "marco");
        Assert.Equal("polo", await ReceiveAsync(client));

        await client.CloseAsync(WebSocketCloseStatus.NormalClosure, statusDescription: null, CancellationToken.None);
    }

    private static string Stub(string equalTo, string data) =>
        $$"""
        {
          "trigger": { "type": "message", "message": { "body": { "equalTo": "{{equalTo}}" } } },
          "actions": [ { "type": "send", "message": { "body": { "data": "{{data}}" } } } ]
        }
        """;

    private static async Task RegisterAsync(Uri http, string mappingJson)
    {
        using var admin = new HttpClient { BaseAddress = http };
        using var content = new StringContent(mappingJson, Encoding.UTF8, "application/json");
        using var response = await admin.PostAsync("/__admin/message-mappings", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static Uri HttpAddress(WebApplication host)
    {
        var address = host.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses
            .First(a => a.StartsWith("http://", StringComparison.Ordinal))
            .Replace("[::]", "127.0.0.1").Replace("0.0.0.0", "127.0.0.1");
        return new Uri(address);
    }

    private static Uri WebSocketUri(Uri http, string path) => new($"ws://{http.Host}:{http.Port}{path}");

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
