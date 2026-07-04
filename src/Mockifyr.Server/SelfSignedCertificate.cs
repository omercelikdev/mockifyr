using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Mockifyr.Server;

/// <summary>
/// Generates an ephemeral self-signed TLS certificate for the standalone host's HTTPS listener
/// (G11a) — mirroring WireMock, which serves HTTPS with a bundled self-signed certificate by default.
/// A configured keystore (PFX path/password) is a later refinement; here the cert is minted in-memory
/// at startup, valid for <c>localhost</c> and the loopback addresses so a local client can connect.
/// </summary>
public static class SelfSignedCertificate
{
    /// <summary>Creates a fresh RSA-2048 self-signed certificate for <c>localhost</c> / loopback.</summary>
    public static X509Certificate2 Create()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(certificateAuthority: false, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, critical: true));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], critical: false)); // server auth

        var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
        subjectAlternativeNames.AddDnsName("localhost");
        subjectAlternativeNames.AddIpAddress(IPAddress.Loopback);
        subjectAlternativeNames.AddIpAddress(IPAddress.IPv6Loopback);
        request.CertificateExtensions.Add(subjectAlternativeNames.Build());

        var now = DateTimeOffset.UtcNow;
        var certificate = request.CreateSelfSigned(now.AddDays(-1), now.AddYears(1));

        // Kestrel needs a cert with an exportable private key; round-trip through PFX to guarantee it
        // (a directly-created cert can carry an ephemeral key that Kestrel rejects on some platforms).
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pkcs12), password: null);
    }
}
