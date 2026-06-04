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

        public const int ChunkHeaderSize = 8 + 8 + 1 + 8 + 8 + 8 + 8 + 4 + 4 + 4; // 77 bytes
    }

    public class ModpkgLayer
    {
        public string Name { get; set; }
        public int Priority { get; set; }
        public string Description { get; set; } // Modernized addition
        public Dictionary<string, Dictionary<string, string>> StringOverrides { get; set; } = new(); // Modernized addition

        public ModpkgLayer(string name, int priority)
        {
            Name = name;
            Priority = priority;
            Description = string.Empty;
        }

        public ModpkgLayer(string name, int priority, string description, Dictionary<string, Dictionary<string, string>> stringOverrides = null)
        {
            Name = name;
            Priority = priority;
            Description = description ?? string.Empty;
            if (stringOverrides != null) StringOverrides = stringOverrides;
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
        public string SiteId { get; set; } = "unknown";
        public string SiteName { get; set; } = "Unknown Site";
        public string SiteUrl { get; set; } = "";
        public string ModId { get; set; } = "0";
        public string ReleaseId { get; set; } = "0";
    }

    public class ModpkgMetadata
    {
        public uint SchemaVersion { get; set; } = 1;
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public List<ModpkgAuthor> Authors { get; set; }
        public ModpkgLicense License { get; set; }
        public DistributorInfo Distributor { get; set; } = new DistributorInfo();

        // Modernized additions
        public List<string> Tags { get; set; } = new();
        public List<string> Champions { get; set; } = new();
        public List<string> Maps { get; set; } = new();
        public List<ModpkgLayer> Layers { get; set; } = new(); // Serialized into metadata map

        public ModpkgMetadata()
        {
            Name = string.Empty;
            DisplayName = string.Empty;
            Description = string.Empty;
            Version = string.Empty;
            Authors = new List<ModpkgAuthor>();
            License = new ModpkgLicense(LicenseType.None);
            Distributor = new DistributorInfo();
        }
    }

    public class LayerInfo
    {
        public string Name { get; set; }
        public int Priority { get; set; }
        public string folder_name { get; set; }
        public bool is_active { get; set; } = false;
        public string Description { get; set; } // Modernized addition
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

        public static ModpkgInfo GetMetadata(string modpkgPath)
        {
            using var reader = new ModpkgReader(modpkgPath);
            var info = new ModpkgInfo
            {
                Metadata = reader.Metadata,
                TotalChunks = reader.Chunks.Count
            };

            var chunksByLayer = reader.Chunks
                .GroupBy(kvp => kvp.Key.LayerHash)
                .ToDictionary(g => g.Key, g => g.Count());

            // Look up detailed configurations mapped in reader layers if populated
            foreach (var layer in reader.Layers.Values.OrderBy(l => l.Priority))
            {
                var layerHash = HashLayerName(layer.Name);
                chunksByLayer.TryGetValue(layerHash, out int count);

                info.Layers.Add(new LayerInfo
                {
                    Name = layer.Name,
                    Priority = layer.Priority,
                    Description = layer.Description,
                    folder_name = $"WAD_{layer.Name}"
                });
            }

            return info;
        }

        public static void Extract(string modpkgPath, string outputDir)
        {
            using var reader = new ModpkgReader(modpkgPath);
            Directory.CreateDirectory(outputDir);

            var chunksByLayer = reader.Chunks
              .GroupBy(kvp => kvp.Key.LayerHash)
              .ToDictionary(g => g.Key, g => g.ToList());

            var imageConverter = new ImageConverterMocks.SimpleEncoder();

            foreach (var (layerHash, chunks) in chunksByLayer)
            {
                string layerName = layerHash == Constants.NO_LAYER_HASH ? "WAD_no_layer" :
                                   reader.Layers.TryGetValue(layerHash, out var l) ? $"WAD_{l.Name}" : $"WAD_{layerHash:X16}";

                foreach (var (key, chunk) in chunks)
                {
                    var chunkPath = reader.ChunkPaths.GetValueOrDefault(chunk.PathHash, $"{chunk.PathHash:X16}");
                    if (chunkPath.Equals(Constants.METADATA_CHUNK_PATH, StringComparison.OrdinalIgnoreCase)) continue;

                    var data = reader.ExtractChunk(chunk);
                    byte[] finalData = data;
                    string filePath;
                    bool conversionRequired = false;

                    if (chunkPath.Equals(Constants.THUMBNAIL_CHUNK_PATH, StringComparison.OrdinalIgnoreCase))
                    {
                        filePath = Path.Combine(outputDir, "META", "image.png");
                        conversionRequired = true;
                    }
                    else
                    {
                        var layerDir = Path.Combine(outputDir, layerName);
                        Directory.CreateDirectory(layerDir);
                        filePath = Path.Combine(layerDir, chunkPath);
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

        public static void Pack(List<(string FolderPath, string LayerName, int Priority)> layers,
            ModpkgMetadata metadata, string outputPath, string thumbnailPath = null, DistributorInfo distributor = null)
        {
            using var writer = new ModpkgWriter(outputPath);

            if (distributor != null) metadata.Distributor = distributor;

            // Align configurations between standard inputs and metadata collections
            foreach (var (folderPath, layerName, priority) in layers)
            {
                writer.AddLayer(layerName, priority);
                if (!metadata.Layers.Any(l => l.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase)))
                {
                    metadata.Layers.Add(new ModpkgLayer(layerName, priority));
                }
            }

            writer.SetMetadata(metadata);

            if (!string.IsNullOrEmpty(thumbnailPath))
            {
                var thumbnailData = LoadThumbnailData(thumbnailPath);
                if (thumbnailData != null) writer.AddMetaChunkData(Constants.THUMBNAIL_CHUNK_PATH, thumbnailData);
            }

            foreach (var (folderPath, layerName, priority) in layers)
            {
                if (!Directory.Exists(folderPath)) throw new DirectoryNotFoundException($"Layer folder not found: {folderPath}");
                var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var relativePath = Path.GetRelativePath(folderPath, file);
                    var data = File.ReadAllBytes(file);
                    writer.AddChunk(relativePath, data, layerName);
                }
            }

            writer.Write();
        }
    }

    internal class ModpkgReader : IDisposable
    {
        private readonly BinaryReader _reader;
        public Dictionary<(ulong PathHash, ulong LayerHash), ModpkgChunk> Chunks { get; } = new();
        public Dictionary<ulong, ModpkgLayer> Layers { get; } = new();
        public Dictionary<ulong, string> ChunkPaths { get; } = new();
        public Dictionary<ulong, string> Wads { get; } = new();
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

        private static ulong ReadMsgpackUInt(MemoryStream stream)
        {
            var header = stream.ReadByte();
            if (header == -1) throw new EndOfStreamException();
            if ((header & 0x80) == 0x00) return (ulong)header;
            if (header == 0xCC) return (ulong)stream.ReadByte();
            if (header == 0xCD) return ReadBigEndianUInt16(stream);
            if (header == 0xCE) return ReadBigEndianUInt32(stream);
            if (header == 0xCF)
            {
                var b = new byte[8]; stream.Read(b, 0, 8);
                if (BitConverter.IsLittleEndian) Array.Reverse(b);
                return BitConverter.ToUInt64(b, 0);
            }
            throw new InvalidDataException();
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
            if ((header & 0x80) == 0x00 || (header & 0xE0) == 0xE0) return; // fixint
            if ((header & 0xE0) == 0xA0) { stream.Seek(header & 0x1F, SeekOrigin.Current); return; } // fixstr
            if ((header & 0xF0) == 0x90) { uint len = (uint)(header & 0x0F); for (int i = 0; i < len; i++) SkipMsgpackValue(stream); return; } // fixarray
            if ((header & 0xF0) == 0x80) { uint len = (uint)(header & 0x0F); for (int i = 0; i < len * 2; i++) SkipMsgpackValue(stream); return; } // fixmap
            switch (header)
            {
                case 0xC0: case 0xC2: case 0xC3: return;
                case 0xCC: stream.Seek(1, SeekOrigin.Current); return;
                case 0xCD: stream.Seek(2, SeekOrigin.Current); return;
                case 0xCE: stream.Seek(4, SeekOrigin.Current); return;
                case 0xCF: stream.Seek(8, SeekOrigin.Current); return;
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
            }

            var wadCount = _reader.ReadUInt32();
            for (int i = 0; i < wadCount; i++)
            {
                var wad = ReadNullTerminatedString();
                var wadHash = ModPkgLib.HashWadName(wad);
                Wads[wadHash] = wad;
            }

            var currentPos = _reader.BaseStream.Position;
            var padding = (8 - (currentPos % 8)) % 8;
            _reader.ReadBytes((int)padding);

            for (int i = 0; i < chunkCount; i++)
            {
                var chunk = ReadChunk();
                ulong layerHash = chunk.LayerIndex == Constants.NO_LAYER_INDEX ? Constants.NO_LAYER_HASH : layerHashes[(int)chunk.LayerIndex];
                Chunks[(chunk.PathHash, layerHash)] = chunk;
            }

            Metadata = ReadMetadataChunk();
        }

        private ModpkgMetadata ReadMetadataChunk()
        {
            var metadata = new ModpkgMetadata();
            var metadataPathHash = ModPkgLib.HashChunkName(Constants.METADATA_CHUNK_PATH);
            ModpkgChunk chunk = Chunks.Values.FirstOrDefault(c => c.PathHash == metadataPathHash) ??
                                Chunks.Values.FirstOrDefault(c => c.PathHash == ModPkgLib.HashChunkName(Constants.METADATA_CHUNK_PATH.Replace('/', '\\')));

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
                        case "description": if (!ReadMsgpackNil(stream)) metadata.Description = ReadMsgpackString(stream); break;
                        case "version": metadata.Version = ReadMsgpackString(stream); break;
                        case "tags": ReadStringList(stream, metadata.Tags); break; // Modernized
                        case "champions": ReadStringList(stream, metadata.Champions); break; // Modernized
                        case "maps": ReadStringList(stream, metadata.Maps); break; // Modernized
                        case "layers": ReadLayersMetadata(stream, metadata.Layers); break; // Modernized
                        case "distributor":
                            if (ReadMsgpackNil(stream)) metadata.Distributor = null;
                            else
                            {
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
                                        case "release_id": metadata.Distributor.ReleaseId = distValue; break;
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
                                        else if (key == "role" && !ReadMsgpackNil(stream)) role = ReadMsgpackString(stream);
                                        else if (key != "role") SkipMsgpackValue(stream);
                                    }
                                    metadata.Authors.Add(new ModpkgAuthor(name, role));
                                }
                            }
                            break;
                        case "license":
                            if (!ReadMsgpackNil(stream)) metadata.License = ReadLicense(stream);
                            break;
                        default:
                            SkipMsgpackValue(stream);
                            break;
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"Error parsing updated metadata chunks: {ex.Message}"); }
            return metadata;
        }

        private static void ReadStringList(MemoryStream stream, List<string> list)
        {
            if (ReadMsgpackNil(stream)) return;
            var count = ReadMsgpackArrayHeader(stream);
            for (uint i = 0; i < count; i++) list.Add(ReadMsgpackString(stream));
        }

        private static void ReadLayersMetadata(MemoryStream stream, List<ModpkgLayer> layers)
        {
            if (ReadMsgpackNil(stream)) return;
            var count = ReadMsgpackArrayHeader(stream);
            for (uint i = 0; i < count; i++)
            {
                var fieldCount = ReadMsgpackMapHeader(stream);
                string name = string.Empty, desc = string.Empty;
                int priority = 0;
                var stringOverrides = new Dictionary<string, Dictionary<string, string>>();

                for (uint j = 0; j < fieldCount; j++)
                {
                    var key = ReadMsgpackString(stream);
                    switch (key)
                    {
                        case "name": name = ReadMsgpackString(stream); break;
                        case "priority": priority = (int)ReadMsgpackUInt(stream); break;
                        case "description": if (!ReadMsgpackNil(stream)) desc = ReadMsgpackString(stream); break;
                        case "string_overrides": ReadStringOverridesMap(stream, stringOverrides); break;
                        default: SkipMsgpackValue(stream); break;
                    }
                }
                layers.Add(new ModpkgLayer(name, priority, desc, stringOverrides));
            }
        }

        private static void ReadStringOverridesMap(MemoryStream stream, Dictionary<string, Dictionary<string, string>> target)
        {
            if (ReadMsgpackNil(stream)) return;
            var outerCount = ReadMsgpackMapHeader(stream);
            for (uint i = 0; i < outerCount; i++)
            {
                var outerKey = ReadMsgpackString(stream);
                var innerMap = new Dictionary<string, string>();
                var innerCount = ReadMsgpackMapHeader(stream);
                for (uint j = 0; j < innerCount; j++)
                {
                    var innerKey = ReadMsgpackString(stream);
                    var innerVal = ReadMsgpackString(stream);
                    innerMap[innerKey] = innerVal;
                }
                target[outerKey] = innerMap;
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

            if (typeStr == "spdx") { license.Type = LicenseType.Spdx; license.SpdxId = spdxId; }
            else if (typeStr == "custom") { license.Type = LicenseType.Custom; license.Name = name; license.Url = url; }
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
        private readonly List<(string Path, byte[] Data, string LayerName)> _chunksToProcess = new();
        private readonly List<(string Path, byte[] Data)> _otherMetaChunks = new();
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

        public void AddChunk(string path, byte[] data, string layerName) => _chunksToProcess.Add((path, data, layerName));

        private static void WriteMsgpackString(MemoryStream stream, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            var len = bytes.Length;

            if (len < 32) stream.WriteByte((byte)(0xa0 | len));
            else if (len < 256) { stream.WriteByte(0xd9); stream.WriteByte((byte)len); }
            else if (len < 65536)
            {
                stream.WriteByte(0xda);
                stream.Write(BitConverter.GetBytes((ushort)len).Reverse().ToArray(), 0, 2);
            }
            else
            {
                stream.WriteByte(0xdb);
                stream.Write(BitConverter.GetBytes((uint)len).Reverse().ToArray(), 0, 4);
            }
            stream.Write(bytes, 0, len);
        }

        private static void WriteMsgpackUInt(MemoryStream stream, ulong value)
        {
            if (value <= 127) stream.WriteByte((byte)value);
            else if (value <= 0xFF) { stream.WriteByte(0xcc); stream.WriteByte((byte)value); }
            else if (value <= 0xFFFF) { stream.WriteByte(0xcd); stream.Write(BitConverter.GetBytes((ushort)value).Reverse().ToArray(), 0, 2); }
            else if (value <= 0xFFFFFFFF) { stream.WriteByte(0xce); stream.Write(BitConverter.GetBytes((uint)value).Reverse().ToArray(), 0, 4); }
            else { stream.WriteByte(0xcf); stream.Write(BitConverter.GetBytes(value).Reverse().ToArray(), 0, 8); }
        }

        private static void WriteMsgpackMapHeader(MemoryStream stream, uint count)
        {
            if (count <= 15) stream.WriteByte((byte)(0x80 | count));
            else if (count <= 0xFFFF) { stream.WriteByte(0xde); stream.Write(BitConverter.GetBytes((ushort)count).Reverse().ToArray(), 0, 2); }
            else { stream.WriteByte(0xdf); stream.Write(BitConverter.GetBytes((uint)count).Reverse().ToArray(), 0, 4); }
        }

        private static void WriteMsgpackArrayHeader(MemoryStream stream, uint count)
        {
            if (count <= 15) stream.WriteByte((byte)(0x90 | count));
            else if (count <= 0xFFFF) { stream.WriteByte(0xdc); stream.Write(BitConverter.GetBytes((ushort)count).Reverse().ToArray(), 0, 2); }
            else { stream.WriteByte(0xdd); stream.Write(BitConverter.GetBytes((uint)count).Reverse().ToArray(), 0, 4); }
        }

        private static void WriteMsgpackNil(MemoryStream stream) => stream.WriteByte(0xc0);

        private byte[] SerializeMetadataMsgpack(ModpkgMetadata metadata)
        {
            using var stream = new MemoryStream();

            // Total fields expanded to include tags, champions, maps, layers collections
            WriteMsgpackMapHeader(stream, 13);

            WriteMsgpackString(stream, "schema_version"); WriteMsgpackUInt(stream, metadata.SchemaVersion);
            WriteMsgpackString(stream, "name"); WriteMsgpackString(stream, metadata.Name);
            WriteMsgpackString(stream, "display_name"); WriteMsgpackString(stream, metadata.DisplayName);

            WriteMsgpackString(stream, "description");
            if (string.IsNullOrEmpty(metadata.Description)) WriteMsgpackNil(stream);
            else WriteMsgpackString(stream, metadata.Description);

            WriteMsgpackString(stream, "version"); WriteMsgpackString(stream, metadata.Version);

            // Modern Serialization Additions
            WriteMsgpackString(stream, "tags");
            WriteMsgpackArrayHeader(stream, (uint)metadata.Tags.Count);
            foreach (var tag2 in metadata.Tags) WriteMsgpackString(stream, tag2);

            WriteMsgpackString(stream, "champions");
            WriteMsgpackArrayHeader(stream, (uint)metadata.Champions.Count);
            foreach (var champ in metadata.Champions) WriteMsgpackString(stream, champ);

            WriteMsgpackString(stream, "maps");
            WriteMsgpackArrayHeader(stream, (uint)metadata.Maps.Count);
            foreach (var map in metadata.Maps) WriteMsgpackString(stream, map);

            WriteMsgpackString(stream, "layers");
            WriteMsgpackArrayHeader(stream, (uint)metadata.Layers.Count);
            foreach (var layer in metadata.Layers)
            {
                WriteMsgpackMapHeader(stream, 4);
                WriteMsgpackString(stream, "name"); WriteMsgpackString(stream, layer.Name);
                WriteMsgpackString(stream, "priority"); WriteMsgpackUInt(stream, (ulong)layer.Priority);
                WriteMsgpackString(stream, "description"); WriteMsgpackString(stream, layer.Description);

                WriteMsgpackString(stream, "string_overrides");
                WriteMsgpackMapHeader(stream, (uint)layer.StringOverrides.Count);
                foreach (var outer in layer.StringOverrides)
                {
                    WriteMsgpackString(stream, outer.Key);
                    WriteMsgpackMapHeader(stream, (uint)outer.Value.Count);
                    foreach (var inner in outer.Value)
                    {
                        WriteMsgpackString(stream, inner.Key);
                        WriteMsgpackString(stream, inner.Value);
                    }
                }
            }

            WriteMsgpackString(stream, "distributor");
            var distributor = metadata.Distributor ?? new DistributorInfo();
            WriteMsgpackMapHeader(stream, 5);
            WriteMsgpackString(stream, "site_id"); WriteMsgpackString(stream, distributor.SiteId);
            WriteMsgpackString(stream, "site_name"); WriteMsgpackString(stream, distributor.SiteName);
            WriteMsgpackString(stream, "site_url"); WriteMsgpackString(stream, distributor.SiteUrl);
            WriteMsgpackString(stream, "mod_id"); WriteMsgpackString(stream, distributor.ModId);
            WriteMsgpackString(stream, "release_id"); WriteMsgpackString(stream, distributor.ReleaseId);

            WriteMsgpackString(stream, "authors");
            WriteMsgpackArrayHeader(stream, (uint)metadata.Authors.Count);
            foreach (var author in metadata.Authors)
            {
                WriteMsgpackMapHeader(stream, 2);
                WriteMsgpackString(stream, "name"); WriteMsgpackString(stream, author.Name);
                WriteMsgpackString(stream, "role"); if (string.IsNullOrEmpty(author.Role)) WriteMsgpackNil(stream); else WriteMsgpackString(stream, author.Role);
            }

            WriteMsgpackString(stream, "license");
            string tag = metadata.License.Type == LicenseType.Spdx ? "spdx" : metadata.License.Type == LicenseType.Custom ? "custom" : "none";
            uint mapSize = metadata.License.Type == LicenseType.Spdx ? 2U : metadata.License.Type == LicenseType.Custom ? 3U : 1U;

            WriteMsgpackMapHeader(stream, mapSize);
            WriteMsgpackString(stream, "type"); WriteMsgpackString(stream, tag);
            if (metadata.License.Type == LicenseType.Spdx) { WriteMsgpackString(stream, "spdx_id"); WriteMsgpackString(stream, metadata.License.SpdxId); }
            else if (metadata.License.Type == LicenseType.Custom) { WriteMsgpackString(stream, "name"); WriteMsgpackString(stream, metadata.License.Name); WriteMsgpackString(stream, "url"); WriteMsgpackString(stream, metadata.License.Url ?? ""); }

            return stream.ToArray();
        }

        public void Write()
        {
            if (_metadata == null) throw new InvalidOperationException("Metadata not set");

            using var fileStream = File.Create(_outputPath);
            using var writer = new BinaryWriter(fileStream);

            var chunkPaths = _chunksToProcess.Select(c => c.Path)
                                      .Concat(new[] { Constants.METADATA_CHUNK_PATH })
                                      .Concat(_otherMetaChunks.Select(c => c.Path))
                                      .Distinct()
                                      .ToList();
            var pathToIndex = chunkPaths.Select((p, i) => (p, i)).ToDictionary(x => x.p, x => (uint)x.i);
            var totalChunks = _chunksToProcess.Count + 1 + _otherMetaChunks.Count;

            writer.Write(Constants.MAGIC);
            writer.Write(Constants.VERSION);
            writer.Write(0U); // signature size placeholder
            writer.Write((uint)totalChunks);
            writer.Write(Array.Empty<byte>()); // empty signature

            writer.Write((uint)_layers.Count);
            foreach (var layer in _layers)
            {
                var nameBytes = Encoding.UTF8.GetBytes(layer.Name);
                writer.Write((uint)nameBytes.Length); writer.Write(nameBytes); writer.Write(layer.Priority);
            }

            writer.Write((uint)chunkPaths.Count);
            foreach (var path in chunkPaths) { writer.Write(Encoding.UTF8.GetBytes(path)); writer.Write((byte)0); }

            writer.Write(0U); // wad count

            var currentPos = writer.BaseStream.Position;
            var padding = (8 - (currentPos % 8)) % 8;
            for (int i = 0; i < padding; i++) writer.Write((byte)0);

            var chunksHeaderPos = writer.BaseStream.Position;
            writer.BaseStream.Seek(totalChunks * ModpkgChunk.ChunkHeaderSize, SeekOrigin.Current);

            var metadataBytes = SerializeMetadataMsgpack(_metadata);
            ProcessAndWriteChunk(writer, Constants.METADATA_CHUNK_PATH, metadataBytes, null, pathToIndex, Constants.NO_LAYER_INDEX, Constants.NO_LAYER_HASH, true);

            foreach (var (path, data) in _otherMetaChunks) ProcessAndWriteChunk(writer, path, data, null, pathToIndex, Constants.NO_LAYER_INDEX, Constants.NO_LAYER_HASH, true);

            foreach (var (path, data, layerName) in _chunksToProcess)
            {
                var layerIndex = string.IsNullOrEmpty(layerName) ? Constants.NO_LAYER_INDEX : (uint)_layers.FindIndex(l => l.Name == layerName);
                var layerHash = string.IsNullOrEmpty(layerName) ? Constants.NO_LAYER_HASH : ModPkgLib.HashLayerName(layerName);
                ProcessAndWriteChunk(writer, path, data, layerName, pathToIndex, layerIndex, layerHash, false);
            }

            writer.BaseStream.Seek(chunksHeaderPos, SeekOrigin.Begin);
            foreach (var chunk in _finalChunks)
            {
                writer.Write(chunk.PathHash); writer.Write(chunk.DataOffset); writer.Write((byte)chunk.Compression);
                writer.Write(chunk.CompressedSize); writer.Write(chunk.UncompressedSize);
                writer.Write(chunk.CompressedChecksum); writer.Write(chunk.UncompressedChecksum);
                writer.Write(chunk.PathIndex); writer.Write(chunk.LayerIndex); writer.Write(chunk.WadIndex);
            }
            writer.Flush();
        }

        private void ProcessAndWriteChunk(BinaryWriter writer, string path, byte[] data, string layerName,
          Dictionary<string, uint> pathToIndex, uint layerIndex, ulong layerHash, bool isMetaChunk)
        {
            var dataOffset = (ulong)writer.BaseStream.Position;
            byte[] compressedData = data;
            CompressionType compression = CompressionType.None;

            if (!isMetaChunk)
            {
                using var compressor = new ZstdSharp.Compressor();
                byte[] compressed = compressor.Wrap(data).ToArray();
                if (compressed.Length < data.Length) { compressedData = compressed; compression = CompressionType.Zstd; }
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
                WadIndex = Constants.NO_WAD_INDEX
            });
        }

        public void Dispose() { }
    }
}