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
/// <para>Covered: the proto3 scalars, <c>string</c>/<c>bytes</c> (base64), nested messages, and
/// (non-packed) repeated fields. 64-bit integers render as JSON strings, per proto3 JSON. Deferred:
/// packed repeated scalars, maps, enums, oneofs, and the well-known wrapper types.</para>
/// </summary>
public static class ProtobufJsonCodec
{
    /// <summary>Decodes a protobuf message into a JSON object using its descriptor.</summary>
    public static JsonObject Decode(MessageDescriptor descriptor, byte[] bytes)
    {
        var stream = new CodedInputStream(bytes);
        return Decode(descriptor, stream);
    }

    private static JsonObject Decode(MessageDescriptor descriptor, CodedInputStream stream)
    {
        var result = new JsonObject();
        uint tag;
        while ((tag = stream.ReadTag()) != 0)
        {
            var fieldNumber = WireFormat.GetTagFieldNumber(tag);
            var field = descriptor.FindFieldByNumber(fieldNumber);
            if (field is null)
            {
                stream.SkipLastField();
                continue;
            }

            var value = ReadValue(field, stream);
            if (field.IsRepeated)
            {
                if (result[field.JsonName] is not JsonArray array)
                {
                    array = [];
                    result[field.JsonName] = array;
                }

                array.Add(value);
            }
            else
            {
                result[field.JsonName] = value;
            }
        }

        return result;
    }

    private static JsonNode? ReadValue(FieldDescriptor field, CodedInputStream stream) => field.FieldType switch
    {
        FieldType.String => stream.ReadString(),
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
        FieldType.Bytes => Convert.ToBase64String(stream.ReadBytes().ToByteArray()),
        FieldType.Message => Decode(field.MessageType, stream.ReadBytes().ToByteArray()),
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

            if (field.IsRepeated)
            {
                if (node is JsonArray array)
                {
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

    // JSON numbers may arrive as numbers or (for 64-bit) strings; accept both.
    private static long AsLong(JsonNode node) =>
        node.GetValueKind() == System.Text.Json.JsonValueKind.String ? long.Parse(node.GetValue<string>()) : node.GetValue<long>();

    private static double AsDouble(JsonNode node) =>
        node.GetValueKind() == System.Text.Json.JsonValueKind.String ? double.Parse(node.GetValue<string>()) : node.GetValue<double>();

    private static bool AsBool(JsonNode node) =>
        node.GetValueKind() == System.Text.Json.JsonValueKind.String ? bool.Parse(node.GetValue<string>()) : node.GetValue<bool>();
}
