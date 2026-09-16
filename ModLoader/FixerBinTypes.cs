using System.Globalization;
using System.Numerics;

namespace Jade.Ritobin;

public static class BinTypeConverter
{
    public static BinValue ConvertValue(BinValue source, BinType targetType)
    {
        if (source.Type == targetType) return source;

        object nativeVal = GetNativeObject(source);

        return targetType switch
        {
            BinType.I8 => new BinI8(ConvertToSByte(nativeVal)),
            BinType.U8 => new BinU8(ConvertToByte(nativeVal)),
            BinType.I16 => new BinI16(ConvertToShort(nativeVal)),
            BinType.U16 => new BinU16(ConvertToUShort(nativeVal)),
            BinType.I32 => new BinI32(ConvertToInt(nativeVal)),
            BinType.U32 => new BinU32(ConvertToUInt(nativeVal)),
            BinType.I64 => new BinI64(ConvertToLong(nativeVal)),
            BinType.U64 => new BinU64(ConvertToULong(nativeVal)),
            BinType.F32 => new BinF32(ConvertToFloat(nativeVal)),
            BinType.Vec2 => new BinVec2(ConvertToVector2(nativeVal)),
            BinType.Vec3 => new BinVec3(ConvertToVector3(nativeVal)),
            BinType.Vec4 => new BinVec4(ConvertToVector4(nativeVal)),
            BinType.Mtx44 => new BinMtx44(ConvertToMatrix4x4(nativeVal)),
            BinType.Rgba => ConvertToRgba(nativeVal),
            BinType.String => new BinString(ConvertToPlainString(source)),
            _ => throw new NotSupportedException($"Conversion from {source.Type} to {targetType} is not supported.")
        };
    }

    private static object GetNativeObject(BinValue val) => val switch
    {
        BinI8 v => v.Value,
        BinU8 v => v.Value,
        BinI16 v => v.Value,
        BinU16 v => v.Value,
        BinI32 v => v.Value,
        BinU32 v => v.Value,
        BinI64 v => v.Value,
        BinU64 v => v.Value,
        BinF32 v => v.Value,
        BinVec2 v => v.Value,
        BinVec3 v => v.Value,
        BinVec4 v => v.Value,
        BinMtx44 v => v.Value,
        BinRgba v => v,
        BinBool v => v.Value ? (sbyte)1 : (sbyte)0,
        BinString v => v.Value,
        _ => throw new InvalidCastException($"Cannot extract native value from {val.GetType().Name}")
    };

    private static sbyte ConvertToSByte(object obj) => obj switch
    {
        sbyte v => v,
        byte v => (sbyte)v,
        short v => (sbyte)v,
        ushort v => (sbyte)v,
        int v => (sbyte)v,
        uint v => (sbyte)v,
        long v => (sbyte)v,
        ulong v => (sbyte)v,
        float v => (sbyte)v,
        bool v => v ? (sbyte)1 : (sbyte)0,
        string s when sbyte.TryParse(s, out var res) => res,
        _ => 0
    };

    private static byte ConvertToByte(object obj) => obj switch
    {
        sbyte v => (byte)v,
        byte v => v,
        short v => (byte)v,
        ushort v => (byte)v,
        int v => (byte)v,
        uint v => (byte)v,
        long v => (byte)v,
        ulong v => (byte)v,
        float v => (byte)v,
        bool v => v ? (byte)1 : (byte)0,
        string s when byte.TryParse(s, out var res) => res,
        _ => 0
    };

    private static short ConvertToShort(object obj) => obj switch
    {
        sbyte v => v,
        byte v => v,
        short v => v,
        ushort v => (short)v,
        int v => (short)v,
        uint v => (short)v,
        long v => (short)v,
        ulong v => (short)v,
        float v => (short)v,
        bool v => v ? (short)1 : (short)0,
        string s when short.TryParse(s, out var res) => res,
        _ => 0
    };

    private static ushort ConvertToUShort(object obj) => obj switch
    {
        sbyte v => (ushort)v,
        byte v => v,
        short v => (ushort)v,
        ushort v => v,
        int v => (ushort)v,
        uint v => (ushort)v,
        long v => (ushort)v,
        ulong v => (ushort)v,
        float v => (ushort)v,
        bool v => v ? (ushort)1 : (ushort)0,
        string s when ushort.TryParse(s, out var res) => res,
        _ => 0
    };

