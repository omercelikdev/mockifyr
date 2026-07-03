using Mediant.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Mockifyr.Application;
using Mockifyr.Core;
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
    public static IServiceCollection AddMockifyr(this IServiceCollection services)
    {
        // Shared singletons — the management path and the serving hot path see the same state.
        services.AddSingleton<InMemoryStubStore>();
        services.AddSingleton<IStubStore>(sp => sp.GetRequiredService<InMemoryStubStore>());
        services.AddSingleton<IScenarioStateStore, InMemoryScenarioStateStore>();
        services.AddSingleton<IRequestJournal, InMemoryRequestJournal>();
        services.AddSingleton<IResponseRenderer, TemplatingResponseRenderer>();
        services.AddSingleton<IServeEventTemplateRenderer, WebhookTemplateRenderer>();
        services.AddSingleton<IServeEventListener>(sp =>
            new WebhookServeEventListener(client: null, sp.GetRequiredService<IServeEventTemplateRenderer>()));
        services.AddSingleton(sp => new StubEngine(
            sp.GetRequiredService<IStubStore>(),
            sp.GetRequiredService<IResponseRenderer>(),
            sp.GetRequiredService<IScenarioStateStore>(),
            sp.GetRequiredService<IRequestJournal>(),
            sp.GetServices<IServeEventListener>()));

        // Management path: Mediant + the Application command/query handlers (scanned by assembly).
        services.AddMediant(typeof(CreateStubCommand).Assembly);
        return services;
    }
}
