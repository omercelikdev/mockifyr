using System.Text.Json;

namespace Mockifyr.Differential.Generator;

/// <summary>
/// A stateful-scenario (G5) case: a set of stubs bound to scenario states, loaded together, then a
/// sequence of requests driven in order. Both sides walk the same state machine, so the harness diffs
/// each step's response — the state advancing identically is the parity claim.
/// </summary>
public sealed record StateScenario(string Description, string MappingsJson, IReadOnlyList<RequestSpec> Steps);

/// <summary>Stateful scenario cases (G5): multi-step state machines and scenario isolation.</summary>
public static class StateScenarios
{
    public static IEnumerable<StateScenario> All()
    {
        // A three-state walk on one URL: Started -> step2 -> step3 (terminal). The 4th call stays at
        // the terminal state.
        var todo = Mappings(
            Stub("todo", "Started", "step2", "/s", "first"),
            Stub("todo", "step2", "step3", "/s", "second"),
            Stub("todo", "step3", null, "/s", "third"));
        yield return new StateScenario(
            "todo-walk",
            todo,
            [Get("/s"), Get("/s"), Get("/s"), Get("/s")]);

        // Two independent scenarios: advancing A must not affect B (per-scenario state).
        var isolated = Mappings(
            Stub("A", "Started", "a2", "/a", "a1"),
            Stub("A", "a2", null, "/a", "a2"),
            Stub("B", "Started", "b2", "/b", "b1"),
            Stub("B", "b2", null, "/b", "b2"));
        yield return new StateScenario(
            "two-scenarios-isolated",
            isolated,
            [Get("/a"), Get("/b"), Get("/a"), Get("/b")]);
    }

    private static RequestSpec Get(string url) => new() { Method = "GET", Url = url };

    private static Dictionary<string, object> Stub(
        string scenario, string? requiredState, string? newState, string url, string body)
    {
        var mapping = new Dictionary<string, object>
        {
            ["scenarioName"] = scenario,
            ["request"] = new Dictionary<string, object> { ["method"] = "GET", ["url"] = url },
            ["response"] = new Dictionary<string, object> { ["status"] = 200, ["body"] = body },
        };
        if (requiredState is not null)
        {
            mapping["requiredScenarioState"] = requiredState;
        }

        if (newState is not null)
        {
            mapping["newScenarioState"] = newState;
        }

        return mapping;
    }

    private static string Mappings(params Dictionary<string, object>[] stubs) =>
        JsonSerializer.Serialize(new Dictionary<string, object> { ["mappings"] = stubs });
}
