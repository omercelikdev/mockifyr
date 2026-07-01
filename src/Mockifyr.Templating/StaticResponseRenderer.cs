using Mockifyr.Core;

namespace Mockifyr.Templating;

/// <summary>
/// Renders a response definition verbatim, with no templating. This is the G0/G2a baseline;
/// the Handlebars.Net-based templating renderer is layered on from G2b.
/// </summary>
public sealed class StaticResponseRenderer : IResponseRenderer
{
    /// <inheritdoc />
    public CanonicalResponse Render(ResponseDefinition definition, RenderContext context)
    {
        _ = context;
        return new CanonicalResponse
        {
            Status = definition.Status,
            StatusMessage = definition.StatusMessage,
            Headers = definition.Headers,
            Body = definition.Body ?? [],
        };
    }
}
