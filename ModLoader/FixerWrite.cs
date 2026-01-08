using System.IO;
using System.Text;

namespace Jade.Ritobin;

public class BinWriter
{
    private readonly MemoryStream _stream;
    private readonly BinaryWriter _writer;

    public BinWriter()
    {
        _stream = new MemoryStream();
        _writer = new BinaryWriter(_stream);
    }

    public byte[] Write(Bin bin)
    {
        WriteSections(bin);
        return _stream.ToArray();
    }

    private void WriteSections(Bin bin)
    {
        if (!bin.Sections.TryGetValue("type", out var typeVal) || typeVal is not BinString typeStr)
            throw new Exception("Missing or invalid 'type' section");

        if (typeStr.Value == "PTCH")
        {
            _writer.Write(Encoding.ASCII.GetBytes("PTCH"));
            _writer.Write((long)0); // Unknown 64-bit value, ritobin writes 1 then 0? No, wait.
            // ritobin: writer.write(std::array{ 'P', 'T', 'C', 'H' }); writer.write(uint32_t{ 1 }); writer.write(uint32_t{ 0 });
            // Wait, ritobin writes PTCH (4 bytes), then 1 (4 bytes), then 0 (4 bytes)?
            // Let's check the C++ code again.
            // writer.write(std::array{ 'P', 'T', 'C', 'H' });
            // writer.write(uint32_t{ 1 });
            // writer.write(uint32_t{ 0 });
            // That's 12 bytes total.
            // My reader read: ReadBytes(4) (PTCH), ReadU64() (unk), ReadBytes(4) (PROP).
            // So ReadU64() covers the 1 and 0 if they are 32-bit each?
            // 1 (u32) + 0 (u32) = 0x00000001 followed by 0x00000000 (little endian) -> 0x0000000000000001?
            // Yes.
            _writer.Write((ulong)1);
        }

        _writer.Write(Encoding.ASCII.GetBytes("PROP"));

        if (!bin.Sections.TryGetValue("version", out var verVal) || verVal is not BinU32 ver)
            throw new Exception("Missing or invalid 'version' section");

        _writer.Write(ver.Value);

        if (ver.Value >= 2)
        {
            WriteLinked(bin);
        }

        WriteEntries(bin);

        if (ver.Value >= 3 && typeStr.Value == "PTCH")
        {
            WritePatches(bin);
        }
    }

    private void WriteLinked(Bin bin)
    {
        if (!bin.Sections.TryGetValue("linked", out var linkedVal))
        {
            _writer.Write((uint)0);
            return;
        }

        if (linkedVal is not BinList list || list.ValueType != BinType.String)
            throw new Exception("Invalid 'linked' section");

        _writer.Write((uint)list.Items.Count);
        foreach (var item in list.Items)
        {
            if (item is BinString str)
                WriteString(str.Value);
            else
                throw new Exception("Invalid linked item type");
        }
    }

    private void WriteEntries(Bin bin)
    {
        if (!bin.Sections.TryGetValue("entries", out var entriesVal))
        {
            _writer.Write((uint)0);
            return;
        }

        if (entriesVal is not BinMap map || map.KeyType != BinType.Hash || map.ValueType != BinType.Embed)
            throw new Exception("Invalid 'entries' section");

        _writer.Write((uint)map.Items.Count);

        // Write hashes first
        var hashesOffset = _stream.Position;
        var hashesSize = map.Items.Count * 4;
        _writer.Write(new byte[hashesSize]); // Reserve space

        var hashes = new List<uint>();

        foreach (var kvp in map.Items)
        {
            if (kvp.Key is not BinHash key || kvp.Value is not BinEmbed value)
                throw new Exception("Invalid entry item");

            hashes.Add(value.Name.Hash);
            WriteEntry(key, value);
        }

        // Go back and write hashes
        var currentPos = _stream.Position;
        _stream.Position = hashesOffset;
        foreach (var hash in hashes)
        {
            _writer.Write(hash);
        }
        _stream.Position = currentPos;
    }

