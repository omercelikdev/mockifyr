using HandlebarsDotNet;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mockifyr.Templating;

/// <summary>
/// JSON-manipulation Handlebars helpers (G2f): <c>jsonArrayAdd</c>, <c>jsonMerge</c>,
/// <c>jsonRemove</c>, and <c>toJson</c>. The first three take JSON <em>strings</em> and emit
/// <strong>compact</strong> JSON; <c>toJson</c> emits <strong>Jackson-pretty</strong> JSON (the same
/// serialization as a <c>jsonPath</c> object). All behaviors are verified by the differential suite —
/// see docs/parity/g2-response.md.
/// </summary>
internal static class JsonHelpers
{
    public static void Register(IHandlebars handlebars)
    {
        handlebars.RegisterHelper("jsonArrayAdd", (_, arguments) => JsonArrayAdd(arguments));
        handlebars.RegisterHelper("jsonMerge", (_, arguments) => JsonMerge(arguments));
        handlebars.RegisterHelper("jsonRemove", (_, arguments) => JsonRemove(arguments));
        handlebars.RegisterHelper("toJson", (_, arguments) => ToJson(arguments));
    }

    // --- jsonArrayAdd -------------------------------------------------------------------------

    private static object JsonArrayAdd(Arguments arguments)
    {
        if (ParseToken(arguments, 0) is not JArray array)
        {
            return string.Empty;
        }

        array.Add(ParseItem(Str(arguments, 1)));

        // maxItems caps the array, dropping the oldest elements from the front.
        if (HashInt(arguments, "maxItems") is { } maxItems && maxItems >= 0)
        {
            while (array.Count > maxItems)
            {
                array.First?.Remove();
            }
        }

        return array.ToString(Formatting.None);
    }

    // --- jsonMerge ----------------------------------------------------------------------------

    private static object JsonMerge(Arguments arguments)
    {
        if (ParseToken(arguments, 0) is not JObject target || ParseToken(arguments, 1) is not JObject source)
        {
            return string.Empty;
        }

        // Deep merge: existing keys keep their position (values overridden), new keys are appended.
        target.Merge(source, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace });
        return target.ToString(Formatting.None);
    }

    // --- jsonRemove ---------------------------------------------------------------------------

    private static object JsonRemove(Arguments arguments)
    {
        var root = ParseToken(arguments, 0);
        if (root is null)
        {
            return string.Empty;
        }

        var target = root.SelectToken(Str(arguments, 1));

        // Remove the selected node — the enclosing property for an object member, else the node itself.
        if (target?.Parent is JProperty property)
        {
            property.Remove();
        }
        else
        {
            target?.Remove();
        }

        return root.ToString(Formatting.None);
    }

    // --- toJson -------------------------------------------------------------------------------

    private static object ToJson(Arguments arguments)
    {
        var token = ParseToken(arguments, 0);
        return token is null ? string.Empty : JacksonJson.Write(token);
    }

    // --- shared -------------------------------------------------------------------------------

    // Accepts either an already-parsed token (e.g. from parseJson) or a JSON string.
    private static JToken? ParseToken(Arguments arguments, int index)
    {
        if (index < arguments.Length && arguments[index] is JToken token)
        {
            return token;
        }

        try
        {
            return JToken.Parse(Str(arguments, index));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // An array item is parsed as JSON when it is valid JSON, otherwise added as a plain string.
    private static JToken ParseItem(string value)
    {
        try
        {
            return JToken.Parse(value);
        }
        catch (JsonException)
        {
            return JValue.CreateString(value);
        }
    }

    private static string Str(Arguments arguments, int index) =>
        index < arguments.Length && arguments[index] is { } value ? value.ToString() ?? string.Empty : string.Empty;

    private static int? HashInt(Arguments arguments, string key) =>
        arguments.Hash is { } hash && hash.TryGetValue(key, out var value) && int.TryParse(value?.ToString(), out var parsed)
            ? parsed
            : null;
}
