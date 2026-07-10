using System.Text.Json.Nodes;
using Mediant.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Mockifyr.Application;
using Mockifyr.Core;
using Mockifyr.Outbound;

namespace Mockifyr.Facade.Admin;

/// <summary>
/// The admin HTTP surface (G7b), whose routes and JSON shapes match the stub-format dialect Mockifyr
/// imports so existing tooling interoperates (verified by the differential suite). Each route is a thin translation of an HTTP
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

    /// <summary>Flattens multi-valued headers into name/value pairs for the journal detail view.</summary>
    private static object HeaderPairs(ILookup<string, string> headers) =>
        headers.Select(g => new { name = g.Key, value = string.Join(", ", g) }).ToArray();

    /// <summary>Decodes a body for display; bodies in the journal are already materialised in memory.</summary>
    private static string Utf8(byte[] body) => System.Text.Encoding.UTF8.GetString(body);

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/__admin");

        // Host status for the dashboard's Settings/Status screen: the active persistence provider and
        // live tenant/stub counts, gathered from DI. Host-config knobs (TLS, ports) are set by CLI flags
        // at startup and aren't admin-mutable, so they are documented in the UI rather than reported here.
        admin.MapGet("/health", (IStubStore store, IStubPersistence persistence) =>
        {
            var tenants = store.GetTenants();
            return Results.Json(new
            {
                name = "Mockifyr",
                version = "1.0",
                persistence = persistence.GetType().Name,
                tenants = tenants.Count,
                totalStubs = tenants.Sum(t => store.GetStubs(t).Count),
            });
        });

        // The tenants that currently exist server-side (a tenant materializes once it has stubs), so the
        // dashboard's switcher can surface tenants created via the API alongside the operator's own list.
        admin.MapGet("/tenants", (IStubStore store) =>
            Results.Json(new { tenants = store.GetTenants().Select(t => t.Value).OrderBy(v => v) }));

        admin.MapGet("/mappings", async (HttpRequest request, ISender sender) =>
        {
            var result = await sender.Send(new GetStubsQuery(TenantOf(request)));
            var mappings = result.Value.Select(FullMapping).ToList();
            return Results.Json(new { mappings });
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

        admin.MapPut("/mappings/{id:guid}", async (Guid id, HttpRequest request, ISender sender) =>
        {
            var result = await sender.Send(new UpdateStubCommand(id, await ReadBody(request), TenantOf(request)));
            return result.IsSuccess
                ? Results.Json(new { id, uuid = id })
                : Results.StatusCode(StatusCodes.Status422UnprocessableEntity);
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
                    loggedDate = e.Timestamp,
                }),
            });
        });

        // Full detail for one journal entry (backs the dashboard's Request/Response/Callback tabs). The
        // list stays lean; headers + bodies are fetched on demand here. Webhooks show the matched stub's
        // configured callbacks (the intent), since outbound firing happens fire-and-forget at the edge.
        admin.MapGet("/requests/{id}", async (string id, HttpRequest request, ISender sender) =>
        {
            var result = await sender.Send(new GetServeEventsQuery(TenantOf(request), UnmatchedOnly: false));
            var e = result.Value.FirstOrDefault(x => x.Id.ToString() == id);
            if (e is null)
            {
                return Results.NotFound();
            }

            return Results.Json(new
            {
                id = e.Id,
                loggedDate = e.Timestamp,
                wasMatched = e.MatchedStub is not null,
                stubId = e.MatchedStub?.Id,
                request = new
                {
                    method = e.Request.Method,
                    url = e.Request.Url,
                    headers = HeaderPairs(e.Request.Headers),
                    body = Utf8(e.Request.Body),
                },
                response = e.Response is null ? null : new
                {
                    status = e.Response.Status,
                    statusMessage = e.Response.StatusMessage,
                    headers = HeaderPairs(e.Response.Headers),
                    body = Utf8(e.Response.Body),
                },
                webhooks = (e.MatchedStub?.Webhooks ?? []).Select(w => new
                {
                    method = w.Method,
                    url = w.Url,
                    headers = w.Headers.Select(h => new { name = h.Key, value = h.Value }),
                    body = w.Body is null ? null : Utf8(w.Body),
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

        // Record mode (G12d): the record-through-proxy admin API (verified by the differential suite). While
        // a session is live, the mock-serving fallback proxies every request to the target and captures a
        // generated stub.
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

        // Git sync (ADR 0007) — host-level, not tenant-scoped: the host has one root-dir working
        // copy. Status always answers (configured=false when the flag is absent); push/pull refuse
        // with a typed error the dashboard can surface (conflict / validation / auth / not set up).
        admin.MapGet("/git/status", async (ISender sender) =>
        {
            var result = await sender.Send(new GitStatusQuery());
            return result.IsSuccess
                ? Results.Json(new
                {
                    configured = result.Value.Configured,
                    remote = result.Value.Remote,
                    branch = result.Value.Branch,
                    dirty = result.Value.Dirty,
                    ahead = result.Value.Ahead,
                    behind = result.Value.Behind,
                    fetchError = result.Value.FetchError,
                })
                : GitFailure(result.Error);
        });

        admin.MapPost("/git/push", async (HttpRequest request, ISender sender) =>
        {
            var result = await sender.Send(new GitPushCommand(await ReadGitMessage(request)));
            return result.IsSuccess
                ? Results.Json(new { pushed = result.Value.Pushed, commit = result.Value.Commit, reason = result.Value.Reason })
                : GitFailure(result.Error);
        });

        admin.MapPost("/git/pull", async (ISender sender) =>
        {
            var result = await sender.Send(new GitPullCommand());
            return result.IsSuccess
                ? Results.Json(new { updated = result.Value.Updated, commit = result.Value.Commit, stubsLoaded = result.Value.StubsLoaded, reason = result.Value.Reason })
                : GitFailure(result.Error);
        });

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

    // Recording responses return a {"mappings":[…]} envelope of the generated stub JSON. The
    // captured stubs are already JSON, so they are spliced in raw rather than re-serialized.
    private static IResult Mappings(IReadOnlyList<string> stubs) =>
        Results.Content("{\"mappings\":[" + string.Join(",", stubs) + "]}", "application/json");

    private static async Task<string> ReadBody(HttpRequest request)
    {
        using var reader = new StreamReader(request.Body);
        return await reader.ReadToEndAsync();
    }

    /// <summary>Reads the optional <c>{"message": "…"}</c> commit message from a push body (empty body is fine).</summary>
    private static async Task<string?> ReadGitMessage(HttpRequest request)
    {
        var body = await ReadBody(request);
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    // Typed Git errors → HTTP: setup problems are 404, refusals (pull-first/diverged/dirty/branch)
    // are 409, a rejected remote tree is 422, remote-auth failures are 502 — deliberately NOT 401,
    // which the dashboard reserves for the host's own admin auth (it would pop the login gate).
    private static IResult GitFailure(Mediant.Results.Error error) =>
        Results.Json(new { error = error.Code, message = error.Description }, statusCode: error.Code switch
        {
            "Git.NotConfigured" or "Git.RemoteBranchMissing" => StatusCodes.Status404NotFound,
            "Git.InvalidMappings" => StatusCodes.Status422UnprocessableEntity,
            "Git.RemoteAhead" or "Git.Diverged" or "Git.DirtyWorkingTree" or "Git.LocalOverlap" or "Git.WrongBranch" => StatusCodes.Status409Conflict,
            "Git.Auth" => StatusCodes.Status502BadGateway,
            _ => StatusCodes.Status500InternalServerError,
        });

    // The full mapping for GET /mappings: the stub's own source JSON with its id/uuid stamped
    // in, so the dashboard can display and faithfully round-trip an edit (not just see an id).
    private static JsonNode FullMapping(StubMapping stub)
    {
        var node = (stub.Source is not null ? JsonNode.Parse(stub.Source) : null) as JsonObject ?? new JsonObject();
        node["id"] = stub.Id.ToString();
        node["uuid"] = stub.Id.ToString();
        return node;
    }
}
