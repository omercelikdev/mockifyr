using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using Mockifyr.Differential.Harness;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Differential validation of the remaining admin/transport behaviors (G12c): the scenarios admin API
/// (list state + `possibleStates`, set-state) and gzip response encoding. Driven over HTTP against
/// both the oracle and a hosted Mockifyr. Requires Docker.
/// </summary>
public sealed class G12cAdminTests : IAsyncLifetime
{
    private const string ScenarioBundle = """
        {"mappings":[
          {"scenarioName":"todo","requiredScenarioState":"Started","newScenarioState":"s2","request":{"method":"GET","url":"/s"},"response":{"status":200,"body":"first"}},
          {"scenarioName":"todo","requiredScenarioState":"s2","newScenarioState":"s3","request":{"method":"GET","url":"/s"},"response":{"status":200,"body":"second"}},
          {"scenarioName":"todo","requiredScenarioState":"s3","request":{"method":"GET","url":"/s"},"response":{"status":200,"body":"third"}}
        ]}
        """;

    private readonly WireMockOracle _oracle = new();
    private readonly MockifyrKestrelHost _mockifyr = new();

    public Task InitializeAsync() => _oracle.StartAsync();

    public async Task DisposeAsync()
    {
        await _mockifyr.DisposeAsync();
        await _oracle.DisposeAsync();
    }

    [Fact]
    public async Task Scenarios_Admin_MatchesTheOracle()
    {
        using var oracleClient = _oracle.CreateAdminClient();
        using var mockifyrClient = new HttpClient { BaseAddress = new Uri(_mockifyr.BaseAddress) };

        Assert.Equal(await DriveScenario(oracleClient), await DriveScenario(mockifyrClient));
    }

    [Fact]
    public async Task Gzip_MatchesTheOracle()
    {
        using var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.None };
        using var oracleClient = _oracle.CreateAdminClient();
        using var mockifyrClient = new HttpClient(handler) { BaseAddress = new Uri(_mockifyr.BaseAddress) };

        Assert.Equal(await DriveGzip(oracleClient), await DriveGzip(mockifyrClient));
    }

    // The observations compared between the two sides for the scenario walk.
    private static async Task<string> DriveScenario(HttpClient client)
    {
        await client.PostAsync("/__admin/mappings/reset", content: null);
        using (var import = Json(ScenarioBundle))
        {
            await client.PostAsync("/__admin/mappings/import", import);
        }

        var first = await Text(client.GetAsync("/s")); // Started -> s2

        using var scenarios = JsonDocument.Parse(await client.GetStringAsync("/__admin/scenarios"));
        var todo = scenarios.RootElement.GetProperty("scenarios").EnumerateArray().First(s => s.GetProperty("name").GetString() == "todo");
        var state = todo.GetProperty("state").GetString();
        var possible = string.Join(",", todo.GetProperty("possibleStates").EnumerateArray().Select(p => p.GetString()).OrderBy(x => x));

        using (var setState = Json("""{"state":"Started"}"""))
        {
            await client.PutAsync("/__admin/scenarios/todo/state", setState);
        }

        var afterReset = await Text(client.GetAsync("/s")); // back at Started -> "first"

        return $"first={first} state={state} possible=[{possible}] afterReset={afterReset}";
    }

    private static async Task<string> DriveGzip(HttpClient client)
    {
        await client.PostAsync("/__admin/mappings/reset", content: null);
        using (var load = Json("""{"request":{"method":"GET","url":"/gz"},"response":{"status":200,"headers":{"Content-Type":"text/plain"},"body":"hello gzip world hello gzip world hello gzip world"}}"""))
        {
            await client.PostAsync("/__admin/mappings", load);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "/gz");
        request.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip");
        using var response = await client.SendAsync(request);

        var encoding = response.Content.Headers.ContentEncoding.FirstOrDefault() ?? "<none>";
        var raw = await response.Content.ReadAsByteArrayAsync();
        var decoded = string.Equals(encoding, "gzip", StringComparison.OrdinalIgnoreCase) ? Gunzip(raw) : Encoding.UTF8.GetString(raw);

        return $"encoding={encoding} body={decoded}";
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private static async Task<string> Text(Task<HttpResponseMessage> send)
    {
        using var response = await send;
        return await response.Content.ReadAsStringAsync();
    }

    private static string Gunzip(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
