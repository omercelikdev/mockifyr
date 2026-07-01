using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mockifyr.Differential.Generator;

/// <summary>
/// Fuzzes <c>equalToJson</c> (body) across the four combinations of <c>ignoreArrayOrder</c> and
/// <c>ignoreExtraElements</c>. For each base document it generates semantically-equivalent
/// variants (reformatted, reordered keys, reordered arrays, extra fields, number reformatting)
/// and clearly-different ones (changed value, missing field, disallowed reorder/extra), each with
/// its expected match decision, to validate our JSON comparator against the oracle.
/// </summary>
public static class JsonScenarios
{
    private static readonly string[] BaseDocs =
    [
        """{"id":1,"name":"alice","active":true}""",
        """{"user":{"id":2,"roles":["admin","user"]},"score":1.5}""",
        """{"nums":[1,2,3],"meta":{"n":"x"},"flag":false}""",
    ];

    /// <summary>Generates equalToJson scenarios for one combination of the ignore options.</summary>
    public static IEnumerable<MatcherScenario> EqualToJson(bool ignoreArrayOrder, bool ignoreExtraElements)
    {
        foreach (var doc in BaseDocs)
        {
            var probes = new List<ProbeRequest>
            {
                Probe(Compact(doc), true),
                Probe(ReorderKeysDeep(doc), true),
                Probe(ReformatNumbers(doc), true),
                Probe(ChangeFirstLeaf(doc), false),
                Probe(RemoveFirstProperty(doc), false),
            };

            var hasArray = doc.Contains('[');

            if (ignoreArrayOrder && hasArray)
            {
                probes.Add(Probe(ReverseArraysDeep(doc), true));
            }
            else if (!ignoreArrayOrder && hasArray)
            {
                probes.Add(Probe(ReverseArraysDeep(doc), false));
            }

            if (ignoreExtraElements)
            {
                probes.Add(Probe(WithExtraField(doc), true));
            }
            else
            {
                probes.Add(Probe(WithExtraField(doc), false));
            }

            yield return new MatcherScenario(
                $"equalToJson[iao={ignoreArrayOrder},iee={ignoreExtraElements}] {Summarize(doc)}",
                Stub(doc, ignoreArrayOrder, ignoreExtraElements),
                probes);
        }
    }

    /// <summary>
    /// Hand-crafted edge cases that generic mutation does not reach: number precision, explicit
    /// null, objects nested inside arrays under ignoreArrayOrder, and extra array elements/fields
    /// under ignoreExtraElements (where WireMock's exact semantics are being pinned).
    /// </summary>
    public static IEnumerable<MatcherScenario> Edges()
    {
        // Number precision: value equality regardless of representation.
        yield return Edge("""{"n":1.0}""", false, false,
            ("""{"n":1}""", true), ("""{"n":1.00}""", true), ("""{"n":1.0000}""", true), ("""{"n":2}""", false));
        yield return Edge("""{"n":100}""", false, false,
            ("""{"n":1e2}""", true), ("""{"n":100.0}""", true), ("""{"n":10}""", false));

        // Explicit null: distinct from a missing key and from other types.
        yield return Edge("""{"a":null}""", false, false,
            ("""{"a":null}""", true), ("""{"a":1}""", false), ("""{"a":"null"}""", false), ("{}", false));
        yield return Edge("""{"a":null}""", false, true,
            ("""{"a":null,"b":2}""", true), ("{}", false));

        // Objects nested inside an array, reordered under ignoreArrayOrder.
        yield return Edge("""[{"id":1},{"id":2}]""", true, false,
            ("""[{"id":2},{"id":1}]""", true), ("""[{"id":1},{"id":3}]""", false));

        // Extra array elements / extra fields inside array elements under ignoreExtraElements
        // (expected decision is a hypothesis; the oracle is the source of truth).
        yield return Edge("[1,2]", false, true,
            ("[1,2,3]", true), ("[1,2]", true), ("[2,1]", false), ("[1]", false));
        yield return Edge("""[{"a":1}]""", false, true,
            ("""[{"a":1,"b":2}]""", true), ("""[{"a":1}]""", true), ("""[{"a":2}]""", false));
    }

    private static MatcherScenario Edge(string expected, bool ignoreArrayOrder, bool ignoreExtraElements, params (string Actual, bool Match)[] actuals) =>
        new(
            $"equalToJson-edge[iao={ignoreArrayOrder},iee={ignoreExtraElements}] {Summarize(expected)}",
            Stub(expected, ignoreArrayOrder, ignoreExtraElements),
            [.. actuals.Select(a => Probe(a.Actual, a.Match))]);

    private static ProbeRequest Probe(string bodyJson, bool expectedMatch) =>
        new(new RequestSpec { Method = "POST", Url = "/p", Body = Encoding.UTF8.GetBytes(bodyJson) }, expectedMatch);

