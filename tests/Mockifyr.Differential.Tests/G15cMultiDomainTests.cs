using Mockifyr.Differential.Generator;
using Mockifyr.Differential.Harness;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// G15c gate: WireMock's multi-domain request matching — <c>request.host</c>, <c>request.port</c>,
/// and <c>request.scheme</c> — diffed against the real oracle. One instance serves many domains;
/// the host/port derive from the <c>Host</c> header and the scheme from the listener the request
/// arrives on. Requires Docker. See docs/parity/g15-extras.md.
/// </summary>
public sealed class G15cMultiDomainTests : IAsyncLifetime
{
    private readonly DifferentialRunner _runner = new();

    public Task InitializeAsync() => _runner.StartAsync();

    public async Task DisposeAsync() => await _runner.DisposeAsync();

    private static KeyValuePair<string, string> Host(string value) => new("Host", value);

    [Fact]
    public async Task Host_EqualTo_Matches_WhenHostHeaderMatches()
    {
        const string mapping =
            """
            {
              "request": { "method": "GET", "urlPath": "/r", "host": { "equalTo": "shop.internal" } },
              "response": { "status": 200, "body": "shop" }
            }
            """;

        var outcome = await _runner.RunAsync(
            mapping, new RequestSpec { Method = "GET", Url = "/r", Headers = [Host("shop.internal")] });

        Assert.Equal(200, outcome.Oracle.Status);
        Assert.Equal("shop", outcome.Oracle.BodyAsText);
        Assert.True(outcome.Diff.IsMatch, outcome.Diff.Report);
    }

    [Fact]
    public async Task Host_EqualTo_NoMatch_WhenHostHeaderDiffers()
    {
        const string mapping =
            """
            {
              "request": { "method": "GET", "urlPath": "/r", "host": { "equalTo": "shop.internal" } },
              "response": { "status": 200, "body": "shop" }
            }
            """;

        var outcome = await _runner.RunAsync(
            mapping, new RequestSpec { Method = "GET", Url = "/r", Headers = [Host("other.internal")] });

        Assert.Equal(404, outcome.Oracle.Status);
        Assert.Equal(404, outcome.Mockifyr.Status);
    }

    [Fact]
    public async Task MultiDomain_RoutesByHost_ToPerDomainResponses()
    {
        // Two stubs identical but for the host: the headline multi-domain use case.
        const string mappings =
            """
            {
              "mappings": [
                {
                  "request": { "method": "GET", "urlPath": "/", "host": { "equalTo": "a.example" } },
                  "response": { "status": 200, "body": "domain-a" }
                },
                {
                  "request": { "method": "GET", "urlPath": "/", "host": { "equalTo": "b.example" } },
                  "response": { "status": 200, "body": "domain-b" }
                }
              ]
            }
            """;

        await _runner.LoadAsync(mappings);

        var toA = await _runner.ProbeAsync(new RequestSpec { Method = "GET", Url = "/", Headers = [Host("a.example")] });
        var toB = await _runner.ProbeAsync(new RequestSpec { Method = "GET", Url = "/", Headers = [Host("b.example")] });

        Assert.Equal("domain-a", toA.Oracle.BodyAsText);
        Assert.Equal("domain-b", toB.Oracle.BodyAsText);
        Assert.True(toA.Diff.IsMatch, toA.Diff.Report);
        Assert.True(toB.Diff.IsMatch, toB.Diff.Report);
    }

    [Fact]
    public async Task Host_Matches_Regex()
    {
        // host is a StringValuePattern, so `matches` (regex) applies just like a header matcher.
        const string mapping =
            """
            {
              "request": { "method": "GET", "urlPath": "/r", "host": { "matches": ".*\\.example" } },
              "response": { "status": 200, "body": "any-example" }
            }
            """;

        var match = await _runner.RunAsync(
            mapping, new RequestSpec { Method = "GET", Url = "/r", Headers = [Host("tenant7.example")] });
        Assert.True(match.Diff.IsMatch, match.Diff.Report);
        Assert.Equal(200, match.Oracle.Status);

        var noMatch = await _runner.RunAsync(
            mapping, new RequestSpec { Method = "GET", Url = "/r", Headers = [Host("tenant7.other")] });
        Assert.Equal(404, noMatch.Oracle.Status);
        Assert.Equal(404, noMatch.Mockifyr.Status);
    }

    [Fact]
    public async Task Port_Matches_WhenHostHeaderPortMatches()
    {
        const string mapping =
            """
            {
              "request": { "method": "GET", "urlPath": "/r", "port": 4321 },
              "response": { "status": 200, "body": "on-4321" }
            }
            """;

        var match = await _runner.RunAsync(
            mapping, new RequestSpec { Method = "GET", Url = "/r", Headers = [Host("svc.internal:4321")] });
        Assert.True(match.Diff.IsMatch, match.Diff.Report);
        Assert.Equal(200, match.Oracle.Status);

        var noMatch = await _runner.RunAsync(
            mapping, new RequestSpec { Method = "GET", Url = "/r", Headers = [Host("svc.internal:9999")] });
        Assert.Equal(404, noMatch.Oracle.Status);
        Assert.Equal(404, noMatch.Mockifyr.Status);
    }

    [Fact]
    public async Task Scheme_Http_Matches_OverHttp()
    {
        const string mapping =
            """
            {
              "request": { "method": "GET", "urlPath": "/r", "scheme": "http" },
              "response": { "status": 200, "body": "plaintext" }
            }
            """;

        await _runner.LoadAsync(mapping);
        var outcome = await _runner.ProbeSchemeAsync(
            new RequestSpec { Method = "GET", Url = "/r" }, https: false);

        Assert.Equal(200, outcome.Oracle.Status);
        Assert.Equal("plaintext", outcome.Oracle.BodyAsText);
        Assert.True(outcome.Diff.IsMatch, outcome.Diff.Report);
    }

    [Fact]
    public async Task Scheme_Https_NoMatch_OverHttp()
    {
        const string mapping =
            """
            {
              "request": { "method": "GET", "urlPath": "/r", "scheme": "https" },
              "response": { "status": 200, "body": "secure" }
            }
            """;

        await _runner.LoadAsync(mapping);
        var outcome = await _runner.ProbeSchemeAsync(
            new RequestSpec { Method = "GET", Url = "/r" }, https: false);

        Assert.Equal(404, outcome.Oracle.Status);
        Assert.Equal(404, outcome.Mockifyr.Status);
    }

    [Fact]
    public async Task Scheme_Https_Matches_OverHttps()
    {
        const string mapping =
            """
            {
              "request": { "method": "GET", "urlPath": "/r", "scheme": "https" },
              "response": { "status": 200, "body": "secure" }
            }
            """;

        await _runner.LoadAsync(mapping);
        var outcome = await _runner.ProbeSchemeAsync(
            new RequestSpec { Method = "GET", Url = "/r" }, https: true);

        Assert.Equal(200, outcome.Oracle.Status);
        Assert.Equal("secure", outcome.Oracle.BodyAsText);
        Assert.True(outcome.Diff.IsMatch, outcome.Diff.Report);
    }
}
