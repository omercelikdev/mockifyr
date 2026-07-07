using Mediant.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Mockifyr.Application;
using Mockifyr.Core;
using Mockifyr.Outbound;

namespace Mockifyr.Facade.Admin;

/// <summary>
/// The WireMock-compatible admin HTTP surface (G7b). Each route is a thin translation of an HTTP
/// request into a Mediant command/query on <see cref="ISender"/>; all logic lives in
/// <c>Mockifyr.Application</c>. Every route is scoped to the tenant named by the <c>X-Mockifyr-Tenant</c>
/// header (the same header the mock-serving facade honours); an absent header resolves to the default
/// tenant, so single-tenant callers are unaffected.
/// </summary>
public static class AdminEndpoints
{
    private const string TenantHeader = "X-Mockifyr-Tenant";

    /// <summary>Resolves the request's tenant from <c>X-Mockifyr-Tenant</c>, defaulting when absent.</summary>
    private static TenantId TenantOf(HttpRequest request) =>
        request.Headers.TryGetValue(TenantHeader, out var value) && !string.IsNullOrEmpty(value)
            ? new TenantId(value!)
            : TenantId.Default;

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/__admin");

        admin.MapGet("/mappings", async (HttpRequest request, ISender sender) =>
        {
            var result = await sender.Send(new GetStubsQuery(TenantOf(request)));
            return Results.Json(new { mappings = result.Value.Select(stub => new { id = stub.Id }) });
        });

        admin.MapPost("/mappings", async (HttpRequest request, ISender sender) =>
        {
            var result = await sender.Send(new CreateStubCommand(await ReadBody(request), TenantOf(request)));
            return result.IsSuccess
                ? Results.Json(new { id = result.Value, uuid = result.Value }, statusCode: StatusCodes.Status201Created)
                : Results.StatusCode(StatusCodes.Status422UnprocessableEntity);
        });

        admin.MapGet("/mappings/{id:guid}", async (Guid id, HttpRequest request, ISender sender) =>
        {
            var result = await sender.Send(new GetStubQuery(id, TenantOf(request)));
            return result.IsSuccess ? Results.Json(new { id = result.Value.Id }) : Results.NotFound();
        });

        admin.MapDelete("/mappings/{id:guid}", async (Guid id, HttpRequest request, ISender sender) =>
        {
            await sender.Send(new DeleteStubCommand(id, TenantOf(request)));
            return Results.Ok();
        });

        admin.MapPost("/mappings/import", async (HttpRequest request, ISender sender) =>
        {
            var result = await sender.Send(new ImportMappingsCommand(await ReadBody(request), TenantOf(request)));
            return result.IsSuccess
                ? Results.Ok()
                : Results.StatusCode(StatusCodes.Status422UnprocessableEntity);
        });

        admin.MapPost("/mappings/reset", async (HttpRequest request, ISender sender) =>
        {
            await sender.Send(new ResetMappingsCommand(TenantOf(request)));
            return Results.Ok();
        });

        admin.MapPost("/requests/count", async (HttpRequest request, ISender sender) =>
        {
            var result = await sender.Send(new CountRequestsQuery(await ReadBody(request), TenantOf(request)));
            return Results.Json(new { count = result.Value });
        });

        admin.MapGet("/requests", async (HttpRequest request, ISender sender) =>
        {
            var unmatchedOnly = request.Query.TryGetValue("unmatched", out var u) && u == "true";
            var result = await sender.Send(new GetServeEventsQuery(TenantOf(request), unmatchedOnly));
            return Results.Json(new
            {
                requests = result.Value.Select(e => new
                {
                    id = e.Id,
                    method = e.Request.Method,
                    url = e.Request.Url,
                    status = e.Response?.Status,
                    wasMatched = e.MatchedStub is not null,
                    stubId = e.MatchedStub?.Id,
                }),
            });
        });

