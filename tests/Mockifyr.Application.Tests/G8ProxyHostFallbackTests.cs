using System.Net;
using System.Net.Sockets;
using System.Text;
using Mockifyr.Core;
using Mockifyr.Outbound;

namespace Mockifyr.Application.Tests;

/// <summary>
/// The container-localhost fallback extended to proxying (#176). The callback fix (#170) left proxy
/// targets out of scope; a stub with <c>proxyBaseUrl: http://localhost:…</c> hit the identical wall.
/// <para>
/// Reproduced against the published image (see docs/parity/g8-proxy.md). These pin the retry
/// behaviour: a refused loopback proxy is retried via the host gateway <em>when containerised</em>, a
/// non-container run never rewrites, and a successful first attempt never retries. Container detection
/// is a runtime property, so tests that need "in a container" assert conditionally on it — the same
/// approach as the callback tests, with the container e2e as the real proof.
/// </para>
/// </summary>
public sealed class G8ProxyHostFallbackTests
{
    private static readonly ProxyDirective Loopback = new("http://localhost:59998");

    private static CanonicalRequest Request() => CanonicalRequestBuilder.Build("GET", "/thing", [], []);

    [Fact]
    public async Task A_reachable_proxy_is_forwarded_and_never_retried()
    {
        var attempts = new List<string>();
        var responder = new ProxyResponder(new HttpClient(new StubHandler(request =>
        {
            attempts.Add(request.RequestUri!.Host);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("up") };
        })));

        var response = await responder.ProxyAsync(Loopback, Request());

        // A service listening on the container's own loopback answers the first attempt, so the
        // fallback cannot displace it.
        Assert.Equal(200, response.Status);
        Assert.Equal(["localhost"], attempts);
    }

    [Fact]
    public async Task A_refused_loopback_proxy_retries_via_the_host_gateway_when_containerised()
    {
        var attempts = new List<string>();
        var responder = new ProxyResponder(new HttpClient(new StubHandler(request =>
        {
            attempts.Add(request.RequestUri!.Host);
            if (request.RequestUri!.Host == ContainerHostFallback.HostGateway)
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("via-host") };
            }

            throw new HttpRequestException("refused", new SocketException((int)SocketError.ConnectionRefused));
        })));

        if (!ContainerHostFallback.IsInContainer)
        {
            // Outside a container there is nothing to fall back to: one attempt, then the explained
            // failure. Keeps the test honest on a developer machine.
            var failure = await Assert.ThrowsAsync<ProxyDeliveryException>(() => responder.ProxyAsync(Loopback, Request()));
            Assert.False(failure.ContainerDiagnosis);
            Assert.Equal(["localhost"], attempts);
            return;
        }

        var response = await responder.ProxyAsync(Loopback, Request());

        Assert.Equal(200, response.Status);
        Assert.Equal(["localhost", ContainerHostFallback.HostGateway], attempts);
    }

    [Fact]
    public async Task A_timeout_is_not_retried_against_a_different_host()
    {
        var attempts = new List<string>();
        var responder = new ProxyResponder(new HttpClient(new StubHandler(request =>
        {
            attempts.Add(request.RequestUri!.Host);
            throw new HttpRequestException("timed out", new SocketException((int)SocketError.TimedOut));
        })));

        var failure = await Assert.ThrowsAsync<ProxyDeliveryException>(() => responder.ProxyAsync(Loopback, Request()));

        Assert.Equal(["localhost"], attempts);
        Assert.False(failure.ContainerDiagnosis);
        Assert.Contains("timed out", failure.Message);
    }

    [Fact]
    public async Task A_non_loopback_proxy_is_never_rewritten()
    {
        var attempts = new List<string>();
        var responder = new ProxyResponder(new HttpClient(new StubHandler(request =>
        {
            attempts.Add(request.RequestUri!.Host);
            throw new HttpRequestException("refused", new SocketException((int)SocketError.ConnectionRefused));
        })));

        await Assert.ThrowsAsync<ProxyDeliveryException>(() =>
            responder.ProxyAsync(new ProxyDirective("http://api.example.com"), Request()));

        // A refused external target is a plain failure, not a container trap: one attempt only.
        Assert.Equal(["api.example.com"], attempts);
    }

    [Fact]
    public async Task The_fallback_can_be_switched_off()
    {
        var attempts = new List<string>();
        var responder = new ProxyResponder(
            new HttpClient(new StubHandler(request =>
            {
                attempts.Add(request.RequestUri!.Host);
                throw new HttpRequestException("refused", new SocketException((int)SocketError.ConnectionRefused));
            })),
            hostFallback: false);

        await Assert.ThrowsAsync<ProxyDeliveryException>(() => responder.ProxyAsync(Loopback, Request()));

        Assert.Equal(["localhost"], attempts);
    }

    [Fact]
    public async Task A_failed_proxy_carries_the_flattened_cause()
    {
        var responder = new ProxyResponder(new HttpClient(new StubHandler(_ =>
            throw new HttpRequestException(
                "The SSL connection could not be established, see inner exception.",
                new System.Security.Authentication.AuthenticationException("RemoteCertificateChainErrors")))));

        var failure = await Assert.ThrowsAsync<ProxyDeliveryException>(() =>
            responder.ProxyAsync(new ProxyDirective("https://api.example.com"), Request()));

        // Same defect the callback path had (#172): the outer message alone says "see inner exception".
        Assert.Contains("RemoteCertificateChainErrors", failure.Message);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
