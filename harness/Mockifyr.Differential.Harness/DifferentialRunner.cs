using Mockifyr.Differential.Generator;

namespace Mockifyr.Differential.Harness;

/// <summary>The full outcome of a differential case: the diff plus both snapshots for reporting.</summary>
public sealed record DifferentialOutcome(
    DiffResult Diff,
    HttpResponseSnapshot Oracle,
    HttpResponseSnapshot Mockifyr);

/// <summary>The outcome of a single probe against a loaded scenario.</summary>
public sealed record ProbeOutcome(HttpResponseSnapshot Oracle, HttpResponseSnapshot Mockifyr, DiffResult Diff)
{
    /// <summary>Whether the oracle served a stub (any status other than 404 no-match).</summary>
    public bool OracleMatched => Oracle.Status != 404;

    /// <summary>Whether Mockifyr served a stub.</summary>
    public bool MockifyrMatched => Mockifyr.Status != 404;

    /// <summary>Whether both sides agree on the match/no-match decision.</summary>
    public bool DecisionAgrees => OracleMatched == MockifyrMatched;
}

/// <summary>
/// Orchestrates differential cases: load the same WireMock JSON into the oracle and Mockifyr,
/// replay the same request(s), and diff. Reuses a single oracle container across cases; for
/// fuzzing it loads a stub once and sends many probes.
/// </summary>
public sealed class DifferentialRunner : IAsyncDisposable
{
    private readonly WireMockOracle _oracle = new();
    private MockifyrUnderTest? _mockifyr;

    /// <summary>Starts the oracle container.</summary>
    public Task StartAsync() => _oracle.StartAsync();

    /// <summary>Runs a single case (reset + load + send) and returns the diff outcome.</summary>
    public async Task<DifferentialOutcome> RunAsync(string wireMockJson, RequestSpec request)
    {
        await LoadAsync(wireMockJson);
        var probe = await ProbeAsync(request);
        return new DifferentialOutcome(probe.Diff, probe.Oracle, probe.Mockifyr);
    }

    /// <summary>Resets the oracle and loads one stub mapping into both sides (for send-many fuzzing).</summary>
    public async Task LoadAsync(string wireMockJson)
    {
        await _oracle.ResetAsync();
        await _oracle.LoadMappingAsync(wireMockJson);
        _mockifyr = new MockifyrUnderTest();
        _mockifyr.ImportWireMockJson(wireMockJson);
    }

    /// <summary>Replays one request against the currently loaded stub on both sides and diffs.</summary>
    public async Task<ProbeOutcome> ProbeAsync(RequestSpec request)
    {
        if (_mockifyr is null)
        {
            throw new InvalidOperationException("Call LoadAsync before ProbeAsync.");
        }

        var oracle = await _oracle.SendAsync(request);
        var mockifyr = _mockifyr.Send(request);
        var diff = ResponseDiffer.Compare(oracle, mockifyr, mockifyr.Headers.Keys);
        return new ProbeOutcome(oracle, mockifyr, diff);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _oracle.DisposeAsync();
}