        admin.MapGet("/scenarios", async (HttpRequest request, ISender sender) =>
        {
            var result = await sender.Send(new GetScenariosQuery(TenantOf(request)));
            return Results.Json(new
            {
                scenarios = result.Value.Select(s => new { id = s.Name, name = s.Name, state = s.State, possibleStates = s.PossibleStates }),
            });
        });

        admin.MapPost("/scenarios/reset", async (HttpRequest request, ISender sender) =>
        {
            await sender.Send(new ResetScenariosCommand(TenantOf(request)));
            return Results.Ok();
        });

        admin.MapPut("/scenarios/{name}/state", async (string name, HttpRequest request, ISender sender) =>
        {
            using var doc = System.Text.Json.JsonDocument.Parse(await ReadBody(request));
            var state = doc.RootElement.TryGetProperty("state", out var s) ? s.GetString() ?? "Started" : "Started";
            await sender.Send(new SetScenarioStateCommand(name, state, TenantOf(request)));
            return Results.Ok();
        });

        // Record mode (G12d): WireMock's record-through-proxy admin API. While a session is live, the
        // mock-serving fallback proxies every request to the target and captures a generated stub.
        admin.MapPost("/recordings/start", async (HttpRequest request, RecordingSession session) =>
        {
            using var doc = System.Text.Json.JsonDocument.Parse(await ReadBody(request));
            var target = doc.RootElement.TryGetProperty("targetBaseUrl", out var t) ? t.GetString() : null;
            if (string.IsNullOrWhiteSpace(target))
            {
                return Results.StatusCode(StatusCodes.Status422UnprocessableEntity);
            }

            session.Start(target);
            return Results.Ok();
        });

        admin.MapGet("/recordings/status", (RecordingSession session) =>
            Results.Json(new { status = session.TargetBaseUrl is null ? "Stopped" : "Recording" }));

        admin.MapPost("/recordings/snapshot", (RecordingSession session) => Mappings(session.Snapshot()));

        admin.MapPost("/recordings/stop", (RecordingSession session) => Mappings(session.Stop()));

        // Custom admin API extensions (G12e): any request under /__admin/ext/<prefix>/… is dispatched
        // to the extension whose RoutePrefix is that first segment. The extension owns everything below
        // it and never sees an HttpContext — the request is lowered to a transport-agnostic shape.
        admin.Map("/ext/{**rest}", async (string? rest, HttpContext http, IEnumerable<IAdminApiExtension> extensions) =>
        {
            var path = rest ?? string.Empty;
            var slash = path.IndexOf('/');
            var prefix = slash < 0 ? path : path[..slash];
            var subpath = slash < 0 ? string.Empty : path[slash..];

            var extension = extensions.FirstOrDefault(e =>
                string.Equals(e.RoutePrefix, prefix, StringComparison.Ordinal));
            if (extension is null)
            {
                http.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            byte[] body;
            using (var buffer = new MemoryStream())
            {
                await http.Request.Body.CopyToAsync(buffer);
                body = buffer.ToArray();
            }

            var apiRequest = new AdminApiRequest(http.Request.Method, subpath, http.Request.QueryString.Value ?? string.Empty, body);
            var response = await extension.HandleAsync(apiRequest, http.RequestAborted);

            http.Response.StatusCode = response.Status;
            http.Response.ContentType = response.ContentType;
            await http.Response.Body.WriteAsync(response.Body);
        });

        return endpoints;
    }

    // WireMock's recording responses return a {"mappings":[…]} envelope of the generated stub JSON. The
    // captured stubs are already JSON, so they are spliced in raw rather than re-serialized.
    private static IResult Mappings(IReadOnlyList<string> stubs) =>
        Results.Content("{\"mappings\":[" + string.Join(",", stubs) + "]}", "application/json");

    private static async Task<string> ReadBody(HttpRequest request)
    {
        using var reader = new StreamReader(request.Body);
        return await reader.ReadToEndAsync();
    }
}
