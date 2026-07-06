using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Mockifyr.Server;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Self-test of WebSocket connect-time messages and <c>filePath</c> bodies (G15g), extending G15d/G15e.
/// Like all WebSocket serving these have no stable WireMock oracle (still beta), so they are validated by
/// a self-test: a <c>connection</c>-triggered mapping sends its actions unsolicited the moment a client
/// connects, and a send action whose body is <c>{ "filePath": … }</c> is served from the host's
/// <c>__files</c> directory. See docs/parity/g15-extras.md. No Docker required.
/// </summary>
public sealed class G15gWebSocketConnectFileTests
{
    [Fact]
    public async Task ConnectionTrigger_SendsUnsolicitedMessageOnConnect()
    {
        await using var host = MockifyrHost.Build(["--port", "0", "--https-port", "0"]);
        await host.StartAsync();
        var http = HttpAddress(host);

        const string mapping =
            """
            {
              "trigger": { "type": "connection" },
              "actions": [ { "type": "send", "message": { "body": { "data": "welcome-aboard" } } } ]
            }
            """;
        await RegisterAsync(http, mapping);

        // Connecting is enough — the client receives the connect-time message without sending anything.
        using var client = await ConnectAsync(http, "/ws");
        Assert.Equal("welcome-aboard", await ReceiveAsync(client));
    }

    [Fact]
    public async Task FilePathBody_IsServedFromFilesDirectory()
    {
        var root = Directory.CreateTempSubdirectory("mockifyr-ws-files-");
        Directory.CreateDirectory(Path.Combine(root.FullName, "__files"));
        File.WriteAllText(Path.Combine(root.FullName, "__files", "greeting.txt"), "hello-from-file");

        await using var host = MockifyrHost.Build(["--port", "0", "--https-port", "0", "--root-dir", root.FullName]);
        await host.StartAsync();
        var http = HttpAddress(host);

        try
        {
            const string mapping =
                """
                {
                  "trigger": { "message": { "body": { "equalTo": "ping" } } },
                  "actions": [ { "type": "send", "message": { "body": { "filePath": "greeting.txt" } } } ]
                }
                """;
            await RegisterAsync(http, mapping);

            using var client = await ConnectAsync(http, "/ws");
            await SendAsync(client, "ping");
            Assert.Equal("hello-from-file", await ReceiveAsync(client));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    private static async Task RegisterAsync(Uri http, string mapping)
    {
        using var admin = new HttpClient { BaseAddress = http };
        using var content = new StringContent(mapping, Encoding.UTF8, "application/json");
        (await admin.PostAsync("/__admin/message-mappings", content)).EnsureSuccessStatusCode();
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
