using System.Text;
using Mediant.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Mockifyr.Core;
using Mockifyr.Server;

namespace Mockifyr.Application.Tests;

/// <summary>
/// Behavioral coverage for environments (G17, issues #165/#166). WireMock has no environments concept,
/// so there is no oracle to diff against — this is a self-test, and it targets the two claims the
/// issues actually make:
/// <list type="bullet">
/// <item>resolution is <b>dynamic</b>: changing a key's active value changes what an already-saved
/// stub serves, on the next request, with no re-save (#165);</item>
/// <item>resolution is <b>tenant-scoped</b>: one tenant can neither see, resolve, nor mutate
/// another's keys, enforced below the HTTP surface so it cannot be bypassed (#166).</item>
/// </list>
/// </summary>
public sealed class G17EnvironmentTests
{
    private static readonly TenantId Acme = new("acme");
    private static readonly TenantId Globex = new("globex");

    private static ServiceProvider Host() => new ServiceCollection().AddMockifyr().BuildServiceProvider();

    private static EnvironmentKey Key(string key, string active, params (string Name, string Value)[] values) =>
        new(key, active, [.. values.Select(v => new EnvironmentValue(v.Name, v.Value))]);

    // Concatenated rather than interpolated: the bodies under test are full of {{…}}, which fights
    // every raw-string interpolation form.
    private static string Mapping(string body) =>
        """{"request":{"method":"GET","urlPath":"/x"},"response":{"status":200,"body":" """.TrimEnd()
        + body + """ "}}""".TrimStart();

    private static string Serve(StubEngine engine, TenantId tenant)
    {
        var result = engine.Handle(tenant, CanonicalRequestBuilder.Build("GET", "/x", [], []));
        return Encoding.UTF8.GetString(result.Response!.Body);
    }

    private static StubEngine EngineOf(ServiceProvider provider) => provider.GetRequiredService<StubEngine>();

    // ---- #165: dynamic resolution -------------------------------------------------------------

    [Fact]
    public async Task Active_value_change_reaches_an_already_saved_stub_without_re_saving_it()
    {
        using var provider = Host();
        var sender = provider.GetRequiredService<ISender>();

        Assert.True((await sender.Send(new PutEnvironmentKeyCommand(
            Key("baseUrl", "dev", ("dev", "https://dev.example.com"), ("prod", "https://api.example.com")),
            TenantId.Default))).IsSuccess);

        Assert.True((await sender.Send(new CreateStubCommand(Mapping("{{baseUrl}}/users"), TenantId.Default))).IsSuccess);

        var engine = EngineOf(provider);
        Assert.Equal("https://dev.example.com/users", Serve(engine, TenantId.Default));

        // The only thing that changes is the active value — the stub is never touched again.
        Assert.True((await sender.Send(new SetEnvironmentActiveValueCommand("baseUrl", "prod", TenantId.Default))).IsSuccess);

        Assert.Equal("https://api.example.com/users", Serve(engine, TenantId.Default));
    }

    [Fact]
    public async Task Each_key_carries_its_own_active_value_independently()
    {
        using var provider = Host();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new PutEnvironmentKeyCommand(Key("baseUrl", "dev", ("dev", "D"), ("prod", "P")), TenantId.Default));
        await sender.Send(new PutEnvironmentKeyCommand(Key("region", "eu", ("eu", "eu-west-1"), ("us", "us-east-1")), TenantId.Default));
        await sender.Send(new CreateStubCommand(Mapping("{{baseUrl}}|{{region}}"), TenantId.Default));

        var engine = EngineOf(provider);
        Assert.Equal("D|eu-west-1", Serve(engine, TenantId.Default));

        // Switching one key must not disturb the other — the flat single-active-environment model
        // this replaces could not express it.
        await sender.Send(new SetEnvironmentActiveValueCommand("region", "us", TenantId.Default));

