using Mockifyr.Adapters.WireMockJson;
using Mockifyr.Core;
using Mockifyr.Stores.InMemory;
using Mockifyr.Templating;

namespace Mockifyr.Facade.Library;

/// <summary>
/// In-process composition of the engine for direct-call use (tests and the differential
/// harness) — the .NET analog of WireMock's "no HTTP server" mode. It wires the default
/// in-memory stores, the static renderer, and the engine behind a small facade.
/// </summary>
/// <remarks>G0 composition: static responses and exact matching. It grows with the roadmap.</remarks>
public sealed class MockifyrServer
{
    private readonly InMemoryStubStore _stubStore = new();
    private readonly StubEngine _engine;

    /// <summary>Creates a server with the default in-memory composition.</summary>
    public MockifyrServer()
    {
        _engine = new StubEngine(
            _stubStore,
            new TemplatingResponseRenderer(),
            new InMemoryScenarioStateStore(),
            new InMemoryRequestJournal(),
            serveEventListeners: []);
    }

    /// <summary>Imports WireMock stub mapping JSON into the given tenant (defaults to the default tenant).</summary>
    public void ImportWireMockJson(string json, TenantId? tenant = null)
    {
        var scope = tenant ?? TenantId.Default;
        foreach (var stub in WireMockMappingReader.Read(json, scope))
        {
            _stubStore.Put(stub);
        }
    }

    /// <summary>Handles a request within the given tenant (defaults to the default tenant).</summary>
    public StubResolution Handle(CanonicalRequest request, TenantId? tenant = null) =>
        _engine.Handle(tenant ?? TenantId.Default, request);
}
