using HandlebarsDotNet;
using Mockifyr.Core;

namespace Mockifyr.Templating;

/// <summary>
/// Builds the Handlebars engine Mockifyr uses for both response templating (G2) and webhook
/// templating (G3b), with HTML escaping disabled (WireMock emits raw <c>{{ }}</c> output) and every
/// built-in helper family registered. User-supplied helper extensions (G10) are registered too.
/// </summary>
internal static class HandlebarsFactory
{
    public static IHandlebars Create(IEnumerable<TemplateHelperExtension>? extraHelpers = null)
    {
        // TextEncoder = null disables HTML escaping, matching WireMock's non-escaping output.
        var configuration = new HandlebarsConfiguration { TextEncoder = null };
        // Teach Handlebars the dual request.path model (bare string + named vars + indexed segments).
        configuration.ObjectDescriptorProviders.Add(new PathModelDescriptorProvider());
        var handlebars = Handlebars.Create(configuration);
        DataHelpers.Register(handlebars);
        DateHelpers.Register(handlebars);
        RandomHelpers.Register(handlebars);
        JsonHelpers.Register(handlebars);
        FormatHelpers.Register(handlebars);
        SystemHelpers.Register(handlebars);

        foreach (var helper in extraHelpers ?? [])
        {
            // Adapt the engine-agnostic render function to a Handlebars return helper.
            var render = helper.Render;
            handlebars.RegisterHelper(helper.Name, (_, arguments) =>
                render([.. Enumerable.Range(0, arguments.Length).Select(i => arguments[i])]));
        }

        return handlebars;
    }
}
