using Microsoft.Extensions.Configuration;
using Mockifyr.Server;

namespace Mockifyr.Application.Tests;

/// <summary>
/// Dashboard-managed outbound certificate trust (#174). The claims under test are the ones the issue
/// makes: a host trusted at runtime takes effect without a restart, the list survives one, and a
/// flag-pinned host refuses changes — the two-mode design Git sync established (ADR 0007).
/// </summary>
public sealed class OutboundTrustStoreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("mockifyr-trust-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static OutboundTlsPolicy Policy(params string[] args) =>
        OutboundTlsPolicy.From(new ConfigurationBuilder().AddCommandLine(args).Build(), args);

    private OutboundTrustStore Store(string? directory = null, params string[] args) =>
        new(Policy(args), directory ?? _dir);

    // ---- runtime management --------------------------------------------------------------------

    [Fact]
    public void A_freshly_started_host_trusts_nothing()
    {
        var status = Store().Status();

        Assert.Empty(status.Hosts);
        Assert.False(status.TrustAll);
        Assert.False(status.Pinned);
        Assert.True(status.Persistent);
    }

    [Fact]
    public void A_trusted_host_takes_effect_immediately()
    {
        var store = Store();
        Assert.False(store.Accepts("dev.mycorp.intra"));

        var result = store.Trust("dev.mycorp.intra");

        // No restart, no client rebuild: the validation callback reads this store per handshake.
        Assert.True(result.IsSuccess);
        Assert.True(store.Accepts("dev.mycorp.intra"));
        Assert.Contains("dev.mycorp.intra", result.Value.Hosts);
    }

    [Fact]
    public void Distrusting_a_host_takes_effect_immediately()
    {
        var store = Store();
        store.Trust("dev.mycorp.intra");

        Assert.True(store.Distrust("dev.mycorp.intra").IsSuccess);
        Assert.False(store.Accepts("dev.mycorp.intra"));
    }

    [Fact]
    public void Distrusting_a_host_that_was_never_trusted_is_a_not_found()
    {
        var result = Store().Distrust("never.trusted");

        Assert.False(result.IsSuccess);
        Assert.Equal("Trust.UnknownHost", result.Error.Code);
    }

    [Fact]
    public void Trusting_one_host_grants_nothing_to_another()
    {
        var store = Store();
        store.Trust("dev.mycorp.intra");

        Assert.False(store.Accepts("evil.example.com"));
        Assert.False(store.Accepts("sub.dev.mycorp.intra")); // no implied suffix matching
        Assert.False(store.Accepts(null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://dev.mycorp.intra")] // a scheme would never match the URI host
    [InlineData("dev.mycorp.intra:8443")]    // nor would a port
    [InlineData("dev.mycorp.intra/path")]
    public void A_host_that_could_never_match_is_refused(string host)
    {
        var result = Store().Trust(host);

        Assert.False(result.IsSuccess);
        Assert.Equal("Trust.InvalidHost", result.Error.Code);
    }

    [Theory]
    [InlineData("dev.mycorp.intra")]
    [InlineData("localhost")]
    [InlineData("10.1.2.3")]
    [InlineData("host.docker.internal")]
    public void Ordinary_host_names_and_addresses_are_accepted(string host) =>
        Assert.True(Store().Trust(host).IsSuccess);

    // ---- persistence ---------------------------------------------------------------------------

    [Fact]
    public void Trusted_hosts_survive_a_restart()
    {
        Store().Trust("dev.mycorp.intra");

        // A second store over the same directory is what a restarted host looks like.
        var restarted = Store();

        Assert.True(restarted.Accepts("dev.mycorp.intra"));
        Assert.Contains("dev.mycorp.intra", restarted.Status().Hosts);
    }

    [Fact]
    public void A_distrusted_host_stays_gone_after_a_restart()
    {
        var store = Store();
        store.Trust("a.intra");
        store.Trust("b.intra");
        store.Distrust("a.intra");

        var restarted = Store();

        Assert.False(restarted.Accepts("a.intra"));
        Assert.True(restarted.Accepts("b.intra"));
    }

    [Fact]
    public void A_host_with_nowhere_to_write_says_so_instead_of_implying_durability()
    {
        var store = new OutboundTrustStore(Policy(), stateDirectory: null);

        Assert.True(store.Trust("dev.mycorp.intra").IsSuccess);
        Assert.True(store.Accepts("dev.mycorp.intra")); // still works for this process
        Assert.False(store.Status().Persistent);        // but is honest that it will not survive
    }

    [Fact]
    public void An_unreadable_store_starts_empty_rather_than_failing_to_start()
    {
        File.WriteAllText(Path.Combine(_dir, "outbound-trust.json"), "{ this is not json");

        // Failing open would be the dangerous direction; failing closed loses nothing but a setting.
        Assert.Empty(Store().Status().Hosts);
    }

    // ---- flag-pinned mode ----------------------------------------------------------------------

    [Fact]
    public void A_flag_pinned_host_is_read_only()
    {
        var store = Store(_dir, "--trust-proxy-target", "pinned.intra");

        Assert.True(store.Pinned);
        Assert.True(store.Accepts("pinned.intra"));

        var trust = store.Trust("other.intra");
        var distrust = store.Distrust("pinned.intra");

        Assert.Equal("Trust.FlagPinned", trust.Error.Code);
        Assert.Equal("Trust.FlagPinned", distrust.Error.Code);
        Assert.False(store.Accepts("other.intra"));
        Assert.True(store.Accepts("pinned.intra")); // unchanged by the refused calls
    }

    [Fact]
    public void A_pinned_host_ignores_previously_stored_hosts()
    {
        // Load-bearing: the flag is meant to be the WHOLE configuration. A file left behind by an
        // earlier unpinned run must not quietly widen what a pinned host accepts.
        Store().Trust("left.over.intra");

        var pinned = Store(_dir, "--trust-proxy-target", "pinned.intra");

        Assert.False(pinned.Accepts("left.over.intra"));
        Assert.True(pinned.Accepts("pinned.intra"));
    }

    [Fact]
    public void Trust_all_is_pinned_and_accepts_everything()
    {
        var store = Store(_dir, "--trust-all-proxy-targets", "true");

        Assert.True(store.TrustAll);
        Assert.True(store.Pinned);
        Assert.True(store.Accepts("anything.example.com"));
        // Flag-only by design: the dashboard must not be able to disable verification wholesale.
        Assert.Equal("Trust.FlagPinned", store.Trust("x.intra").Error.Code);
    }

    [Fact]
    public void The_default_no_op_implementation_reports_unmanaged_rather_than_failing()
    {
        var trust = new NoOutboundTrust();

        Assert.Empty(trust.Status().Hosts);
        Assert.False(trust.Status().Persistent);
        Assert.Equal("Trust.Unavailable", trust.Trust("x").Error.Code);
    }
}
