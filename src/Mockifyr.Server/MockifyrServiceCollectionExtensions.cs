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
        services.AddSingleton<IRequestJournal, InMemoryRequestJournal>();

        // Persistence (G16): no-op by default (purely in-memory). A file/db-backed provider is
        // registered on top (e.g. by MockifyrHost when --root-dir is set) and wins the resolution.
        services.AddSingleton<IStubPersistence, NullStubPersistence>();

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
        services.AddSingleton<IResponseRenderer>(_ => new TemplatingResponseRenderer(extensions.TemplateHelpers));

        // Serve-event listeners: the built-in webhook plus any user extensions.
        services.AddSingleton<IServeEventTemplateRenderer, WebhookTemplateRenderer>();
        services.AddSingleton<IServeEventListener>(sp =>
            new WebhookServeEventListener(client: null, sp.GetRequiredService<IServeEventTemplateRenderer>()));
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
        services.AddSingleton<ProxyResponder>(_ => new ProxyResponder());
        services.AddSingleton<StubRecorder>(_ => new StubRecorder());
        services.AddSingleton<RecordingSession>();

        // Management path: Mediant + the Application command/query handlers (scanned by assembly).
        services.AddMediant(typeof(CreateStubCommand).Assembly);
        return services;
    }
}
