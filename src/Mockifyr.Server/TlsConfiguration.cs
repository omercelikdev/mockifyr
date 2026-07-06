using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Configuration;

namespace Mockifyr.Server;

/// <summary>
/// Resolves the HTTPS listener's TLS configuration (G11c) from host config: a configured server
/// keystore (PFX) instead of the ephemeral self-signed cert, and optional mutual TLS — requiring and
/// validating a client certificate. Mirrors WireMock's <c>--https-keystore</c>/<c>--keystore-password</c>,
/// <c>--https-require-client-auth</c>, and <c>--https-truststore</c>/<c>--truststore-password</c> flags.
/// Being standard mutual-TLS transport auth (no WireMock-specific semantics), it is self-tested rather
/// than diffed — see docs/parity/g11-tls-http2.md.
/// </summary>
public static class TlsConfiguration
{
    /// <summary>
    /// Builds the <see cref="HttpsConnectionAdapterOptions"/> configuration for the secure listener.
    /// The server certificate is loaded from <c>https-keystore</c> (+ <c>https-keystore-password</c>)
    /// when given, else the caller's self-signed default. When <c>https-require-client-auth</c> is set,
    /// the listener requires a client certificate; if a <c>https-truststore</c> is configured the client
    /// cert must chain to it, otherwise any presented (well-formed) client cert is accepted.
    /// </summary>
    public static Action<HttpsConnectionAdapterOptions> Build(IConfiguration configuration, X509Certificate2 fallbackServerCertificate)
    {
        var serverCertificate = LoadServerCertificate(configuration) ?? fallbackServerCertificate;
        var requireClientAuth = configuration.GetValue<bool>("https-require-client-auth");
        var trustAnchor = LoadTrustAnchor(configuration);

        return options =>
        {
            options.ServerCertificate = serverCertificate;

            if (!requireClientAuth)
            {
                return;
            }

            options.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
            options.ClientCertificateValidation = (clientCertificate, chain, _) =>
                trustAnchor is null || ChainsToTrustAnchor(clientCertificate, trustAnchor);
        };
    }

    private static X509Certificate2? LoadServerCertificate(IConfiguration configuration)
    {
        var keystore = configuration["https-keystore"];
        if (string.IsNullOrWhiteSpace(keystore))
        {
            return null;
        }

        var password = configuration["https-keystore-password"];
        return X509CertificateLoader.LoadPkcs12FromFile(keystore, password);
    }

    // The truststore is a CA certificate the client cert must chain to. Loaded from a PFX/CER/PEM file.
    private static X509Certificate2? LoadTrustAnchor(IConfiguration configuration)
    {
        var truststore = configuration["https-truststore"];
        if (string.IsNullOrWhiteSpace(truststore))
        {
            return null;
        }

        var password = configuration["https-truststore-password"];
        return string.IsNullOrEmpty(password)
            ? X509CertificateLoader.LoadCertificateFromFile(truststore)
            : X509CertificateLoader.LoadPkcs12FromFile(truststore, password);
    }

    // Validate the presented client certificate against the configured trust anchor only (custom root
    // trust), so a certificate the OS happens to trust is not accepted unless it chains to our anchor.
    private static bool ChainsToTrustAnchor(X509Certificate2 clientCertificate, X509Certificate2 trustAnchor)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(trustAnchor);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return chain.Build(clientCertificate);
    }
}
