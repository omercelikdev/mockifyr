using System.Net.Security;
using System.Text.Json;
using Mediant.Results;
using Mockifyr.Application;

namespace Mockifyr.Server;

/// <summary>
/// The mutable, host-level store of outbound certificate trust (#174) — the runtime counterpart of the
/// startup-only <see cref="OutboundTlsPolicy"/>.
/// <para>
/// Two modes, mirroring Git sync (ADR 0007): a host started with <c>--trust-proxy-target</c> or
/// <c>--trust-all-proxy-targets</c> is <b>pinned</b> and read-only here; a host started without them
/// is managed from the dashboard. Unlike Git sync — which gets restart survival free from
/// <c>.git/config</c> — trusted hosts have no natural store, so they are written to a JSON file
/// beside the mappings. A host with no root directory has nowhere to write and says so via
/// <see cref="OutboundTrustStatus.Persistent"/> rather than pretending to be durable.
/// </para>
/// <para>
/// The point of the store being mutable is that the TLS validation callback reads it <em>per
/// handshake</em>: trusting a host takes effect on the next outbound call, with no restart and no
/// client rebuild. Removing trust likewise applies to new connections — an already-established
/// pooled connection is not torn down.
/// </para>
/// </summary>
public sealed class OutboundTrustStore : IOutboundTrust
{
    private const string FileName = "outbound-trust.json";

    private readonly Lock _gate = new();
    private readonly HashSet<string> _hosts = new(StringComparer.OrdinalIgnoreCase);
    private readonly string? _file;

    /// <summary>Whether verification is disabled for every target (flag-only).</summary>
    public bool TrustAll { get; }

    /// <summary>Whether a startup flag fixed the configuration, making it read-only here.</summary>
    public bool Pinned { get; }

    /// <param name="policy">The startup policy; a non-default one pins the configuration.</param>
    /// <param name="stateDirectory">Where to persist; null keeps trust in memory only.</param>
    public OutboundTrustStore(OutboundTlsPolicy policy, string? stateDirectory)
    {
        TrustAll = policy.TrustAll;
        Pinned = !policy.IsDefault;
        _file = stateDirectory is { Length: > 0 } ? Path.Combine(stateDirectory, FileName) : null;

        foreach (var host in policy.TrustedHosts)
        {
            _hosts.Add(host);
        }

        // A pinned host ignores whatever was stored: the flag is the whole configuration, so a
        // leftover file must not quietly widen it.
        if (!Pinned)
        {
            foreach (var host in Load())
            {
                _hosts.Add(host);
            }
        }
    }

    /// <inheritdoc />
    public OutboundTrustStatus Status()
    {
        lock (_gate)
        {
            return new OutboundTrustStatus(
                [.. _hosts.Order(StringComparer.Ordinal)], TrustAll, Pinned, Persistent: _file is not null);
        }
    }

    /// <inheritdoc />
    public Result<OutboundTrustStatus> Trust(string host)
    {
        if (Pinned)
        {
            return Error.Conflict(
                "Trust.FlagPinned",
                "Outbound trust is pinned by --trust-proxy-target/--trust-all-proxy-targets at startup and is read-only here.");
        }

        var normalized = (host ?? string.Empty).Trim();
        if (!IsValidHost(normalized))
        {
            return Error.Validation(
                "Trust.InvalidHost",
                "Expected a host name or IP address, without a scheme, port or path.");
        }

        lock (_gate)
        {
            _hosts.Add(normalized);
            Persist();
            return Status();
        }
    }

    /// <inheritdoc />
    public Result<OutboundTrustStatus> Distrust(string host)
    {
        if (Pinned)
        {
            return Error.Conflict(
                "Trust.FlagPinned",
                "Outbound trust is pinned by --trust-proxy-target/--trust-all-proxy-targets at startup and is read-only here.");
        }

        lock (_gate)
        {
            if (!_hosts.Remove((host ?? string.Empty).Trim()))
            {
                return Error.NotFound("Trust.UnknownHost", $"'{host}' is not a trusted host.");
            }

            Persist();
            return Status();
        }
    }

    /// <summary>
    /// True when a certificate that failed validation should still be accepted for
    /// <paramref name="host"/>. A clean certificate never reaches this.
    /// </summary>
    public bool Accepts(string? host)
    {
        if (TrustAll)
        {
            return true;
        }

        lock (_gate)
        {
            return host is not null && _hosts.Contains(host);
        }
    }

    /// <summary>
    /// An <see cref="HttpClient"/> whose validation consults this store on every handshake, so a host
    /// trusted from the dashboard applies to the very next call. Built once; the mutability lives in
    /// the store, not in the client.
    /// </summary>
    public HttpClient CreateClient() =>
        new(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (request, _, _, errors) =>
                // The host comes from the request URI, not the certificate: trusting "dev.corp" must
                // mean "the endpoint I addressed as dev.corp", never "anything presenting that name".
                errors == SslPolicyErrors.None || Accepts(request.RequestUri?.Host),
        });

    // A host name or IP, nothing else — a value carrying a scheme or port would never match the URI
    // host at validation time and would sit in the list looking effective.
    private static bool IsValidHost(string host) =>
        host.Length > 0 && Uri.CheckHostName(host) != UriHostNameType.Unknown;

    private sealed record StoredTrust(IReadOnlyList<string> Hosts);

    private IReadOnlyList<string> Load()
    {
        if (_file is null || !File.Exists(_file))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<StoredTrust>(File.ReadAllText(_file))?.Hosts ?? [];
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // An unreadable file must not stop the host from starting; it starts with nothing trusted,
            // which is the safe direction to fail in.
            return [];
        }
    }

    // Called under _gate.
    private void Persist()
    {
        if (_file is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(new StoredTrust([.. _hosts.Order(StringComparer.Ordinal)])));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The in-memory change still stands; it just will not survive a restart.
        }
    }
}
