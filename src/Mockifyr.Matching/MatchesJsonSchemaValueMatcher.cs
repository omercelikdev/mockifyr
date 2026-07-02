using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Mockifyr.Core;

namespace Mockifyr.Matching;

/// <summary>
/// Matches a JSON body against a JSON Schema (WireMock's <c>matchesJsonSchema</c>). The body must
/// parse as JSON and validate against the schema. WireMock uses networknt/json-schema-validator;
/// we use json-everything's JsonSchema.Net. The dialect is taken from the schema's <c>$schema</c>
/// (defaulting to Draft 2020-12, WireMock's default); when a <c>schemaVersion</c> is given and the
/// schema omits <c>$schema</c>, the matching meta-schema is injected so the requested draft is used.
/// Divergences are pinned by the differential suite — see docs/parity/g1-matching.md.
/// </summary>
public sealed class MatchesJsonSchemaValueMatcher : IValueMatcher
{
    private readonly JsonSchema? _schema;

    /// <param name="schemaJson">The JSON Schema (inline or string form).</param>
    /// <param name="schemaVersion">WireMock schema version token (<c>V6</c>/<c>V7</c>/<c>V201909</c>/<c>V202012</c>).</param>
    public MatchesJsonSchemaValueMatcher(string schemaJson, string? schemaVersion = null)
    {
        _schema = TryBuild(schemaJson, schemaVersion);
    }

    /// <inheritdoc />
    public MatchResult Match(bool present, IReadOnlyList<string> values)
    {
        if (!present || _schema is null || values.Count == 0)
        {
            return MatchResult.NoMatch(1d);
        }

        try
        {
            using var document = JsonDocument.Parse(values[0]);
            return _schema.Evaluate(document.RootElement).IsValid ? MatchResult.Exact : MatchResult.NoMatch(1d);
        }
        catch (JsonException)
        {
            return MatchResult.NoMatch(1d);
        }
    }

    private static JsonSchema? TryBuild(string schemaJson, string? schemaVersion)
    {
        try
        {
            var node = JsonNode.Parse(schemaJson);

            // A schema declaring $schema self-selects its dialect; otherwise honour schemaVersion.
            if (node is JsonObject obj && !obj.ContainsKey("$schema") && MetaSchemaId(schemaVersion) is { } id)
            {
                obj["$schema"] = id;
                return JsonSchema.FromText(obj.ToJsonString());
            }

            return JsonSchema.FromText(schemaJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // WireMock's default is Draft 2020-12; V4 is unsupported by JsonSchema.Net and deferred.
    private static string? MetaSchemaId(string? schemaVersion) => schemaVersion switch
    {
        "V6" => MetaSchemas.Draft6Id.ToString(),
        "V7" => MetaSchemas.Draft7Id.ToString(),
        "V201909" => MetaSchemas.Draft201909Id.ToString(),
        "V202012" => MetaSchemas.Draft202012Id.ToString(),
        _ => null,
    };
}
