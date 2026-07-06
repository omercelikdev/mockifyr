using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Mockifyr.Differential.Harness;

/// <summary>
/// A fixed-response upstream for the proxy (G8) differential. Both the oracle (via
/// <c>host.docker.internal</c>) and Mockifyr (via <c>127.0.0.1</c>) proxy to it; it echoes the
/// received path in the body so path/query forwarding is observable, and returns a known header, so
/// the two proxied responses can be diffed.
/// </summary>
public sealed class UpstreamServer : IDisposable
{
    private readonly HttpListener _listener = new();

    public UpstreamServer()
    {
        Port = FreeTcpPort();
        _listener.Prefixes.Add($"http://+:{Port}/");
        _listener.Start();
        _ = Task.Run(AcceptLoopAsync);
    }

    /// <summary>The ephemeral port the upstream listens on.</summary>
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

            // Drain the request body (a proxied POST forwards it) before responding.
            using (var reader = new StreamReader(context.Request.InputStream))
            {
                _ = await reader.ReadToEndAsync();
            }

            var payload = Encoding.UTF8.GetBytes($"{{\"from\":\"upstream\",\"path\":\"{context.Request.Url?.PathAndQuery}\"}}");
            context.Response.StatusCode = 200;
            context.Response.Headers["X-Upstream"] = "real-server";
            // Echo a proxy-added request header back (for validating additionalProxyRequestHeaders).
            var added = context.Request.Headers["X-Proxy-Added"];
            if (!string.IsNullOrEmpty(added))
            {
                context.Response.Headers["X-Echoed-Added"] = added;
            }
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