    private static string Stub(string expectedJson, bool ignoreArrayOrder, bool ignoreExtraElements)
    {
        var matcher = new Dictionary<string, object> { ["equalToJson"] = expectedJson };
        if (ignoreArrayOrder)
        {
            matcher["ignoreArrayOrder"] = true;
        }

        if (ignoreExtraElements)
        {
            matcher["ignoreExtraElements"] = true;
        }

        var mapping = new Dictionary<string, object>
        {
            ["request"] = new Dictionary<string, object>
            {
                ["method"] = "POST",
                ["urlPath"] = "/p",
                ["bodyPatterns"] = new object[] { matcher },
            },
            ["response"] = new Dictionary<string, object> { ["status"] = 200, ["body"] = "ok" },
        };

        return JsonSerializer.Serialize(mapping);
    }

    private static string Compact(string json) => JsonNode.Parse(json)!.ToJsonString();

    private static string ReorderKeysDeep(string json) => Transform(json, ReorderKeys);

    private static string ReverseArraysDeep(string json) => Transform(json, ReverseArrays);

    private static string ReformatNumbers(string json) =>
        Transform(json, node => node is JsonValue v && v.TryGetValue(out int i) ? JsonValue.Create((double)i) : null);

    private static string WithExtraField(string json)
    {
        var node = JsonNode.Parse(json)!;
        if (node is JsonObject obj)
        {
            obj["_extra"] = "E";
        }

        return node.ToJsonString();
    }

    private static string ChangeFirstLeaf(string json)
    {
        var node = JsonNode.Parse(json)!;
        MutateFirstLeaf(node);
        return node.ToJsonString();
    }

    private static string RemoveFirstProperty(string json)
    {
        var node = JsonNode.Parse(json)!;
        if (node is JsonObject obj && obj.Count > 0)
        {
            obj.Remove(obj.First().Key);
        }

        return node.ToJsonString();
    }

    // Rebuilds the tree applying a node replacement where the callback returns non-null.
    private static string Transform(string json, Func<JsonNode, JsonNode?> replace) =>
        Rebuild(JsonNode.Parse(json)!, replace).ToJsonString();

    private static JsonNode Rebuild(JsonNode node, Func<JsonNode, JsonNode?> replace)
    {
        switch (node)
        {
            case JsonObject obj:
            {
                var rebuilt = new JsonObject();
                foreach (var kv in obj)
                {
                    rebuilt[kv.Key] = kv.Value is null ? null : Rebuild(kv.Value, replace);
                }

                return replace(rebuilt) ?? rebuilt;
            }

            case JsonArray arr:
            {
                var rebuilt = new JsonArray();
                foreach (var item in arr)
                {
                    rebuilt.Add(item is null ? null : Rebuild(item, replace));
                }

                return replace(rebuilt) ?? rebuilt;
            }

            default:
                return replace(node) ?? node.DeepClone();
        }
    }

    private static JsonNode? ReorderKeys(JsonNode node)
    {
        if (node is not JsonObject obj)
        {
            return null;
        }

        var reordered = new JsonObject();
        foreach (var kv in obj.Reverse().ToList())
        {
            reordered[kv.Key] = kv.Value?.DeepClone();
        }

        return reordered;
    }

    private static JsonNode? ReverseArrays(JsonNode node)
    {
        if (node is not JsonArray arr)
        {
            return null;
        }

        var reversed = new JsonArray();
        foreach (var item in arr.Reverse().ToList())
        {
            reversed.Add(item?.DeepClone());
        }

        return reversed;
    }

    private static bool MutateFirstLeaf(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var kv in obj.ToList())
                {
                    if (kv.Value is JsonValue value)
                    {
                        obj[kv.Key] = MutateValue(value);
                        return true;
                    }

                    if (MutateFirstLeaf(kv.Value))
                    {
                        return true;
                    }
                }

                return false;

            case JsonArray arr:
                for (var i = 0; i < arr.Count; i++)
                {
                    if (arr[i] is JsonValue value)
                    {
                        arr[i] = MutateValue(value);
                        return true;
                    }

                    if (MutateFirstLeaf(arr[i]))
                    {
                        return true;
                    }
                }

                return false;

            default:
                return false;
        }
    }

    private static JsonNode MutateValue(JsonValue value)
    {
        if (value.TryGetValue(out int i))
        {
            return JsonValue.Create(i + 1);
        }

        if (value.TryGetValue(out string? s))
        {
            return JsonValue.Create(s + "X");
        }

        if (value.TryGetValue(out bool b))
        {
            return JsonValue.Create(!b);
        }

        return JsonValue.Create(999);
    }

    private static string Summarize(string json) => json.Length > 40 ? json[..40] + "…" : json;
}
