using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mockifyr.Templating;

/// <summary>
/// Serializes a Newtonsoft <see cref="JToken"/> using the Jackson <c>DefaultPrettyPrinter</c>
/// layout: multi-line objects with a two-space indent and a <c>" : "</c> separator, and
/// single-line <c>[ a, b ]</c> arrays. Shared by the <c>jsonPath</c> object rendering (G2c) and
/// the <c>toJson</c> helper (G2f); verified by the differential suite — see
/// docs/parity/g2-response.md.
/// </summary>
internal static class JacksonJson
{
    public static string Write(JToken token)
    {
        var sb = new StringBuilder();
        Write(token, sb, 0);
        return sb.ToString();
    }

    private static void Write(JToken token, StringBuilder sb, int depth)
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
                    Write(props[i].Value, sb, depth + 1);
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
                    Write(items[i], sb, depth);
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
}
