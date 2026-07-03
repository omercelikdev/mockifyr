using System.Text.Json;

namespace Mockifyr.Differential.Generator;

/// <summary>
/// A webhook (G3a) case: a mapping with a <c>webhook</c> post-serve action whose target host is the
/// <c>__WEBHOOK_HOST__</c> token (rewritten per side by the harness), the request that triggers it,
/// and the declared header names to compare. The harness fires it on both sides and diffs the
/// captured outbound call.
/// </summary>
public sealed record WebhookScenario(
    string Description,
    string MappingTemplate,
    RequestSpec Trigger,
    IReadOnlyList<string> ComparedHeaders);

/// <summary>Webhook post-serve-action cases (G3a): static method/url/headers/body delivery.</summary>
public static class WebhookScenarios
{
    public static IEnumerable<WebhookScenario> All()
    {
        // POST webhook with declared headers and a JSON body.
        yield return Build(
            "post-json",
            new Dictionary<string, object> { ["method"] = "POST", ["url"] = "/trigger" },
            method: "POST",
            path: "/callback",
            headers: new Dictionary<string, object> { ["Content-Type"] = "application/json", ["X-Custom"] = "v1" },
            body: "{\"hello\":\"world\"}",
            trigger: new RequestSpec { Method = "POST", Url = "/trigger" });

        // GET webhook with no body and no declared headers.
        yield return Build(
            "get-empty",
            new Dictionary<string, object> { ["method"] = "GET", ["url"] = "/g" },
            method: "GET",
            path: "/notify",
            headers: null,
            body: null,
            trigger: new RequestSpec { Method = "GET", Url = "/g" });

        // PUT webhook with a single custom header and a text body, to a distinct path.
        yield return Build(
            "put-text",
            new Dictionary<string, object> { ["method"] = "POST", ["url"] = "/p" },
            method: "PUT",
            path: "/sink/42",
            headers: new Dictionary<string, object> { ["X-Trace"] = "abc-123" },
            body: "plain text payload",
            trigger: new RequestSpec { Method = "POST", Url = "/p" });
    }

    private static WebhookScenario Build(
        string description,
        Dictionary<string, object> requestPattern,
        string method,
        string path,
        Dictionary<string, object>? headers,
        string? body,
        RequestSpec trigger)
    {
        var parameters = new Dictionary<string, object>
        {
            ["method"] = method,
            ["url"] = $"http://__WEBHOOK_HOST__{path}",
        };
        if (headers is not null)
        {
            parameters["headers"] = headers;
        }

        if (body is not null)
        {
            parameters["body"] = body;
        }

        var mapping = new Dictionary<string, object>
        {
            ["request"] = requestPattern,
            ["response"] = new Dictionary<string, object> { ["status"] = 200, ["body"] = "served" },
            ["postServeActions"] = new object[]
            {
                new Dictionary<string, object> { ["name"] = "webhook", ["parameters"] = parameters },
            },
        };

        return new WebhookScenario(
            $"webhook[{description}]",
            JsonSerializer.Serialize(mapping),
            trigger,
            headers?.Keys.ToList() ?? []);
    }
}
