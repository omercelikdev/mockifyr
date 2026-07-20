using Mediant.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Mockifyr.Application;
using Mockifyr.Core;
using Mockifyr.Outbound;
using Mockifyr.ServeEvents.Webhook;
using Mockifyr.Stores.InMemory;
using Mockifyr.Templating;

namespace Mockifyr.Server;

/// <summary>
/// The composition root wiring (G7). Registers the shared in-memory state, the serving engine, and
/// the Mediant management-path handlers into one container — so the admin/CQRS path and the
/// mock-serving hot path operate on the <em>same</em> stores. The hot path resolves
/// <see cref="StubEngine"/> directly (no mediator); the management path goes through Mediant.
/// </summary>
public static class MockifyrServiceCollectionExtensions
{
    public static IServiceCollection AddMockifyr(
        this IServiceCollection services, Action<MockifyrExtensions>? configure = null)
    {
        var extensions = new MockifyrExtensions();
        configure?.Invoke(extensions);

        // Shared singletons — the management path and the serving hot path see the same state.
        services.AddSingleton<InMemoryStubStore>();
        services.AddSingleton<IStubStore>(sp => sp.GetRequiredService<InMemoryStubStore>());
        services.AddSingleton<IScenarioStateStore, InMemoryScenarioStateStore>();

        // Environments (G17): one instance serves both the admin store and the serve-path resolver, so
        // a change to a key's active value is visible to the very next request without a reload.
        services.AddSingleton<InMemoryEnvironmentStore>();
        services.AddSingleton<IEnvironmentStore>(sp => sp.GetRequiredService<InMemoryEnvironmentStore>());
        services.AddSingleton<IEnvironmentResolver>(sp => sp.GetRequiredService<InMemoryEnvironmentStore>());
        services.AddSingleton<IRequestJournal, InMemoryRequestJournal>();

        // Persistence (G16): no-op by default (purely in-memory). A file/db-backed provider is
        // registered on top (e.g. by MockifyrHost when --root-dir is set) and wins the resolution.
        services.AddSingleton<IStubPersistence, NullStubPersistence>();
        services.AddSingleton<IEnvironmentPersistence, NullEnvironmentPersistence>();

        // Git sync (ADR 0007): unconfigured by default; MockifyrHost registers the real service on
        // top when --git-remote is set and it wins the resolution.
        services.AddSingleton<IGitSync, NotConfiguredGitSync>();

        // Custom matcher registry (G10), populated with the user's named matchers.
        var registry = new InMemoryMatcherRegistry();
        foreach (var (name, matcher) in extensions.Matchers)
        {
            registry.Register(name, matcher);
        }

        services.AddSingleton<IMatcherRegistry>(registry);

        // The renderer gets the user's template helpers (G10).
        services.AddSingleton<IResponseRenderer>(sp => new TemplatingResponseRenderer(
            extensions.TemplateHelpers,
            environments: sp.GetRequiredService<IEnvironmentResolver>()));

        // Serve-event listeners: the built-in webhook plus any user extensions.
        services.AddSingleton<IServeEventTemplateRenderer>(sp =>
            new WebhookTemplateRenderer(sp.GetRequiredService<IEnvironmentResolver>()));
        // WebhookOptions is resolved optionally: a host that registers one (MockifyrHost, from the
        // --webhook-host-fallback flag) is honoured, and one that does not keeps the defaults. The
        // factory runs at resolution time, after every registration, so ordering does not matter.
        services.AddSingleton<IServeEventListener>(sp =>
            new WebhookServeEventListener(
                // Outbound TLS trust (#172): both outbound paths share one policy, so a host trusted
                // for proxying is trusted for callbacks too.
                client: (sp.GetService<OutboundTlsPolicy>() ?? OutboundTlsPolicy.Default).CreateClient(),
                sp.GetRequiredService<IServeEventTemplateRenderer>(),
                sp.GetService<WebhookOptions>()?.HostFallback ?? true));
        foreach (var listener in extensions.ServeEventListeners)
        {
            services.AddSingleton<IServeEventListener>(listener);
        }

        // Response transformers (G10).
        foreach (var transformer in extensions.ResponseTransformers)
        {
            services.AddSingleton<IResponseTransformer>(transformer);
        }

        // Custom admin API extensions (G12e), served under /__admin/ext/<prefix>/*.
        foreach (var adminExtension in extensions.AdminApiExtensions)
        {
            services.AddSingleton<IAdminApiExtension>(adminExtension);
        }

        services.AddSingleton(sp => new StubEngine(
            sp.GetRequiredService<IStubStore>(),
            sp.GetRequiredService<IResponseRenderer>(),
            sp.GetRequiredService<IScenarioStateStore>(),
            sp.GetRequiredService<IRequestJournal>(),
            sp.GetServices<IServeEventListener>(),
            sp.GetServices<IResponseTransformer>()));

        // Outbound edge (G12d): the proxy responder + recorder for proxy directives and record mode,
        // and the shared live-recording state the admin control endpoints and the fallback both see.
        services.AddSingleton<ProxyResponder>(sp =>
            new ProxyResponder((sp.GetService<OutboundTlsPolicy>() ?? OutboundTlsPolicy.Default).CreateClient()));
        services.AddSingleton<StubRecorder>(_ => new StubRecorder());
        services.AddSingleton<RecordingSession>();

        // Management path: Mediant + the Application command/query handlers (scanned by assembly).
        services.AddMediant(typeof(CreateStubCommand).Assembly);
        return services;
    }
}
