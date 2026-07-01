namespace Mockifyr.Differential.Harness;

/// <summary>The full outcome of a differential case: the diff plus both snapshots for reporting.</summary>
public sealed record DifferentialOutcome(
    DiffResult Diff,
    HttpResponseSnapshot Oracle,
    HttpResponseSnapshot Mockifyr);

/// <summary>
/// Orchestrates a differential case end to end: load the same WireMock JSON into the oracle and
/// into Mockifyr, replay the same request against both, and diff the responses. Reuses a single
/// oracle container across cases.
/// </summary>
public sealed class DifferentialRunner : IAsyncDisposable
{
    private readonly WireMockOracle _oracle = new();

    /// <summary>Starts the oracle container.</summary>
    public Task StartAsync() => _oracle.StartAsync();

    /// <summary>Runs a single case and returns the diff outcome.</summary>
    public async Task<DifferentialOutcome> RunAsync(string wireMockJson, RequestSpec request)
    {
        await _oracle.ResetAsync();
        await _oracle.LoadMappingAsync(wireMockJson);
        var oracleResponse = await _oracle.SendAsync(request);

        var mockifyr = new MockifyrUnderTest();
        mockifyr.ImportWireMockJson(wireMockJson);
        var mockifyrResponse = mockifyr.Send(request);

        var diff = ResponseDiffer.Compare(oracleResponse, mockifyrResponse, mockifyrResponse.Headers.Keys);
        return new DifferentialOutcome(diff, oracleResponse, mockifyrResponse);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _oracle.DisposeAsync();
}
