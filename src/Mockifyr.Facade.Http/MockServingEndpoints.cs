using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Mockifyr.Core;

namespace Mockifyr.Facade.Http;

/// <summary>
/// The mock-serving HTTP facade (G12): a fallback endpoint that turns every non-admin request into a
/// <see cref="CanonicalRequest"/>, resolves it through the (pure) <see cref="StubEngine"/>, and writes
/// the response over the wire — status, the custom reason phrase (<c>statusMessage</c>), declared
/// headers, and body — applying the response <c>delay</c> directive. Fault emission is G12b. Tenant
/// resolution reads an optional <c>X-Mockifyr-Tenant</c> header, else the default tenant.
/// </summary>
public static class MockServingEndpoints
{
    private const string TenantHeader = "X-Mockifyr-Tenant";

    // Recomputed by Kestrel; setting them explicitly would conflict with the framed response.
    private static readonly HashSet<string> SkipHeaders =
        new(StringComparer.OrdinalIgnoreCase) { "Content-Length", "Transfer-Encoding", "Connection" };

    public static IEndpointRouteBuilder MapMockServing(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapFallback(ServeAsync);
        return endpoints;
    }

    private static async Task ServeAsync(HttpContext context)
    {
        var engine = context.RequestServices.GetRequiredService<StubEngine>();
        var request = await BuildRequestAsync(context);
        var tenant = context.Request.Headers.TryGetValue(TenantHeader, out var t) && !string.IsNullOrEmpty(t)
            ? new TenantId(t!)
            : TenantId.Default;

        var resolution = engine.Handle(tenant, request);

        if (resolution.Response is not { } response)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (response.Delay is { Milliseconds: > 0 } delay)
        {
            await Task.Delay(delay.Milliseconds);
        }

        context.Response.StatusCode = response.Status;
        if (!string.IsNullOrEmpty(response.StatusMessage))
        {
            // The custom reason phrase (statusMessage) goes on the status line.
            context.Features.Get<IHttpResponseFeature>()!.ReasonPhrase = response.StatusMessage;
        }

        foreach (var group in response.Headers)
        {
            if (!SkipHeaders.Contains(group.Key))
            {
                context.Response.Headers.Append(group.Key, group.ToArray());
            }
        }

        await context.Response.Body.WriteAsync(response.Body);
    }

    private static async Task<CanonicalRequest> BuildRequestAsync(HttpContext context)
    {
        using var buffer = new MemoryStream();
        await context.Request.Body.CopyToAsync(buffer);

        var headers = context.Request.Headers
            .SelectMany(header => header.Value.Select(value => new KeyValuePair<string, string>(header.Key, value ?? string.Empty)))
            .ToList();

        var url = context.Request.Path + context.Request.QueryString;
        return CanonicalRequestBuilder.Build(context.Request.Method, url, headers, buffer.ToArray());
    }
}
