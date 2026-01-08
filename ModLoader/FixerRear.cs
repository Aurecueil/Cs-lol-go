using System.Numerics;
using System.Text;

namespace Jade.Ritobin;

public class BinReader
{
    private readonly byte[] _data;
    private int _offset;

    public BinReader(byte[] data)
    {
        _data = data;
        _offset = 0;
    }

    public Bin Read()
    {
        var bin = new Bin();
        ReadSections(bin);
        return bin;
    }

    private void ReadSections(Bin bin)
    {
        var magic = ReadBytes(4);
        bool isPatch = false;
        var magicStr = Encoding.ASCII.GetString(magic);

        if (magicStr == "PTCH")
        {
            var unk = ReadU64(); // patch header?
            magic = ReadBytes(4);
            magicStr = Encoding.ASCII.GetString(magic);
            bin.Sections["type"] = new BinString("PTCH");
            isPatch = true;
        }
        else
        {
            bin.Sections["type"] = new BinString("PROP");
        }

        if (magicStr != "PROP")
        {
            // Log what we actually found for debugging
            var hexMagic = BitConverter.ToString(magic).Replace("-", " ");
            throw new Exception($"Invalid magic. Expected 'PROP' but found '{magicStr}' (hex: {hexMagic}). This file may not be a valid .bin file.");
        }

        var version = ReadU32();
        bin.Sections["version"] = new BinU32(version);

        if (version >= 2)
        {
            ReadLinked(bin);
        }

        ReadEntries(bin);

        if (isPatch)
        {
            ReadPatches(bin);
        }
    }

    private void ReadLinked(Bin bin)
    {
        var list = new BinList(BinType.String);
        var count = ReadU32();
        for (int i = 0; i < count; i++)
        {
            list.Items.Add(new BinString(ReadString()));
        }
        bin.Sections["linked"] = list;
    }

    private void ReadEntries(Bin bin)
    {
        var count = ReadU32();
        var entryNameHashes = new List<uint>();
        for (int i = 0; i < count; i++)
        {
            entryNameHashes.Add(ReadU32());
        }

        var map = new BinMap(BinType.Hash, BinType.Embed);
        foreach (var hash in entryNameHashes)
        {
            var entryKeyHash = new BinHash(new FNV1a(0));
            var entry = new BinEmbed(new FNV1a(hash));
            ReadEntry(entryKeyHash, entry);
            map.Items.Add(new KeyValuePair<BinValue, BinValue>(entryKeyHash, entry));
        }
        bin.Sections["entries"] = map;
    }

    private void ReadEntry(BinHash entryKeyHash, BinEmbed entry)
    {
        var length = ReadU32();
        var startPos = _offset;

        entryKeyHash.Value = ReadFNV1a();
        var count = ReadU16();

        for (int i = 0; i < count; i++)
        {
            var name = ReadFNV1a();
            var type = (BinType)ReadU8();
            var item = ReadValue(type);
            entry.Items.Add(new BinField(name, item));
        }

        if (_offset != startPos + length)
        {
            throw new Exception($"Entry length mismatch. Expected {length}, got {_offset - startPos}");
        }
    }

    private void ReadPatches(Bin bin)
    {
        var count = ReadU32();
        var map = new BinMap(BinType.Hash, BinType.Embed);

        for (int i = 0; i < count; i++)
        {
            var entryKeyHash = new BinHash(new FNV1a(0));
            var entry = new BinEmbed(new FNV1a("patch"));
            ReadPatch(entryKeyHash, entry);
            map.Items.Add(new KeyValuePair<BinValue, BinValue>(entryKeyHash, entry));
        }
        bin.Sections["patches"] = map;
    }

    private void ReadPatch(BinHash patchKeyHash, BinEmbed patch)
    {
        patchKeyHash.Value = ReadFNV1a();
        var length = ReadU32();
        var startPos = _offset;

        var type = (BinType)ReadU8();
        var name = ReadString();
        var value = ReadValue(type);

        if (_offset != startPos + length)
        {
            throw new Exception($"Patch length mismatch");
        }

        patch.Items.Add(new BinField(new FNV1a("path"), new BinString(name)));
        patch.Items.Add(new BinField(new FNV1a("value"), value));
    }

