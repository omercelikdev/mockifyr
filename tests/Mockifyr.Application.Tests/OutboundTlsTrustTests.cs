using Microsoft.Extensions.Configuration;
using Mockifyr.Server;
using Mockifyr.ServeEvents.Webhook;

namespace Mockifyr.Application.Tests;

/// <summary>
/// Outbound certificate trust (#172): <c>--trust-proxy-target</c> / <c>--trust-all-proxy-targets</c>,
/// mirroring WireMock's flags, plus the error-message defect that hid the reason a TLS call failed.
/// <para>
/// These pin the policy decisions rather than TLS itself — the handshake is .NET's, but <em>which
/// host is trusted</em>, and <em>that trusting one does not trust another</em>, is ours to get right.
/// </para>
/// </summary>
public sealed class OutboundTlsTrustTests
{
    private static OutboundTlsPolicy Policy(params string[] args) =>
        OutboundTlsPolicy.From(new ConfigurationBuilder().AddCommandLine(args).Build(), args);

    [Fact]
    public void No_flags_leaves_verification_untouched()
    {
        var policy = Policy();

        Assert.True(policy.IsDefault);
        Assert.False(policy.Accepts("anything.example.com"));
        Assert.Null(policy.Describe());
    }

    [Fact]
    public void A_named_host_is_trusted()
    {
        var policy = Policy("--trust-proxy-target", "dev.mycorp.intra");

        Assert.True(policy.Accepts("dev.mycorp.intra"));
        Assert.False(policy.IsDefault);
    }

    [Fact]
    public void Trusting_one_host_does_not_trust_another()
    {
        // The whole point of per-host trust: reaching one internal endpoint must not silently accept
        // every certificate everywhere.
        var policy = Policy("--trust-proxy-target", "dev.mycorp.intra");

        Assert.False(policy.Accepts("evil.example.com"));
        Assert.False(policy.Accepts(null));
        Assert.False(policy.Accepts("sub.dev.mycorp.intra"));
    }

    [Fact]
    public void Host_matching_ignores_case_because_dns_does()
    {
        var policy = Policy("--trust-proxy-target", "Dev.MyCorp.Intra");

        Assert.True(policy.Accepts("dev.mycorp.intra"));
    }

    [Fact]
    public void The_flag_is_repeatable_like_wiremocks()
    {
        // .NET's command-line provider collapses a repeated key to its last value, so the raw args
        // are scanned too. Without that, "--trust-proxy-target a --trust-proxy-target b" would
        // silently trust only b — the failure mode being guarded here.
        var policy = Policy(
            "--trust-proxy-target", "a.intra",
            "--trust-proxy-target", "b.intra",
            "--trust-proxy-target", "c.intra");

        Assert.True(policy.Accepts("a.intra"));
        Assert.True(policy.Accepts("b.intra"));
        Assert.True(policy.Accepts("c.intra"));
    }

    [Fact]
    public void A_comma_separated_list_is_accepted_too()
    {
        var policy = Policy("--trust-proxy-target", "a.intra, b.intra;c.intra");

        Assert.True(policy.Accepts("a.intra"));
        Assert.True(policy.Accepts("b.intra"));
        Assert.True(policy.Accepts("c.intra"));
        Assert.False(policy.Accepts("d.intra"));
    }

    [Fact]
    public void Trust_all_accepts_every_host()
    {
        var policy = Policy("--trust-all-proxy-targets", "true");

        Assert.True(policy.Accepts("anything.example.com"));
        Assert.True(policy.TrustAll);
        Assert.Contains("DISABLED", policy.Describe());
    }

    [Fact]
    public void A_relaxed_host_says_so_out_loud()
    {
        // Trusting a certificate is security-relevant; a host doing it must not do it quietly.
        var description = Policy("--trust-proxy-target", "dev.mycorp.intra").Describe();

        Assert.NotNull(description);
        Assert.Contains("dev.mycorp.intra", description);
    }

    [Fact]
    public void The_default_policy_builds_a_stock_client()
    {
        using var client = OutboundTlsPolicy.Default.CreateClient();

        Assert.NotNull(client);
    }

    [Fact]
    public void A_trusting_policy_builds_a_client_with_a_validation_callback()
    {
        using var client = Policy("--trust-proxy-target", "dev.mycorp.intra").CreateClient();

        Assert.NotNull(client);
    }

    // ---- the discarded-reason defect ------------------------------------------------------------

    [Fact]
    public void A_transport_failure_reports_the_inner_reason_not_just_see_inner_exception()
    {
        // The exact shape .NET produces for an untrusted certificate. Journalling only the outer
        // message left the operator with a sentence that refers to information they never get.
        var tls = new HttpRequestException(
            "The SSL connection could not be established, see inner exception.",
            new System.Security.Authentication.AuthenticationException(
                "The remote certificate is invalid according to the validation procedure: RemoteCertificateChainErrors"));

        var described = ContainerHostFallback.Describe(tls);

        Assert.Contains("The SSL connection could not be established", described);
        Assert.Contains("RemoteCertificateChainErrors", described);
    }

    [Fact]
    public void A_single_message_is_left_alone()
    {
        var described = ContainerHostFallback.Describe(new InvalidOperationException("plain failure"));

        Assert.Equal("plain failure", described);
    }

    [Fact]
    public void A_repeated_message_in_the_chain_is_not_echoed_twice()
    {
        // Wrapping frequently restates the same text; the journal line must stay readable.
        var duplicated = new HttpRequestException("same", new InvalidOperationException("same"));

        Assert.Equal("same", ContainerHostFallback.Describe(duplicated));
    }

    [Fact]
    public void A_deep_chain_is_flattened_in_order()
    {
        var chain = new HttpRequestException("outer",
            new InvalidOperationException("middle", new TimeoutException("root")));

        Assert.Equal("outer -> middle -> root", ContainerHostFallback.Describe(chain));
    }
}
