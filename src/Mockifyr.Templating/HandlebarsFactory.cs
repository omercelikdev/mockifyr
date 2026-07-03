using HandlebarsDotNet;

namespace Mockifyr.Templating;

/// <summary>
/// Builds the Handlebars engine Mockifyr uses for both response templating (G2) and webhook
/// templating (G3b), with HTML escaping disabled (WireMock emits raw <c>{{ }}</c> output) and every
/// built-in helper family registered.
/// </summary>
internal static class HandlebarsFactory
{
    public static IHandlebars Create()
    {
        // TextEncoder = null disables HTML escaping, matching WireMock's non-escaping output.
        var handlebars = Handlebars.Create(new HandlebarsConfiguration { TextEncoder = null });
        DataHelpers.Register(handlebars);
        DateHelpers.Register(handlebars);
        RandomHelpers.Register(handlebars);
        JsonHelpers.Register(handlebars);
        FormatHelpers.Register(handlebars);
        SystemHelpers.Register(handlebars);
        return handlebars;
    }
}
