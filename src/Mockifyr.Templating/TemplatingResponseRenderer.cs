using System.Text;
using HandlebarsDotNet;
using Mockifyr.Core;

namespace Mockifyr.Templating;

/// <summary>
/// Renders a response definition, applying Handlebars templating when the stub declares the
/// <c>response-template</c> transformer (WireMock's response templating). The template model exposes
/// the request as <c>{{request.method}}</c>, <c>url</c>, <c>path</c>, <c>pathSegments.[n]</c>,
/// <c>query.name</c>, <c>headers.Name</c>, and <c>body</c>. Escaping is disabled to match WireMock
/// (raw <c>{{ }}</c> output). The built-in data helpers (<c>jsonPath</c>, <c>xPath</c>,
/// <c>regexExtract</c>, <c>formData</c>, <c>parseJson</c>) are registered from G2c, the date helpers
/// (<c>parseDate</c>, <c>date</c>) from G2d, the random helpers (<c>randomValue</c>,
/// <c>pickRandom</c>, <c>randomInt</c>, <c>randomDecimal</c>) from G2e, and the JSON-manipulation
/// helpers (<c>jsonArrayAdd</c>, <c>jsonMerge</c>, <c>jsonRemove</c>, <c>toJson</c>) from G2f, and the
/// format/math/array/string helpers (<c>math</c>, <c>numberFormat</c>, <c>size</c>, <c>join</c>,
/// <c>substring</c>, <c>replace</c>, <c>upper</c>, <c>lower</c>, <c>capitalize</c>, <c>trim</c>) from
/// G2g, and the system helpers (<c>systemValue</c>, <c>hostname</c>) from G2h. See
/// docs/parity/g2-response.md.
/// </summary>
public sealed class TemplatingResponseRenderer : IResponseRenderer
{
    private const string ResponseTemplateTransformer = "response-template";

    // TextEncoder = null disables HTML escaping, matching WireMock's non-escaping output.
    private readonly IHandlebars _handlebars;

    public TemplatingResponseRenderer() => _handlebars = HandlebarsFactory.Create();

    /// <inheritdoc />
    public CanonicalResponse Render(ResponseDefinition definition, RenderContext context)
    {
        if (!definition.Transformers.Contains(ResponseTemplateTransformer))
        {
            return new CanonicalResponse
            {
                Status = definition.Status,
                StatusMessage = definition.StatusMessage,
                Headers = definition.Headers,
                Body = definition.Body ?? [],
                Delay = definition.Delay,
                Fault = definition.Fault,
            };
        }

        var model = BuildModel(context.Request);

        var body = definition.Body is { } raw
            ? Encoding.UTF8.GetBytes(RenderTemplate(Encoding.UTF8.GetString(raw), model))
            : [];

        var headers = definition.Headers
            .SelectMany(group => group.Select(value =>
                new KeyValuePair<string, string>(group.Key, RenderTemplate(value, model))))
            .ToLookup(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        return new CanonicalResponse
        {
            Status = definition.Status,
            StatusMessage = definition.StatusMessage,
            Headers = headers,
            Body = body,
            Delay = definition.Delay,
            Fault = definition.Fault,
        };
    }

    private string RenderTemplate(string template, object model) => _handlebars.Compile(template)(model);

    private static Dictionary<string, object?> BuildModel(CanonicalRequest request) =>
        new() { ["request"] = RequestModel.Build(request) };
}
