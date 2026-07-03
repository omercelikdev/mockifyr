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
/// helpers (<c>jsonArrayAdd</c>, <c>jsonMerge</c>, <c>jsonRemove</c>, <c>toJson</c>) from G2f; further
/// helper families arrive with G2g–G2h. See docs/parity/g2-response.md.
/// </summary>
public sealed class TemplatingResponseRenderer : IResponseRenderer
{
    private const string ResponseTemplateTransformer = "response-template";

    // TextEncoder = null disables HTML escaping, matching WireMock's non-escaping output.
    private readonly IHandlebars _handlebars;

    public TemplatingResponseRenderer()
    {
        _handlebars = Handlebars.Create(new HandlebarsConfiguration { TextEncoder = null });
        DataHelpers.Register(_handlebars);
        DateHelpers.Register(_handlebars);
        RandomHelpers.Register(_handlebars);
        JsonHelpers.Register(_handlebars);
    }

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
        };
    }

    private string RenderTemplate(string template, object model) => _handlebars.Compile(template)(model);

    private static Dictionary<string, object?> BuildModel(CanonicalRequest request) => new()
    {
        ["request"] = new Dictionary<string, object?>
        {
            ["method"] = request.Method,
            ["url"] = request.Url,
            ["path"] = request.Path,
            ["pathSegments"] = request.PathSegments,
            ["query"] = request.Query.ToDictionary(group => group.Key, group => (object?)group.First()),
            ["headers"] = request.Headers.ToDictionary(
                group => group.Key, group => (object?)group.First(), StringComparer.OrdinalIgnoreCase),
            ["body"] = Encoding.UTF8.GetString(request.Body),
        },
    };
}
