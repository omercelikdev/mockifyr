using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Mockifyr.Server;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Validation of embedded dashboard serving (G12g): with <c>--dashboard &lt;dir&gt;</c> the host serves the
/// built UI under the reserved <c>/__mockifyr</c> prefix (static assets + an SPA fallback to index.html
/// for client routes), while every other path is still owned by the mock-serving catch-all. Mockifyr-
/// specific (no oracle), so a self-test. No Docker required.
/// </summary>
public sealed class G12gDashboardTests : IAsyncLifetime
{
    private readonly string _dir = Directory.CreateTempSubdirectory("mockifyr-dash-").FullName;
    private WebApplication? _host;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        File.WriteAllText(Path.Combine(_dir, "index.html"), "<!doctype html><title>Mockifyr</title><div id=root></div>");
        _host = MockifyrHost.Build(["--port", "0", "--https-port", "0", "--dashboard", _dir]);
        await _host.StartAsync();

        var address = _host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses
            .First(a => a.StartsWith("http://", StringComparison.Ordinal))
            .Replace("[::]", "127.0.0.1").Replace("0.0.0.0", "127.0.0.1");
        _client = new HttpClient { BaseAddress = new Uri(address) };
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_host is not null) await _host.DisposeAsync();
        Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public async Task Dashboard_IsServedUnderReservedPrefix_WithoutBreakingMockServing()
    {
        // The dashboard index is served at the prefix root.
        var index = await _client!.GetAsync("/__mockifyr/");
        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
        Assert.Contains("text/html", index.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Mockifyr", await index.Content.ReadAsStringAsync());

        // A client-side route falls back to index.html (SPA).
        var route = await _client.GetAsync("/__mockifyr/stubs");
        Assert.Equal(HttpStatusCode.OK, route.StatusCode);
        Assert.Contains("id=root", await route.Content.ReadAsStringAsync());

        // Every other path is still the mock-serving catch-all — no stub loaded, so a 404 (not the SPA).
        var mocked = await _client.GetAsync("/api/anything");
        Assert.Equal(HttpStatusCode.NotFound, mocked.StatusCode);
    }
}