    private BinValue ReadValue(BinType type)
    {
        switch (type)
        {
            case BinType.None: return new BinNone();
            case BinType.Bool: return new BinBool(ReadBool());
            case BinType.I8: return new BinI8(ReadI8());
            case BinType.U8: return new BinU8(ReadU8());
            case BinType.I16: return new BinI16(ReadI16());
            case BinType.U16: return new BinU16(ReadU16());
            case BinType.I32: return new BinI32(ReadI32());
            case BinType.U32: return new BinU32(ReadU32());
            case BinType.I64: return new BinI64(ReadI64());
            case BinType.U64: return new BinU64(ReadU64());
            case BinType.F32: return new BinF32(ReadF32());
            case BinType.Vec2: return new BinVec2(ReadVec2());
            case BinType.Vec3: return new BinVec3(ReadVec3());
            case BinType.Vec4: return new BinVec4(ReadVec4());
            case BinType.Mtx44: return new BinMtx44(ReadMtx44());
            case BinType.Rgba: return new BinRgba(ReadU8(), ReadU8(), ReadU8(), ReadU8());
            case BinType.String: return new BinString(ReadString());
            case BinType.Hash: return new BinHash(ReadFNV1a());
            case BinType.File: return new BinFile(ReadXXH64());

            case BinType.List:
                {
                    var valueType = (BinType)ReadU8();
                    var size = ReadU32();
                    var startPos = _offset;
                    var count = ReadU32();
                    var list = new BinList(valueType);
                    for (int i = 0; i < count; i++)
                    {
                        list.Items.Add(ReadValue(valueType));
                    }
                    if (_offset != startPos + size) throw new Exception("List size mismatch");
                    return list;
                }

            case BinType.List2:
                {
                    var valueType = (BinType)ReadU8();
                    var size = ReadU32();
                    var startPos = _offset;
                    var count = ReadU32();
                    var list = new BinList2(valueType);
                    for (int i = 0; i < count; i++)
                    {
                        list.Items.Add(ReadValue(valueType));
                    }
                    if (_offset != startPos + size) throw new Exception("List2 size mismatch");
                    return list;
                }

            case BinType.Pointer:
                {
                    var name = ReadFNV1a();
                    var ptr = new BinPointer(name);
                    if (name.Hash == 0) return ptr;

                    var size = ReadU32();
                    var startPos = _offset;
                    var count = ReadU16();
                    for (int i = 0; i < count; i++)
                    {
                        var fieldName = ReadFNV1a();
                        var fieldType = (BinType)ReadU8();
                        ptr.Items.Add(new BinField(fieldName, ReadValue(fieldType)));
                    }
                    if (_offset != startPos + size) throw new Exception("Pointer size mismatch");
                    return ptr;
                }

            case BinType.Embed:
                {
                    var name = ReadFNV1a();
                    var size = ReadU32();
                    var startPos = _offset;
                    var count = ReadU16();
                    var embed = new BinEmbed(name);
                    for (int i = 0; i < count; i++)
                    {
                        var fieldName = ReadFNV1a();
                        var fieldType = (BinType)ReadU8();
                        embed.Items.Add(new BinField(fieldName, ReadValue(fieldType)));
                    }
                    if (_offset != startPos + size) throw new Exception("Embed size mismatch");
                    return embed;
                }

            case BinType.Link: return new BinLink(ReadFNV1a());

            case BinType.Option:
                {
                    var valueType = (BinType)ReadU8();
                    var count = ReadU8();
                    var opt = new BinOption(valueType);
                    if (count != 0)
                    {
                        opt.Items.Add(ReadValue(valueType));
                    }
                    return opt;
                }

            case BinType.Map:
                {
                    var keyType = (BinType)ReadU8();
                    var valueType = (BinType)ReadU8();
                    var size = ReadU32();
                    var startPos = _offset;
                    var count = ReadU32();
                    var map = new BinMap(keyType, valueType);
                    for (int i = 0; i < count; i++)
                    {
                        var key = ReadValue(keyType);
                        var val = ReadValue(valueType);
                        map.Items.Add(new KeyValuePair<BinValue, BinValue>(key, val));
                    }
                    if (_offset != startPos + size) throw new Exception("Map size mismatch");
                    return map;
                }

            case BinType.Flag: return new BinFlag(ReadBool());

            default: throw new Exception($"Unknown type: {type}");
        }
    }

    // Primitive readers
    private byte[] ReadBytes(int count)
    {
        var bytes = new byte[count];
        Array.Copy(_data, _offset, bytes, 0, count);
        _offset += count;
        return bytes;
    }

    private bool ReadBool() => ReadU8() != 0;
    private sbyte ReadI8() => (sbyte)ReadU8();
    private byte ReadU8() => _data[_offset++];
    private short ReadI16() => BitConverter.ToInt16(ReadBytes(2));
    private ushort ReadU16() => BitConverter.ToUInt16(ReadBytes(2));
    private int ReadI32() => BitConverter.ToInt32(ReadBytes(4));
    private uint ReadU32() => BitConverter.ToUInt32(ReadBytes(4));
    private long ReadI64() => BitConverter.ToInt64(ReadBytes(8));
    private ulong ReadU64() => BitConverter.ToUInt64(ReadBytes(8));
    private float ReadF32() => BitConverter.ToSingle(ReadBytes(4));

    private Vector2 ReadVec2() => new Vector2(ReadF32(), ReadF32());
    private Vector3 ReadVec3() => new Vector3(ReadF32(), ReadF32(), ReadF32());
    private Vector4 ReadVec4() => new Vector4(ReadF32(), ReadF32(), ReadF32(), ReadF32());

    private Matrix4x4 ReadMtx44()
    {
        return new Matrix4x4(
            ReadF32(), ReadF32(), ReadF32(), ReadF32(),
            ReadF32(), ReadF32(), ReadF32(), ReadF32(),
            ReadF32(), ReadF32(), ReadF32(), ReadF32(),
            ReadF32(), ReadF32(), ReadF32(), ReadF32()
        );
    }

    private string ReadString()
    {
        var length = ReadU16();
        var bytes = ReadBytes(length);
        return Encoding.UTF8.GetString(bytes);
    }

    private FNV1a ReadFNV1a() => new FNV1a(ReadU32());
    private XXH64 ReadXXH64() => new XXH64(ReadU64());
}
