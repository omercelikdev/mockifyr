using HandlebarsDotNet;
using Mockifyr.Core;

namespace Mockifyr.Templating;

/// <summary>
/// Renders webhook fields (URL, header values, body) as Handlebars templates against the triggering
/// request, which WireMock exposes as <c>originalRequest</c> (G3b). Reuses the same engine and helper
/// set as response templating — so <c>{{jsonPath originalRequest.body '$.id'}}</c>,
/// <c>{{originalRequest.headers.X}}</c>, etc. all work. See docs/parity/g3-webhook.md.
/// </summary>
public sealed class WebhookTemplateRenderer : IServeEventTemplateRenderer
{
    private readonly IHandlebars _handlebars = HandlebarsFactory.Create();

    /// <inheritdoc />
    public string Render(string template, CanonicalRequest originalRequest)
    {
        var model = new Dictionary<string, object?> { ["originalRequest"] = RequestModel.Build(originalRequest) };
        return _handlebars.Compile(template)(model);
    }
}
