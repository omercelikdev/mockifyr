using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using HandlebarsDotNet;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mockifyr.Templating;

/// <summary>
/// WireMock's built-in <em>data extraction</em> Handlebars helpers (G2c): <c>jsonPath</c>,
/// <c>xPath</c>, <c>regexExtract</c>, <c>formData</c>, and <c>parseJson</c>. All behaviors are
/// pinned against the WireMock oracle — see docs/parity/g2-response.md. The value-returning
/// helpers (<c>jsonPath</c>, <c>xPath</c>) emit the extracted value; the assigning helpers
/// (<c>regexExtract</c> with a variable, <c>formData</c>, <c>parseJson</c>) write a variable into
/// the root template scope and render nothing.
/// </summary>
internal static class DataHelpers
{
    public static void Register(IHandlebars handlebars)
    {
        handlebars.RegisterHelper("jsonPath", (_, arguments) => JsonPath(arguments));
        handlebars.RegisterHelper("xPath", (_, arguments) => XPath(arguments));
        handlebars.RegisterHelper("regexExtract", RegexExtract);
        handlebars.RegisterHelper("formData", FormData);
        handlebars.RegisterHelper("parseJson", ParseJson);
    }

    // --- jsonPath -----------------------------------------------------------------------------

    private static object JsonPath(Arguments arguments)
    {
        var body = Str(arguments, 0);
        var path = Str(arguments, 1);

        JToken root;
        try
        {
            root = JToken.Parse(body);
        }
        catch (JsonException)
        {
            return string.Empty;
        }

        JToken? selected;
        try
        {
            selected = root.SelectToken(path);
        }
        catch (JsonException)
        {
            return string.Empty;
        }

        return Represent(selected);
    }

    // WireMock renders a jsonPath result as: raw scalar (string/number/boolean), an empty string for
    // null/missing, a compact array (`[1,2,3]`), and a Jackson-pretty-printed object.
    private static string Represent(JToken? token) => token?.Type switch
    {
        null or JTokenType.Null => string.Empty,
        JTokenType.String => token.Value<string>() ?? string.Empty,
        JTokenType.Boolean => token.Value<bool>() ? "true" : "false",
        JTokenType.Object => WriteJacksonObject((JObject)token),
        JTokenType.Array => token.ToString(Newtonsoft.Json.Formatting.None),
        _ => token.ToString(Newtonsoft.Json.Formatting.None),
    };

    // Reproduces Jackson's DefaultPrettyPrinter (WireMock's object serialization): multi-line objects
    // with a two-space indent and a `" : "` separator, and single-line `[ a, b ]` arrays.
    private static string WriteJacksonObject(JObject obj)
    {
        var sb = new StringBuilder();
        WriteJackson(obj, sb, 0);
        return sb.ToString();
    }

    private static void WriteJackson(JToken token, StringBuilder sb, int depth)
    {
        switch (token)
        {
            case JObject obj:
                var props = obj.Properties().ToList();
                if (props.Count == 0)
                {
                    sb.Append("{ }");
                    break;
                }

                sb.Append('{');
                for (var i = 0; i < props.Count; i++)
                {
                    sb.Append('\n');
                    Indent(sb, depth + 1);
                    sb.Append(JsonConvert.ToString(props[i].Name)).Append(" : ");
                    WriteJackson(props[i].Value, sb, depth + 1);
                    if (i < props.Count - 1)
                    {
                        sb.Append(',');
                    }
                }

                sb.Append('\n');
                Indent(sb, depth);
                sb.Append('}');
                break;

            case JArray arr:
                var items = arr.ToList();
                if (items.Count == 0)
                {
                    sb.Append("[ ]");
                    break;
                }

                sb.Append("[ ");
                for (var i = 0; i < items.Count; i++)
                {
                    WriteJackson(items[i], sb, depth);
                    if (i < items.Count - 1)
                    {
                        sb.Append(", ");
                    }
                }

                sb.Append(" ]");
                break;

            default:
                sb.Append(token.Type == JTokenType.String
                    ? JsonConvert.ToString(token.Value<string>())
                    : token.ToString(Newtonsoft.Json.Formatting.None));
                break;
        }
    }

    private static void Indent(StringBuilder sb, int depth) => sb.Append(' ', depth * 2);

    // --- xPath --------------------------------------------------------------------------------

