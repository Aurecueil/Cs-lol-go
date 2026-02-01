using ModManager;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Threading.Tasks;
using ZstdSharp;

namespace ModLoader
{
    public class OverlayTool
    {
        public void AnalyzeWad(string wadPath, string logOutputPath)
        {
            using var fs = new FileStream(wadPath, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);
            using var writer = new StreamWriter(logOutputPath);

            writer.WriteLine($"Analysis for: {Path.GetFileName(wadPath)}");
            writer.WriteLine("Hash | TypeByte | Subchunk | MagicBytes | CompSize | UncompSize | Ratio | RealType");
            writer.WriteLine("--------------------------------------------------------------------------------");

            fs.Seek(268, SeekOrigin.Begin);
            uint fileCount = br.ReadUInt32();

            for (int i = 0; i < fileCount; i++)
            {
                ulong hash = br.ReadUInt64();
                uint offset = br.ReadUInt32();
                uint cSize = br.ReadUInt32();
                uint uSize = br.ReadUInt32();
                byte flags = br.ReadByte();
                byte[] subchunk = br.ReadBytes(3);
                ulong checksum = br.ReadUInt64();

                // Peek at the first 4 bytes of data
                long currentPos = fs.Position;
                fs.Seek(offset, SeekOrigin.Begin);
                byte[] magic = br.ReadBytes(Math.Min(4, (int)cSize));
                fs.Seek(currentPos, SeekOrigin.Begin);

                string magicHex = BitConverter.ToString(magic).Replace("-", " ");

                // Identify real compression by magic
                string realType = "Unknown";
                if (magic.Length >= 4)
                {
                    if (magic[0] == 0x28 && magic[1] == 0xB5 && magic[2] == 0x2F && magic[3] == 0xFD)
                        realType = "Standard Zstd";
                    else if (magic[0] == 0x1F && magic[1] == 0x8B)
                        realType = "GZip";
                    else if (cSize == uSize)
                        realType = "Raw/Uncompressed";
                    else
                        realType = "Encrypted/Dictionary Zstd";
                }

                double ratio = (double)cSize / uSize;

                writer.WriteLine($"{hash:X16} | {flags:X2} | {BitConverter.ToString(subchunk)} | {magicHex} | {cSize} | {uSize} | {ratio:P2} | {realType}");
            }

            writer.WriteLine("--- End of Analysis ---");
            Console.WriteLine($"Analysis complete: {logOutputPath}");
        }
        private class WadEntryReference
        {
            public ulong Hash;
            public uint Size;
            public uint UncompressedSize;
            public byte[] Metadata = new byte[4]; // [0]=Flags, [1-3]=SubchunkIndex
            public ulong DataChecksum;
            public string SourcePath;
            public uint OriginalOffset; 
            public byte[] PrecompressedData;
            public bool IsInWad;
        }

        private static readonly HashSet<string> RawExtensions = new(StringComparer.OrdinalIgnoreCase)
        {".webm"
        };

        public void MkOverlay(string srcDir, string dstDir, string gameDir, HashSet<string> modNames)
        {
            var hashDict = new Dictionary<ulong, WadEntryReference>();

            // 1. Discovery - Original Game File
            // string baseWad = Path.Combine(gameDir, "DATA/FINAL/Champions/Viego.wad.client");
            // if (File.Exists(baseWad)) ReadWadMetadata(baseWad, hashDict);

            // 2. Discovery - Mod Overrides
            foreach (var mod in modNames)
            {
                string wadFolder = Path.Combine(srcDir, mod, "WAD");
                if (!Directory.Exists(wadFolder)) continue;
                
                // Process folders (loose files) and WADs within the mod folder
                foreach (string entry in Directory.GetFileSystemEntries(wadFolder))
                {
                    if (Directory.Exists(entry)) ReadAndCompressFolder(entry, hashDict);
                    else if (File.Exists(entry) && entry.EndsWith(".wad.client")) ReadWadMetadata(entry, hashDict);
                }
            }
            
            if (hashDict.Count == 0) return; // Prevent writing empty files

            // 3. Sort & Write
            var sortedEntries = hashDict.Values.OrderBy(e => e.Hash).ToList();
            byte[] headerChecksum = CalculateTocChecksum(sortedEntries);

            string outputPath = Path.Combine(dstDir, "DATA/FINAL/Champions/Viego.wad.client");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            WriteWadHybrid(sortedEntries, headerChecksum, outputPath);
        }

