using System.Collections.Concurrent;
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
///
/// <para><c>format</c> follows WireMock: it is an <em>assertion</em> on Draft-07 and earlier, and an
/// annotation-only no-op on 2019-09 and later — the reverse of JsonSchema.Net's own defaults, so the
/// dialect is pinned explicitly (inject <c>$schema</c> when absent) and format assertion is toggled per
/// draft. Divergences are pinned by the differential suite — see docs/parity/g1-matching.md.</para>
/// </summary>
public sealed class MatchesJsonSchemaValueMatcher : IValueMatcher
{
    // Remote `$ref` resolution: WireMock/networknt fetches an http(s) `$ref`, so JsonSchema.Net is
    // taught to as well (off by default). Fetched schemas are cached; failures resolve to no-match.
    private static readonly HttpClient RemoteHttp = new() { Timeout = TimeSpan.FromSeconds(5) };
    private static readonly ConcurrentDictionary<Uri, IBaseDocument?> RemoteCache = new();

    static MatchesJsonSchemaValueMatcher()
    {
        SchemaRegistry.Global.Fetch = (uri, _) => RemoteCache.GetOrAdd(uri, FetchRemote);
    }

    private static IBaseDocument? FetchRemote(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        try
        {
            return JsonSchema.FromText(RemoteHttp.GetStringAsync(uri).GetAwaiter().GetResult());
        }
        catch (Exception)
        {
            return null;
        }
    }

    private readonly JsonSchema? _schema;
    private readonly EvaluationOptions _options;

    /// <summary>The JSON Schema draft, resolved from <c>$schema</c> or the <c>schemaVersion</c> token.</summary>
    private enum Draft
    {
        V4,
        V6,
        V7,
        V201909,
        V202012,
    }

    /// <param name="schemaJson">The JSON Schema (inline or string form).</param>
    /// <param name="schemaVersion">WireMock schema version token (<c>V6</c>/<c>V7</c>/<c>V201909</c>/<c>V202012</c>).</param>
    public MatchesJsonSchemaValueMatcher(string schemaJson, string? schemaVersion = null)
    {
        (_schema, _options) = TryBuild(schemaJson, schemaVersion);
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
            var root = document.RootElement;
            if (_schema.Evaluate(root, _options).IsValid)
            {
                return MatchResult.Exact;
            }

            // WireMock/networknt quirk (learned from the oracle): a **non-string scalar** body is ALSO
            // validated as its JSON-literal string form, so e.g. `123` matches `{"type":"string"}`,
            // `{"enum":["123"]}`, or `{"const":"123"}`. Objects/arrays get no such fallback, and a string
            // body uses its parsed (unquoted) value only. See docs/parity/g1-matching.md.
            if (root.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null)
            {
                using var asString = JsonDocument.Parse(JsonSerializer.Serialize(values[0]));
                if (_schema.Evaluate(asString.RootElement, _options).IsValid)
                {
                    return MatchResult.Exact;
                }
            }

            return MatchResult.NoMatch(1d);
        }
        catch (JsonException)
        {
            return MatchResult.NoMatch(1d);
        }
    }

    private static (JsonSchema? Schema, EvaluationOptions Options) TryBuild(string schemaJson, string? schemaVersion)
    {
        try
        {
            var node = JsonNode.Parse(schemaJson);
            var declared = node is JsonObject obj && obj.TryGetPropertyValue("$schema", out var s)
                ? s?.GetValue<string>()
                : null;
            var draft = ResolveDraft(declared, schemaVersion);

            // Pin the dialect explicitly. JsonSchema.Net keys `format` off the declared dialect: with no
            // `$schema` it asserts `format`, which WireMock does not on 2019-09+. Injecting the resolved
            // meta-schema makes it treat `format` as annotation-only there, matching WireMock.
            if (node is JsonObject target && declared is null)
            {
                target["$schema"] = MetaSchemaId(draft);
                schemaJson = target.ToJsonString();
            }

            var schema = JsonSchema.FromText(schemaJson);

            // WireMock asserts `format` on Draft-07 and earlier only. JsonSchema.Net does the reverse by
            // default, so drive it explicitly with RequireFormatValidation.
            var options = new EvaluationOptions { RequireFormatValidation = AssertsFormat(draft) };
            return (schema, options);
        }
        catch (JsonException)
        {
            return (null, new EvaluationOptions());
        }
    }

    // Resolves the draft from an explicit `$schema` URI, else the WireMock `schemaVersion` token, else
    // WireMock's default (2020-12).
    private static Draft ResolveDraft(string? declaredSchema, string? schemaVersion)
    {
        if (declaredSchema is { } uri)
        {
            if (uri.Contains("draft-04", StringComparison.Ordinal)) return Draft.V4;
            if (uri.Contains("draft-06", StringComparison.Ordinal)) return Draft.V6;
            if (uri.Contains("draft-07", StringComparison.Ordinal)) return Draft.V7;
            if (uri.Contains("2019-09", StringComparison.Ordinal)) return Draft.V201909;
            return Draft.V202012;
        }

        return schemaVersion switch
        {
            "V4" => Draft.V4,
            "V6" => Draft.V6,
            "V7" => Draft.V7,
            "V201909" => Draft.V201909,
            _ => Draft.V202012,
        };
    }

    // The canonical meta-schema id for a draft. V4 is unsupported by JsonSchema.Net (deferred) and falls
    // back to the 2020-12 structure.
    private static string MetaSchemaId(Draft draft) => draft switch
    {
        Draft.V6 => MetaSchemas.Draft6Id.ToString(),
        Draft.V7 => MetaSchemas.Draft7Id.ToString(),
        Draft.V201909 => MetaSchemas.Draft201909Id.ToString(),
        _ => MetaSchemas.Draft202012Id.ToString(),
    };

    // WireMock asserts `format` on Draft-07 and earlier; 2019-09+ treats it as an annotation.
    private static bool AssertsFormat(Draft draft) => draft is Draft.V4 or Draft.V6 or Draft.V7;
}
