using System.Net.Security;
using Microsoft.Extensions.Configuration;

namespace Mockifyr.Server;

/// <summary>
/// Certificate trust for Mockifyr's <em>outbound</em> calls — callbacks and proxying (#172).
/// <para>
/// Mockifyr's existing <c>--https-*</c> flags configure the listener (traffic coming in). Nothing
/// configured the client side, so an endpoint served by an internal corporate CA — the common shape
/// of a dev cluster — was unreachable: the host machine trusts that CA via its keychain, a Linux
/// container does not, which is exactly why the same call succeeds from Postman and fails here.
/// </para>
/// <para>
/// The surface is WireMock's, not an invention: <c>--trust-proxy-target &lt;host&gt;</c> (repeatable)
/// and <c>--trust-all-proxy-targets</c>. WireMock scopes them to proxying; Mockifyr applies them to
/// both outbound paths, since a callback hits the identical wall. Verification is unchanged unless a
/// flag is passed, and per-host trust exists so reaching one internal endpoint does not mean
/// accepting every certificate everywhere.
/// </para>
/// </summary>
public sealed record OutboundTlsPolicy(bool TrustAll, IReadOnlySet<string> TrustedHosts)
{
    /// <summary>The default: every certificate is verified normally.</summary>
    public static readonly OutboundTlsPolicy Default =
        new(TrustAll: false, TrustedHosts: new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    /// <summary>True when nothing is trusted explicitly, so the stock handler will do.</summary>
    public bool IsDefault => !TrustAll && TrustedHosts.Count == 0;

    /// <summary>
    /// Reads the policy from configuration. <paramref name="args"/> is scanned as well as the parsed
    /// configuration because a repeated <c>--trust-proxy-target</c> collapses to a single value in
    /// .NET's command-line provider — the later occurrence overwrites the earlier. WireMock's flag is
    /// repeatable, so both that form and a comma-separated list are accepted.
    /// </summary>
    public static OutboundTlsPolicy From(IConfiguration configuration, string[] args)
    {
        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var host in Split(configuration["trust-proxy-target"]))
        {
            hosts.Add(host);
        }

        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--trust-proxy-target" or "/trust-proxy-target")
            {
                foreach (var host in Split(args[i + 1]))
                {
                    hosts.Add(host);
                }
            }
        }

        return new OutboundTlsPolicy(configuration.GetValue("trust-all-proxy-targets", false), hosts);
    }

    private static IEnumerable<string> Split(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// True when a certificate that failed validation should nonetheless be accepted for
    /// <paramref name="host"/>. A clean certificate never reaches this.
    /// </summary>
    public bool Accepts(string? host) =>
        TrustAll || (host is not null && TrustedHosts.Contains(host));

    /// <summary>
    /// An <see cref="HttpClient"/> honouring this policy, or a stock one when nothing is trusted
    /// explicitly — so the default path keeps .NET's own handler rather than a hand-rolled callback.
    /// </summary>
    public HttpClient CreateClient()
    {
        if (IsDefault)
        {
            return new HttpClient();
        }

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (request, _, _, errors) =>
                // The host comes from the request URI, not the certificate: trusting "dev.corp" must
                // mean "the endpoint I addressed as dev.corp", never "anything presenting that name".
                errors == SslPolicyErrors.None || Accepts(request.RequestUri?.Host),
        };

        return new HttpClient(handler);
    }

    /// <summary>
    /// A one-line summary for the startup banner. Trusting certificates is a security-relevant choice,
    /// so a host configured that way says so out loud rather than doing it quietly.
    /// </summary>
    public string? Describe() => TrustAll
        ? "outbound TLS: certificate verification DISABLED for all targets (--trust-all-proxy-targets)"
        : TrustedHosts.Count > 0
            ? $"outbound TLS: certificate verification relaxed for {string.Join(", ", TrustedHosts.Order(StringComparer.Ordinal))}"
            : null;
}
