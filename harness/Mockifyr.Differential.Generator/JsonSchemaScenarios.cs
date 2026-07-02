using System.Text;
using System.Text.Json;

namespace Mockifyr.Differential.Generator;

/// <summary>
/// Fuzzes <c>matchesJsonSchema</c> (body) over the common JSON Schema subset (type / required /
/// properties / numeric bounds / enum / array items). Covers both the inline-object and string
/// schema forms and an explicit <c>schemaVersion</c>. Draft-specific edges (Draft 4, format
/// assertions, $ref resolution) are validated later; see docs/parity/g1-matching.md.
/// </summary>
public static class JsonSchemaScenarios
{
    private const string ObjectSchema =
        """{"type":"object","required":["n"],"properties":{"n":{"type":"integer","minimum":5,"maximum":100}}}""";

    private const string EnumSchema =
        """{"type":"object","required":["color"],"properties":{"color":{"enum":["red","green","blue"]}}}""";

    private const string ArraySchema =
        """{"type":"array","items":{"type":"number"},"minItems":2}""";

    /// <summary>Inline-object schema, default schema version.</summary>
    public static IEnumerable<MatcherScenario> InlineObject()
    {
        yield return Build("object/required/bounds", Inline(ObjectSchema),
            ("""{"n":7}""", true),
            ("""{"n":5}""", true),           // minimum is inclusive
            ("""{"n":100}""", true),         // maximum is inclusive
            ("""{"n":7,"extra":1}""", true), // additionalProperties allowed by default
            ("""{"n":3}""", false),          // below minimum
            ("""{"n":101}""", false),        // above maximum
            ("""{"n":"7"}""", false),        // wrong type
            ("""{"x":1}""", false),          // missing required
            ("not json", false));

        yield return Build("enum", Inline(EnumSchema),
            ("""{"color":"red"}""", true),
            ("""{"color":"blue"}""", true),
            ("""{"color":"purple"}""", false),
            ("""{}""", false));

        yield return Build("array/items/minItems", Inline(ArraySchema),
            ("[1,2]", true),
            ("[1,2,3]", true),
            ("[1]", false),          // below minItems
            ("""[1,"x"]""", false),  // wrong item type
            ("[]", false));
    }

    /// <summary>String-form schema and an explicit <c>schemaVersion</c> — both must agree with the oracle.</summary>
    public static IEnumerable<MatcherScenario> StringFormAndVersion()
    {
        // Schema supplied as an escaped JSON string rather than an inline object.
        yield return Build("string-form schema", ObjectSchema,
            ("""{"n":7}""", true),
            ("""{"n":3}""", false),
            ("""{"x":1}""", false));

        // Draft-07 schema selected explicitly via schemaVersion.
        const string draft7 =
            """{"$schema":"http://json-schema.org/draft-07/schema#","type":"object","required":["id"],"properties":{"id":{"type":"string"}}}""";
        yield return BuildVersioned("schemaVersion V7", Inline(draft7), "V7",
            ("""{"id":"abc"}""", true),
            ("""{"id":123}""", false),
            ("""{"other":"x"}""", false));
    }

    private static object Inline(string schemaJson) => JsonSerializer.Deserialize<JsonElement>(schemaJson);

    private static MatcherScenario Build(string description, object schema, params (string Body, bool Match)[] bodies) =>
        Compose(description, new Dictionary<string, object> { ["matchesJsonSchema"] = schema }, bodies);

    private static MatcherScenario BuildVersioned(
        string description, object schema, string schemaVersion, params (string Body, bool Match)[] bodies) =>
        Compose(
            description,
            new Dictionary<string, object> { ["matchesJsonSchema"] = schema, ["schemaVersion"] = schemaVersion },
            bodies);

    private static MatcherScenario Compose(string description, Dictionary<string, object> matcher, (string Body, bool Match)[] bodies)
    {
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

        var probes = bodies
            .Select(b => new ProbeRequest(
                new RequestSpec { Method = "POST", Url = "/p", Body = Encoding.UTF8.GetBytes(b.Body) },
                b.Match))
            .ToList();

        return new MatcherScenario($"matchesJsonSchema[{description}]", JsonSerializer.Serialize(mapping), probes);
    }
}
