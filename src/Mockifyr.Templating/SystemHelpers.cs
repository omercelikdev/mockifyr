using System.Net;
using HandlebarsDotNet;

namespace Mockifyr.Templating;

/// <summary>
/// WireMock's system Handlebars helpers (G2h): <c>systemValue</c> and <c>hostname</c>.
/// <c>systemValue</c> is <em>secure by default</em> — every key is denied until an allowlist is
/// configured (a deploy/config concern for G12), so it renders WireMock's deny error verbatim.
/// <c>hostname</c> resolves the local host name (host-specific, so validated structurally). See
/// docs/parity/g2-response.md.
/// </summary>
internal static class SystemHelpers
{
    public static void Register(IHandlebars handlebars)
    {
        handlebars.RegisterHelper("systemValue", (_, arguments) => SystemValue(arguments));
        handlebars.RegisterHelper("hostname", (_, _) => Dns.GetHostName());
    }

    private static object SystemValue(Arguments arguments)
    {
        var key = Hash(arguments, "key") ?? string.Empty;

        // No allowlist is configured, so — like WireMock's default deny-all — access to every key is
        // refused. Reading a permitted value is a config/deploy concern deferred to G12.
        return $"[ERROR: Access to {key} is denied]";
    }

    private static string? Hash(Arguments arguments, string key) =>
        arguments.Hash is { } hash && hash.TryGetValue(key, out var value) ? value?.ToString() : null;
}
