using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Mockifyr.Differential.Harness;

/// <summary>
/// A host-side HTTP server that publishes a single JSON Schema document, for the remote-<c>$ref</c>
/// differential (G1h). The oracle (via <c>host.docker.internal</c>) and Mockifyr (via <c>127.0.0.1</c>)
/// both resolve a <c>$ref</c> to it; the referenced schema requires a <c>city</c> property.
/// </summary>
public sealed class SchemaServer : IDisposable
{
    private const string Schema =
        "{\"type\":\"object\",\"required\":[\"city\"],\"properties\":{\"city\":{\"type\":\"string\"}}}";

    private readonly HttpListener _listener = new();

    public SchemaServer()
    {
        Port = FreeTcpPort();
        _listener.Prefixes.Add($"http://+:{Port}/");
        _listener.Start();
        _ = Task.Run(AcceptLoopAsync);
    }

    /// <summary>The ephemeral port the schema is served on.</summary>
    public int Port { get; }

    private async Task AcceptLoopAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception)
            {
                break;
            }

            var payload = Encoding.UTF8.GetBytes(Schema);
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.OutputStream.Write(payload);
            context.Response.Close();
        }
    }

    private static int FreeTcpPort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    public void Dispose()
    {
        try
        {
            _listener.Stop();
            _listener.Close();
        }
        catch (Exception)
        {
            // Already stopped.
        }
    }
}
