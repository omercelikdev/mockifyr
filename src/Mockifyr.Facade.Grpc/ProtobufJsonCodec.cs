using System.Text.Json;
using System.Text.Json.Nodes;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace Mockifyr.Facade.Grpc;

/// <summary>
/// Converts protobuf wire bytes to/from JSON driven purely by a <see cref="MessageDescriptor"/> (G13).
/// C#'s Google.Protobuf has no runtime <c>DynamicMessage</c>, so — given only a descriptor loaded from a
/// <c>.dsc</c> — this walks the wire format with <see cref="CodedInputStream"/>/<see cref="CodedOutputStream"/>
/// and maps each field by its descriptor. The JSON shape mirrors proto3 canonical JSON (the same
/// protobuf-java-util form WireMock's gRPC extension produces), so the resulting JSON flows through the
/// unchanged engine (<c>equalToJson</c> matching, <c>jsonBody</c> responses).
///
/// <para>Covered: the proto3 scalars, <c>string</c>/<c>bytes</c> (base64), nested messages, repeated
/// fields (packed and unpacked), <c>enum</c> (by value name), and <c>map</c> (as a JSON object).
/// 64-bit integers render as JSON strings, per proto3 JSON. Deferred: <c>oneof</c> and the well-known
/// wrapper types.</para>
/// </summary>
public static class ProtobufJsonCodec
{
    /// <summary>Decodes a protobuf message into a JSON object using its descriptor.</summary>
    public static JsonObject Decode(MessageDescriptor descriptor, byte[] bytes) => Decode(descriptor, new CodedInputStream(bytes));

    private static JsonObject Decode(MessageDescriptor descriptor, CodedInputStream stream)
    {
        var result = new JsonObject();
        uint tag;
        while ((tag = stream.ReadTag()) != 0)
        {
            var field = descriptor.FindFieldByNumber(WireFormat.GetTagFieldNumber(tag));
            if (field is null)
            {
                stream.SkipLastField();
                continue;
            }

            if (field.IsMap)
            {
                var map = result[field.JsonName] as JsonObject;
                if (map is null)
                {
                    map = [];
                    result[field.JsonName] = map;
                }

                var (key, value) = DecodeMapEntry(field, stream.ReadBytes().ToByteArray());
                map[key] = value;
            }
            else if (field.IsRepeated)
            {
                var array = result[field.JsonName] as JsonArray;
                if (array is null)
                {
                    array = [];
                    result[field.JsonName] = array;
                }

                if (IsPackable(field) && WireFormat.GetTagWireType(tag) == WireFormat.WireType.LengthDelimited)
                {
                    var packed = new CodedInputStream(stream.ReadBytes().ToByteArray());
                    while (!packed.IsAtEnd)
                    {
                        array.Add(ReadScalar(field, packed));
                    }
                }
                else
                {
                    array.Add(ReadValue(field, stream));
                }
            }
            else
            {
                result[field.JsonName] = ReadValue(field, stream);
            }
        }

        return result;
    }

    private static (string Key, JsonNode? Value) DecodeMapEntry(FieldDescriptor field, byte[] entryBytes)
    {
        var keyField = field.MessageType.FindFieldByNumber(1);
        var valueField = field.MessageType.FindFieldByNumber(2);
        var entry = Decode(field.MessageType, entryBytes);
        // proto3 JSON map keys are always strings; the value keeps its natural JSON type. Detach the
        // value from the entry object before returning it (a JsonNode can have only one parent).
        var key = entry.TryGetPropertyValue(keyField.JsonName, out var k) && k is not null ? k.ToString() : string.Empty;
        var value = entry.TryGetPropertyValue(valueField.JsonName, out var v) ? v?.DeepClone() : null;
        return (key, value);
    }

    private static JsonNode? ReadValue(FieldDescriptor field, CodedInputStream stream) => field.FieldType switch
    {
        FieldType.String => stream.ReadString(),
        FieldType.Bytes => Convert.ToBase64String(stream.ReadBytes().ToByteArray()),
        FieldType.Message => Decode(field.MessageType, stream.ReadBytes().ToByteArray()),
        _ => ReadScalar(field, stream),
    };

