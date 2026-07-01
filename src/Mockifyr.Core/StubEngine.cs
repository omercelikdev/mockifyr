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
    public StubResolution Handle(TenantId tenant, CanonicalRequest request)
    {
        var input = new MatchInput { Request = request };
        var stubs = _stubStore.GetStubs(tenant); // ISOLATION: only this tenant's stubs are visible.

        var scored = new List<(StubMapping Stub, MatchResult Result, int Index)>(stubs.Count);
        for (var i = 0; i < stubs.Count; i++)
        {
            var stub = stubs[i];
            if (!IsEligible(tenant, stub))
            {
                continue;
            }

            scored.Add((stub, Evaluate(stub.Request, input), i));
        }

        var exact = scored.Where(x => x.Result.IsExactMatch).ToList();
        if (exact.Count > 0)
        {
            // Lower priority wins; ties broken by recency (last added wins).
            var winner = exact.OrderBy(x => x.Stub.Priority).ThenByDescending(x => x.Index).First().Stub;
            var response = _renderer.Render(winner.Response, new RenderContext { Request = request });

            ApplyTransition(tenant, winner);
            DispatchServeEvent(tenant, request, winner, response);

            return new StubResolution { Matched = true, Response = response, MatchedStub = winner, NearMisses = [] };
        }

        var nearMisses = scored
            .OrderBy(x => x.Result.Distance)
            .Take(3)
            .Select(x => new NearMiss(x.Stub, x.Result.Distance))
            .ToList();

        DispatchServeEvent(tenant, request, matchedStub: null, response: null);

        return new StubResolution { Matched = false, NearMisses = nearMisses };
    }

    private bool IsEligible(TenantId tenant, StubMapping stub)
    {
        if (stub.Scenario is not { } scenario)
        {
            return true;
        }

        if (scenario.RequiredState is null)
        {
            return true;
        }

        return string.Equals(
            _scenarioStore.GetState(tenant, scenario.ScenarioName),
            scenario.RequiredState,
            StringComparison.Ordinal);
    }

    private void ApplyTransition(TenantId tenant, StubMapping stub)
    {
        if (stub.Scenario is { NewState: { } newState } scenario)
        {
            _scenarioStore.SetState(tenant, scenario.ScenarioName, newState);
        }
    }

    private void DispatchServeEvent(TenantId tenant, CanonicalRequest request, StubMapping? matchedStub, CanonicalResponse? response)
    {
        var serveEvent = new ServeEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenant,
            Request = request,
            MatchedStub = matchedStub,
            Response = response,
            SubEvents = [],
        };

        _journal.Record(serveEvent);

        foreach (var listener in _serveEventListeners)
        {
            // Fire-and-forget: outbound I/O (e.g. webhooks) must not block serving. Full
            // correlation and error handling arrive at G3.
            _ = listener.OnServeEventAsync(serveEvent, CancellationToken.None);
        }
    }

    private static MatchResult Evaluate(RequestPattern pattern, MatchInput input)
    {
        var exact = true;
        var distance = 0d;

        foreach (var matcher in EnumerateMatchers(pattern))
        {
            var result = matcher.Match(input);
            if (!result.IsExactMatch)
            {
                exact = false;
            }

            distance += result.Distance;
        }

        return exact ? MatchResult.Exact : MatchResult.NoMatch(distance);
    }

    private static IEnumerable<IMatcher> EnumerateMatchers(RequestPattern pattern)
    {
        if (pattern.Url is not null)
        {
            yield return pattern.Url;
        }

        if (pattern.Method is not null)
        {
            yield return pattern.Method;
        }

        foreach (var matcher in pattern.Headers)
        {
            yield return matcher;
        }

        foreach (var matcher in pattern.Query)
        {
            yield return matcher;
        }

        foreach (var matcher in pattern.Cookies)
        {
            yield return matcher;
        }

        foreach (var matcher in pattern.Body)
        {
            yield return matcher;
        }
    }
}
