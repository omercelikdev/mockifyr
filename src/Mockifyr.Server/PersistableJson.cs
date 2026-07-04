using System.Text.Json.Nodes;

namespace Mockifyr.Server;

/// <summary>
/// Prepares a stub's source JSON for persistence (G16): stamps the stub's id into the mapping so a
/// reload keeps it stable — the WireMock reader mints a fresh id when a mapping has none. Shared by
/// every persistence provider (file, LiteDB, …) so ids round-trip identically regardless of backend.
/// </summary>
internal static class PersistableJson
{
    public static string WithId(string mappingJson, Guid id)
    {
        var node = JsonNode.Parse(mappingJson)!.AsObject();
        node["id"] = id.ToString();
        node["uuid"] = id.ToString();
        return node.ToJsonString();
    }
}
