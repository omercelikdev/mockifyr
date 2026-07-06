using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Mockifyr.Server;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Self-test of mutual TLS + a configured keystore (G11c). Mutual TLS is standard transport auth — the
/// server requires a client certificate and validates it against a configured trust anchor — with no
/// WireMock-specific semantics to diff, so (like WebSocket serving and plaintext h2c) it is validated by
/// a self-test rather than differentially. The host is started with a configured server keystore (PFX),
/// a truststore (the CA), and <c>--https-require-client-auth</c>; a client presenting a CA-signed
/// certificate is served the stub, while a client presenting <em>no</em> certificate fails the
/// handshake. See docs/parity/g11-tls-http2.md. No Docker required.
/// </summary>
public sealed class G11cMutualTlsTests : IAsyncLifetime
{
    private readonly string _dir = Directory.CreateTempSubdirectory("mockifyr-mtls-").FullName;
    private WebApplication? _host;
    private X509Certificate2 _clientCertificate = null!;
    private Uri _httpsBase = null!;
    private Uri _httpBase = null!;

    public async Task InitializeAsync()
    {
        var now = DateTimeOffset.UtcNow;

        // A test CA, a CA-signed client certificate, and a localhost server certificate.
        using var ca = CreateCa(now);
        _clientCertificate = CreateClientCertificate(ca, now);
        using var serverCertificate = CreateServerCertificate(now);

        // Write the server keystore (PFX) and the truststore (CA public cert) the host loads by config.
        var keystorePath = Path.Combine(_dir, "server.pfx");
        var truststorePath = Path.Combine(_dir, "ca.cer");
        File.WriteAllBytes(keystorePath, serverCertificate.Export(X509ContentType.Pkcs12, "server-pass"));
        File.WriteAllBytes(truststorePath, ca.Export(X509ContentType.Cert));

        _host = MockifyrHost.Build(
        [
            "--port", "0", "--https-port", "0",
            "--https-keystore", keystorePath, "--https-keystore-password", "server-pass",
            "--https-require-client-auth", "true", "--https-truststore", truststorePath,
        ]);
        await _host.StartAsync();

        var addresses = _host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses;
        _httpsBase = new Uri(Loopback(addresses.First(a => a.StartsWith("https://", StringComparison.Ordinal))));
        _httpBase = new Uri(Loopback(addresses.First(a => a.StartsWith("http://", StringComparison.Ordinal))));

        // Load the stub over the plaintext port (no client-cert requirement there).
        using var admin = new HttpClient { BaseAddress = _httpBase };
        const string stub = """{"request":{"method":"GET","url":"/secure"},"response":{"status":200,"body":"mtls-ok"}}""";
        using var content = new StringContent(stub, Encoding.UTF8, "application/json");
        await admin.PostAsync("/__admin/mappings", content);
    }

    public async Task DisposeAsync()
    {
        _clientCertificate.Dispose();
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }

        Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public async Task ClientCertificate_PresentedAndTrusted_ServesTheStub()
    {
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };
        handler.ClientCertificates.Add(_clientCertificate);

        using var client = new HttpClient(handler) { BaseAddress = _httpsBase };
        using var response = await client.GetAsync("/secure");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("mtls-ok", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task NoClientCertificate_IsRejected()
    {
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };

        using var client = new HttpClient(handler) { BaseAddress = _httpsBase };

        // Requiring a client certificate the caller does not present fails the TLS handshake.
        await Assert.ThrowsAnyAsync<HttpRequestException>(() => client.GetAsync("/secure"));
    }

    private static string Loopback(string address) =>
        address.Replace("[::]", "127.0.0.1").Replace("0.0.0.0", "127.0.0.1");

    private static X509Certificate2 CreateCa(DateTimeOffset now)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Mockifyr Test CA", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: false, 0, critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, critical: true));
        return request.CreateSelfSigned(now.AddDays(-1), now.AddYears(1));
    }

    private static X509Certificate2 CreateClientCertificate(X509Certificate2 ca, DateTimeOffset now)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=mockifyr-client", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(certificateAuthority: false, hasPathLengthConstraint: false, 0, critical: true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.2")], critical: false)); // client auth
        var serial = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        using var signed = request.Create(ca, now.AddDays(-1), now.AddDays(365), serial);
        using var withKey = signed.CopyWithPrivateKey(key);
        // Round-trip through PKCS#12 so the private key is usable by the TLS client on every platform.
        return X509CertificateLoader.LoadPkcs12(withKey.Export(X509ContentType.Pkcs12), password: null);
    }

    private static X509Certificate2 CreateServerCertificate(DateTimeOffset now)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(certificateAuthority: false, hasPathLengthConstraint: false, 0, critical: true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], critical: false)); // server auth
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("localhost");
        san.AddIpAddress(IPAddress.Loopback);
        san.AddIpAddress(IPAddress.IPv6Loopback);
        request.CertificateExtensions.Add(san.Build());
        return request.CreateSelfSigned(now.AddDays(-1), now.AddYears(1));
    }
}