        private void ReadAndCompressFolder(string rootFolder, Dictionary<ulong, WadEntryReference> dict)
        {
            var files = Directory.GetFiles(rootFolder, "*", SearchOption.AllDirectories);
            var results = new WadEntryReference[files.Length];

            Parallel.For(0, files.Length, i =>
            {
                string file = files[i];
                string relPath = Path.GetRelativePath(rootFolder, file).Replace('\\', '/').ToLowerInvariant();
                byte[] raw = File.ReadAllBytes(file);

                ulong pathHash;
                if (!relPath.Contains('/') && ulong.TryParse(Path.GetFileNameWithoutExtension(file), System.Globalization.NumberStyles.HexNumber, null, out ulong mh))
                    pathHash = mh;
                else
                    pathHash = Repatheruwu.HashPath(relPath);

                bool shouldStayRaw = RawExtensions.Contains(Path.GetExtension(file)) || raw.Length < 128;

                byte[] compressed;
                byte type;
                if (shouldStayRaw) { compressed = raw; type = 0; }
                else
                {
                    using var compressor = new Compressor(3);
                    compressed = compressor.Wrap(raw).ToArray();
                    type = 3;
                }

                results[i] = new WadEntryReference
                {
                    Hash = pathHash,
                    PrecompressedData = compressed,
                    Size = (uint)compressed.Length,
                    UncompressedSize = (uint)raw.Length,
                    Metadata = new byte[] { type, 0, 0, 0 },
                    DataChecksum = XxHash3.HashToUInt64(compressed),
                    IsInWad = false
                };
            });

            foreach (var r in results) if (r != null) dict[r.Hash] = r;
        }

        private void ReadWadMetadata(string path, Dictionary<ulong, WadEntryReference> dict)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs);

            fs.Seek(268, SeekOrigin.Begin);
            uint count = br.ReadUInt32();

            for (int i = 0; i < count; i++)
            {
                var entry = new WadEntryReference
                {
                    SourcePath = path,
                    IsInWad = true,
                    Hash = br.ReadUInt64(),
                    OriginalOffset = br.ReadUInt32(),
                    Size = br.ReadUInt32(),
                    UncompressedSize = br.ReadUInt32(),
                    Metadata = br.ReadBytes(4),
                    DataChecksum = br.ReadUInt64()
                };
                dict[entry.Hash] = entry;
            }
        }

        private void WriteWadHybrid(List<WadEntryReference> entries, byte[] headerChecksum, string outputPath)
        {
            using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);

            // Header (v3.4)
            bw.Write(new[] { 'R', 'W' });
            bw.Write((byte)3); bw.Write((byte)4);
            bw.Write(headerChecksum);
            bw.Write(new byte[248]);
            bw.Write((uint)entries.Count);

            uint currentOffset = 272 + (uint)(entries.Count * 32);
            var writtenOffsets = new Dictionary<ulong, uint>();

            // TOC Loop
            foreach (var e in entries)
            {
                if (!writtenOffsets.TryGetValue(e.DataChecksum, out uint off))
                {
                    off = currentOffset;
                    writtenOffsets[e.DataChecksum] = currentOffset;
                    currentOffset += e.Size;
                }
                bw.Write(e.Hash);
                bw.Write(off);
                bw.Write(e.Size);
                bw.Write(e.UncompressedSize);
                bw.Write(e.Metadata);
                bw.Write(e.DataChecksum);
            }

            // Data Loop
            var writtenChecksums = new HashSet<ulong>();
            var openedWads = new Dictionary<string, FileStream>();
            byte[] buffer = new byte[81920];

            try
            {
                foreach (var e in entries)
                {
                    if (!writtenChecksums.Add(e.DataChecksum)) continue;
                    if (!e.IsInWad) bw.Write(e.PrecompressedData);
                    else
                    {
                        if (!openedWads.TryGetValue(e.SourcePath, out var sfs))
                            openedWads[e.SourcePath] = sfs = new FileStream(e.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        sfs.Seek(e.OriginalOffset, SeekOrigin.Begin);
                        long rem = e.Size;
                        while (rem > 0)
                        {
                            int r = sfs.Read(buffer, 0, (int)Math.Min(buffer.Length, rem));
                            bw.Write(buffer, 0, r);
                            rem -= r;
                        }
                    }
                }
            }
            finally { foreach (var s in openedWads.Values) s.Dispose(); }
        }

        private byte[] CalculateTocChecksum(List<WadEntryReference> entries)
        {
            var hasher = new XxHash128();
            hasher.Append(new byte[] { (byte)'R', (byte)'W', 3, 4 });
            foreach (var e in entries)
            {
                hasher.Append(BitConverter.GetBytes(e.Hash));
                hasher.Append(BitConverter.GetBytes(e.DataChecksum));
            }
            byte[] hash = hasher.GetCurrentHash();
            Array.Reverse(hash);
            return hash;
        }
    }
}