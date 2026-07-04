using System.Text;
using Mockifyr.Core;

namespace Mockifyr.Templating;

/// <summary>
/// Builds the Handlebars model for a request — <c>method</c>, <c>url</c>, <c>path</c>,
/// <c>pathSegments</c>, <c>query</c> (first value), <c>headers</c> (first value), <c>body</c>. It is
/// rooted under <c>request</c> for response templating (G2) and under <c>originalRequest</c> for
/// webhook templating (G3b) — the sub-structure is identical. <c>path</c> is a dual
/// <see cref="PathModel"/>: bare it is the full path, and it exposes named path variables (from a
/// matched <c>urlPathTemplate</c>) plus indexed segments.
/// </summary>
internal static class RequestModel
{
    public static Dictionary<string, object?> Build(CanonicalRequest request, string? urlPathTemplate = null) => new()
    {
        ["method"] = request.Method,
        ["url"] = request.Url,
        ["path"] = new PathModel(
            request.Path, request.PathSegments, PathModel.ExtractVariables(urlPathTemplate, request.PathSegments)),
        ["pathSegments"] = request.PathSegments,
        ["query"] = request.Query.ToDictionary(group => group.Key, group => (object?)group.First()),
        ["headers"] = request.Headers.ToDictionary(
            group => group.Key, group => (object?)group.First(), StringComparer.OrdinalIgnoreCase),
        ["body"] = Encoding.UTF8.GetString(request.Body),
    };
}
