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
        ["cookies"] = request.Cookies.ToDictionary(cookie => cookie.Key, cookie => (object?)cookie.Value, StringComparer.Ordinal),
        ["parts"] = Parts(request.Parts),
        ["body"] = Encoding.UTF8.GetString(request.Body),
        ["bodyAsBase64"] = Convert.ToBase64String(request.Body),
        ["host"] = request.Host,
        ["port"] = request.Port,
        ["scheme"] = request.Scheme,
        ["baseUrl"] = BaseUrl(request),
    };

    // Exposes multipart parts as `request.parts.<name>` (G15f, verified by the differential suite), each
    // carrying `name`, its `headers` (first value per header) and the part `body` as text — so a template
    // can reach `{{request.parts.avatar.body}}` or `{{request.parts.avatar.headers.[Content-Type]}}`.
    // Later parts with a duplicate name win, since the parts are keyed by name in a map.
    private static Dictionary<string, object?> Parts(IReadOnlyList<MultipartPart> parts)
    {
        var model = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var part in parts)
        {
            model[part.Name] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = part.Name,
                ["headers"] = part.Headers.ToDictionary(
                    header => header.Key, header => (object?)header.Value, StringComparer.OrdinalIgnoreCase),
                ["body"] = Encoding.UTF8.GetString(part.Body),
            };
        }

        return model;
    }

    // request.baseUrl is `scheme://host[:port]`, built from the request's scheme + Host header.
    private static string? BaseUrl(CanonicalRequest request)
    {
        if (request.Scheme is null || request.Host is null)
        {
            return null;
        }

        return request.Port is { } port
            ? $"{request.Scheme}://{request.Host}:{port}"
            : $"{request.Scheme}://{request.Host}";
    }
}
