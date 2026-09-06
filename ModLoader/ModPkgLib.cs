using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Text;

namespace ModPkgLibSpace
{
    public static class Constants
    {
        public static readonly byte[] MAGIC = Encoding.ASCII.GetBytes("_modpkg_");
        public const uint VERSION = 1;
        public const uint NO_LAYER_INDEX = 0xFFFFFFFF;
        public const uint NO_WAD_INDEX = 0xFFFFFFFF;
        public const ulong NO_LAYER_HASH = 0xFFFFFFFFFFFFFFFF;

        // Meta Chunk Paths
        public const string METADATA_CHUNK_PATH = "_meta_/info.msgpack";
        public const string METADATA_ALT_CHUNK_PATH = "_meta_/metadata.msgpack";
        public const string THUMBNAIL_CHUNK_PATH = "_meta_/thumbnail.webp";
        public const string README_CHUNK_PATH = "_meta_/README.md";
    }

    public enum CompressionType : byte
    {
        None = 0,
        Zstd = 1
    }

    public enum LicenseType : byte
    {
        None = 0,
        Spdx = 1,
        Custom = 2
    }

    public class ModpkgChunk
    {
        public ulong PathHash { get; set; }
        public ulong DataOffset { get; set; }
        public CompressionType Compression { get; set; }
        public ulong CompressedSize { get; set; }
        public ulong UncompressedSize { get; set; }
        public ulong CompressedChecksum { get; set; }
        public ulong UncompressedChecksum { get; set; }
        public uint PathIndex { get; set; }
        public uint LayerIndex { get; set; }
        public uint WadIndex { get; set; }

        public ulong LayerHash { get; set; }

        public const int ChunkHeaderSize = 8 + 8 + 1 + 8 + 8 + 8 + 8 + 4 + 4 + 4; // 61 bytes
    }

    public class ModpkgLayer
    {
        public string Name { get; set; }
        public int Priority { get; set; }
        public string Description { get; set; }

        public ModpkgLayer(string name, int priority, string description = null)
        {
            Name = name;
            Priority = priority;
            Description = description ?? string.Empty;
        }
    }

    public class ModpkgLayerMetadata
    {
        public string Name { get; set; } = string.Empty;
        public int Priority { get; set; }
        public string Description { get; set; }

        public ModpkgLayerMetadata() { }

        public ModpkgLayerMetadata(string name, int priority, string description = null)
        {
            Name = name;
            Priority = priority;
            Description = description;
        }
    }

    public class ModpkgAuthor
    {
        public string Name { get; set; }
        public string Role { get; set; }

        public ModpkgAuthor(string name, string role = null)
        {
            Name = name;
            Role = role;
        }
    }

    public class ModpkgLicense
    {
        public LicenseType Type { get; set; }
        public string SpdxId { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }

        public ModpkgLicense(LicenseType type, string spdxId = null, string name = null, string url = null)
        {
            Type = type;
            SpdxId = spdxId;
            Name = name;
            Url = url;
        }

        public override string ToString()
        {
            return Type switch
            {
                LicenseType.None => "None",
                LicenseType.Spdx => $"SPDX: {SpdxId}",
                LicenseType.Custom => Url != null ? $"Custom: {Name} ({Url})" : $"Custom: {Name}",
                _ => $"Unknown ({Type})"
            };
        }
    }

    public class DistributorInfo
    {
        public string SiteId { get; set; } = string.Empty;
        public string SiteName { get; set; } = string.Empty;
        public string SiteUrl { get; set; } = string.Empty;
        public string ModId { get; set; } = string.Empty;

        public DistributorInfo() { }

        public DistributorInfo(string siteId, string siteName, string siteUrl, string modId)
        {
            SiteId = siteId;
            SiteName = siteName;
            SiteUrl = siteUrl;
            ModId = modId;
        }
    }