    private void WriteEntry(BinHash key, BinEmbed value)
    {
        var startPos = _stream.Position;
        _writer.Write((uint)0); // Length placeholder
        _writer.Write(key.Value.Hash);
        _writer.Write((ushort)value.Items.Count);

        foreach (var field in value.Items)
        {
            _writer.Write(field.Key.Hash);
            _writer.Write((byte)field.Value.Type);
            WriteValue(field.Value);
        }

        var endPos = _stream.Position;
        var length = (uint)(endPos - startPos - 4); // Length excludes the length field itself?
                                                    // Reader: ReadU32() (length), startPos = offset. ... if (_offset != startPos + length)
                                                    // So length includes everything AFTER the length field.
                                                    // Yes.

        _stream.Position = startPos;
        _writer.Write(length);
        _stream.Position = endPos;
    }

    private void WritePatches(Bin bin)
    {
        if (!bin.Sections.TryGetValue("patches", out var patchesVal))
        {
            _writer.Write((uint)0);
            return;
        }

        if (patchesVal is not BinMap map || map.KeyType != BinType.Hash || map.ValueType != BinType.Embed)
            throw new Exception("Invalid 'patches' section");

        _writer.Write((uint)map.Items.Count);

        foreach (var kvp in map.Items)
        {
            if (kvp.Key is not BinHash key || kvp.Value is not BinEmbed value)
                throw new Exception("Invalid patch item");

            WritePatch(key, value);
        }
    }

    private void WritePatch(BinHash key, BinEmbed value)
    {
        _writer.Write(key.Value.Hash);
        var startPos = _stream.Position;
        _writer.Write((uint)0); // Length placeholder

        var pathField = value.Items.FirstOrDefault(f => f.Key.Hash == FNV1a.Calculate("path"));
        var valueField = value.Items.FirstOrDefault(f => f.Key.Hash == FNV1a.Calculate("value"));

        if (pathField == null || valueField == null)
            throw new Exception("Invalid patch structure");

        if (pathField.Value is not BinString pathStr)
            throw new Exception("Invalid patch path type");

        _writer.Write((byte)valueField.Value.Type);
        WriteString(pathStr.Value);
        WriteValue(valueField.Value);

        var endPos = _stream.Position;
        var length = (uint)(endPos - startPos - 4);

        _stream.Position = startPos;
        _writer.Write(length);
        _stream.Position = endPos;
    }

