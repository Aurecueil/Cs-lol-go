using System;
using System.Collections.Generic;
using System.Numerics;

namespace Jade.Ritobin;

public enum BinType : byte
{
    None = 0,
    Bool = 1,
    I8 = 2,
    U8 = 3,
    I16 = 4,
    U16 = 5,
    I32 = 6,
    U32 = 7,
    I64 = 8,
    U64 = 9,
    F32 = 10,
    Vec2 = 11,
    Vec3 = 12,
    Vec4 = 13,
    Mtx44 = 14,
    Rgba = 15,
    String = 16,
    Hash = 17,
    File = 18,
    List = 0x80 | 0,
    List2 = 0x80 | 1,
    Pointer = 0x80 | 2,
    Embed = 0x80 | 3,
    Link = 0x80 | 4,
    Option = 0x80 | 5,
    Map = 0x80 | 6,
    Flag = 0x80 | 7,
}

public struct FNV1a
{
    public uint Hash { get; set; }
    public string? String { get; set; }

    public FNV1a(uint hash, string? str = null)
    {
        Hash = hash;
        String = str;
    }

    public FNV1a(string str)
    {
        Hash = Calculate(str);
        String = str;
    }

    public static uint Calculate(string text)
    {
        uint hash = 0x811c9dc5;
        foreach (char c in text.ToLowerInvariant())
        {
            hash ^= (byte)c;
            hash *= 0x01000193;
        }
        return hash;
    }

    public override string ToString() => String ?? $"0x{Hash:x8}";
}

public struct XXH64
{
    public ulong Hash { get; set; }
    public string? String { get; set; }

    public XXH64(ulong hash, string? str = null)
    {
        Hash = hash;
        String = str;
    }

    public override string ToString() => String ?? $"0x{Hash:x16}";
}

public class Bin
{
    public Dictionary<string, BinValue> Sections { get; } = new();
}

public abstract class BinValue
{
    public abstract BinType Type { get; }
}

public class BinNone : BinValue
{
    public override BinType Type => BinType.None;
}

public class BinBool : BinValue
{
    public override BinType Type => BinType.Bool;
    public bool Value { get; set; }
    public BinBool(bool value) => Value = value;
}

public class BinI8 : BinValue
{
    public override BinType Type => BinType.I8;
    public sbyte Value { get; set; }
    public BinI8(sbyte value) => Value = value;
}

public class BinU8 : BinValue
{
    public override BinType Type => BinType.U8;
    public byte Value { get; set; }
    public BinU8(byte value) => Value = value;
}

public class BinI16 : BinValue
{
    public override BinType Type => BinType.I16;
    public short Value { get; set; }
    public BinI16(short value) => Value = value;
}

public class BinU16 : BinValue
{
    public override BinType Type => BinType.U16;
    public ushort Value { get; set; }
    public BinU16(ushort value) => Value = value;
}

public class BinI32 : BinValue
{
    public override BinType Type => BinType.I32;
    public int Value { get; set; }
    public BinI32(int value) => Value = value;
}

public class BinU32 : BinValue
{
    public override BinType Type => BinType.U32;
    public uint Value { get; set; }
    public BinU32(uint value) => Value = value;
}

public class BinI64 : BinValue
{
    public override BinType Type => BinType.I64;
    public long Value { get; set; }
    public BinI64(long value) => Value = value;
}

public class BinU64 : BinValue
{
    public override BinType Type => BinType.U64;
    public ulong Value { get; set; }
    public BinU64(ulong value) => Value = value;
}

public class BinF32 : BinValue
{
    public override BinType Type => BinType.F32;
    public float Value { get; set; }
    public BinF32(float value) => Value = value;
}

public class BinVec2 : BinValue
{
    public override BinType Type => BinType.Vec2;
    public Vector2 Value { get; set; }
    public BinVec2(Vector2 value) => Value = value;
}

public class BinVec3 : BinValue
{
    public override BinType Type => BinType.Vec3;
    public Vector3 Value { get; set; }
    public BinVec3(Vector3 value) => Value = value;
}

public class BinVec4 : BinValue
{
    public override BinType Type => BinType.Vec4;
    public Vector4 Value { get; set; }
    public BinVec4(Vector4 value) => Value = value;
}

public class BinMtx44 : BinValue
{
    public override BinType Type => BinType.Mtx44;
    public Matrix4x4 Value { get; set; }
    public BinMtx44(Matrix4x4 value) => Value = value;
}

public class BinRgba : BinValue
{
    public override BinType Type => BinType.Rgba;
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }
    public byte A { get; set; }
    public BinRgba(byte r, byte g, byte b, byte a) { R = r; G = g; B = b; A = a; }
}

public class BinString : BinValue
{
    public override BinType Type => BinType.String;
    public string Value { get; set; }
    public BinString(string value) => Value = value;
}

public class BinHash : BinValue
{
    public override BinType Type => BinType.Hash;
    public FNV1a Value { get; set; }
    public BinHash(FNV1a value) => Value = value;
}

public class BinFile : BinValue
{
    public override BinType Type => BinType.File;
    public XXH64 Value { get; set; }
    public BinFile(XXH64 value) => Value = value;
}

public class BinList : BinValue
{
    public override BinType Type => BinType.List;
    public BinType ValueType { get; set; }
    public List<BinValue> Items { get; } = new();
    public BinList(BinType valueType) => ValueType = valueType;
}

public class BinList2 : BinValue
{
    public override BinType Type => BinType.List2;
    public BinType ValueType { get; set; }
    public List<BinValue> Items { get; } = new();
    public BinList2(BinType valueType) => ValueType = valueType;
}

public class BinPointer : BinValue
{
    public override BinType Type => BinType.Pointer;
    public FNV1a Name { get; set; }
    public List<BinField> Items { get; } = new();
    public BinPointer(FNV1a name) => Name = name;
}

public class BinEmbed : BinValue
{
    public override BinType Type => BinType.Embed;
    public FNV1a Name { get; set; }
    public List<BinField> Items { get; } = new();
    public BinEmbed(FNV1a name) => Name = name;
}

public class BinLink : BinValue
{
    public override BinType Type => BinType.Link;
    public FNV1a Value { get; set; }
    public BinLink(FNV1a value) => Value = value;
}

public class BinOption : BinValue
{
    public override BinType Type => BinType.Option;
    public BinType ValueType { get; set; }
    public List<BinValue> Items { get; } = new(); // 0 or 1 item
    public BinOption(BinType valueType) => ValueType = valueType;
}

public class BinMap : BinValue
{
    public override BinType Type => BinType.Map;
    public BinType KeyType { get; set; }
    public BinType ValueType { get; set; }
    public List<KeyValuePair<BinValue, BinValue>> Items { get; } = new();
    public BinMap(BinType keyType, BinType valueType)
    {
        KeyType = keyType;
        ValueType = valueType;
    }
}

public class BinFlag : BinValue
{
    public override BinType Type => BinType.Flag;
    public bool Value { get; set; }
    public BinFlag(bool value) => Value = value;
}

public class BinField
{
    public FNV1a Key { get; set; }
    public BinValue Value { get; set; }
    public BinField(FNV1a key, BinValue value)
    {
        Key = key;
        Value = value;
    }
}
