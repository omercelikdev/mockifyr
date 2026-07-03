using System.Text;
using Mediant.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Mockifyr.Application;
using Mockifyr.Core;
using Mockifyr.Server;

namespace Mockifyr.Application.Tests;

/// <summary>
/// In-process validation of the public extension API (G10): user-supplied matchers, serve-event
/// listeners, template helpers, and response transformers registered through
/// <c>AddMockifyr(cfg =&gt; …)</c> are actually invoked by the engine/renderer/adapter. Custom
/// extensions have no WireMock equivalent, so this is unit-validated (not oracle-differential).
/// </summary>
public sealed class ExtensibilityTests
{
    private static readonly TenantId Tenant = TenantId.Default;

    [Fact]
    public async Task CustomServeEventListener_IsInvoked()
    {
        var listener = new CountingListener();
        var (sender, engine) = Compose(cfg => cfg.AddServeEventListener(listener));

        await sender.Send(new CreateStubCommand(
            """{"request":{"method":"GET","url":"/x"},"response":{"status":200}}""", Tenant));
        engine.Handle(Tenant, Request("GET", "/x"));

        Assert.Equal(1, listener.Count);
    }

    [Fact]
    public async Task CustomTemplateHelper_IsUsable()
    {
        var (sender, engine) = Compose(cfg =>
            cfg.AddTemplateHelper("shout", args => (args[0]?.ToString() ?? string.Empty).ToUpperInvariant() + "!"));

        await sender.Send(new CreateStubCommand(
            """{"request":{"method":"GET","url":"/t"},"response":{"status":200,"transformers":["response-template"],"body":"{{shout 'hello'}}"}}""",
            Tenant));
        var resolution = engine.Handle(Tenant, Request("GET", "/t"));

        Assert.Equal("HELLO!", Encoding.UTF8.GetString(resolution.Response!.Body));
    }

    [Fact]
    public async Task CustomResponseTransformer_IsApplied()
    {
        var (sender, engine) = Compose(cfg => cfg.AddResponseTransformer(new SuffixTransformer()));

        await sender.Send(new CreateStubCommand(
            """{"request":{"method":"GET","url":"/r"},"response":{"status":200,"body":"base"}}""", Tenant));
        var resolution = engine.Handle(Tenant, Request("GET", "/r"));

        Assert.Equal("base-transformed", Encoding.UTF8.GetString(resolution.Response!.Body));
    }

    [Fact]
    public async Task CustomMatcher_GatesMatching()
    {
        var (sender, engine) = Compose(cfg => cfg.AddMatcher("even-body", new EvenBodyMatcher()));

        await sender.Send(new CreateStubCommand(
            """{"request":{"method":"POST","url":"/c","customMatcher":{"name":"even-body"}},"response":{"status":200,"body":"ok"}}""",
            Tenant));

        Assert.True(engine.Handle(Tenant, Request("POST", "/c", "abcd")).Matched);   // even → matches
        Assert.False(engine.Handle(Tenant, Request("POST", "/c", "abc")).Matched);   // odd → no match
    }

    private static (ISender Sender, StubEngine Engine) Compose(Action<MockifyrExtensions> configure)
    {
        var provider = new ServiceCollection().AddMockifyr(configure).BuildServiceProvider();
        return (provider.GetRequiredService<ISender>(), provider.GetRequiredService<StubEngine>());
    }

    private static CanonicalRequest Request(string method, string url, string? body = null) =>
        CanonicalRequestBuilder.Build(method, url, [], body is null ? null : Encoding.UTF8.GetBytes(body));

    private sealed class CountingListener : IServeEventListener
    {
        public int Count { get; private set; }

        public Task OnServeEventAsync(ServeEvent serveEvent, CancellationToken cancellationToken)
        {
            Count++;
            return Task.CompletedTask;
        }
    }

    private sealed class SuffixTransformer : IResponseTransformer
    {
        public string Name => "suffix";

        public CanonicalResponse Transform(CanonicalResponse response, ServeEvent serveEvent) =>
            response with { Body = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(response.Body) + "-transformed") };
    }

    private sealed class EvenBodyMatcher : IMatcher
    {
        public MatchResult Match(MatchInput input) =>
            input.Request.Body.Length % 2 == 0 ? MatchResult.Exact : MatchResult.NoMatch(1d);
    }
}
