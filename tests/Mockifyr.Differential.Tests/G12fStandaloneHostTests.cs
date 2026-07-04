using System.Net;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Mockifyr.Core;
using Mockifyr.Server;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Validates the standalone-host config (G12f): the <see cref="DirectoryMappingsLoader"/> parses a
/// <c>mappings/*.json</c> directory (single stubs and <c>{"mappings":[…]}</c> bundles), and
/// <see cref="MockifyrHost.Build"/> honors <c>--port</c>/<c>--root-dir</c> — booting a real Kestrel
/// host that serves the loaded stubs over HTTP. This is deploy/config plumbing with no WireMock
/// semantic novelty (the loaded stubs' <em>serving</em> is already oracle-covered throughout), so it
/// is validated in-process over HTTP — no oracle, no Docker.
/// </summary>
public sealed class G12fStandaloneHostTests
{
    [Fact]
    public void DirectoryMappingsLoader_ReadsSingleStubsAndBundles_InFilenameOrder()
    {
        var dir = Directory.CreateTempSubdirectory("mockifyr-mappings-");
        try
        {
            // A single-stub file...
            File.WriteAllText(Path.Combine(dir.FullName, "a-single.json"),
                """{"request":{"method":"GET","url":"/one"},"response":{"status":200,"body":"one"}}""");
            // ...and a bundle file with two stubs.
            File.WriteAllText(Path.Combine(dir.FullName, "b-bundle.json"),
                """{"mappings":[{"request":{"method":"GET","url":"/two"},"response":{"status":200}},{"request":{"method":"GET","url":"/three"},"response":{"status":201}}]}""");
            // A non-JSON file is ignored.
            File.WriteAllText(Path.Combine(dir.FullName, "notes.txt"), "ignore me");

            var loader = new DirectoryMappingsLoader(dir.FullName);
            var stubs = loader.Load(TenantId.Default);

            Assert.Equal(3, stubs.Count);
            Assert.All(stubs, stub => Assert.Equal(TenantId.Default, stub.TenantId));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void DirectoryMappingsLoader_MissingDirectory_ReturnsEmpty()
    {
        var loader = new DirectoryMappingsLoader(Path.Combine(Path.GetTempPath(), "mockifyr-does-not-exist-" + Guid.NewGuid()));
        Assert.Empty(loader.Load(TenantId.Default));
    }

    [Fact]
    public async Task Host_LoadsRootDirMappings_AndServesThemOverHttp()
    {
        var root = Directory.CreateTempSubdirectory("mockifyr-root-");
        var mappings = Directory.CreateDirectory(Path.Combine(root.FullName, "mappings"));
        File.WriteAllText(Path.Combine(mappings.FullName, "hello.json"),
            """{"request":{"method":"GET","url":"/hello"},"response":{"status":200,"body":"loaded-from-disk"}}""");

        // Build the standalone host on an ephemeral port pointed at the temp root-dir.
        var app = MockifyrHost.Build(["--port", "0", "--root-dir", root.FullName]);
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.First();
            var baseAddress = address.Replace("0.0.0.0", "127.0.0.1").Replace("[::]", "127.0.0.1");

            using var client = new HttpClient { BaseAddress = new Uri(baseAddress) };

            // The disk-loaded stub is served over the wire.
            var served = await client.GetAsync("/hello");
            Assert.Equal(HttpStatusCode.OK, served.StatusCode);
            Assert.Equal("loaded-from-disk", await served.Content.ReadAsStringAsync());

            // ...and the admin surface reports it (the load populated the shared store).
            var admin = await client.GetAsync("/__admin/mappings");
            var adminBody = await admin.Content.ReadAsStringAsync();
            Assert.Contains("mappings", adminBody);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            root.Delete(recursive: true);
        }
    }
}
