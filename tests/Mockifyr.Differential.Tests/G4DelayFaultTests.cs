using System.Linq;
using Mockifyr.Adapters.WireMockJson;
using Mockifyr.Core;
using Mockifyr.Differential.Generator;
using Mockifyr.Differential.Harness;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Parsing of the G4 delay/fault directives (pure — no oracle). Faults are socket-level behaviors
/// the transport facade (G12) emits, so they can't be diffed in-process; here we verify the engine
/// records the directive so G12 has something to apply.
/// </summary>
public sealed class G4DirectiveParsingTests
{
    [Fact]
    public void FixedDelay_IsParsedOntoTheResponse()
    {
        const string json = """
            {"request":{"method":"GET","url":"/d"},"response":{"status":200,"body":"x","fixedDelayMilliseconds":250}}
            """;

        var stub = WireMockMappingReader.Read(json, TenantId.Default).Single();

        Assert.Equal(250, stub.Response.Delay?.Milliseconds);
    }

    [Theory]
    [InlineData("EMPTY_RESPONSE", FaultKind.EmptyResponse)]
    [InlineData("MALFORMED_RESPONSE_CHUNK", FaultKind.MalformedResponseChunk)]
    [InlineData("RANDOM_DATA_THEN_CLOSE", FaultKind.RandomDataThenClose)]
    [InlineData("CONNECTION_RESET_BY_PEER", FaultKind.ConnectionResetByPeer)]
    public void Fault_IsParsedOntoTheResponse(string wireMockName, FaultKind expected)
    {
        var json = "{\"request\":{\"method\":\"GET\",\"url\":\"/f\"},\"response\":{\"fault\":\"" +
                   wireMockName + "\"}}";

        var stub = WireMockMappingReader.Read(json, TenantId.Default).Single();

        Assert.Equal(expected, stub.Response.Fault?.Kind);
    }
}

/// <summary>
/// Differential validation of the response <c>fixedDelay</c> (G4): the delayed response's content
/// still matches the oracle, and both sides take at least the requested delay. Only a generous lower
/// bound is asserted — a fixed delay can never make a response faster, so this is robust against CI
/// timing noise while still catching a delay that isn't applied. Requires Docker.
/// </summary>
public sealed class G4DelayTests : IAsyncLifetime
{
    private readonly DifferentialRunner _runner = new();

    public Task InitializeAsync() => _runner.StartAsync();

    public async Task DisposeAsync() => await _runner.DisposeAsync();

    [Fact]
    public async Task FixedDelay_ContentParityAndTiming()
    {
        const int delayMs = 400;
        var json = "{\"request\":{\"method\":\"GET\",\"url\":\"/delay\"}," +
                   "\"response\":{\"status\":200,\"body\":\"delayed\",\"fixedDelayMilliseconds\":" + delayMs + "}}";

        await _runner.LoadAsync(json);
        var (outcome, oracleMs, mockifyrMs) =
            await _runner.ProbeTimedAsync(new RequestSpec { Method = "GET", Url = "/delay" });

        Assert.True(outcome.OracleMatched, "oracle did not serve the stub");
        Assert.True(outcome.Diff.IsMatch, $"delayed response content diverged: {outcome.Diff.Report}");

        const long lowerBound = 300; // < delayMs, generous for scheduling/CI variance.
        Assert.True(oracleMs >= lowerBound, $"the oracle applied no delay (took {oracleMs}ms)");
        Assert.True(mockifyrMs >= lowerBound, $"mockifyr applied no delay (took {mockifyrMs}ms)");
    }
}