    private void WriteValue(BinValue value)
    {
        switch (value)
        {
            case BinNone: break;
            case BinBool b: _writer.Write((byte)(b.Value ? 1 : 0)); break;
            case BinI8 i8: _writer.Write(i8.Value); break;
            case BinU8 u8: _writer.Write(u8.Value); break;
            case BinI16 i16: _writer.Write(i16.Value); break;
            case BinU16 u16: _writer.Write(u16.Value); break;
            case BinI32 i32: _writer.Write(i32.Value); break;
            case BinU32 u32: _writer.Write(u32.Value); break;
            case BinI64 i64: _writer.Write(i64.Value); break;
            case BinU64 u64: _writer.Write(u64.Value); break;
            case BinF32 f32: _writer.Write(f32.Value); break;
            case BinVec2 v2: _writer.Write(v2.Value.X); _writer.Write(v2.Value.Y); break;
            case BinVec3 v3: _writer.Write(v3.Value.X); _writer.Write(v3.Value.Y); _writer.Write(v3.Value.Z); break;
            case BinVec4 v4: _writer.Write(v4.Value.X); _writer.Write(v4.Value.Y); _writer.Write(v4.Value.Z); _writer.Write(v4.Value.W); break;
            case BinMtx44 m:
                _writer.Write(m.Value.M11); _writer.Write(m.Value.M12); _writer.Write(m.Value.M13); _writer.Write(m.Value.M14);
                _writer.Write(m.Value.M21); _writer.Write(m.Value.M22); _writer.Write(m.Value.M23); _writer.Write(m.Value.M24);
                _writer.Write(m.Value.M31); _writer.Write(m.Value.M32); _writer.Write(m.Value.M33); _writer.Write(m.Value.M34);
                _writer.Write(m.Value.M41); _writer.Write(m.Value.M42); _writer.Write(m.Value.M43); _writer.Write(m.Value.M44);
                break;
            case BinRgba c: _writer.Write(c.R); _writer.Write(c.G); _writer.Write(c.B); _writer.Write(c.A); break;
            case BinString s: WriteString(s.Value); break;
            case BinHash h: _writer.Write(h.Value.Hash); break;
            case BinFile f: _writer.Write(f.Value.Hash); break;

            case BinList l:
                _writer.Write((byte)l.ValueType);
                var lStart = _stream.Position;
                _writer.Write((uint)0); // Size
                _writer.Write((uint)l.Items.Count);
                foreach (var item in l.Items) WriteValue(item);
                var lEnd = _stream.Position;
                _stream.Position = lStart;
                _writer.Write((uint)(lEnd - lStart - 4));
                _stream.Position = lEnd;
                break;

            case BinList2 l2:
                _writer.Write((byte)l2.ValueType);
                var l2Start = _stream.Position;
                _writer.Write((uint)0); // Size
                _writer.Write((uint)l2.Items.Count);
                foreach (var item in l2.Items) WriteValue(item);
                var l2End = _stream.Position;
                _stream.Position = l2Start;
                _writer.Write((uint)(l2End - l2Start - 4));
                _stream.Position = l2End;
                break;

            case BinPointer p:
                _writer.Write(p.Name.Hash);
                if (p.Name.Hash == 0) break;
                var pStart = _stream.Position;
                _writer.Write((uint)0); // Size
                _writer.Write((ushort)p.Items.Count);
                foreach (var item in p.Items)
                {
                    _writer.Write(item.Key.Hash);
                    _writer.Write((byte)item.Value.Type);
                    WriteValue(item.Value);
                }
                var pEnd = _stream.Position;
                _stream.Position = pStart;
                _writer.Write((uint)(pEnd - pStart - 4));
                _stream.Position = pEnd;
                break;

            case BinEmbed e:
                _writer.Write(e.Name.Hash);
                var eStart = _stream.Position;
                _writer.Write((uint)0); // Size
                _writer.Write((ushort)e.Items.Count);
                foreach (var item in e.Items)
                {
                    _writer.Write(item.Key.Hash);
                    _writer.Write((byte)item.Value.Type);
                    WriteValue(item.Value);
                }
                var eEnd = _stream.Position;
                _stream.Position = eStart;
                _writer.Write((uint)(eEnd - eStart - 4));
                _stream.Position = eEnd;
                break;

            case BinLink lnk: _writer.Write(lnk.Value.Hash); break;

            case BinOption o:
                _writer.Write((byte)o.ValueType);
                _writer.Write((byte)o.Items.Count);
                if (o.Items.Count > 0) WriteValue(o.Items[0]);
                break;

            case BinMap m:
                _writer.Write((byte)m.KeyType);
                _writer.Write((byte)m.ValueType);
                var mStart = _stream.Position;
                _writer.Write((uint)0); // Size
                _writer.Write((uint)m.Items.Count);
                foreach (var kvp in m.Items)
                {
                    WriteValue(kvp.Key);
                    WriteValue(kvp.Value);
                }
                var mEnd = _stream.Position;
                _stream.Position = mStart;
                _writer.Write((uint)(mEnd - mStart - 4));
                _stream.Position = mEnd;
                break;

            case BinFlag fl: _writer.Write((byte)(fl.Value ? 1 : 0)); break;

            default: throw new Exception($"Unknown value type: {value.GetType().Name}");
        }
    }

    private void WriteString(string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        _writer.Write((ushort)bytes.Length);
        _writer.Write(bytes);
    }
}