    private static JsonNode? ReadScalar(FieldDescriptor field, CodedInputStream stream) => field.FieldType switch
    {
        FieldType.Bool => stream.ReadBool(),
        FieldType.Int32 => stream.ReadInt32(),
        FieldType.SInt32 => stream.ReadSInt32(),
        FieldType.SFixed32 => stream.ReadSFixed32(),
        FieldType.UInt32 => stream.ReadUInt32(),
        FieldType.Fixed32 => stream.ReadFixed32(),
        FieldType.Float => stream.ReadFloat(),
        FieldType.Double => stream.ReadDouble(),
        // 64-bit integers are JSON strings in proto3 canonical JSON.
        FieldType.Int64 => stream.ReadInt64().ToString(),
        FieldType.SInt64 => stream.ReadSInt64().ToString(),
        FieldType.SFixed64 => stream.ReadSFixed64().ToString(),
        FieldType.UInt64 => stream.ReadUInt64().ToString(),
        FieldType.Fixed64 => stream.ReadFixed64().ToString(),
        // Enums render as the value name in proto3 canonical JSON (falling back to the number).
        FieldType.Enum => field.EnumType.FindValueByNumber(stream.ReadEnum())?.Name is { } name
            ? name
            : throw new NotSupportedException($"Unknown enum value on field '{field.Name}'."),
        _ => throw new NotSupportedException($"gRPC field type {field.FieldType} is not supported yet (field '{field.Name}')."),
    };

    /// <summary>Encodes a JSON object into a protobuf message using its descriptor.</summary>
    public static byte[] Encode(MessageDescriptor descriptor, JsonObject json)
    {
        using var buffer = new MemoryStream();
        var stream = new CodedOutputStream(buffer);
        Encode(descriptor, json, stream);
        stream.Flush();
        return buffer.ToArray();
    }

    private static void Encode(MessageDescriptor descriptor, JsonObject json, CodedOutputStream stream)
    {
        foreach (var field in descriptor.Fields.InFieldNumberOrder())
        {
            if (!TryGetProperty(json, field, out var node) || node is null)
            {
                continue;
            }

            if (field.IsMap)
            {
                foreach (var (key, value) in node.AsObject())
                {
                    stream.WriteTag(field.FieldNumber, WireFormat.WireType.LengthDelimited);
                    stream.WriteBytes(ByteString.CopyFrom(EncodeMapEntry(field, key, value)));
                }
            }
            else if (field.IsRepeated)
            {
                if (node is JsonArray array)
                {
                    // Repeated fields are written unpacked; every protobuf reader accepts both forms.
                    foreach (var element in array)
                    {
                        WriteValue(field, element, stream);
                    }
                }
            }
            else
            {
                WriteValue(field, node, stream);
            }
        }
    }

    private static byte[] EncodeMapEntry(FieldDescriptor field, string key, JsonNode? value)
    {
        var keyField = field.MessageType.FindFieldByNumber(1);
        var valueField = field.MessageType.FindFieldByNumber(2);
        var entry = new JsonObject
        {
            // A map key arrives as a JSON property name (string); restore its natural type for encoding.
            [keyField.JsonName] = KeyNode(keyField, key),
            [valueField.JsonName] = value?.DeepClone(),
        };
        return Encode(field.MessageType, entry);
    }

    private static JsonNode KeyNode(FieldDescriptor keyField, string key) => keyField.FieldType switch
    {
        FieldType.String => key,
        FieldType.Bool => bool.Parse(key),
        _ => long.Parse(key),
    };

    // Accept either the proto3 JSON name (camelCase) or the original field name, like protobuf's parser.
    private static bool TryGetProperty(JsonObject json, FieldDescriptor field, out JsonNode? node) =>
        json.TryGetPropertyValue(field.JsonName, out node) || json.TryGetPropertyValue(field.Name, out node);

