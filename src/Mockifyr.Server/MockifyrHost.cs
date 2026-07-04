using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mockifyr.Core;
using Mockifyr.Facade.Admin;
using Mockifyr.Facade.Http;

namespace Mockifyr.Server;

/// <summary>
/// The standalone-host composition (G12f). Turns command-line/config into a runnable Mockifyr host:
/// binds the mock-serving port (<c>--port</c>, WireMock's default <c>8080</c>) and, when a
/// <c>--root-dir</c> is given, loads its <c>mappings/*.json</c> into the default tenant at startup via
/// the <see cref="IMappingsLoader"/> seam. Kept separate from <c>Program</c> so the same wiring is
/// exercised by tests (which drive it on an ephemeral port with a temp root-dir).
/// </summary>
public static class MockifyrHost
{
    /// <summary>WireMock's default mock-serving port.</summary>
    public const int DefaultPort = 8080;

    /// <summary>
    /// Builds the standalone host from <paramref name="args"/> (config keys <c>port</c> and
    /// <c>root-dir</c>, supplied as <c>--port</c>/<c>--root-dir</c>). The returned app is built but not
    /// started; startup mappings have already been applied to the store.
    /// </summary>
    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddMockifyr();

        // A root-dir registers a directory loader for <root-dir>/mappings, resolved after the matcher
        // registry exists (customMatcher references in files resolve against it).
        var rootDir = builder.Configuration["root-dir"];
        if (!string.IsNullOrWhiteSpace(rootDir))
        {
            var mappingsDir = Path.Combine(rootDir, "mappings");
            builder.Services.AddSingleton<IMappingsLoader>(sp =>
                new DirectoryMappingsLoader(mappingsDir, sp.GetRequiredService<IMatcherRegistry>()));
        }

        var app = builder.Build();

        // Bind the mock-serving port. Port 0 asks Kestrel for an ephemeral port (used by tests).
        var port = builder.Configuration.GetValue("port", DefaultPort);
        app.Urls.Add($"http://0.0.0.0:{port}");

        app.MapAdminEndpoints();
        app.MapMockServing();

        ApplyStartupMappings(app);
        return app;
    }

    /// <summary>Loads every registered <see cref="IMappingsLoader"/> into the store for the default tenant.</summary>
    private static void ApplyStartupMappings(WebApplication app)
    {
        var store = app.Services.GetRequiredService<IStubStore>();
        foreach (var loader in app.Services.GetServices<IMappingsLoader>())
        {
            foreach (var stub in loader.Load(TenantId.Default))
            {
                store.Put(stub);
            }
        }
    }
}
