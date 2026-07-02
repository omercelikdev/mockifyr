using System.Text;
using System.Text.Json;
using Mockifyr.Core;
using Mockifyr.Matching;

namespace Mockifyr.Adapters.WireMockJson;

/// <summary>
/// Reads WireMock stub mapping JSON (the canonical format the differential harness loads into
/// both the oracle and Mockifyr) into the internal domain model. This adapter is itself under
/// differential test. See docs/decisions/0004.
/// </summary>
/// <remarks>
/// G0 scope: <c>request.method</c>, <c>request.url</c>/<c>request.urlPath</c>, and
/// <c>response.status</c>/<c>body</c>/<c>headers</c>/<c>priority</c>. The surface grows with the
/// matching (G1) and response (G2) roadmap items.
/// </remarks>
public static class WireMockMappingReader
{
    /// <summary>Reads one or more mappings (a single object or a <c>{"mappings":[...]}</c> wrapper).</summary>
    public static IReadOnlyList<StubMapping> Read(string json, TenantId tenant)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("mappings", out var mappings) &&
            mappings.ValueKind == JsonValueKind.Array)
        {
            return [.. mappings.EnumerateArray().Select(m => ReadOne(m, tenant))];
        }

        return [ReadOne(root, tenant)];
    }

    private static StubMapping ReadOne(JsonElement mapping, TenantId tenant)
    {
        var request = mapping.TryGetProperty("request", out var r) ? r : default;
        var response = mapping.TryGetProperty("response", out var rsp) ? rsp : default;

        return new StubMapping
        {
            Id = Guid.NewGuid(),
            TenantId = tenant,
            Priority = mapping.TryGetProperty("priority", out var p) && p.TryGetInt32(out var pri) ? pri : 5,
            Request = ReadRequest(request),
            Response = ReadResponse(response),
        };
    }

    private static RequestPattern ReadRequest(JsonElement request)
    {
        IMatcher? url = null;
        if (request.ValueKind == JsonValueKind.Object)
        {
            if (request.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String)
            {
                url = new UrlEqualToMatcher(u.GetString()!);
            }
            else if (request.TryGetProperty("urlPath", out var up) && up.ValueKind == JsonValueKind.String)
            {
                url = new UrlPathEqualToMatcher(up.GetString()!);
            }
        }

        var method = request.ValueKind == JsonValueKind.Object &&
                     request.TryGetProperty("method", out var m) && m.ValueKind == JsonValueKind.String
            ? m.GetString()!
            : "ANY";

        var headers = new List<IMatcher>(
            ReadNamedMatchers(request, "headers", static (name, vm) => new HeaderMatcher(name, vm)));
        if (ReadBasicAuth(request) is { } basicAuth)
        {
            headers.Add(basicAuth);
        }

        return new RequestPattern
        {
            Url = url,
            Method = new MethodMatcher(method),
            Headers = headers,
            Query = ReadNamedMatchers(request, "queryParameters", static (name, vm) => new QueryMatcher(name, vm)),
            Cookies = ReadNamedMatchers(request, "cookies", static (name, vm) => new CookieMatcher(name, vm)),
            Body = ReadBodyMatchers(request),
        };
    }

    /// <summary>
    /// Reads <c>basicAuthCredentials</c> into an <c>Authorization: Basic &lt;base64(user:pass)&gt;</c>
    /// header matcher. Verified against the oracle: it is exact-equality on the computed token.
    /// </summary>
    private static HeaderMatcher? ReadBasicAuth(JsonElement request)
    {
        if (request.ValueKind != JsonValueKind.Object ||
            !request.TryGetProperty("basicAuthCredentials", out var creds) ||
            creds.ValueKind != JsonValueKind.Object ||
            !creds.TryGetProperty("username", out var u) || u.ValueKind != JsonValueKind.String ||
            !creds.TryGetProperty("password", out var p) || p.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var token = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{u.GetString()}:{p.GetString()}"));
        return new HeaderMatcher("Authorization", new EqualToValueMatcher(token));
    }

    private static IReadOnlyList<IMatcher> ReadNamedMatchers(
        JsonElement request,
        string property,
        Func<string, IValueMatcher, IMatcher> factory)
    {
        if (request.ValueKind != JsonValueKind.Object ||
            !request.TryGetProperty(property, out var container) ||
            container.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var matchers = new List<IMatcher>();
        foreach (var entry in container.EnumerateObject())
        {
            if (BuildValueMatcher(entry.Value) is { } value)
            {
                matchers.Add(factory(entry.Name, value));
            }
        }

        return matchers;
    }

    private static IReadOnlyList<IMatcher> ReadBodyMatchers(JsonElement request)
    {
        if (request.ValueKind != JsonValueKind.Object ||
            !request.TryGetProperty("bodyPatterns", out var patterns) ||
            patterns.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var matchers = new List<IMatcher>();
        foreach (var pattern in patterns.EnumerateArray())
        {
            if (BuildValueMatcher(pattern) is { } value)
            {
                matchers.Add(new BodyMatcher(value));
            }
        }

        return matchers;
    }

    private static IValueMatcher? BuildValueMatcher(JsonElement spec)
    {
        if (spec.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var caseInsensitive = spec.TryGetProperty("caseInsensitive", out var ci) &&
                              ci.ValueKind == JsonValueKind.True;

        // Logical combinators (and/or/not) wrap other content patterns on the same target value.
        if (spec.TryGetProperty("and", out var andArr) && andArr.ValueKind == JsonValueKind.Array)
        {
            var subs = BuildValueMatchers(andArr);
            return subs.Count > 0 ? new AndValueMatcher(subs) : null;
        }

        if (spec.TryGetProperty("or", out var orArr) && orArr.ValueKind == JsonValueKind.Array)
        {
            var subs = BuildValueMatchers(orArr);
            return subs.Count > 0 ? new OrValueMatcher(subs) : null;
        }

        if (spec.TryGetProperty("not", out var notSpec) && notSpec.ValueKind == JsonValueKind.Object)
        {
            return BuildValueMatcher(notSpec) is { } inner ? new NotValueMatcher(inner) : null;
        }

        if (spec.TryGetProperty("equalToJson", out var ej))
        {
            // equalToJson accepts either a JSON string or inline JSON.
            var expectedJson = ej.ValueKind == JsonValueKind.String ? ej.GetString()! : ej.GetRawText();
            var ignoreArrayOrder = spec.TryGetProperty("ignoreArrayOrder", out var iao) && iao.ValueKind == JsonValueKind.True;
            var ignoreExtraElements = spec.TryGetProperty("ignoreExtraElements", out var iee) && iee.ValueKind == JsonValueKind.True;
            return new EqualToJsonValueMatcher(expectedJson, ignoreArrayOrder, ignoreExtraElements);
        }

        if (spec.TryGetProperty("matchesJsonPath", out var jsonPath))
        {
            // String form is presence; object form is `{ "expression": "...", <sub-matcher> }`.
            if (jsonPath.ValueKind == JsonValueKind.String)
            {
                return new MatchesJsonPathValueMatcher(jsonPath.GetString()!);
            }

            if (jsonPath.ValueKind == JsonValueKind.Object &&
                jsonPath.TryGetProperty("expression", out var expression) &&
                expression.ValueKind == JsonValueKind.String)
            {
                return new MatchesJsonPathValueMatcher(expression.GetString()!, BuildValueMatcher(jsonPath));
            }

            return null;
        }

        if (spec.TryGetProperty("matchesJsonSchema", out var schema) &&
            schema.ValueKind is JsonValueKind.String or JsonValueKind.Object or JsonValueKind.Array)
        {
            // The schema is either an escaped JSON string or inline JSON (object/array).
            var schemaJson = schema.ValueKind == JsonValueKind.String ? schema.GetString()! : schema.GetRawText();
            var schemaVersion = spec.TryGetProperty("schemaVersion", out var sv) && sv.ValueKind == JsonValueKind.String
                ? sv.GetString()
                : null;
            return new MatchesJsonSchemaValueMatcher(schemaJson, schemaVersion);
        }

        if (spec.TryGetProperty("equalToXml", out var equalToXml) && equalToXml.ValueKind == JsonValueKind.String)
        {
            return new EqualToXmlValueMatcher(equalToXml.GetString()!);
        }

        if (spec.TryGetProperty("matchesXPath", out var xPath))
        {
            if (xPath.ValueKind == JsonValueKind.String)
            {
                return new MatchesXPathValueMatcher(xPath.GetString()!);
            }

            if (xPath.ValueKind == JsonValueKind.Object &&
                xPath.TryGetProperty("expression", out var xPathExpression) &&
                xPathExpression.ValueKind == JsonValueKind.String)
            {
                return new MatchesXPathValueMatcher(xPathExpression.GetString()!, BuildValueMatcher(xPath));
            }

            return null;
        }

        if (spec.TryGetProperty("before", out var before) && before.ValueKind == JsonValueKind.String)
        {
            return new DateTimeValueMatcher(DateTimeComparison.Before, before.GetString()!, ActualFormat(spec));
        }

        if (spec.TryGetProperty("after", out var after) && after.ValueKind == JsonValueKind.String)
        {
            return new DateTimeValueMatcher(DateTimeComparison.After, after.GetString()!, ActualFormat(spec));
        }

        if (spec.TryGetProperty("equalToDateTime", out var edt) && edt.ValueKind == JsonValueKind.String)
        {
            return new DateTimeValueMatcher(DateTimeComparison.Equal, edt.GetString()!, ActualFormat(spec));
        }

        if (spec.TryGetProperty("equalTo", out var eq) && eq.ValueKind == JsonValueKind.String)
        {
            return new EqualToValueMatcher(eq.GetString()!, caseInsensitive);
        }

        // Note: WireMock JSON has no `equalToIgnoreCase` key; case-insensitive equality is
        // `equalTo` with `caseInsensitive: true` (handled above). Verified against the oracle.

        if (spec.TryGetProperty("contains", out var c) && c.ValueKind == JsonValueKind.String)
        {
            return new ContainsValueMatcher(c.GetString()!);
        }

        if (spec.TryGetProperty("matches", out var mt) && mt.ValueKind == JsonValueKind.String)
        {
            return new MatchesValueMatcher(mt.GetString()!);
        }

        if (spec.TryGetProperty("doesNotMatch", out var dnm) && dnm.ValueKind == JsonValueKind.String)
        {
            return new DoesNotMatchValueMatcher(dnm.GetString()!);
        }

        if (spec.TryGetProperty("absent", out var absent) && absent.ValueKind == JsonValueKind.True)
        {
            return new AbsentValueMatcher();
        }

        return null;
    }

    /// <summary>Builds the value matchers from an array of matcher specs, skipping unreadable entries.</summary>
    private static List<IValueMatcher> BuildValueMatchers(JsonElement array)
    {
        var matchers = new List<IValueMatcher>();
        foreach (var element in array.EnumerateArray())
        {
            if (BuildValueMatcher(element) is { } matcher)
            {
                matchers.Add(matcher);
            }
        }

        return matchers;
    }

    /// <summary>Reads the optional <c>actualFormat</c> parse pattern shared by the date/time matchers.</summary>
    private static string? ActualFormat(JsonElement spec) =>
        spec.TryGetProperty("actualFormat", out var af) && af.ValueKind == JsonValueKind.String
            ? af.GetString()
            : null;

    private static ResponseDefinition ReadResponse(JsonElement response)
    {
        var status = response.ValueKind == JsonValueKind.Object &&
                     response.TryGetProperty("status", out var s) && s.TryGetInt32(out var code)
            ? code
            : 200;

        byte[]? body = null;
        if (response.ValueKind == JsonValueKind.Object &&
            response.TryGetProperty("body", out var b) && b.ValueKind == JsonValueKind.String)
        {
            body = Encoding.UTF8.GetBytes(b.GetString()!);
        }

        var headerPairs = new List<KeyValuePair<string, string>>();
        if (response.ValueKind == JsonValueKind.Object &&
            response.TryGetProperty("headers", out var h) && h.ValueKind == JsonValueKind.Object)
        {
            foreach (var header in h.EnumerateObject())
            {
                if (header.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var v in header.Value.EnumerateArray())
                    {
                        headerPairs.Add(new(header.Name, v.GetString() ?? string.Empty));
                    }
                }
                else
                {
                    headerPairs.Add(new(header.Name, header.Value.GetString() ?? string.Empty));
                }
            }
        }

        return new ResponseDefinition
        {
            Status = status,
            Headers = headerPairs.ToLookup(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
            Body = body,
            Transformers = [],
        };
    }
}