    private static object XPath(Arguments arguments)
    {
        var body = Str(arguments, 0);
        var expression = Str(arguments, 1);

        XDocument document;
        try
        {
            document = XDocument.Parse(body);
        }
        catch (XmlException)
        {
            return string.Empty;
        }

        object result;
        try
        {
            result = document.XPathEvaluate(expression);
        }
        catch (XPathException)
        {
            return string.Empty;
        }

        return result switch
        {
            // Node set: WireMock emits the first node — an element serialized as XML, or the text
            // value of a text node / attribute.
            IEnumerable<object> nodes => nodes.OfType<XObject>().FirstOrDefault() switch
            {
                XElement element => element.ToString(),
                XAttribute attribute => attribute.Value,
                XText text => text.Value,
                { } other => other.ToString() ?? string.Empty,
                null => string.Empty,
            },
            bool boolean => boolean ? "true" : "false",
            double number => FormatNumber(number),
            string text => text,
            _ => result?.ToString() ?? string.Empty,
        };
    }

    private static string FormatNumber(double value) =>
        double.IsFinite(value) && value == Math.Floor(value)
            ? ((long)value).ToString(CultureInfo.InvariantCulture)
            : value.ToString(CultureInfo.InvariantCulture);

    // --- regexExtract -------------------------------------------------------------------------

    private static object RegexExtract(Context context, Arguments arguments)
    {
        var input = Str(arguments, 0);
        var pattern = Str(arguments, 1);
        var variableName = arguments.Length > 2 && arguments[2] is string name ? name : null;
        var fallback = Hash(arguments, "default");

        Regex regex;
        try
        {
            regex = new Regex(pattern);
        }
        catch (ArgumentException)
        {
            return fallback ?? string.Empty;
        }

        var match = regex.Match(input);

        if (variableName is not null)
        {
            // Assignment form: expose the capture groups (group 1..n) as an indexable list, so
            // `{{name.0}}` is the first capture group. Renders nothing itself.
            var groups = new List<string>();
            if (match.Success)
            {
                for (var i = 1; i < match.Groups.Count; i++)
                {
                    groups.Add(match.Groups[i].Value);
                }
            }

            Assign(context, variableName, groups);
            return string.Empty;
        }

        if (match.Success)
        {
            return match.Value;
        }

        return fallback ?? $"[ERROR: Nothing matched {pattern}]";
    }

    // --- formData -----------------------------------------------------------------------------

    private static object FormData(Context context, Arguments arguments)
    {
        var body = Str(arguments, 0);
        var variableName = Str(arguments, 1);
        var urlDecode = Hash(arguments, "urlDecode") is "True" or "true";

        var form = new Dictionary<string, object?>();
        foreach (var pair in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var key = separator < 0 ? pair : pair[..separator];
            var value = separator < 0 ? string.Empty : pair[(separator + 1)..];

            if (urlDecode)
            {
                key = FormDecode(key);
                value = FormDecode(value);
            }

            // First value wins, mirroring the request-model query semantics (multi-value indexing
            // is a deferred follow-up — see docs/parity/g2-response.md).
            form.TryAdd(key, value);
        }

        Assign(context, variableName, form);
        return string.Empty;
    }

    private static string FormDecode(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));

    // --- parseJson ----------------------------------------------------------------------------

    private static object ParseJson(Context context, Arguments arguments)
    {
        var json = Str(arguments, 0);
        var variableName = Str(arguments, 1);

        JToken? token;
        try
        {
            token = JToken.Parse(json);
        }
        catch (JsonException)
        {
            token = null;
        }

        Assign(context, variableName, token);
        return string.Empty;
    }

    // --- shared -------------------------------------------------------------------------------

    // Writes a variable into the root template scope so later expressions resolve it. WireMock's
    // assign helpers are root-scoped; at the top level the current context value is the root model.
    private static void Assign(Context context, string name, object? value)
    {
        if (context.Value is IDictionary<string, object?> root)
        {
            root[name] = value;
        }
    }

    private static string Str(Arguments arguments, int index) =>
        index < arguments.Length && arguments[index] is { } value ? value.ToString() ?? string.Empty : string.Empty;

    private static string? Hash(Arguments arguments, string key) =>
        arguments.Hash is { } hash && hash.TryGetValue(key, out var value) ? value?.ToString() : null;
}