        Assert.Equal("D|us-east-1", Serve(engine, TenantId.Default));
    }

    [Fact]
    public async Task The_stub_stores_the_reference_verbatim_and_never_the_resolved_value()
    {
        using var provider = Host();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new PutEnvironmentKeyCommand(Key("baseUrl", "dev", ("dev", "https://dev.example.com")), TenantId.Default));
        await sender.Send(new CreateStubCommand(Mapping("{{baseUrl}}/users"), TenantId.Default));

        // The critical requirement of #165: what was saved is the expression, not today's value.
        var stub = Assert.Single(provider.GetRequiredService<IStubStore>().GetStubs(TenantId.Default));
        var stored = Encoding.UTF8.GetString(stub.Response.Body!);
        Assert.Equal("{{baseUrl}}/users", stored);
        Assert.DoesNotContain("dev.example.com", stored);
    }

    [Fact]
    public async Task Resolution_applies_to_stubs_that_never_opted_into_response_templating()
    {
        using var provider = Host();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new PutEnvironmentKeyCommand(Key("baseUrl", "dev", ("dev", "https://dev")), TenantId.Default));
        // Note: no "transformers":["response-template"] on this stub. Substitution must still happen,
        // otherwise the feature would silently miss the majority of stubs.
        await sender.Send(new CreateStubCommand(Mapping("{{baseUrl}}/x"), TenantId.Default));

        Assert.Equal("https://dev/x", Serve(EngineOf(provider), TenantId.Default));
    }

    // ---- #166: tenant scoping -----------------------------------------------------------------

    [Fact]
    public async Task A_key_defined_in_one_tenant_is_invisible_to_another()
    {
        using var provider = Host();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new PutEnvironmentKeyCommand(Key("baseUrl", "dev", ("dev", "https://acme.internal")), Acme));

        var acme = await sender.Send(new GetEnvironmentsQuery(Acme));
        var globex = await sender.Send(new GetEnvironmentsQuery(Globex));

        Assert.Equal("baseUrl", Assert.Single(acme.Value).Key);
        Assert.Empty(globex.Value);
    }

    [Fact]
    public async Task Another_tenants_stub_does_not_resolve_against_the_owning_tenants_key()
    {
        using var provider = Host();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new PutEnvironmentKeyCommand(Key("baseUrl", "dev", ("dev", "https://acme.internal")), Acme));
        await sender.Send(new CreateStubCommand(Mapping("{{baseUrl}}/x"), Acme));
        await sender.Send(new CreateStubCommand(Mapping("{{baseUrl}}/x"), Globex));

        var engine = EngineOf(provider);
        Assert.Equal("https://acme.internal/x", Serve(engine, Acme));

        // Globex has no such key: the reference must fall through untouched rather than leak Acme's
        // internal URL. This is the actual data-leak the issue reports.
        var leaked = Serve(engine, Globex);
        Assert.DoesNotContain("acme.internal", leaked);
    }

    [Fact]
    public async Task A_tenant_cannot_delete_another_tenants_key()
    {
        using var provider = Host();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new PutEnvironmentKeyCommand(Key("baseUrl", "dev", ("dev", "X")), Acme));

        var cross = await sender.Send(new DeleteEnvironmentKeyCommand("baseUrl", Globex));

        Assert.False(cross.IsSuccess);
        Assert.Equal("Environment.UnknownKey", cross.Error.Code);
        Assert.Single((await sender.Send(new GetEnvironmentsQuery(Acme))).Value);
    }

    [Fact]
    public async Task A_tenant_cannot_switch_another_tenants_active_value()
    {
        using var provider = Host();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new PutEnvironmentKeyCommand(Key("baseUrl", "dev", ("dev", "D"), ("prod", "P")), Acme));

        var cross = await sender.Send(new SetEnvironmentActiveValueCommand("baseUrl", "prod", Globex));

        Assert.False(cross.IsSuccess);
        Assert.Equal("Environment.UnknownKey", cross.Error.Code);
        Assert.Equal("dev", Assert.Single((await sender.Send(new GetEnvironmentsQuery(Acme))).Value).ActiveValue);
    }

    [Fact]
    public async Task Resetting_one_tenant_leaves_the_other_intact()
    {
        using var provider = Host();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new PutEnvironmentKeyCommand(Key("k", "a", ("a", "1")), Acme));
        await sender.Send(new PutEnvironmentKeyCommand(Key("k", "a", ("a", "2")), Globex));

        await sender.Send(new ResetEnvironmentsCommand(Acme));

        Assert.Empty((await sender.Send(new GetEnvironmentsQuery(Acme))).Value);
        Assert.Single((await sender.Send(new GetEnvironmentsQuery(Globex))).Value);
    }

    [Fact]
    public async Task The_same_key_name_holds_different_values_in_different_tenants()
    {
        using var provider = Host();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new PutEnvironmentKeyCommand(Key("baseUrl", "v", ("v", "https://acme")), Acme));
        await sender.Send(new PutEnvironmentKeyCommand(Key("baseUrl", "v", ("v", "https://globex")), Globex));
        await sender.Send(new CreateStubCommand(Mapping("{{baseUrl}}/x"), Acme));
        await sender.Send(new CreateStubCommand(Mapping("{{baseUrl}}/x"), Globex));

        var engine = EngineOf(provider);
        Assert.Equal("https://acme/x", Serve(engine, Acme));
        Assert.Equal("https://globex/x", Serve(engine, Globex));
    }

    // ---- validation ---------------------------------------------------------------------------

    [Theory]
    [InlineData("now")]
    [InlineData("request")]
    [InlineData("random")]
    public async Task A_key_named_after_a_builtin_helper_is_refused(string reserved)
    {
        using var provider = Host();

        var result = await provider.GetRequiredService<ISender>()
            .Send(new PutEnvironmentKeyCommand(Key(reserved, "a", ("a", "1")), TenantId.Default));

        Assert.False(result.IsSuccess);
        Assert.Equal("Environment.ReservedKey", result.Error.Code);
    }

    [Fact]
    public async Task A_reserved_key_being_refused_keeps_the_helper_working()
    {
        using var provider = Host();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new PutEnvironmentKeyCommand(Key("now", "a", ("a", "SHADOWED")), TenantId.Default));
        await sender.Send(new CreateStubCommand(
            """{"request":{"method":"GET","urlPath":"/x"},"response":{"status":200,"transformers":["response-template"],"body":"{{now format='yyyy'}}"}}""",
            TenantId.Default));

        var served = Serve(EngineOf(provider), TenantId.Default);

        Assert.DoesNotContain("SHADOWED", served);
        Assert.Equal(4, served.Length); // a year, i.e. the helper still ran
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("has.dot")]
    [InlineData("9lives")]
    [InlineData("")]
    public async Task A_malformed_key_is_refused(string key)
    {
        using var provider = Host();

        var result = await provider.GetRequiredService<ISender>()
            .Send(new PutEnvironmentKeyCommand(Key(key, "a", ("a", "1")), TenantId.Default));

        Assert.False(result.IsSuccess);
        Assert.Equal("Environment.InvalidKey", result.Error.Code);
    }

    [Fact]
    public async Task An_active_value_naming_nothing_is_refused()
    {
        using var provider = Host();

        var result = await provider.GetRequiredService<ISender>()
            .Send(new PutEnvironmentKeyCommand(Key("baseUrl", "missing", ("dev", "D")), TenantId.Default));

        Assert.False(result.IsSuccess);
        Assert.Equal("Environment.UnknownActiveValue", result.Error.Code);
    }

    [Fact]
    public async Task A_key_with_no_values_is_refused()
    {
        using var provider = Host();

        var result = await provider.GetRequiredService<ISender>()
            .Send(new PutEnvironmentKeyCommand(new EnvironmentKey("baseUrl", "dev", []), TenantId.Default));

        Assert.False(result.IsSuccess);
        Assert.Equal("Environment.NoValues", result.Error.Code);
    }

    [Fact]
    public async Task Duplicate_value_names_are_refused()
    {
        using var provider = Host();

        var result = await provider.GetRequiredService<ISender>()
            .Send(new PutEnvironmentKeyCommand(Key("baseUrl", "dev", ("dev", "1"), ("dev", "2")), TenantId.Default));

        Assert.False(result.IsSuccess);
        Assert.Equal("Environment.DuplicateValue", result.Error.Code);
    }

    // ---- coexistence with templating ------------------------------------------------------------

    [Fact]
    public async Task Environment_references_and_handlebars_helpers_coexist_in_one_body()
    {
        using var provider = Host();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new PutEnvironmentKeyCommand(Key("baseUrl", "dev", ("dev", "https://dev")), TenantId.Default));
        await sender.Send(new CreateStubCommand(
            """{"request":{"method":"GET","urlPath":"/x"},"response":{"status":200,"transformers":["response-template"],"body":"{{baseUrl}}/{{request.method}}"}}""",
            TenantId.Default));

        Assert.Equal("https://dev/GET", Serve(EngineOf(provider), TenantId.Default));
    }

    [Fact]
    public async Task An_undefined_reference_is_left_for_handlebars_rather_than_blanked_by_the_env_pass()
    {
        using var provider = Host();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new PutEnvironmentKeyCommand(Key("baseUrl", "dev", ("dev", "https://dev")), TenantId.Default));
        // No response-template transformer, so Handlebars does not run either: an undefined reference
        // survives as literal text, which is what makes it diagnosable rather than silently empty.
        await sender.Send(new CreateStubCommand(Mapping("{{baseUrl}}|{{typoKey}}"), TenantId.Default));

        Assert.Equal("https://dev|{{typoKey}}", Serve(EngineOf(provider), TenantId.Default));
    }
}
