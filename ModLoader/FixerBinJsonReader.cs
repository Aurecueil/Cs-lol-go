using System.Numerics;
using System.Text.Json;

namespace Jade.Ritobin;

public class BinJsonReader
{
    private readonly string _json;

    public BinJsonReader(string json)
    {
        _json = json;
    }

    public Bin Read()
    {
        var bin = new Bin();
        using var doc = JsonDocument.Parse(_json);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
            throw new Exception("Root must be an object");

        foreach (var property in root.EnumerateObject())
        {
            var sectionName = property.Name;
            var sectionData = property.Value;

            if (sectionData.ValueKind != JsonValueKind.Object)
                continue;

            if (sectionData.TryGetProperty("type", out var typeProp) && sectionData.TryGetProperty("value", out var valueProp))
            {
                var typeStr = typeProp.GetString();
                if (typeStr == null) continue;

                var type = ParseTypeName(typeStr);
                bin.Sections[sectionName] = ReadValue(valueProp, type);
            }
            else
            {
                // Simple format (used by some tools)
                // We'll skip for now or try to guess?
                // Let's stick to ritobin's format.
            }
        }

        return bin;
    }

    private BinType ParseTypeName(string name)
    {
        // Handle list[type], list2[type], map[key,val], option[type]
        if (name.StartsWith("list[")) return BinType.List;
        if (name.StartsWith("list2[")) return BinType.List2;
        if (name.StartsWith("map[")) return BinType.Map;
        if (name.StartsWith("option[")) return BinType.Option;

        return name.ToLowerInvariant() switch
        {
            "none" => BinType.None,
            "bool" => BinType.Bool,
            "i8" => BinType.I8,
            "u8" => BinType.U8,
            "i16" => BinType.I16,
            "u16" => BinType.U16,
            "i32" => BinType.I32,
            "u32" => BinType.U32,
            "i64" => BinType.I64,
            "u64" => BinType.U64,
            "f32" => BinType.F32,
            "vec2" => BinType.Vec2,
            "vec3" => BinType.Vec3,
            "vec4" => BinType.Vec4,
            "mtx44" => BinType.Mtx44,
            "rgba" => BinType.Rgba,
            "string" => BinType.String,
            "hash" => BinType.Hash,
            "file" => BinType.File,
            "link" => BinType.Link,
            "pointer" => BinType.Pointer,
            "embed" => BinType.Embed,
            "flag" => BinType.Flag,
            _ => throw new Exception($"Unknown type: {name}")
        };
    }

