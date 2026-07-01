namespace Mockifyr.Core;

/// <summary>A stub that did not match, with its distance, for near-miss diagnostics.</summary>
public sealed record NearMiss(StubMapping Stub, double Distance);

/// <summary>The outcome of handling a request: a matched response, or the ranked near-misses.</summary>
public sealed record StubResolution
{
    /// <summary>Whether a stub matched.</summary>
    public required bool Matched { get; init; }

    /// <summary>The response to serve, when matched.</summary>
    public CanonicalResponse? Response { get; init; }

    /// <summary>The stub that matched, when matched.</summary>
    public StubMapping? MatchedStub { get; init; }

    /// <summary>The closest non-matching stubs, when not matched.</summary>
    public required IReadOnlyList<NearMiss> NearMisses { get; init; }
}

/// <summary>
/// The transport-agnostic core coordinator. It owns no matching or templating logic itself;
/// it orchestrates the contracts. It performs no I/O and is fully deterministic, which is the
/// precondition for differential testing. See ARCHITECTURE.md sections 4-5.
/// </summary>
/// <remarks>
/// This is a skeleton. The request lifecycle (stub selection by priority/recency, scenario
/// eligibility and transition, rendering, journaling, and serve-event dispatch) is implemented
/// starting at roadmap item G1a, each step validated against the WireMock oracle.
/// </remarks>
public sealed class StubEngine
{
    private readonly IStubStore _stubStore;
    private readonly IResponseRenderer _renderer;
    private readonly IScenarioStateStore _scenarioStore;
    private readonly IRequestJournal _journal;
    private readonly IReadOnlyList<IServeEventListener> _serveEventListeners;

    /// <summary>Creates the engine with its collaborators.</summary>
    public StubEngine(
        IStubStore stubStore,
        IResponseRenderer renderer,
        IScenarioStateStore scenarioStore,
        IRequestJournal journal,
        IEnumerable<IServeEventListener> serveEventListeners)
    {
        _stubStore = stubStore;
        _renderer = renderer;
        _scenarioStore = scenarioStore;
        _journal = journal;
        _serveEventListeners = [.. serveEventListeners];
    }

    /// <summary>
    /// Resolves a request within a tenant scope to a response (or near-misses). Matching always
    /// runs inside the given <paramref name="tenant"/>; a tenant can never see another's stubs.
    /// </summary>
    /// <exception cref="NotImplementedException">
    /// The lifecycle is implemented from roadmap item G1a onward.
    /// </exception>
    public StubResolution Handle(TenantId tenant, CanonicalRequest request)
    {
        _ = tenant;
        _ = request;
        throw new NotImplementedException(
            "StubEngine.Handle is implemented from roadmap item G1a, validated against the WireMock oracle.");
    }
}
