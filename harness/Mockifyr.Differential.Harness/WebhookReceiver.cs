using System.Net;
using System.Net.Sockets;

namespace Mockifyr.Differential.Harness;

/// <summary>A single webhook delivery captured by the receiver.</summary>
public sealed record CapturedWebhook(
    string Method,
    string Path,
    IReadOnlyDictionary<string, string> Headers,
    string Body);

/// <summary>
/// An in-process HTTP endpoint that records the webhook deliveries both sides fire, so the harness
/// can diff them. It binds to all interfaces on an ephemeral port: the oracle container reaches it
/// via <c>host.docker.internal</c>, Mockifyr (in-process) via <c>127.0.0.1</c>.
/// </summary>
public sealed class WebhookReceiver : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly List<CapturedWebhook> _captured = [];
    private readonly Lock _gate = new();

    public WebhookReceiver()
    {
        Port = FreeTcpPort();
        _listener.Prefixes.Add($"http://+:{Port}/");
        _listener.Start();
        _ = Task.Run(AcceptLoopAsync);
    }

    /// <summary>The ephemeral port the receiver listens on.</summary>
    public int Port { get; }

    /// <summary>Discards any captured deliveries (call before each side fires).</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _captured.Clear();
        }
    }

    /// <summary>Waits until one delivery is captured, or returns null after the timeout.</summary>
    public async Task<CapturedWebhook?> WaitForOneAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            lock (_gate)
            {
                if (_captured.Count > 0)
                {
                    return _captured[0];
                }
            }

            await Task.Delay(25);
        }

        return null;
    }

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
                break; // Listener stopped/disposed.
            }

            var request = context.Request;
            string body;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                body = await reader.ReadToEndAsync();
            }

            var headers = request.Headers.AllKeys
                .Where(k => k is not null)
                .ToDictionary(k => k!, k => request.Headers[k] ?? string.Empty, StringComparer.OrdinalIgnoreCase);

            lock (_gate)
            {
                _captured.Add(new CapturedWebhook(request.HttpMethod, request.Url?.AbsolutePath ?? "/", headers, body));
            }

            context.Response.StatusCode = 200;
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