    private static int ConvertToInt(object obj) => obj switch
    {
        sbyte v => v,
        byte v => v,
        short v => v,
        ushort v => v,
        int v => v,
        uint v => (int)v,
        long v => (int)v,
        ulong v => (int)v,
        float v => (int)v,
        bool v => v ? 1 : 0,
        string s when int.TryParse(s, out var res) => res,
        _ => 0
    };

    private static uint ConvertToUInt(object obj) => obj switch
    {
        sbyte v => (uint)v,
        byte v => v,
        short v => (uint)v,
        ushort v => v,
        int v => (uint)v,
        uint v => v,
        long v => (uint)v,
        ulong v => (uint)v,
        float v => (uint)v,
        bool v => v ? 1u : 0u,
        string s when uint.TryParse(s, out var res) => res,
        _ => 0
    };

    private static long ConvertToLong(object obj) => obj switch
    {
        sbyte v => v,
        byte v => v,
        short v => v,
        ushort v => v,
        int v => v,
        uint v => v,
        long v => v,
        ulong v => (long)v,
        float v => (long)v,
        bool v => v ? 1L : 0L,
        string s when long.TryParse(s, out var res) => res,
        _ => 0
    };

    private static ulong ConvertToULong(object obj) => obj switch
    {
        sbyte v => (ulong)v,
        byte v => v,
        short v => (ulong)v,
        ushort v => v,
        int v => (ulong)v,
        uint v => v,
        long v => (ulong)v,
        ulong v => v,
        float v => (ulong)v,
        bool v => v ? 1UL : 0UL,
        string s when ulong.TryParse(s, out var res) => res,
        _ => 0
    };

    private static float ConvertToFloat(object obj) => obj switch
    {
        sbyte v => v,
        byte v => v,
        short v => v,
        ushort v => v,
        int v => v,
        uint v => v,
        long v => v,
        ulong v => v,
        float v => v,
        Vector2 v => v.X,
        Vector3 v => v.X,
        Vector4 v => v.X,
        BinRgba c => c.R,
        bool v => v ? 1f : 0f,
        string s when float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var res) => res,
        _ => 0f
    };

    private static Vector2 ConvertToVector2(object obj) => obj switch
    {
        Vector2 v => v,
        Vector3 v => new Vector2(v.X, v.Y),
        Vector4 v => new Vector2(v.X, v.Y),
        Matrix4x4 m => new Vector2(m.M11, m.M12),
        float f => new Vector2(f, f),
        int i => new Vector2(i, i),
        _ => Vector2.Zero
    };

    private static Vector3 ConvertToVector3(object obj) => obj switch
    {
        Vector2 v => new Vector3(v.X, v.Y, 0f),
        Vector3 v => v,
        Vector4 v => new Vector3(v.X, v.Y, v.Z),
        Matrix4x4 m => new Vector3(m.M11, m.M12, m.M13),
        float f => new Vector3(f, f, f),
        int i => new Vector3(i, i, i),
        _ => Vector3.Zero
    };

    private static Vector4 ConvertToVector4(object obj) => obj switch
    {
        Vector2 v => new Vector4(v.X, v.Y, 0f, 0f),
        Vector3 v => new Vector4(v.X, v.Y, v.Z, 0f),
        Vector4 v => v,
        Matrix4x4 m => new Vector4(m.M11, m.M12, m.M13, m.M14),
        BinRgba c => new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f),
        float f => new Vector4(f, f, f, f),
        int i => new Vector4(i, i, i, i),
        _ => Vector4.Zero
    };

    private static Matrix4x4 ConvertToMatrix4x4(object obj) => obj switch
    {
        Matrix4x4 m => m,
        Vector4 v => new Matrix4x4(v.X, v.Y, v.Z, v.W, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
        _ => Matrix4x4.Identity
    };

    private static BinRgba ConvertToRgba(object obj) => obj switch
    {
        BinRgba c => new BinRgba(c.R, c.G, c.B, c.A),
        Vector4 v => new BinRgba(
            (byte)Math.Clamp(v.X * 255f, 0, 255),
            (byte)Math.Clamp(v.Y * 255f, 0, 255),
            (byte)Math.Clamp(v.Z * 255f, 0, 255),
            (byte)Math.Clamp(v.W * 255f, 0, 255)
        ),
        int i => new BinRgba((byte)(i >> 24), (byte)(i >> 16), (byte)(i >> 8), (byte)i),
        _ => new BinRgba(0, 0, 0, 255)
    };

    private static string ConvertToPlainString(BinValue val) => val switch
    {
        BinString s => s.Value,
        BinHash h => h.Value.ToString(),
        BinFile f => f.Value.ToString(),
        _ => val.ToString() ?? string.Empty
    };
}
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
