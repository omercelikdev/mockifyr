using System.Diagnostics;
using System.Text;
using Mockifyr.Differential.Harness;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Differential validation of fault injection over the wire (G12b). All four <c>fault</c> kinds
/// surface to an HTTP client as a failed request (a broken connection), so the outcome is compared as
/// <em>request failed vs succeeded</em> against the oracle — both sides must fail on a fault stub and
/// both must succeed on a control. Uses a real Kestrel socket (faults need genuine transport).
/// Requires Docker.
/// </summary>
public sealed class G12bFaultTests : IAsyncLifetime
{
    private readonly WireMockOracle _oracle = new();
    private readonly MockifyrKestrelHost _mockifyr = new();

    public Task InitializeAsync() => _oracle.StartAsync();

    public async Task DisposeAsync()
    {
        await _mockifyr.DisposeAsync();
        await _oracle.DisposeAsync();
    }

    [Theory]
    [InlineData("EMPTY_RESPONSE", true)]
    [InlineData("MALFORMED_RESPONSE_CHUNK", true)]
    [InlineData("RANDOM_DATA_THEN_CLOSE", true)]
    [InlineData("CONNECTION_RESET_BY_PEER", true)]
    [InlineData("", false)] // control: a normal response, no fault
    public async Task Fault_BreaksTheConnection_LikeTheOracle(string fault, bool expectFailure)
    {
        var stub = fault.Length == 0
            ? """{"request":{"method":"GET","url":"/f"},"response":{"status":200,"body":"ok"}}"""
            : "{\"request\":{\"method\":\"GET\",\"url\":\"/f\"},\"response\":{\"fault\":\"" + fault + "\"}}";

        using var oracleClient = _oracle.CreateAdminClient();
        using var mockifyrClient = new HttpClient { BaseAddress = new Uri(_mockifyr.BaseAddress) };

        var oracleFailed = await RequestFailed(oracleClient, stub, "/f");
        var mockifyrFailed = await RequestFailed(mockifyrClient, stub, "/f");

        Assert.Equal(expectFailure, oracleFailed);   // the oracle behaves as documented (real WireMock)
        Assert.Equal(oracleFailed, mockifyrFailed);  // and Mockifyr matches it
    }

    [Fact]
    public async Task UniformDelayDistribution_AppliesAtLeastTheLowerBound_LikeTheOracle()
    {
        const int lower = 400;
        var stub = "{\"request\":{\"method\":\"GET\",\"url\":\"/dd\"},\"response\":{\"status\":200,\"body\":\"ok\"," +
                   "\"delayDistribution\":{\"type\":\"uniform\",\"lower\":" + lower + ",\"upper\":500}}}";

        using var oracleClient = _oracle.CreateAdminClient();
        using var mockifyrClient = new HttpClient { BaseAddress = new Uri(_mockifyr.BaseAddress) };

        var oracleMs = await TimeRequest(oracleClient, stub, "/dd");
        var mockifyrMs = await TimeRequest(mockifyrClient, stub, "/dd");

        // A uniform delay can never be shorter than its lower bound — a robust lower-bound assertion.
        const long bound = 350; // < lower, generous for scheduling/CI variance.
        Assert.True(oracleMs >= bound, $"oracle applied no delay ({oracleMs}ms)");
        Assert.True(mockifyrMs >= bound, $"mockifyr applied no delay ({mockifyrMs}ms)");
    }

    private static async Task<long> TimeRequest(HttpClient client, string stubJson, string url)
    {
        await client.PostAsync("/__admin/mappings/reset", content: null);
        using (var load = new StringContent(stubJson, Encoding.UTF8, "application/json"))
        {
            await client.PostAsync("/__admin/mappings", load);
        }

        var timer = Stopwatch.StartNew();
        using var response = await client.GetAsync(url);
        _ = await response.Content.ReadAsByteArrayAsync();
        timer.Stop();
        return timer.ElapsedMilliseconds;
    }

    private static async Task<bool> RequestFailed(HttpClient client, string stubJson, string url)
    {
        await client.PostAsync("/__admin/mappings/reset", content: null);
        using (var load = new StringContent(stubJson, Encoding.UTF8, "application/json"))
        {
            await client.PostAsync("/__admin/mappings", load);
        }

        try
        {
            using var response = await client.GetAsync(url);
            _ = await response.Content.ReadAsByteArrayAsync();
            return false;
        }
        catch (HttpRequestException)
        {
            return true;
        }
    }
}
