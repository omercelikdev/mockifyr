using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Mockifyr.Differential.Generator;
using Mockifyr.Differential.Harness;
using Mockifyr.Server;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Differential validation of <c>--global-response-templating</c> (#148): both sides start with the
/// flag, load a mapping whose body carries <c>{{…}}</c> template expressions but NO per-stub
/// <c>transformers</c>, and must serve byte-identical rendered responses. This is the export shape
/// modern WireMock hosts produce (templating globally on, so the flag never lands in the mapping).
/// Requires Docker.
/// </summary>
public sealed class G2iGlobalTemplatingTests : IAsyncLifetime
{
    private const string StubJson =
        """
        {"request":{"method":"POST","urlPath":"/gt"},
         "response":{"status":200,"headers":{"X-Echo":"{{request.query.q}}"},
                     "body":"m={{request.method}} v={{jsonPath request.body '$.v'}}"}}
        """;

    private static readonly RequestSpec Request = new()
    {
        Method = "POST",
        Url = "/gt?q=hello",
        Headers = [new("Content-Type", "application/json")],
        Body = Encoding.UTF8.GetBytes("""{"v":"x1"}"""),
    };

    private readonly WireMockOracle _oracle = new("--global-response-templating");

    public Task InitializeAsync() => _oracle.StartAsync();

    public async Task DisposeAsync() => await _oracle.DisposeAsync();

    [Fact]
    public async Task TemplatedBodyWithoutTransformers_RendersIdentically()
    {
        await _oracle.LoadMappingAsync(StubJson);
        var oracle = await _oracle.SendAsync(Request);

        var app = MockifyrHost.Build(["--port", "0", "--global-response-templating", "true"]);
        await app.StartAsync();
        await using (app)
        {
            using var client = Client(app);
            using var content = new StringContent("""{"v":"x1"}""", Encoding.UTF8, "application/json");
            using var created = await client.PostAsync("/__admin/mappings",
                new StringContent(StubJson, Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);

            using var served = await client.PostAsync("/gt?q=hello", content);
            var body = await served.Content.ReadAsByteArrayAsync();

            Assert.Equal(oracle.Status, (int)served.StatusCode);
            Assert.True(oracle.Body.AsSpan().SequenceEqual(body),
                $"body oracle=\"{Encoding.UTF8.GetString(oracle.Body)}\" mockifyr=\"{Encoding.UTF8.GetString(body)}\"");

            var oracleEcho = oracle.Headers.FirstOrDefault(h => h.Key.Equals("X-Echo", StringComparison.OrdinalIgnoreCase));
            var servedEcho = served.Headers.TryGetValues("X-Echo", out var values) ? string.Join(",", values) : null;
            Assert.Equal(oracleEcho.Value?.FirstOrDefault(), servedEcho);
        }
    }

    [Fact]
    public async Task WithoutTheFlag_TemplateTextStaysLiteral()
    {
        // The opt-in default must not change: without the flag (and without transformers) the body
        // is served verbatim — matching the oracle run WITHOUT --global-response-templating, which
        // the existing G2 suite already covers. Structural check here: the template stays literal.
        var app = MockifyrHost.Build(["--port", "0"]);
        await app.StartAsync();
        await using (app)
        {
            using var client = Client(app);
            using var created = await client.PostAsync("/__admin/mappings",
                new StringContent(StubJson, Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);

            using var content = new StringContent("""{"v":"x1"}""", Encoding.UTF8, "application/json");
            using var served = await client.PostAsync("/gt?q=hello", content);
            var body = await served.Content.ReadAsStringAsync();
            Assert.Contains("{{request.method}}", body);
        }
    }

    private static HttpClient Client(WebApplication app)
    {
        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses
            .First(a => a.StartsWith("http://", StringComparison.Ordinal))
            .Replace("[::]", "127.0.0.1").Replace("0.0.0.0", "127.0.0.1");
        return new HttpClient { BaseAddress = new Uri(address) };
    }
}