    public class ModpkgMetadata
    {
        public uint SchemaVersion { get; set; } = 1;
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; }
        public string Version { get; set; } = "1.0.0";
        public DistributorInfo Distributor { get; set; }
        public List<ModpkgAuthor> Authors { get; set; } = new();
        public ModpkgLicense License { get; set; } = new(LicenseType.None);
        public List<ModpkgLayerMetadata> Layers { get; set; } = new();
    }

    public class LayerInfo
    {
        public string Name { get; set; }
        public int Priority { get; set; }
        public string folder_name { get; set; }
        public bool is_active { get; set; } = false;
        public string Description { get; set; }
    }

    public class ModpkgInfo
    {
        public ModpkgMetadata Metadata { get; set; }
        public List<LayerInfo> Layers { get; set; }
        public int TotalChunks { get; set; }

        public ModpkgInfo()
        {
            Layers = new List<LayerInfo>();
        }
    }

    public static class ImageConverterMocks
    {
        public class Bitmap : IDisposable
        {
            private SixLabors.ImageSharp.Image image;
            public Bitmap(string path) => image = SixLabors.ImageSharp.Image.Load(path);
            public SixLabors.ImageSharp.Image GetImage() => image;
            public void Dispose() => image?.Dispose();
        }

        public class SimpleEncoder
        {
            public byte[] DecodeWebPToPng(byte[] webpData)
            {
                using var ms = new MemoryStream(webpData);
                using var image = SixLabors.ImageSharp.Image.Load(ms);
                using var outputMs = new MemoryStream();
                var encoder = new SixLabors.ImageSharp.Formats.Png.PngEncoder();
                image.Save(outputMs, encoder);
                return outputMs.ToArray();
            }

            public byte[] DecodeWebPToJpg(byte[] webpData, int quality = 90)
            {
                using var ms = new MemoryStream(webpData);
                using var image = SixLabors.ImageSharp.Image.Load(ms);
                using var outputMs = new MemoryStream();
                var encoder = new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = quality };
                image.Save(outputMs, encoder);
                return outputMs.ToArray();
            }
        }
    }

    public static class ModPkgLib
    {
        private static ulong HashToUInt64(byte[] hashBytes)
        {
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(hashBytes);
            }
            return BitConverter.ToUInt64(hashBytes, 0);
        }

        public static ulong HashLayerName(string name)
        {
            var bytes = Encoding.UTF8.GetBytes(name.ToLowerInvariant());
            var hash = XxHash3.Hash(bytes);
            return HashToUInt64(hash);
        }

        public static ulong HashChunkName(string name)
        {
            var bytes = Encoding.UTF8.GetBytes(name.ToLowerInvariant());
            var hash = XxHash64.Hash(bytes, 0);
            return HashToUInt64(hash);
        }

        public static ulong HashWadName(string name)
        {
            var bytes = Encoding.UTF8.GetBytes(name.ToLowerInvariant());
            var hash = XxHash3.Hash(bytes);
            return HashToUInt64(hash);
        }

        public static ulong XXH3_64(byte[] data)
        {
            var hash = XxHash3.Hash(data);
            return HashToUInt64(hash);
        }

        public static (string WadName, string ChunkPath) ParseWadPath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return (null, string.Empty);
            var normalized = relativePath.Replace('\\', '/').TrimStart('/');

            int wadClientIdx = normalized.IndexOf(".wad.client", StringComparison.OrdinalIgnoreCase);
            if (wadClientIdx >= 0)
            {
                int endIdx = wadClientIdx + ".wad.client".Length;
                if (endIdx == normalized.Length) return (normalized, string.Empty);
                if (normalized[endIdx] == '/')
                {
                    return (normalized.Substring(0, endIdx), normalized.Substring(endIdx + 1));
                }
            }

            int wadIdx = normalized.IndexOf(".wad", StringComparison.OrdinalIgnoreCase);
            if (wadIdx >= 0)
            {
                int endIdx = wadIdx + ".wad".Length;
                if (endIdx == normalized.Length) return (normalized, string.Empty);
                if (normalized[endIdx] == '/')
                {
                    return (normalized.Substring(0, endIdx), normalized.Substring(endIdx + 1));
                }
            }

            return (null, normalized);
        }

        public static ModpkgInfo GetMetadata(string modpkgPath)
        {
            using var reader = new ModpkgReader(modpkgPath);
            var info = new ModpkgInfo
            {
                Metadata = reader.Metadata,
                TotalChunks = reader.Chunks.Count
            };

            var chunksByLayer = reader.Chunks
                .GroupBy(c => c.LayerHash)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var layer in reader.Layers.Values.OrderBy(l => l.Priority))
            {
                var layerHash = HashLayerName(layer.Name);
                chunksByLayer.TryGetValue(layerHash, out int count);

                var metaLayer = reader.Metadata?.Layers?.FirstOrDefault(l => l.Name.Equals(layer.Name, StringComparison.OrdinalIgnoreCase));

                // Save base layer as "WAD", other layers as "WAD_<LayerName>"
                string folderName = layer.Name.Equals("base", StringComparison.OrdinalIgnoreCase)
                    ? "WAD"
                    : $"WAD_{layer.Name}";

                info.Layers.Add(new LayerInfo
                {
                    Name = layer.Name,
                    Priority = layer.Priority,
                    Description = metaLayer?.Description ?? layer.Description ?? string.Empty,
                    folder_name = folderName
                });
            }

            return info;
        }

        public static void Extract(string modpkgPath, string outputDir)
        {
            using var reader = new ModpkgReader(modpkgPath);
            Directory.CreateDirectory(outputDir);

            string metaFolderName = Directory.Exists(Path.Combine(outputDir, "meta")) ? "meta" :
                                    Directory.Exists(Path.Combine(outputDir, "META")) ? "META" : "meta";
            string metaDir = Path.Combine(outputDir, metaFolderName);

            var chunksByLayer = reader.Chunks
                .GroupBy(c => c.LayerHash)
                .ToDictionary(g => g.Key, g => g.ToList());

            var imageConverter = new ImageConverterMocks.SimpleEncoder();

            foreach (var (layerHash, chunks) in chunksByLayer)
            {
                bool isThumbnailLayer = reader.Layers.TryGetValue(layerHash, out var l) &&
                    (l.Name.Equals("thumbnail", StringComparison.OrdinalIgnoreCase) ||
                     l.Name.Equals("thumb", StringComparison.OrdinalIgnoreCase));

                // Base layer maps directly to "WAD"
                string layerName;
                if (layerHash == Constants.NO_LAYER_HASH)
                {
                    layerName = "WAD_no_layer";
                }
                else if (l != null)
                {
                    layerName = l.Name.Equals("base", StringComparison.OrdinalIgnoreCase) ? "WAD" : $"WAD_{l.Name}";
                }
                else
                {
                    layerName = $"WAD_{layerHash:X16}";
                }

                var layerDir = Path.Combine(outputDir, layerName);

                foreach (var chunk in chunks)
                {
                    string chunkPath = chunk.PathIndex < reader.ChunkPathList.Count
                        ? reader.ChunkPathList[(int)chunk.PathIndex]
                        : reader.ChunkPaths.GetValueOrDefault(chunk.PathHash, $"{chunk.PathHash:X16}");

                    if (chunkPath.Equals(Constants.METADATA_CHUNK_PATH, StringComparison.OrdinalIgnoreCase) ||
                        chunkPath.Equals(Constants.METADATA_ALT_CHUNK_PATH, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var data = reader.ExtractChunk(chunk);
                    byte[] finalData = data;
                    string filePath;
                    bool conversionRequired = false;

                    if (isThumbnailLayer || chunkPath.Equals(Constants.THUMBNAIL_CHUNK_PATH, StringComparison.OrdinalIgnoreCase))
                    {
                        filePath = Path.Combine(metaDir, "image.png");
                        conversionRequired = true;
                    }
                    else
                    {
                        if (chunk.WadIndex != Constants.NO_WAD_INDEX && chunk.WadIndex < reader.WadNames.Count)
                        {
                            string wadName = reader.WadNames[(int)chunk.WadIndex];
                            filePath = Path.Combine(layerDir, wadName, chunkPath);
                        }
                        else
                        {
                            filePath = Path.Combine(layerDir, chunkPath);
                        }
                    }

                    if (conversionRequired)
                    {
                        try { finalData = imageConverter.DecodeWebPToPng(data) ?? data; }
                        catch { finalData = data; }
                    }

                    var fileDir = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(fileDir)) Directory.CreateDirectory(fileDir);

                    File.WriteAllBytes(filePath, finalData);
                }
            }

            var hashesDir = Path.Combine(metaDir, "hashes");
            Directory.CreateDirectory(hashesDir);
            var hashesFilePath = Path.Combine(hashesDir, "game.hashes.txt");

            var gamePaths = reader.ChunkPathList
                .Where(p => !p.StartsWith("_meta_/", StringComparison.OrdinalIgnoreCase) &&
                            !p.StartsWith("_meta_\\", StringComparison.OrdinalIgnoreCase))
                .Select(p =>
                {
                    var (_, cleanPath) = ParseWadPath(p);
                    return string.IsNullOrEmpty(cleanPath) ? p : cleanPath;
                })
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            File.WriteAllLines(hashesFilePath, gamePaths);
        }

        private static byte[] LoadThumbnailData(string thumbnailPath)
        {
            if (string.IsNullOrEmpty(thumbnailPath) || !File.Exists(thumbnailPath)) return null;
            var extension = Path.GetExtension(thumbnailPath).ToLowerInvariant();

            try
            {
                if (extension == ".webp") return File.ReadAllBytes(thumbnailPath);
                if (extension == ".png" || extension == ".jpg" || extension == ".jpeg")
                {
                    using var image = SixLabors.ImageSharp.Image.Load(thumbnailPath);
                    using var ms = new MemoryStream();
                    image.Save(ms, new SixLabors.ImageSharp.Formats.Webp.WebpEncoder { Quality = 100 });
                    return ms.ToArray();
                }
                return null;
            }
            catch { return null; }
        }

        private static List<string> DiscoverExistingHashPaths(List<(string FolderPath, string LayerName, int Priority)> layers)
        {
            var discovered = new List<string>();
            var searchDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (folderPath, _, _) in layers)
            {
                if (string.IsNullOrWhiteSpace(folderPath)) continue;
                searchDirs.Add(folderPath);

                var parent = Directory.GetParent(folderPath)?.FullName;
                if (!string.IsNullOrEmpty(parent))
                {
                    searchDirs.Add(parent);
                    var grandParent = Directory.GetParent(parent)?.FullName;
                    if (!string.IsNullOrEmpty(grandParent)) searchDirs.Add(grandParent);
                }
            }

            string[] candidateSubPaths =
            {
                Path.Combine("meta", "hashes", "game.hashes.txt"),
                Path.Combine("META", "hashes", "game.hashes.txt"),
                Path.Combine("hashes", "game.hashes.txt"),
                Path.Combine("meta", "files.txt"),
                Path.Combine("META", "files.txt"),
                "files.txt"
            };

            foreach (var dir in searchDirs)
            {
                foreach (var sub in candidateSubPaths)
                {
                    var fullPath = Path.Combine(dir, sub);
                    if (File.Exists(fullPath))
                    {
                        foreach (var rawLine in File.ReadAllLines(fullPath))
                        {
                            var line = rawLine.Trim();
                            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                            string pathCandidate = line;
                            var parts = line.Split(new[] { ' ', '\t', ',' }, 2, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length == 2 && parts[0].Length == 16 && ulong.TryParse(parts[0], System.Globalization.NumberStyles.HexNumber, null, out _))
                            {
                                pathCandidate = parts[1].Trim();
                            }

                            var (_, cleanPath) = ParseWadPath(pathCandidate);
                            string finalPath = string.IsNullOrEmpty(cleanPath) ? pathCandidate : cleanPath;

                            if (!string.IsNullOrWhiteSpace(finalPath) &&
                                !finalPath.StartsWith("_meta_/", StringComparison.OrdinalIgnoreCase) &&
                                !finalPath.StartsWith("_meta_\\", StringComparison.OrdinalIgnoreCase))
                            {
                                discovered.Add(finalPath);
                            }
                        }
                    }
                }
            }

            return discovered.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static void Pack(List<(string FolderPath, string LayerName, int Priority)> layers,
            ModpkgMetadata metadata, string outputPath, string thumbnailPath = null, DistributorInfo distributor = null,
            IEnumerable<string> extraHeaderPaths = null)
        {
            using var writer = new ModpkgWriter(outputPath);

            if (distributor != null) metadata.Distributor = distributor;

            // 1. Discover extra paths from game.hashes.txt / files.txt
            var discoveredPaths = DiscoverExistingHashPaths(layers);
            writer.AddExtraPaths(discoveredPaths);

            if (extraHeaderPaths != null)
            {
                writer.AddExtraPaths(extraHeaderPaths);
            }

            // 2. Resolve thumbnail
            if (string.IsNullOrEmpty(thumbnailPath))
            {
                foreach (var (folderPath, layerName, _) in layers)
                {
                    if (layerName.Equals("thumbnail", StringComparison.OrdinalIgnoreCase) ||
                        layerName.Equals("thumb", StringComparison.OrdinalIgnoreCase))
                    {
                        var imgFile = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                                               .FirstOrDefault(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                                                    f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
                                                                    f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase));
                        if (imgFile != null) { thumbnailPath = imgFile; break; }
                    }

                    var parent = Directory.GetParent(folderPath)?.FullName;
                    if (parent != null)
                    {
                        var candidate = Path.Combine(parent, "meta", "image.png");
                        if (!File.Exists(candidate)) candidate = Path.Combine(parent, "META", "image.png");
                        if (File.Exists(candidate)) { thumbnailPath = candidate; break; }
                    }
                }
            }

            if (!string.IsNullOrEmpty(thumbnailPath))
            {
                var thumbnailData = LoadThumbnailData(thumbnailPath);
                if (thumbnailData != null) writer.AddMetaChunkData(Constants.THUMBNAIL_CHUNK_PATH, thumbnailData);
            }

            // 3. Register layers (Normalizing "WAD" folder input to "base" layer)
            foreach (var (folderPath, rawLayerName, priority) in layers)
            {
                if (rawLayerName.Equals("thumbnail", StringComparison.OrdinalIgnoreCase) ||
                    rawLayerName.Equals("thumb", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string layerName = rawLayerName.Equals("WAD", StringComparison.OrdinalIgnoreCase) ? "base" : rawLayerName;

                writer.AddLayer(layerName, priority);
                var existingLayer = metadata.Layers.FirstOrDefault(l => l.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase));
                if (existingLayer != null)
                {
                    existingLayer.Priority = priority;
                }
                else
                {
                    metadata.Layers.Add(new ModpkgLayerMetadata(layerName, priority));
                }
            }

            writer.SetMetadata(metadata);

            // 4. Register chunks
            foreach (var (folderPath, rawLayerName, priority) in layers)
            {
                if (rawLayerName.Equals("thumbnail", StringComparison.OrdinalIgnoreCase) ||
                    rawLayerName.Equals("thumb", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string layerName = rawLayerName.Equals("WAD", StringComparison.OrdinalIgnoreCase) ? "base" : rawLayerName;

                if (!Directory.Exists(folderPath)) throw new DirectoryNotFoundException($"Layer folder not found: {folderPath}");
                var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var relativePath = Path.GetRelativePath(folderPath, file);
                    var (wadName, chunkPath) = ParseWadPath(relativePath);
                    var data = File.ReadAllBytes(file);
                    writer.AddChunk(chunkPath, data, layerName, wadName);
                }
            }

            writer.Write();
        }
    }

    internal class ModpkgReader : IDisposable
    {
        private readonly BinaryReader _reader;
        public List<ModpkgChunk> Chunks { get; } = new();
        public Dictionary<ulong, ModpkgLayer> Layers { get; } = new();
        public Dictionary<ulong, string> ChunkPaths { get; } = new();
        public List<string> ChunkPathList { get; } = new();
        public Dictionary<ulong, string> Wads { get; } = new();
        public List<string> WadNames { get; } = new();
        public ModpkgMetadata Metadata { get; private set; }

        public ModpkgReader(string filePath)
        {
            _reader = new BinaryReader(File.OpenRead(filePath));
            ReadModpkg();
        }

        private static string ReadMsgpackString(MemoryStream stream)
        {
            var header = stream.ReadByte();
            if (header == -1) throw new EndOfStreamException();

            int len = (header & 0xE0) == 0xA0 ? header & 0x1F :
                      header == 0xD9 ? stream.ReadByte() :
                      header == 0xDA ? ReadBigEndianUInt16(stream) :
                      header == 0xDB ? (int)ReadBigEndianUInt32(stream) :
                      throw new InvalidDataException($"Invalid Msgpack string header: 0x{header:X2}");

            var valueBytes = new byte[len];
            stream.Read(valueBytes, 0, len);
            return Encoding.UTF8.GetString(valueBytes);
        }

        private static ushort ReadBigEndianUInt16(MemoryStream stream)
        {
            var b = new byte[2]; stream.Read(b, 0, 2);
            if (BitConverter.IsLittleEndian) Array.Reverse(b);
            return BitConverter.ToUInt16(b, 0);
        }

        private static uint ReadBigEndianUInt32(MemoryStream stream)
        {
            var b = new byte[4]; stream.Read(b, 0, 4);
            if (BitConverter.IsLittleEndian) Array.Reverse(b);
            return BitConverter.ToUInt32(b, 0);
        }

        private static ulong ReadBigEndianUInt64(MemoryStream stream)
        {
            var b = new byte[8]; stream.Read(b, 0, 8);
            if (BitConverter.IsLittleEndian) Array.Reverse(b);
            return BitConverter.ToUInt64(b, 0);
        }

        private static ulong ReadMsgpackUInt(MemoryStream stream)
        {
            var header = stream.ReadByte();
            if (header == -1) throw new EndOfStreamException();
            if ((header & 0x80) == 0x00) return (ulong)header;
            if (header == 0xCC) return (ulong)stream.ReadByte();
            if (header == 0xCD) return ReadBigEndianUInt16(stream);
            if (header == 0xCE) return ReadBigEndianUInt32(stream);
            if (header == 0xCF) return ReadBigEndianUInt64(stream);
            throw new InvalidDataException($"Invalid Msgpack unsigned integer header: 0x{header:X2}");
        }

        private static long ReadMsgpackInt(MemoryStream stream)
        {
            var header = stream.ReadByte();
            if (header == -1) throw new EndOfStreamException();
            if ((header & 0x80) == 0x00) return header;
            if ((header & 0xE0) == 0xE0) return (sbyte)header;
            return header switch
            {
                0xCC => stream.ReadByte(),
                0xCD => ReadBigEndianUInt16(stream),
                0xCE => ReadBigEndianUInt32(stream),
                0xCF => (long)ReadBigEndianUInt64(stream),
                0xD0 => (sbyte)stream.ReadByte(),
                0xD1 => (short)ReadBigEndianUInt16(stream),
                0xD2 => (int)ReadBigEndianUInt32(stream),
                0xD3 => (long)ReadBigEndianUInt64(stream),
                _ => throw new InvalidDataException($"Invalid Msgpack integer header: 0x{header:X2}")
            };
        }

        private static uint ReadMsgpackMapHeader(MemoryStream stream)
        {
            var header = stream.ReadByte();
            if (header == -1) throw new EndOfStreamException();
            if ((header & 0xF0) == 0x80) return (uint)(header & 0x0F);
            if (header == 0xDE) return ReadBigEndianUInt16(stream);
            if (header == 0xDF) return ReadBigEndianUInt32(stream);
            throw new InvalidDataException();
        }

        private static uint ReadMsgpackArrayHeader(MemoryStream stream)
        {
            var header = stream.ReadByte();
            if (header == -1) throw new EndOfStreamException();
            if ((header & 0xF0) == 0x90) return (uint)(header & 0x0F);
            if (header == 0xDC) return ReadBigEndianUInt16(stream);
            if (header == 0xDD) return ReadBigEndianUInt32(stream);
            throw new InvalidDataException();
        }

        private static bool ReadMsgpackNil(MemoryStream stream)
        {
            var header = stream.ReadByte();
            if (header == 0xC0) return true;
            if (header != -1) stream.Seek(-1, SeekOrigin.Current);
            return false;
        }

        private static void SkipMsgpackValue(MemoryStream stream)
        {
            var header = stream.ReadByte();
            if (header == -1) return;
            if ((header & 0x80) == 0x00 || (header & 0xE0) == 0xE0) return;
            if ((header & 0xE0) == 0xA0) { stream.Seek(header & 0x1F, SeekOrigin.Current); return; }
            if ((header & 0xF0) == 0x90) { uint len = (uint)(header & 0x0F); for (int i = 0; i < len; i++) SkipMsgpackValue(stream); return; }
            if ((header & 0xF0) == 0x80) { uint len = (uint)(header & 0x0F); for (int i = 0; i < len * 2; i++) SkipMsgpackValue(stream); return; }
            switch (header)
            {
                case 0xC0: case 0xC2: case 0xC3: return;
                case 0xCC: case 0xD0: stream.Seek(1, SeekOrigin.Current); return;
                case 0xCD: case 0xD1: stream.Seek(2, SeekOrigin.Current); return;
                case 0xCE: case 0xD2: stream.Seek(4, SeekOrigin.Current); return;
                case 0xCF: case 0xD3: stream.Seek(8, SeekOrigin.Current); return;
                case 0xD9: int len8 = stream.ReadByte(); stream.Seek(len8, SeekOrigin.Current); return;
                case 0xDA: int len16 = ReadBigEndianUInt16(stream); stream.Seek(len16, SeekOrigin.Current); return;
                case 0xDB: long len32 = ReadBigEndianUInt32(stream); stream.Seek(len32, SeekOrigin.Current); return;
                case 0xDC: uint arr16 = ReadBigEndianUInt16(stream); for (int i = 0; i < arr16; i++) SkipMsgpackValue(stream); return;
                case 0xDD: uint arr32 = ReadBigEndianUInt32(stream); for (int i = 0; i < arr32; i++) SkipMsgpackValue(stream); return;
                case 0xDE: uint map16 = ReadBigEndianUInt16(stream); for (int i = 0; i < map16 * 2; i++) SkipMsgpackValue(stream); return;
                case 0xDF: uint map32 = ReadBigEndianUInt32(stream); for (int i = 0; i < map32 * 2; i++) SkipMsgpackValue(stream); return;
            }
        }

        private void ReadModpkg()
        {
            var magic = _reader.ReadBytes(8);
            if (!magic.SequenceEqual(Constants.MAGIC)) throw new InvalidDataException("Invalid magic");

            var version = _reader.ReadUInt32();
            if (version != Constants.VERSION) throw new InvalidDataException($"Unsupported version: {version}");

            var signatureSize = _reader.ReadUInt32();
            var chunkCount = _reader.ReadUInt32();
            _reader.ReadBytes((int)signatureSize);

            var layerCount = _reader.ReadUInt32();
            var layerHashes = new List<ulong>();
            for (int i = 0; i < layerCount; i++)
            {
                var nameLen = _reader.ReadUInt32();
                var name = Encoding.UTF8.GetString(_reader.ReadBytes((int)nameLen));
                var priority = _reader.ReadInt32();
                var layerHash = ModPkgLib.HashLayerName(name);
                Layers[layerHash] = new ModpkgLayer(name, priority);
                layerHashes.Add(layerHash);
            }

            var pathCount = _reader.ReadUInt32();
            for (int i = 0; i < pathCount; i++)
            {
                var path = ReadNullTerminatedString();
                var pathHash = ModPkgLib.HashChunkName(path);
                ChunkPaths[pathHash] = path;
                ChunkPathList.Add(path);
            }

            var wadCount = _reader.ReadUInt32();
            for (int i = 0; i < wadCount; i++)
            {
                var wad = ReadNullTerminatedString();
                var wadHash = ModPkgLib.HashWadName(wad);
                Wads[wadHash] = wad;
                WadNames.Add(wad);
            }

            var currentPos = _reader.BaseStream.Position;
            var padding = (8 - (currentPos % 8)) % 8;
            _reader.ReadBytes((int)padding);

            for (int i = 0; i < chunkCount; i++)
            {
                var chunk = ReadChunk();
                chunk.LayerHash = chunk.LayerIndex == Constants.NO_LAYER_INDEX ? Constants.NO_LAYER_HASH : layerHashes[(int)chunk.LayerIndex];
                Chunks.Add(chunk);
            }

            Metadata = ReadMetadataChunk();
        }

        private ModpkgMetadata ReadMetadataChunk()
        {
            var metadata = new ModpkgMetadata();
            var metadataPathHash = ModPkgLib.HashChunkName(Constants.METADATA_CHUNK_PATH);
            var altPathHash = ModPkgLib.HashChunkName(Constants.METADATA_ALT_CHUNK_PATH);

            ModpkgChunk chunk = Chunks.FirstOrDefault(c => c.PathHash == metadataPathHash)
                             ?? Chunks.FirstOrDefault(c => c.PathHash == altPathHash)
                             ?? Chunks.FirstOrDefault(c => c.PathHash == ModPkgLib.HashChunkName(Constants.METADATA_CHUNK_PATH.Replace('/', '\\')))
                             ?? Chunks.FirstOrDefault(c => c.PathHash == ModPkgLib.HashChunkName(Constants.METADATA_ALT_CHUNK_PATH.Replace('/', '\\')));

            if (chunk == null && Chunks.Count > 0 && ChunkPathList.Count > 0)
            {
                if (ChunkPathList[0].StartsWith("_meta_/", StringComparison.OrdinalIgnoreCase))
                {
                    chunk = Chunks[0];
                }
            }

            if (chunk == null) return metadata;

            var data = ExtractChunk(chunk);
            using var stream = new MemoryStream(data);

            try
            {
                var fieldCount = ReadMsgpackMapHeader(stream);
                for (int i = 0; i < fieldCount; i++)
                {
                    var fieldName = ReadMsgpackString(stream);
                    switch (fieldName)
                    {
                        case "schema_version": metadata.SchemaVersion = (uint)ReadMsgpackUInt(stream); break;
                        case "name": metadata.Name = ReadMsgpackString(stream); break;
                        case "display_name": metadata.DisplayName = ReadMsgpackString(stream); break;
                        case "description": metadata.Description = ReadMsgpackNil(stream) ? null : ReadMsgpackString(stream); break;
                        case "version": metadata.Version = ReadMsgpackString(stream); break;
                        case "layers": ReadLayersMetadata(stream, metadata.Layers); break;
                        case "distributor":
                            if (ReadMsgpackNil(stream))
                            {
                                metadata.Distributor = null;
                            }
                            else
                            {
                                metadata.Distributor = new DistributorInfo();
                                var distMapCount = ReadMsgpackMapHeader(stream);
                                for (int j = 0; j < distMapCount; j++)
                                {
                                    var distFieldName = ReadMsgpackString(stream);
                                    var distValue = ReadMsgpackString(stream);
                                    switch (distFieldName)
                                    {
                                        case "site_id": metadata.Distributor.SiteId = distValue; break;
                                        case "site_name": metadata.Distributor.SiteName = distValue; break;
                                        case "site_url": metadata.Distributor.SiteUrl = distValue; break;
                                        case "mod_id": metadata.Distributor.ModId = distValue; break;
                                        default: break;
                                    }
                                }
                            }
                            break;
                        case "authors":
                            if (!ReadMsgpackNil(stream))
                            {
                                var authorCount = ReadMsgpackArrayHeader(stream);
                                for (int ac = 0; ac < authorCount; ac++)
                                {
                                    var amc = ReadMsgpackMapHeader(stream);
                                    string name = string.Empty, role = null;
                                    for (int j = 0; j < amc; j++)
                                    {
                                        var key = ReadMsgpackString(stream);
                                        if (key == "name") name = ReadMsgpackString(stream);
                                        else if (key == "role") role = ReadMsgpackNil(stream) ? null : ReadMsgpackString(stream);
                                        else SkipMsgpackValue(stream);
                                    }
                                    metadata.Authors.Add(new ModpkgAuthor(name, role));
                                }
                            }
                            break;
                        case "license":
                            metadata.License = ReadMsgpackNil(stream) ? new ModpkgLicense(LicenseType.None) : ReadLicense(stream);
                            break;
                        default:
                            SkipMsgpackValue(stream);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing metadata chunk: {ex.Message}");
            }
            return metadata;
        }

        private static void ReadLayersMetadata(MemoryStream stream, List<ModpkgLayerMetadata> layers)
        {
            if (ReadMsgpackNil(stream)) return;
            var count = ReadMsgpackArrayHeader(stream);
            for (uint i = 0; i < count; i++)
            {
                var fieldCount = ReadMsgpackMapHeader(stream);
                string name = string.Empty;
                string desc = null;
                int priority = 0;

                for (uint j = 0; j < fieldCount; j++)
                {
                    var key = ReadMsgpackString(stream);
                    switch (key)
                    {
                        case "name": name = ReadMsgpackString(stream); break;
                        case "priority": priority = (int)ReadMsgpackInt(stream); break;
                        case "description": desc = ReadMsgpackNil(stream) ? null : ReadMsgpackString(stream); break;
                        default: SkipMsgpackValue(stream); break;
                    }
                }
                layers.Add(new ModpkgLayerMetadata(name, priority, desc));
            }
        }

        private static ModpkgLicense ReadLicense(MemoryStream stream)
        {
            var fieldCount = ReadMsgpackMapHeader(stream);
            var license = new ModpkgLicense(LicenseType.None);
            string typeStr = null, spdxId = null, name = null, url = null;

            for (int i = 0; i < fieldCount; i++)
            {
                var key = ReadMsgpackString(stream);
                switch (key)
                {
                    case "type": typeStr = ReadMsgpackString(stream); break;
                    case "spdx_id": spdxId = ReadMsgpackString(stream); break;
                    case "name": name = ReadMsgpackString(stream); break;
                    case "url": url = ReadMsgpackString(stream); break;
                    default: SkipMsgpackValue(stream); break;
                }
            }

            if (string.Equals(typeStr, "spdx", StringComparison.OrdinalIgnoreCase))
            {
                license.Type = LicenseType.Spdx;
                license.SpdxId = spdxId;
            }
            else if (string.Equals(typeStr, "custom", StringComparison.OrdinalIgnoreCase))
            {
                license.Type = LicenseType.Custom;
                license.Name = name;
                license.Url = url;
            }
            else
            {
                license.Type = LicenseType.None;
            }
            return license;
        }

        private string ReadNullTerminatedString()
        {
            var bytes = new List<byte>();
            byte b;
            while ((b = _reader.ReadByte()) != 0) bytes.Add(b);
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        private ModpkgChunk ReadChunk()
        {
            return new ModpkgChunk
            {
                PathHash = _reader.ReadUInt64(),
                DataOffset = _reader.ReadUInt64(),
                Compression = (CompressionType)_reader.ReadByte(),
                CompressedSize = _reader.ReadUInt64(),
                UncompressedSize = _reader.ReadUInt64(),
                CompressedChecksum = _reader.ReadUInt64(),
                UncompressedChecksum = _reader.ReadUInt64(),
                PathIndex = _reader.ReadUInt32(),
                LayerIndex = _reader.ReadUInt32(),
                WadIndex = _reader.ReadUInt32()
            };
        }

        public byte[] ExtractChunk(ModpkgChunk chunk)
        {
            _reader.BaseStream.Seek((long)chunk.DataOffset, SeekOrigin.Begin);
            var compressedData = _reader.ReadBytes((int)chunk.CompressedSize);

            if (ModPkgLib.XXH3_64(compressedData) != chunk.CompressedChecksum) throw new InvalidDataException();

            byte[] data = chunk.Compression == CompressionType.None ? compressedData :
                          chunk.Compression == CompressionType.Zstd ? new ZstdSharp.Decompressor().Unwrap(compressedData, (int)chunk.UncompressedSize).ToArray() :
                          throw new NotSupportedException();

            if (ModPkgLib.XXH3_64(data) != chunk.UncompressedChecksum) throw new InvalidDataException();
            return data;
        }

        public void Dispose() => _reader?.Dispose();
    }

    internal class ModpkgWriter : IDisposable
    {
        private readonly string _outputPath;
        private ModpkgMetadata _metadata;
        private readonly List<ModpkgLayer> _layers = new();
        private readonly List<(string Path, byte[] Data, string LayerName, string WadName)> _chunksToProcess = new();
        private readonly List<(string Path, byte[] Data)> _otherMetaChunks = new();
        private readonly List<string> _extraHeaderPaths = new();
        private readonly List<ModpkgChunk> _finalChunks = new();

        public ModpkgWriter(string outputPath) => _outputPath = outputPath;

        public void SetMetadata(ModpkgMetadata metadata) => _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));

        public void AddMetaChunkData(string path, byte[] data)
        {
            var existingIndex = _otherMetaChunks.FindIndex(c => c.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0) _otherMetaChunks[existingIndex] = (path, data);
            else _otherMetaChunks.Add((path, data));
        }

        public void AddLayer(string name, int priority)
        {
            if (!_layers.Any(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) _layers.Add(new ModpkgLayer(name, priority));
        }

        public void AddChunk(string path, byte[] data, string layerName, string wadName = null)
        {
            _chunksToProcess.Add((path, data, layerName, wadName));
        }

        public void AddExtraPaths(IEnumerable<string> paths)
        {
            if (paths == null) return;
            foreach (var p in paths)
            {
                if (!string.IsNullOrWhiteSpace(p) && !_extraHeaderPaths.Contains(p, StringComparer.OrdinalIgnoreCase))
                {
                    _extraHeaderPaths.Add(p);
                }
            }
        }

        private static void WriteMsgpackString(MemoryStream stream, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            var len = bytes.Length;

            if (len < 32) stream.WriteByte((byte)(0xA0 | len));
            else if (len < 256) { stream.WriteByte(0xD9); stream.WriteByte((byte)len); }
            else if (len < 65536)
            {
                stream.WriteByte(0xDA);
                stream.Write(BitConverter.GetBytes((ushort)len).Reverse().ToArray(), 0, 2);
            }
            else
            {
                stream.WriteByte(0xDB);
                stream.Write(BitConverter.GetBytes((uint)len).Reverse().ToArray(), 0, 4);
            }
            stream.Write(bytes, 0, len);
        }

        private static void WriteMsgpackUInt(MemoryStream stream, ulong value)
        {
            if (value <= 127) stream.WriteByte((byte)value);
            else if (value <= 0xFF) { stream.WriteByte(0xCC); stream.WriteByte((byte)value); }
            else if (value <= 0xFFFF) { stream.WriteByte(0xCD); stream.Write(BitConverter.GetBytes((ushort)value).Reverse().ToArray(), 0, 2); }
            else if (value <= 0xFFFFFFFF) { stream.WriteByte(0xCE); stream.Write(BitConverter.GetBytes((uint)value).Reverse().ToArray(), 0, 4); }
            else { stream.WriteByte(0xCF); stream.Write(BitConverter.GetBytes(value).Reverse().ToArray(), 0, 8); }
        }

        private static void WriteMsgpackInt(MemoryStream stream, long value)
        {
            if (value >= 0)
            {
                WriteMsgpackUInt(stream, (ulong)value);
                return;
            }

            if (value >= -32)
            {
                stream.WriteByte((byte)(0xE0 | (value & 0x1F)));
            }
            else if (value >= sbyte.MinValue)
            {
                stream.WriteByte(0xD0);
                stream.WriteByte((byte)(sbyte)value);
            }
            else if (value >= short.MinValue)
            {
                stream.WriteByte(0xD1);
                var b = BitConverter.GetBytes((short)value);
                if (BitConverter.IsLittleEndian) Array.Reverse(b);
                stream.Write(b, 0, 2);
            }
            else if (value >= int.MinValue)
            {
                stream.WriteByte(0xD2);
                var b = BitConverter.GetBytes((int)value);
                if (BitConverter.IsLittleEndian) Array.Reverse(b);
                stream.Write(b, 0, 4);
            }
            else
            {
                stream.WriteByte(0xD3);
                var b = BitConverter.GetBytes(value);
                if (BitConverter.IsLittleEndian) Array.Reverse(b);
                stream.Write(b, 0, 8);
            }
        }

        private static void WriteMsgpackMapHeader(MemoryStream stream, uint count)
        {
            if (count <= 15) stream.WriteByte((byte)(0x80 | count));
            else if (count <= 0xFFFF) { stream.WriteByte(0xDE); stream.Write(BitConverter.GetBytes((ushort)count).Reverse().ToArray(), 0, 2); }
            else { stream.WriteByte(0xDF); stream.Write(BitConverter.GetBytes((uint)count).Reverse().ToArray(), 0, 4); }
        }

        private static void WriteMsgpackArrayHeader(MemoryStream stream, uint count)
        {
            if (count <= 15) stream.WriteByte((byte)(0x90 | count));
            else if (count <= 0xFFFF) { stream.WriteByte(0xDC); stream.Write(BitConverter.GetBytes((ushort)count).Reverse().ToArray(), 0, 2); }
            else { stream.WriteByte(0xDD); stream.Write(BitConverter.GetBytes((uint)count).Reverse().ToArray(), 0, 4); }
        }

        private static void WriteMsgpackNil(MemoryStream stream) => stream.WriteByte(0xC0);

        private byte[] SerializeMetadataMsgpack(ModpkgMetadata metadata)
        {
            using var stream = new MemoryStream();

            WriteMsgpackMapHeader(stream, 9);

            WriteMsgpackString(stream, "schema_version");
            WriteMsgpackUInt(stream, metadata.SchemaVersion);

            WriteMsgpackString(stream, "name");
            WriteMsgpackString(stream, metadata.Name);

            WriteMsgpackString(stream, "display_name");
            WriteMsgpackString(stream, metadata.DisplayName);

            WriteMsgpackString(stream, "description");
            if (metadata.Description == null) WriteMsgpackNil(stream);
            else WriteMsgpackString(stream, metadata.Description);

            WriteMsgpackString(stream, "version");
            WriteMsgpackString(stream, string.IsNullOrEmpty(metadata.Version) ? "1.0.0" : metadata.Version);

            WriteMsgpackString(stream, "distributor");
            if (metadata.Distributor == null)
            {
                WriteMsgpackNil(stream);
            }
            else
            {
                WriteMsgpackMapHeader(stream, 4);
                WriteMsgpackString(stream, "site_id");
                WriteMsgpackString(stream, metadata.Distributor.SiteId ?? string.Empty);
                WriteMsgpackString(stream, "site_name");
                WriteMsgpackString(stream, metadata.Distributor.SiteName ?? string.Empty);
                WriteMsgpackString(stream, "site_url");
                WriteMsgpackString(stream, metadata.Distributor.SiteUrl ?? string.Empty);
                WriteMsgpackString(stream, "mod_id");
                WriteMsgpackString(stream, metadata.Distributor.ModId ?? string.Empty);
            }

            WriteMsgpackString(stream, "authors");
            var authors = metadata.Authors ?? new List<ModpkgAuthor>();
            WriteMsgpackArrayHeader(stream, (uint)authors.Count);
            foreach (var author in authors)
            {
                WriteMsgpackMapHeader(stream, 2);
                WriteMsgpackString(stream, "name");
                WriteMsgpackString(stream, author.Name ?? string.Empty);
                WriteMsgpackString(stream, "role");
                if (string.IsNullOrEmpty(author.Role)) WriteMsgpackNil(stream);
                else WriteMsgpackString(stream, author.Role);
            }

            WriteMsgpackString(stream, "license");
            var license = metadata.License ?? new ModpkgLicense(LicenseType.None);
            if (license.Type == LicenseType.Spdx)
            {
                WriteMsgpackMapHeader(stream, 2);
                WriteMsgpackString(stream, "type");
                WriteMsgpackString(stream, "spdx");
                WriteMsgpackString(stream, "spdx_id");
                WriteMsgpackString(stream, license.SpdxId ?? string.Empty);
            }
            else if (license.Type == LicenseType.Custom)
            {
                WriteMsgpackMapHeader(stream, 3);
                WriteMsgpackString(stream, "type");
                WriteMsgpackString(stream, "custom");
                WriteMsgpackString(stream, "name");
                WriteMsgpackString(stream, license.Name ?? string.Empty);
                WriteMsgpackString(stream, "url");
                WriteMsgpackString(stream, license.Url ?? string.Empty);
            }
            else
            {
                WriteMsgpackMapHeader(stream, 1);
                WriteMsgpackString(stream, "type");
                WriteMsgpackString(stream, "none");
            }

            WriteMsgpackString(stream, "layers");
            var layers = metadata.Layers ?? new List<ModpkgLayerMetadata>();
            WriteMsgpackArrayHeader(stream, (uint)layers.Count);
            foreach (var layer in layers)
            {
                WriteMsgpackMapHeader(stream, 3);
                WriteMsgpackString(stream, "name");
                WriteMsgpackString(stream, layer.Name ?? string.Empty);
                WriteMsgpackString(stream, "priority");
                WriteMsgpackInt(stream, layer.Priority);
                WriteMsgpackString(stream, "description");
                if (layer.Description == null) WriteMsgpackNil(stream);
                else WriteMsgpackString(stream, layer.Description);
            }

            return stream.ToArray();
        }

        public void Write()
        {
            if (_metadata == null) throw new InvalidOperationException("Metadata not set");

            using var fileStream = File.Create(_outputPath);
            using var writer = new BinaryWriter(fileStream);

            var chunkPaths = new List<string> { Constants.METADATA_CHUNK_PATH };
            foreach (var (p, _) in _otherMetaChunks)
            {
                if (!chunkPaths.Contains(p, StringComparer.OrdinalIgnoreCase))
                {
                    chunkPaths.Add(p);
                }
            }

            foreach (var (p, _, _, _) in _chunksToProcess)
            {
                if (!chunkPaths.Contains(p, StringComparer.OrdinalIgnoreCase))
                {
                    chunkPaths.Add(p);
                }
            }

            foreach (var p in _extraHeaderPaths)
            {
                if (!chunkPaths.Contains(p, StringComparer.OrdinalIgnoreCase))
                {
                    chunkPaths.Add(p);
                }
            }

            var pathToIndex = chunkPaths.Select((p, i) => (p, i)).ToDictionary(x => x.p, x => (uint)x.i, StringComparer.OrdinalIgnoreCase);

            var wads = _chunksToProcess.Select(c => c.WadName)
                                       .Where(w => !string.IsNullOrEmpty(w))
                                       .Distinct(StringComparer.OrdinalIgnoreCase)
                                       .ToList();
            var wadToIndex = wads.Select((w, i) => (w, i)).ToDictionary(x => x.w, x => (uint)x.i, StringComparer.OrdinalIgnoreCase);

            var totalChunks = 1 + _otherMetaChunks.Count + _chunksToProcess.Count;

            writer.Write(Constants.MAGIC);
            writer.Write(Constants.VERSION);
            writer.Write(0U);
            writer.Write((uint)totalChunks);
            writer.Write(Array.Empty<byte>());

            // 1. Layers Table
            writer.Write((uint)_layers.Count);
            foreach (var layer in _layers)
            {
                var nameBytes = Encoding.UTF8.GetBytes(layer.Name);
                writer.Write((uint)nameBytes.Length);
                writer.Write(nameBytes);
                writer.Write(layer.Priority);
            }

            // 2. Paths Table
            writer.Write((uint)chunkPaths.Count);
            foreach (var path in chunkPaths)
            {
                writer.Write(Encoding.UTF8.GetBytes(path));
                writer.Write((byte)0);
            }

            // 3. WADs Table
            writer.Write((uint)wads.Count);
            foreach (var wad in wads)
            {
                writer.Write(Encoding.UTF8.GetBytes(wad));
                writer.Write((byte)0);
            }

            var currentPos = writer.BaseStream.Position;
            var padding = (8 - (currentPos % 8)) % 8;
            for (int i = 0; i < padding; i++) writer.Write((byte)0);

            var chunksHeaderPos = writer.BaseStream.Position;
            writer.BaseStream.Seek(totalChunks * ModpkgChunk.ChunkHeaderSize, SeekOrigin.Current);

            // 4. Chunk Payloads
            var metadataBytes = SerializeMetadataMsgpack(_metadata);
            ProcessAndWriteChunk(writer, Constants.METADATA_CHUNK_PATH, metadataBytes, pathToIndex, Constants.NO_LAYER_INDEX, Constants.NO_WAD_INDEX, true);

            foreach (var (path, data) in _otherMetaChunks)
            {
                ProcessAndWriteChunk(writer, path, data, pathToIndex, Constants.NO_LAYER_INDEX, Constants.NO_WAD_INDEX, true);
            }

            foreach (var (path, data, layerName, wadName) in _chunksToProcess)
            {
                var layerIndex = string.IsNullOrEmpty(layerName) ? Constants.NO_LAYER_INDEX : (uint)_layers.FindIndex(l => l.Name == layerName);
                var wadIndex = string.IsNullOrEmpty(wadName) ? Constants.NO_WAD_INDEX : wadToIndex[wadName];
                ProcessAndWriteChunk(writer, path, data, pathToIndex, layerIndex, wadIndex, false);
            }

            // 5. Chunk Descriptors Table
            writer.BaseStream.Seek(chunksHeaderPos, SeekOrigin.Begin);
            foreach (var chunk in _finalChunks)
            {
                writer.Write(chunk.PathHash);
                writer.Write(chunk.DataOffset);
                writer.Write((byte)chunk.Compression);
                writer.Write(chunk.CompressedSize);
                writer.Write(chunk.UncompressedSize);
                writer.Write(chunk.CompressedChecksum);
                writer.Write(chunk.UncompressedChecksum);
                writer.Write(chunk.PathIndex);
                writer.Write(chunk.LayerIndex);
                writer.Write(chunk.WadIndex);
            }
            writer.Flush();
        }

        private void ProcessAndWriteChunk(BinaryWriter writer, string path, byte[] data,
          Dictionary<string, uint> pathToIndex, uint layerIndex, uint wadIndex, bool isMetaChunk)
        {
            var dataOffset = (ulong)writer.BaseStream.Position;
            byte[] compressedData = data;
            CompressionType compression = CompressionType.None;

            if (!isMetaChunk)
            {
                using var compressor = new ZstdSharp.Compressor();
                byte[] compressed = compressor.Wrap(data).ToArray();
                if (compressed.Length < data.Length)
                {
                    compressedData = compressed;
                    compression = CompressionType.Zstd;
                }
            }

            writer.Write(compressedData);

            _finalChunks.Add(new ModpkgChunk
            {
                PathHash = ModPkgLib.HashChunkName(path),
                DataOffset = dataOffset,
                Compression = compression,
                CompressedSize = (ulong)compressedData.Length,
                UncompressedSize = (ulong)data.Length,
                CompressedChecksum = ModPkgLib.XXH3_64(compressedData),
                UncompressedChecksum = ModPkgLib.XXH3_64(data),
                PathIndex = pathToIndex[path],
                LayerIndex = layerIndex,
                WadIndex = wadIndex
            });
        }

        public void Dispose() { }
    }
}