    private static void WriteValue(FieldDescriptor field, JsonNode? node, CodedOutputStream stream)
    {
        if (node is null)
        {
            return;
        }

        switch (field.FieldType)
        {
            case FieldType.String:
                stream.WriteTag(field.FieldNumber, WireFormat.WireType.LengthDelimited);
                stream.WriteString(node.GetValue<string>());
                break;
            case FieldType.Bool:
                stream.WriteTag(field.FieldNumber, WireFormat.WireType.Varint);
                stream.WriteBool(AsBool(node));
                break;
            case FieldType.Int32:
                stream.WriteTag(field.FieldNumber, WireFormat.WireType.Varint);
                stream.WriteInt32((int)AsLong(node));
                break;
            case FieldType.SInt32:
                stream.WriteTag(field.FieldNumber, WireFormat.WireType.Varint);
                stream.WriteSInt32((int)AsLong(node));
                break;
            case FieldType.UInt32:
                stream.WriteTag(field.FieldNumber, WireFormat.WireType.Varint);
                stream.WriteUInt32((uint)AsLong(node));
                break;
            case FieldType.Fixed32:
                stream.WriteTag(field.FieldNumber, WireFormat.WireType.Fixed32);
                stream.WriteFixed32((uint)AsLong(node));
                break;
            case FieldType.SFixed32:
                stream.WriteTag(field.FieldNumber, WireFormat.WireType.Fixed32);
                stream.WriteSFixed32((int)AsLong(node));
                break;
            case FieldType.Float:
                stream.WriteTag(field.FieldNumber, WireFormat.WireType.Fixed32);
                stream.WriteFloat((float)AsDouble(node));
                break;
            case FieldType.Double:
                stream.WriteTag(field.FieldNumber, WireFormat.WireType.Fixed64);
                stream.WriteDouble(AsDouble(node));
                break;
            case FieldType.Int64:
                stream.WriteTag(field.FieldNumber, WireFormat.WireType.Varint);
                stream.WriteInt64(AsLong(node));
                break;
            case FieldType.SInt64:
                stream.WriteTag(field.FieldNumber, WireFormat.WireType.Varint);
                stream.WriteSInt64(AsLong(node));
                break;
            case FieldType.UInt64:
                stream.WriteTag(field.FieldNumber, WireFormat.WireType.Varint);
                stream.WriteUInt64((ulong)AsLong(node));
                break;
            case FieldType.Fixed64:
                stream.WriteTag(field.FieldNumber, WireFormat.WireType.Fixed64);
                stream.WriteFixed64((ulong)AsLong(node));
                break;
            case FieldType.SFixed64:
                stream.WriteTag(field.FieldNumber, WireFormat.WireType.Fixed64);
                stream.WriteSFixed64(AsLong(node));
                break;
            case FieldType.Enum:
                stream.WriteTag(field.FieldNumber, WireFormat.WireType.Varint);
                stream.WriteEnum(EnumNumber(field, node));
                break;
            case FieldType.Bytes:
                stream.WriteTag(field.FieldNumber, WireFormat.WireType.LengthDelimited);
                stream.WriteBytes(ByteString.CopyFrom(Convert.FromBase64String(node.GetValue<string>())));
                break;
            case FieldType.Message:
                stream.WriteTag(field.FieldNumber, WireFormat.WireType.LengthDelimited);
                stream.WriteBytes(ByteString.CopyFrom(Encode(field.MessageType, node.AsObject())));
                break;
            default:
                throw new NotSupportedException($"gRPC field type {field.FieldType} is not supported yet (field '{field.Name}').");
        }
    }

    // An enum value may arrive as its name (proto3 JSON) or its number; resolve to the wire number.
    private static int EnumNumber(FieldDescriptor field, JsonNode node) =>
        node.GetValueKind() == JsonValueKind.String
            ? field.EnumType.FindValueByName(node.GetValue<string>())?.Number
              ?? throw new NotSupportedException($"Unknown enum name '{node.GetValue<string>()}' on field '{field.Name}'.")
            : (int)AsLong(node);

    private static bool IsPackable(FieldDescriptor field) =>
        field.FieldType is not (FieldType.String or FieldType.Bytes or FieldType.Message or FieldType.Group);

    // JSON numbers may arrive as numbers or (for 64-bit) strings; accept both.
    private static long AsLong(JsonNode node) =>
        node.GetValueKind() == JsonValueKind.String ? long.Parse(node.GetValue<string>()) : node.GetValue<long>();

    private static double AsDouble(JsonNode node) =>
        node.GetValueKind() == JsonValueKind.String ? double.Parse(node.GetValue<string>()) : node.GetValue<double>();

    private static bool AsBool(JsonNode node) =>
        node.GetValueKind() == JsonValueKind.String ? bool.Parse(node.GetValue<string>()) : node.GetValue<bool>();
}