    private BinValue ReadValue(JsonElement element, BinType type)
    {
        switch (type)
        {
            case BinType.None: return new BinNone();
            case BinType.Bool: return new BinBool(element.GetBoolean());
            case BinType.I8: return new BinI8(element.GetSByte());
            case BinType.U8: return new BinU8(element.GetByte());
            case BinType.I16: return new BinI16(element.GetInt16());
            case BinType.U16: return new BinU16(element.GetUInt16());
            case BinType.I32: return new BinI32(element.GetInt32());
            case BinType.U32: return new BinU32(element.GetUInt32());
            case BinType.I64: return new BinI64(element.GetInt64());
            case BinType.U64: return new BinU64(element.GetUInt64());
            case BinType.F32: return new BinF32(element.GetSingle());
            case BinType.Vec2: return new BinVec2(new Vector2(element[0].GetSingle(), element[1].GetSingle()));
            case BinType.Vec3: return new BinVec3(new Vector3(element[0].GetSingle(), element[1].GetSingle(), element[2].GetSingle()));
            case BinType.Vec4: return new BinVec4(new Vector4(element[0].GetSingle(), element[1].GetSingle(), element[2].GetSingle(), element[3].GetSingle()));
            case BinType.Mtx44:
                return new BinMtx44(new Matrix4x4(
                    element[0].GetSingle(), element[1].GetSingle(), element[2].GetSingle(), element[3].GetSingle(),
                    element[4].GetSingle(), element[5].GetSingle(), element[6].GetSingle(), element[7].GetSingle(),
                    element[8].GetSingle(), element[9].GetSingle(), element[10].GetSingle(), element[11].GetSingle(),
                    element[12].GetSingle(), element[13].GetSingle(), element[14].GetSingle(), element[15].GetSingle()
                ));
            case BinType.Rgba: return new BinRgba(element[0].GetByte(), element[1].GetByte(), element[2].GetByte(), element[3].GetByte());
            case BinType.String: return new BinString(element.GetString() ?? "");
            case BinType.Hash: return new BinHash(ParseFNV1a(element));
            case BinType.File: return new BinFile(new XXH64(0, element.GetString())); // Simplified
            case BinType.Link: return new BinLink(ParseFNV1a(element));
            case BinType.Pointer:
            {
                var name = ParseFNV1a(element.GetProperty("name"));
                var pointer = new BinPointer(name);
                if (name.Hash != 0)
                {
                    foreach (var field in element.GetProperty("items").EnumerateArray())
                    {
                        var key = ParseFNV1a(field.GetProperty("key"));
                        var fieldType = ParseTypeName(field.GetProperty("type").GetString()!);
                        var fieldValue = ReadValue(field.GetProperty("value"), fieldType);
                        pointer.Items.Add(new BinField(key, fieldValue));
                    }
                }
                return pointer;
            }
            case BinType.Embed:
            {
                var name = ParseFNV1a(element.GetProperty("name"));
                var embed = new BinEmbed(name);
                foreach (var field in element.GetProperty("items").EnumerateArray())
                {
                    var key = ParseFNV1a(field.GetProperty("key"));
                    var fieldType = ParseTypeName(field.GetProperty("type").GetString()!);
                    var fieldValue = ReadValue(field.GetProperty("value"), fieldType);
                    embed.Items.Add(new BinField(key, fieldValue));
                }
                return embed;
            }
            case BinType.List:
            case BinType.List2:
            {
                var valueType = ParseTypeName(element.GetProperty("valueType").GetString()!);
                var list = type == BinType.List ? (BinValue)new BinList(valueType) : (BinValue)new BinList2(valueType);
                var items = (list is BinList l) ? l.Items : ((BinList2)list).Items;
                foreach (var item in element.GetProperty("items").EnumerateArray())
                {
                    items.Add(ReadValue(item, valueType));
                }
                return list;
            }
            case BinType.Option:
            {
                var valueType = ParseTypeName(element.GetProperty("valueType").GetString()!);
                var opt = new BinOption(valueType);
                foreach (var item in element.GetProperty("items").EnumerateArray())
                {
                    opt.Items.Add(ReadValue(item, valueType));
                }
                return opt;
            }
            case BinType.Map:
            {
                var keyType = ParseTypeName(element.GetProperty("keyType").GetString()!);
                var valueType = ParseTypeName(element.GetProperty("valueType").GetString()!);
                var map = new BinMap(keyType, valueType);
                foreach (var item in element.GetProperty("items").EnumerateArray())
                {
                    var key = ReadValue(item.GetProperty("key"), keyType);
                    var val = ReadValue(item.GetProperty("value"), valueType);
                    map.Items.Add(new KeyValuePair<BinValue, BinValue>(key, val));
                }
                return map;
            }
            case BinType.Flag: return new BinFlag(element.GetBoolean());
            default: throw new Exception($"Unknown type: {type}");
        }
    }

    private FNV1a ParseFNV1a(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return new FNV1a(element.GetUInt32());
        }
        if (element.ValueKind == JsonValueKind.String)
        {
            var s = element.GetString()!;
            if (s.StartsWith("0x")) return new FNV1a(uint.Parse(s.Substring(2), System.Globalization.NumberStyles.HexNumber));
            return new FNV1a(s);
        }
        return new FNV1a(0);
    }
}
