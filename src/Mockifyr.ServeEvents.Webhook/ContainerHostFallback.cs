using System.Net.Sockets;

namespace Mockifyr.ServeEvents.Webhook;

/// <summary>
/// Resolves the "callback to localhost fails inside a container" trap (#170).
/// <para>
/// When Mockifyr runs in a container, <c>localhost</c> means <em>the container</em>, not the machine
/// the operator is sitting at. A webhook aimed at <c>http://localhost:5004</c> therefore fails with
/// <c>Connection refused</c> even though the identical request succeeds from Postman on the host —
/// the two are simply resolving different addresses. Reproduced against the published image: the
/// target process never receives the request.
/// </para>
/// <para>
/// The remedy is a <b>fallback, not a rewrite</b>. The URL is attempted exactly as written; only when
/// the connection is <em>refused</em> — the precise signature of "nothing is listening here" — is the
/// host gateway tried. That ordering is what makes it regression-free: if something really is
/// listening on the container's own loopback, the first attempt succeeds and the fallback never runs.
/// The fallback can only turn a hard failure into a success, never the reverse.
/// </para>
/// </summary>
internal static class ContainerHostFallback
{
    /// <summary>The Docker/Podman Desktop DNS alias for the host machine.</summary>
    internal const string HostGateway = "host.docker.internal";

    private static readonly Lazy<bool> InContainer = new(DetectContainer);

    /// <summary>
    /// True when this process is running in a container. Three independent signals, because none is
    /// universal: the .NET base images export the environment variable, Docker writes
    /// <c>/.dockerenv</c>, and Podman writes <c>/run/.containerenv</c>.
    /// </summary>
    public static bool IsInContainer => InContainer.Value;

    private static bool DetectContainer()
    {
        try
        {
            return string.Equals(
                       Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
                       "true",
                       StringComparison.OrdinalIgnoreCase)
                   || File.Exists("/.dockerenv")
                   || File.Exists("/run/.containerenv");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A probe that cannot read the filesystem must not take down webhook delivery.
            return false;
        }
    }

    /// <summary>
    /// The address to retry <paramref name="url"/> at, or <c>null</c> when the fallback does not
    /// apply — not in a container, not a loopback target, or the URL is not one we can rewrite.
    /// </summary>
    public static string? RetryTargetFor(string url)
    {
        if (!IsInContainer || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || !IsLoopback(uri.Host))
        {
            return null;
        }

        return new UriBuilder(uri) { Host = HostGateway }.Uri.ToString();
    }

    /// <summary>
    /// True for the names that mean "this machine". <see cref="Uri.IsLoopback"/> covers the numeric
    /// forms; <c>localhost</c> is matched by name because that is what operators actually type.
    /// </summary>
    private static bool IsLoopback(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
        || (Uri.TryCreate($"http://{host}", UriKind.Absolute, out var probe) && probe.IsLoopback);

    /// <summary>
    /// True when <paramref name="exception"/> is specifically "nothing accepted the connection".
    /// Deliberately narrow: a timeout, a DNS failure or a TLS error must NOT trigger a retry against
    /// a different host, because for those the target address is not the thing in question.
    /// </summary>
    public static bool IsConnectionRefused(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SocketException { SocketErrorCode: SocketError.ConnectionRefused })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Turns a raw transport error into one that names the actual cause, so a container/networking
    /// problem is not mistaken for "the target server is down" (an explicit ask in #170).
    /// </summary>
    /// <param name="originalUrl">The address as the operator wrote it.</param>
    /// <param name="failure">The error to report — the first attempt's, or the retry's.</param>
    /// <param name="fallbackAttempted">
    /// Whether the host-gateway retry already ran. When it did, <paramref name="failure"/> is the
    /// <em>retry's</em> error, whose own kind is irrelevant: what earns the explanation is that the
    /// original loopback attempt was refused. Re-testing the retry's error here would drop the
    /// diagnosis exactly when the operator most needs it — e.g. an unresolvable host.docker.internal
    /// surfaces as "Network is unreachable", which reads like the target being down.
    /// </param>
    public static string Explain(string originalUrl, Exception failure, bool fallbackAttempted)
    {
        var message = Describe(failure);
        if (!IsInContainer || RetryTargetFor(originalUrl) is null)
        {
            return message;
        }

        if (fallbackAttempted)
        {
            return $"{message} Mockifyr is running in a container, so '{Host(originalUrl)}' is the " +
                   $"container itself, and the retry via {HostGateway} failed too — on Linux that name " +
                   "only exists when the container is started with " +
                   "--add-host=host.docker.internal:host-gateway. Otherwise the target really is down.";
        }

        return IsConnectionRefused(failure)
            ? $"{message} Mockifyr is running in a container, so '{Host(originalUrl)}' is the container " +
              $"itself, not your machine. Target {HostGateway} (or the host's address) to reach a " +
              "service running outside the container."
            : message;
    }

    /// <summary>
    /// Flattens an exception chain into one line (#172). Transport failures put the reason in an
    /// <em>inner</em> exception: a TLS failure's outer message is the self-defeating "The SSL
    /// connection could not be established, see inner exception", while the inner one names the
    /// actual cause ("RemoteCertificateChainErrors", "RemoteCertificateNameMismatch", an expiry, a
    /// protocol mismatch). Journalling only the outer message discarded precisely the part an
    /// operator needs.
    /// </summary>
    public static string Describe(Exception exception)
    {
        var messages = new List<string>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            // Chains often restate the same text; keep the first of each so the line stays readable.
            if (!messages.Contains(current.Message, StringComparer.Ordinal))
            {
                messages.Add(current.Message);
            }
        }

        return string.Join(" -> ", messages);
    }

    private static string Host(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : url;
}

/// <summary>
/// Host-level webhook settings. Resolved optionally by the listener registration, so a host that
/// never registers one keeps the defaults — and so the flag can be applied without re-registering
/// <see cref="IServeEventListener"/>, which would add a second listener and deliver every webhook twice.
/// </summary>
/// <param name="HostFallback">
/// Whether a refused loopback callback is retried via the host gateway while containerised (#170).
/// </param>
public sealed record WebhookOptions(bool HostFallback = true);
