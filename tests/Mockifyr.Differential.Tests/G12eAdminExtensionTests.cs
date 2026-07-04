using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Mockifyr.Core;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Validates custom admin-API extension routing (G12e): a registered <see cref="IAdminApiExtension"/>
/// serves requests under <c>/__admin/ext/&lt;prefix&gt;/*</c>. Custom admin endpoints have no WireMock
/// equivalent, so — like the other extension seams (G10) — this is validated <em>in-process over
/// HTTP</em> (no oracle, no Docker): prefix routing, subpath + query + body lowering, the returned
/// status/content-type/body, and the unknown-prefix 404.
/// </summary>
public sealed class G12eAdminExtensionTests
{
    /// <summary>A sample extension exercising subpath, query, and body — the surface the facade lowers.</summary>
    private sealed class DemoAdminExtension : IAdminApiExtension
    {
        public string RoutePrefix => "demo";

        public Task<AdminApiResponse> HandleAsync(AdminApiRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(request.Subpath switch
            {
                "/ping" => AdminApiResponse.Json("{\"pong\":true}"),
                "/echo" => AdminApiResponse.Json("{\"query\":\"" + request.Query + "\"}"),
                "/upper" => new AdminApiResponse(201, "text/plain", Encoding.UTF8.GetBytes(
                    Encoding.UTF8.GetString(request.Body).ToUpperInvariant())),
                _ => AdminApiResponse.Json("{\"error\":\"unknown\"}", status: 404),
            });
    }

    private static WebApplicationFactory<Program> HostWith(IAdminApiExtension extension) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton(extension)));

    [Fact]
    public async Task RegisteredExtension_ServesUnderItsPrefix()
    {
        await using var factory = HostWith(new DemoAdminExtension());
        using var client = factory.CreateClient();

        // GET with a subpath → the extension's JSON response.
        var ping = await client.GetAsync("/__admin/ext/demo/ping");
        Assert.Equal(200, (int)ping.StatusCode);
        Assert.Equal("application/json", ping.Content.Headers.ContentType?.MediaType);
        Assert.Equal("{\"pong\":true}", await ping.Content.ReadAsStringAsync());

        // The raw query string (leading '?') is passed through verbatim.
        var echo = await client.GetAsync("/__admin/ext/demo/echo?x=1&y=2");
        Assert.Equal("{\"query\":\"?x=1&y=2\"}", await echo.Content.ReadAsStringAsync());

        // POST body is lowered to the extension, which sets its own status + content-type.
        using var content = new StringContent("hello", Encoding.UTF8, "text/plain");
        var upper = await client.PostAsync("/__admin/ext/demo/upper", content);
        Assert.Equal(201, (int)upper.StatusCode);
        Assert.Equal("text/plain", upper.Content.Headers.ContentType?.MediaType);
        Assert.Equal("HELLO", await upper.Content.ReadAsStringAsync());

        // A subpath the extension doesn't recognize → the extension's own 404 (it owns everything below).
        var unknown = await client.GetAsync("/__admin/ext/demo/nope");
        Assert.Equal(404, (int)unknown.StatusCode);
    }

    [Fact]
    public async Task UnknownPrefix_Returns404()
    {
        await using var factory = HostWith(new DemoAdminExtension());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/__admin/ext/missing/whatever");
        Assert.Equal(404, (int)response.StatusCode);
    }

    [Fact]
    public async Task ExtBaseWithNoRegistration_Returns404()
    {
        // No extension registered → the prefix resolves to nothing → 404 (routing doesn't crash).
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/__admin/ext/demo/ping");
        Assert.Equal(404, (int)response.StatusCode);
    }
}
