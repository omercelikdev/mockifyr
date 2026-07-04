using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mockifyr.Core;
using Mockifyr.Facade.Admin;
using Mockifyr.Facade.Http;

namespace Mockifyr.Server;

/// <summary>
/// The standalone-host composition (G12f + G11a). Turns command-line/config into a runnable Mockifyr
/// host: binds the mock-serving port (<c>--port</c>, WireMock's default <c>8080</c>), optionally an
/// HTTPS port (<c>--https-port</c>) with a self-signed certificate, and, when a <c>--root-dir</c> is
/// given, loads its <c>mappings/*.json</c> into the default tenant at startup via the
/// <see cref="IMappingsLoader"/> seam. Kept separate from <c>Program</c> so the same wiring is
/// exercised by tests (which drive it on ephemeral ports).
/// </summary>
public static class MockifyrHost
{
    /// <summary>WireMock's default mock-serving port.</summary>
    public const int DefaultPort = 8080;

    /// <summary>
    /// Builds the standalone host from <paramref name="args"/> (config keys <c>port</c>,
    /// <c>https-port</c>, <c>root-dir</c>, supplied as <c>--port</c>/<c>--https-port</c>/
    /// <c>--root-dir</c>). The returned app is built but not started; startup mappings have already been
    /// applied to the store.
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

            // A root-dir also makes stub mutations durable (G16a): they persist to the same mappings
            // directory the loader reads on startup. Registered last so it wins over the no-op default.
            builder.Services.AddSingleton<IStubPersistence>(new FileSystemStubPersistence(mappingsDir));
        }

        // LiteDB persistence (G16b): stubs persist to an embedded single-file database and reload on
        // startup. The LiteDatabase is a DI-created singleton so the container disposes it on shutdown
        // (flushing the file before the next process opens it).
        var liteDbPath = builder.Configuration["litedb"];
        if (!string.IsNullOrWhiteSpace(liteDbPath))
        {
            builder.Services.AddSingleton(_ => new LiteDB.LiteDatabase(liteDbPath));
            builder.Services.AddSingleton<IStubPersistence>(sp =>
                new LiteDbStubPersistence(sp.GetRequiredService<LiteDB.LiteDatabase>()));
            builder.Services.AddSingleton<IMappingsLoader>(sp =>
                new LiteDbMappingsLoader(sp.GetRequiredService<LiteDB.LiteDatabase>(), sp.GetRequiredService<IMatcherRegistry>()));
        }

        // PostgreSQL persistence (G16c): stubs persist to a SQL table and reload on startup.
        var postgres = builder.Configuration["postgres"];
        if (!string.IsNullOrWhiteSpace(postgres))
        {
            builder.Services.AddSingleton<IStubPersistence>(new PostgresStubPersistence(postgres));
            builder.Services.AddSingleton<IMappingsLoader>(sp =>
                new PostgresMappingsLoader(postgres, sp.GetRequiredService<IMatcherRegistry>()));
        }

        // Port 0 asks Kestrel for an ephemeral port (used by tests).
        var port = builder.Configuration.GetValue("port", DefaultPort);
        var httpsPort = builder.Configuration.GetValue<int?>("https-port");

        // When HTTPS is enabled both listeners are configured on Kestrel directly (self-signed cert,
        // like WireMock's default); otherwise the HTTP port alone is bound via app.Urls.
        if (httpsPort is { } securePort)
        {
            var certificate = SelfSignedCertificate.Create();
            builder.WebHost.ConfigureKestrel(options =>
            {
                // HTTP/2 (G11b): both listeners speak HTTP/1.1 and HTTP/2 — ALPN-negotiated h2 on TLS,
                // and prior-knowledge h2c on plaintext — matching WireMock, which serves HTTP/2 on both
                // its ports by default. See docs/parity/g11-tls-http2.md.
                options.ListenAnyIP(port, listen => listen.Protocols = HttpProtocols.Http1AndHttp2);
                options.ListenAnyIP(securePort, listen =>
                {
                    listen.Protocols = HttpProtocols.Http1AndHttp2;
                    listen.UseHttps(certificate);
                });
            });
        }

        var app = builder.Build();

        if (httpsPort is null)
        {
            app.Urls.Add($"http://0.0.0.0:{port}");
        }

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
