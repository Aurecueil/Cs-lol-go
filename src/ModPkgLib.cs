using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization; // Added for JSON snake_case alignment
using ZstdSharp;

// Note: ModManager.CustomMessageBox is assumed to be available from the ModManager dependency.
// For metadata serialization, System.Text.Json is used as a standard replacement for the Rust's Msgpack,
// as external NuGet dependencies cannot be added here.

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

        public ModpkgLayer(string name, int priority)
        {
            Name = name;
            Priority = priority;
        }
    }

    // Simplified ModpkgAuthor for Msgpack encoding
    public class ModpkgAuthor
    {
        public string Name { get; set; }
        public string Role { get; set; } // Nullable string maps to Option<String> in Rust

        public ModpkgAuthor(string name, string role = null)
        {
            Name = name;
            Role = role;
        }
    }

    // Simplified ModpkgLicense for Msgpack encoding
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

    // DistributorInfo structure for Msgpack encoding
    public class DistributorInfo
    {
        public string SiteId { get; set; } = "unknown";
        public string SiteName { get; set; } = "Unknown Site";
        public string SiteUrl { get; set; } = "";
        public string ModId { get; set; } = "0";
        public string ReleaseId { get; set; } = "0";
    }

    // Simplified ModpkgMetadata to facilitate Msgpack encoding
    public class ModpkgMetadata
    {
        public uint SchemaVersion { get; set; } = 1;
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public List<ModpkgAuthor> Authors { get; set; }
        public ModpkgLicense License { get; set; }
        // Default distributor for compatibility
        public DistributorInfo Distributor { get; set; } = new DistributorInfo();

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

    public class ModpkgLayerMetadata
    {
        public string Name { get; set; }
        public int Priority { get; set; }
        public string Description { get; set; }
    }

    public class LayerInfo
    {
        public string Name { get; set; }
        public int Priority { get; set; }
        public string folder_name { get; set; }
        public bool is_active { get; set; } = false;
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

    // WARNING: This entire static class is a conceptual placeholder.
    // It assumes the existence of types (Bitmap, SimpleEncoder, etc.) from external libraries 
    // (like System.Drawing/ImageSharp/SkiaSharp) which are not available in this single-file environment.
    // Actual execution will fail at runtime unless these dependencies are manually provided externally.
    
    public static class ImageConverterMocks
    {
        public class Bitmap : IDisposable
        {
            private SixLabors.ImageSharp.Image image;

            public Bitmap(string path)
            {
                image = SixLabors.ImageSharp.Image.Load(path);
            }

            public SixLabors.ImageSharp.Image GetImage() => image;

            public void Dispose()
            {
                image?.Dispose();
            }
        }

        public class SimpleEncoder
        {
            // Decode WebP to PNG
            public byte[] DecodeWebPToPng(byte[] webpData)
            {
                using (var ms = new MemoryStream(webpData))
                using (var image = SixLabors.ImageSharp.Image.Load(ms))
                using (var outputMs = new MemoryStream())
                {
                    var encoder = new SixLabors.ImageSharp.Formats.Png.PngEncoder();
                    image.Save(outputMs, encoder);
                    return outputMs.ToArray();
                }
            }

            // Bonus: Decode WebP to JPG
            public byte[] DecodeWebPToJpg(byte[] webpData, int quality = 90)
            {
                using (var ms = new MemoryStream(webpData))
                using (var image = SixLabors.ImageSharp.Image.Load(ms))
                using (var outputMs = new MemoryStream())
                {
                    var encoder = new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder
                    {
                        Quality = quality
                    };
                    image.Save(outputMs, encoder);
                    return outputMs.ToArray();
                }
            }
        }
    }

    public static class ModPkgLib
    {
        /// <summary>
        /// Converts the 8-byte hash array returned by System.IO.Hashing to a ulong value.
        /// This correctly handles the common case where hash functions return Big Endian bytes
        /// but BitConverter reads Little Endian bytes on x86/x64 platforms.
        /// </summary>
        private static ulong HashToUInt64(byte[] hashBytes)
        {
            // XxHash implementations often return hash bytes in Big Endian order.
            // On a Little Endian machine (standard for C# runtime), we must reverse the bytes
            // to correctly reconstruct the ulong value using BitConverter.
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(hashBytes);
            }
            return BitConverter.ToUInt64(hashBytes, 0);
        }

        // Hashing functions aligned with Rust implementation

        /// <summary>
        /// Hashes a layer name using XXH3_64 (aligned with Rust's xxh3::xxh3_64).
        /// </summary>
        public static ulong HashLayerName(string name)
        {
            var bytes = Encoding.UTF8.GetBytes(name.ToLowerInvariant());
            var hash = XxHash3.Hash(bytes);
            return HashToUInt64(hash);
        }

        /// <summary>
        /// Hashes a chunk name using XXH64 with seed 0 (aligned with Rust's xxh64::xxh64(..., 0)).
        /// </summary>
        public static ulong HashChunkName(string name)
        {
            var bytes = Encoding.UTF8.GetBytes(name.ToLowerInvariant());
            var hash = XxHash64.Hash(bytes, 0); // Explicitly use seed 0
            return HashToUInt64(hash);
        }

        /// <summary>
        /// Hashes a wad name using XXH3_64 (aligned with Rust's xxh3::xxh3_64).
        /// </summary>
        public static ulong HashWadName(string name)
        {
            var bytes = Encoding.UTF8.GetBytes(name.ToLowerInvariant());
            var hash = XxHash3.Hash(bytes);
            return HashToUInt64(hash);
        }

        /// <summary>
        /// Calculates the XXH3_64 checksum for data (aligned with Rust's xxh3_64).
        /// </summary>
        public static ulong XXH3_64(byte[] data)
        {
            var hash = XxHash3.Hash(data);
            return HashToUInt64(hash);
        }

        // Get metadata and layer information
        public static ModpkgInfo GetMetadata(string modpkgPath)
        {
            using var reader = new ModpkgReader(modpkgPath);
            var info = new ModpkgInfo
            {
                Metadata = reader.Metadata,
                TotalChunks = reader.Chunks.Count
            };

            // Group chunks by LayerHash to count files per layer
            var chunksByLayer = reader.Chunks
                .GroupBy(kvp => kvp.Key.LayerHash)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var layer in reader.Layers.Values.OrderBy(l => l.Priority))
            {
                var layerHash = HashLayerName(layer.Name);
                chunksByLayer.TryGetValue(layerHash, out int count);

                info.Layers.Add(new LayerInfo
                {
                    Name = layer.Name,
                    Priority = layer.Priority,
                    folder_name = $"WAD_{layer.Name}"
                });
            }

            return info;
        }

        // Extract modpkg to folder
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
                string layerName;
                if (layerHash == Constants.NO_LAYER_HASH)
                {
                    layerName = "WAD_no_layer";
                }
                else if (reader.Layers.TryGetValue(layerHash, out var layer))
                {
                    layerName = $"WAD_{layer.Name}";
                }
                else
                {
                    // Fallback for layers not defined in the header but present in chunks (shouldn't happen in v1)
                    layerName = $"WAD_{layerHash:X16}";
                }

                foreach (var (key, chunk) in chunks)
                {
                    // Use path from ChunkPaths if available, otherwise use hash as filename
                    var chunkPath = reader.ChunkPaths.GetValueOrDefault(chunk.PathHash, $"{chunk.PathHash:X16}");

                    // Skip extracting the metadata chunk, as requested
                    if (chunkPath.Equals(Constants.METADATA_CHUNK_PATH, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var data = reader.ExtractChunk(chunk);
                    byte[] finalData = data;
                    string filePath;
                    bool conversionRequired = false;

                    if (chunkPath.Equals(Constants.THUMBNAIL_CHUNK_PATH, StringComparison.OrdinalIgnoreCase))
                    {
                        // Special handling for thumbnail: save as image.png in the root output directory
                        filePath = Path.Combine(outputDir, "META","image.png");
                        conversionRequired = true; // WebP data needs conversion back to PNG
                    }
                    else
                    {
                        var layerDir = Path.Combine(outputDir, layerName);
                        Directory.CreateDirectory(layerDir); // Ensure layer directory exists
                        filePath = Path.Combine(layerDir, chunkPath);
                    }

                    if (conversionRequired)
                    {
                        try
                        {
                            // Attempt WebP -> PNG conversion (will likely fail due to Mock)
                            finalData = imageConverter.DecodeWebPToPng(data);
                            if (finalData == null)
                            {
                                Console.WriteLine("Warning: WebP to PNG conversion failed (Mock). Saving raw WebP data under .png name.");
                                finalData = data; // Fallback to saving raw WebP data if conversion fails
                            }
                        }
                        catch (NotImplementedException ex)
                        {
                            Console.WriteLine($"Warning: Image extraction failed. {ex.Message} Saving raw WebP data under .png name.");
                            finalData = data;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Warning: Image extraction failed. Error: {ex.Message} Saving raw WebP data under .png name.");
                            finalData = data;
                        }
                    }

                    var fileDir = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(fileDir) && !Directory.Exists(fileDir))
                    {
                        Directory.CreateDirectory(fileDir);
                    }

                    File.WriteAllBytes(filePath, finalData);
                }
            }
        }

        /// <summary>
        /// Loads thumbnail data from path. Attempts to read .webp, .png, or .jpg and converts to WebP.
        /// Conversion for non-WebP files relies on ImageConverterMocks.SimpleEncoder.
        /// </summary>
        private static byte[] LoadThumbnailData(string thumbnailPath)
        {
            if (string.IsNullOrEmpty(thumbnailPath) || !File.Exists(thumbnailPath))
            {
                return null;
            }

            var extension = Path.GetExtension(thumbnailPath).ToLowerInvariant();

            try
            {
                // Already WebP → return as-is
                if (extension == ".webp")
                    return File.ReadAllBytes(thumbnailPath);

                // PNG/JPG → load and convert to WebP
                if (extension == ".png" || extension == ".jpg" || extension == ".jpeg")
                {
                    using (var image = SixLabors.ImageSharp.Image.Load(thumbnailPath))
                    using (var ms = new MemoryStream())
                    {
                        var encoder = new SixLabors.ImageSharp.Formats.Webp.WebpEncoder
                        {
                            Quality = 100
                        };
                        image.Save(ms, encoder);
                        return ms.ToArray();
                    }
                }

                Console.WriteLine($"Warning: Thumbnail file type {extension} is invalid. Only .webp, .png, or .jpg are accepted. Skipping.");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to process thumbnail {thumbnailPath}. Skipping. Error: {ex.Message}");
                return null;
            }
        }

        // Pack folders into modpkg
        public static void Pack(List<(string FolderPath, string LayerName, int Priority)> layers,
                                ModpkgMetadata metadata, string outputPath, string thumbnailPath = null, DistributorInfo distributor = null)
        {
            using var writer = new ModpkgWriter(outputPath);

            // Apply distributor data if provided separately
            if (distributor != null)
            {
                metadata.Distributor = distributor;
            }

            writer.SetMetadata(metadata);

            // 1. Add layers to header (ModpkgWriter handles deduplication/indexing)
            foreach (var (folderPath, layerName, priority) in layers)
            {
                writer.AddLayer(layerName, priority);
            }

            // 2. Handle Thumbnail Meta Chunk (must be done before writing content chunks)
            if (!string.IsNullOrEmpty(thumbnailPath))
            {
                var thumbnailData = LoadThumbnailData(thumbnailPath); // Use the new method
                if (thumbnailData != null)
                {
                    writer.AddMetaChunkData(Constants.THUMBNAIL_CHUNK_PATH, thumbnailData);
                }
            }

            // 3. Add content chunks
            foreach (var (folderPath, layerName, priority) in layers)
            {
                if (!Directory.Exists(folderPath))
                {
                    throw new DirectoryNotFoundException($"Layer folder not found: {folderPath}");
                }

                var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var relativePath = Path.GetRelativePath(folderPath, file);
                    var data = File.ReadAllBytes(file);
                    writer.AddChunk(relativePath, data, layerName);
                }
            }

            // 4. Write the final package
            writer.Write();
        }
    }

    internal class ModpkgReader : IDisposable
    {
        private readonly BinaryReader _reader;
        public Dictionary<(ulong PathHash, ulong LayerHash), ModpkgChunk> Chunks { get; }
        public Dictionary<ulong, ModpkgLayer> Layers { get; }
        public Dictionary<ulong, string> ChunkPaths { get; }
        public Dictionary<ulong, string> Wads { get; }
        public ModpkgMetadata Metadata { get; private set; }

        public ModpkgReader(string filePath)
        {
            _reader = new BinaryReader(File.OpenRead(filePath));
            Chunks = new Dictionary<(ulong, ulong), ModpkgChunk>();
            Layers = new Dictionary<ulong, ModpkgLayer>();
            ChunkPaths = new Dictionary<ulong, string>();
            Wads = new Dictionary<ulong, string>();
            ReadModpkg();
        }

        // --- Msgpack Helper Methods for Reading ---

        private static string ReadMsgpackString(MemoryStream stream)
        {
            var header = stream.ReadByte();
            if (header == -1) throw new EndOfStreamException("Msgpack string header expected.");

            int len;
            if ((header & 0xE0) == 0xA0) // Fixstr (0xa0 to 0xbf)
            {
                len = header & 0x1F;
            }
            else if (header == 0xD9) // Str 8
            {
                len = stream.ReadByte();
            }
            else if (header == 0xDA) // Str 16
            {
                var bytes = new byte[2];
                stream.Read(bytes, 0, 2);
                if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
                len = BitConverter.ToUInt16(bytes, 0);
            }
            else if (header == 0xDB) // Str 32
            {
                var bytes = new byte[4];
                stream.Read(bytes, 0, 4);
                if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
                len = (int)BitConverter.ToUInt32(bytes, 0);
            }
            else
            {
                throw new InvalidDataException($"Invalid Msgpack string header: 0x{header:X2}");
            }

            var valueBytes = new byte[len];
            stream.Read(valueBytes, 0, len);
            return Encoding.UTF8.GetString(valueBytes);
        }

        private static ulong ReadMsgpackUInt(MemoryStream stream)
        {
            var header = stream.ReadByte();
            if (header == -1) throw new EndOfStreamException("Msgpack unsigned int header expected.");

            if ((header & 0x80) == 0x00) // Positive fixint (0x00 to 0x7f)
            {
                return (ulong)header;
            }
            else if (header == 0xCC) // uint 8
            {
                return (ulong)stream.ReadByte();
            }
            else if (header == 0xCD) // uint 16
            {
                var bytes = new byte[2];
                stream.Read(bytes, 0, 2);
                if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
                return BitConverter.ToUInt16(bytes, 0);
            }
            else if (header == 0xCE) // uint 32
            {
                var bytes = new byte[4];
                stream.Read(bytes, 0, 4);
                if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
                return BitConverter.ToUInt32(bytes, 0);
            }
            else if (header == 0xCF) // uint 64
            {
                var bytes = new byte[8];
                stream.Read(bytes, 0, 8);
                if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
                return BitConverter.ToUInt64(bytes, 0);
            }
            else
            {
                throw new InvalidDataException($"Invalid Msgpack uint header: 0x{header:X2}");
            }
        }

        private static uint ReadMsgpackMapHeader(MemoryStream stream)
        {
            var header = stream.ReadByte();
            if (header == -1) throw new EndOfStreamException("Msgpack map header expected.");

            if ((header & 0xF0) == 0x80) // Fixmap (0x80 to 0x8f)
            {
                return (uint)(header & 0x0F);
            }
            else if (header == 0xDE) // Map 16
            {
                var bytes = new byte[2];
                stream.Read(bytes, 0, 2);
                if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
                return BitConverter.ToUInt16(bytes, 0);
            }
            else if (header == 0xDF) // Map 32
            {
                var bytes = new byte[4];
                stream.Read(bytes, 0, 4);
                if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
                return BitConverter.ToUInt32(bytes, 0);
            }
            else
            {
                throw new InvalidDataException($"Invalid Msgpack map header: 0x{header:X2}");
            }
        }

        private static uint ReadMsgpackArrayHeader(MemoryStream stream)
        {
            var header = stream.ReadByte();
            if (header == -1) throw new EndOfStreamException("Msgpack array header expected.");

            if ((header & 0xF0) == 0x90) // Fixarray (0x90 to 0x9f)
            {
                return (uint)(header & 0x0F);
            }
            else if (header == 0xDC) // Array 16
            {
                var bytes = new byte[2];
                stream.Read(bytes, 0, 2);
                if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
                return BitConverter.ToUInt16(bytes, 0);
            }
            else if (header == 0xDD) // Array 32
            {
                var bytes = new byte[4];
                stream.Read(bytes, 0, 4);
                if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
                return BitConverter.ToUInt32(bytes, 0);
            }
            else
            {
                throw new InvalidDataException($"Invalid Msgpack array header: 0x{header:X2}");
            }
        }

        private static bool ReadMsgpackNil(MemoryStream stream)
        {
            var header = stream.ReadByte();
            if (header == -1) throw new EndOfStreamException("Msgpack value header expected.");

            if (header == 0xC0) return true; // Nil

            // If not Nil, put the byte back and return false
            stream.Seek(-1, SeekOrigin.Current);
            return false;
        }

        // --- End Msgpack Helper Methods ---

        private void ReadModpkg()
        {
            // Read magic
            var magic = _reader.ReadBytes(8);
            if (!magic.SequenceEqual(Constants.MAGIC))
                throw new InvalidDataException("Invalid magic");

            // Read version
            var version = _reader.ReadUInt32();
            if (version != Constants.VERSION)
            {
                // CustomMessageBox is assumed to exist in the environment
                // ModManager.CustomMessageBox.Show($"Unsupported Modpkg Version: {version}");
                throw new InvalidDataException($"Unsupported version: {version}");
            }

            // Read signature size, chunk count, and signature
            var signatureSize = _reader.ReadUInt32();
            var chunkCount = _reader.ReadUInt32();
            _reader.ReadBytes((int)signatureSize); // Signature (currently ignored/empty)

            // Read layers
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

            // Read chunk paths
            var pathCount = _reader.ReadUInt32();
            for (int i = 0; i < pathCount; i++)
            {
                var path = ReadNullTerminatedString();
                var pathHash = ModPkgLib.HashChunkName(path);
                ChunkPaths[pathHash] = path;
            }

            // Read wads
            var wadCount = _reader.ReadUInt32();
            for (int i = 0; i < wadCount; i++)
            {
                var wad = ReadNullTerminatedString();
                var wadHash = ModPkgLib.HashWadName(wad);
                Wads[wadHash] = wad;
            }

            // Align to 8 bytes before chunk headers (TOC)
            var currentPos = _reader.BaseStream.Position;
            var padding = (8 - (currentPos % 8)) % 8;
            _reader.ReadBytes((int)padding);

            // Read chunks (TOC)
            var chunksHeaderEndPos = _reader.BaseStream.Position + (chunkCount * ModpkgChunk.ChunkHeaderSize);
            for (int i = 0; i < chunkCount; i++)
            {
                var chunk = ReadChunk();
                ulong layerHash = chunk.LayerIndex == Constants.NO_LAYER_INDEX
                    ? Constants.NO_LAYER_HASH
                    : layerHashes[(int)chunk.LayerIndex];
                Chunks[(chunk.PathHash, layerHash)] = chunk;
            }

            // Now load metadata from the chunk.
            Metadata = ReadMetadataChunk();
        }

        private ModpkgMetadata ReadMetadataChunk()
        {
            var metadata = new ModpkgMetadata();
            var metadataPathHash = ModPkgLib.HashChunkName(Constants.METADATA_CHUNK_PATH);
            var key = (metadataPathHash, Constants.NO_LAYER_HASH);

            if (!Chunks.TryGetValue(key, out var chunk))
            {
                return metadata; // Return empty metadata
            }

            var data = ExtractChunk(chunk);
            using var stream = new MemoryStream(data);

            try
            {
                // Read top-level map (9 fields expected)
                var fieldCount = ReadMsgpackMapHeader(stream);
                if (fieldCount != 9)
                {
                    throw new InvalidDataException($"Expected 9 fields in metadata map, found {fieldCount}");
                }

                for (int i = 0; i < fieldCount; i++)
                {
                    var fieldName = ReadMsgpackString(stream);

                    switch (fieldName)
                    {
                        case "schema_version":
                            metadata.SchemaVersion = (uint)ReadMsgpackUInt(stream);
                            break;
                        case "name":
                            metadata.Name = ReadMsgpackString(stream);
                            break;
                        case "display_name":
                            metadata.DisplayName = ReadMsgpackString(stream);
                            break;
                        case "description":
                            if (!ReadMsgpackNil(stream))
                            {
                                metadata.Description = ReadMsgpackString(stream);
                            }
                            break;
                        case "version":
                            metadata.Version = ReadMsgpackString(stream);
                            break;
                        case "distributor":
                            // Consume distributor map (4 fields expected in C# writer)
                            var distMapCount = ReadMsgpackMapHeader(stream);
                            if (distMapCount > 5)
                            {
                                // If map count is wrong, attempt to skip fields based on expected types (simplification)
                                for (int j = 0; j < distMapCount; j++) { stream.ReadByte(); ReadMsgpackString(stream); } // Skip key and value
                            }
                            else
                            {
                                for (int j = 0; j < distMapCount; j++)
                                {
                                    var distFieldName = ReadMsgpackString(stream); // Read field name
                                    var distValue = ReadMsgpackString(stream); // Read string value (simplification)

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
                            ReadAuthors(stream, metadata.Authors);
                            break;
                        case "license":
                            metadata.License = ReadLicense(stream);
                            break;
                        case "layers":
                            ReadMsgpackArrayHeader(stream); // Skip layer array header (always 0 in C# writer)
                            break;
                        default:
                            // Skip unknown fields by consuming the next value type (simplification)
                            stream.ReadByte();
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading Msgpack metadata: {ex.Message}");
                // Return partial metadata on error
            }

            return metadata;
        }

        private static void ReadAuthors(MemoryStream stream, List<ModpkgAuthor> authors)
        {
            var authorCount = ReadMsgpackArrayHeader(stream);
            for (int i = 0; i < authorCount; i++)
            {
                ReadMsgpackMapHeader(stream); // Author map header (2 fields: name, role)

                string name = null;
                string role = null;

                // Read name
                ReadMsgpackString(stream); // "name"
                name = ReadMsgpackString(stream);

                // Read role
                ReadMsgpackString(stream); // "role"
                if (!ReadMsgpackNil(stream))
                {
                    role = ReadMsgpackString(stream);
                }

                authors.Add(new ModpkgAuthor(name, role));
            }
        }

        private static ModpkgLicense ReadLicense(MemoryStream stream)
        {
            ReadMsgpackMapHeader(stream); // License outer map header (1, 2, or 3 fields total)

            ReadMsgpackString(stream); // "type" key
            var tag = ReadMsgpackString(stream); // "none", "spdx", or "custom"

            var license = new ModpkgLicense(LicenseType.None);

            switch (tag)
            {
                case "none":
                    license.Type = LicenseType.None;
                    break;
                case "spdx":
                    license.Type = LicenseType.Spdx;
                    ReadMsgpackString(stream); // "spdx_id" key
                    license.SpdxId = ReadMsgpackString(stream);
                    break;
                case "custom":
                    license.Type = LicenseType.Custom;
                    ReadMsgpackString(stream); // "name" key
                    license.Name = ReadMsgpackString(stream);
                    ReadMsgpackString(stream); // "url" key
                    license.Url = ReadMsgpackString(stream);
                    break;
            }
            return license;
        }



        private string ReadNullTerminatedString()
        {
            var bytes = new List<byte>();
            byte b;
            while ((b = _reader.ReadByte()) != 0)
            {
                bytes.Add(b);
            }
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

            // Verify compressed checksum
            var actualChecksum = ModPkgLib.XXH3_64(compressedData);
            if (actualChecksum != chunk.CompressedChecksum)
                throw new InvalidDataException($"Compressed checksum mismatch for chunk 0x{chunk.PathHash:X16}");

            byte[] data;
            if (chunk.Compression == CompressionType.None)
            {
                data = compressedData;
            }
            else if (chunk.Compression == CompressionType.Zstd)
            {
                data = ZstdSharp.Zstd.Decompress(compressedData, (int)chunk.UncompressedSize);

            }
            else
            {
                throw new NotSupportedException($"Unsupported compression: {chunk.Compression}");
            }

            // Verify uncompressed checksum
            actualChecksum = ModPkgLib.XXH3_64(data);
            if (actualChecksum != chunk.UncompressedChecksum)
                throw new InvalidDataException($"Uncompressed checksum mismatch for chunk 0x{chunk.PathHash:X16}");

            return data;
        }

        public void Dispose()
        {
            _reader?.Dispose();
        }
    }

    internal class ModpkgWriter : IDisposable
    {
        private readonly string _outputPath;
        private ModpkgMetadata _metadata;
        private readonly List<ModpkgLayer> _layers = new();
        // Stores content chunks to be processed: (Path, Data, LayerName)
        private readonly List<(string Path, byte[] Data, string LayerName)> _chunksToProcess = new();
        // Stores other meta chunks to be processed: (Path, Data)
        private readonly List<(string Path, byte[] Data)> _otherMetaChunks = new();
        // Stores data needed for the final chunk headers list
        private readonly List<ModpkgChunk> _finalChunks = new();

        public ModpkgWriter(string outputPath)
        {
            _outputPath = outputPath;
        }

        public void SetMetadata(ModpkgMetadata metadata)
        {
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        }

        /// <summary>
        /// Adds data for a meta chunk (like thumbnail or readme). Meta chunks are stored uncompressed.
        /// </summary>
        public void AddMetaChunkData(string path, byte[] data)
        {
            // Meta chunks are always path-hashed and don't belong to a layer or wad.
            // Check if meta chunk with this path already exists and replace it.
            var existingIndex = _otherMetaChunks.FindIndex(c => c.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

            if (existingIndex >= 0)
            {
                _otherMetaChunks[existingIndex] = (path, data);
            }
            else
            {
                _otherMetaChunks.Add((path, data));
            }
        }

        public void AddLayer(string name, int priority)
        {
            // Only add unique layers
            if (!_layers.Any(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                _layers.Add(new ModpkgLayer(name, priority));
            }
        }

        public void AddChunk(string path, byte[] data, string layerName)
        {
            _chunksToProcess.Add((path, data, layerName));
        }

        // --- Msgpack Helper Methods ---

        private static void WriteMsgpackString(MemoryStream stream, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            var len = bytes.Length;

            if (len < 32)
            {
                // Fixstr format (0xa0 | len)
                stream.WriteByte((byte)(0xa0 | len));
            }
            else if (len < 256)
            {
                // Str 8 format (0xd9)
                stream.WriteByte(0xd9);
                stream.WriteByte((byte)len);
            }
            else if (len < 65536)
            {
                // Str 16 format (0xda)
                stream.WriteByte(0xda);
                stream.Write(BitConverter.GetBytes((ushort)len).Reverse().ToArray(), 0, 2); // Big Endian
            }
            else
            {
                // Str 32 format (0xdb) - unlikely needed but robust
                stream.WriteByte(0xdb);
                stream.Write(BitConverter.GetBytes((uint)len).Reverse().ToArray(), 0, 4); // Big Endian
            }
            stream.Write(bytes, 0, len);
        }

        private static void WriteMsgpackUInt(MemoryStream stream, ulong value)
        {
            if (value <= 127)
            {
                // Positive fixint
                stream.WriteByte((byte)value);
            }
            else if (value <= 0xFF) // uint 8
            {
                stream.WriteByte(0xcc);
                stream.WriteByte((byte)value);
            }
            else if (value <= 0xFFFF) // uint 16
            {
                stream.WriteByte(0xcd);
                stream.Write(BitConverter.GetBytes((ushort)value).Reverse().ToArray(), 0, 2);
            }
            else if (value <= 0xFFFFFFFF) // uint 32
            {
                stream.WriteByte(0xce);
                stream.Write(BitConverter.GetBytes((uint)value).Reverse().ToArray(), 0, 4);
            }
            else
            {
                // uint 64
                stream.WriteByte(0xcf);
                stream.Write(BitConverter.GetBytes(value).Reverse().ToArray(), 0, 8);
            }
        }

        private static void WriteMsgpackMapHeader(MemoryStream stream, uint count)
        {
            if (count <= 15)
            {
                // Fixmap (0x80 | count)
                stream.WriteByte((byte)(0x80 | count));
            }
            else if (count <= 0xFFFF)
            {
                // Map 16
                stream.WriteByte(0xde);
                stream.Write(BitConverter.GetBytes((ushort)count).Reverse().ToArray(), 0, 2);
            }
            else
            {
                // Map 32
                stream.WriteByte(0xdf);
                stream.Write(BitConverter.GetBytes((uint)count).Reverse().ToArray(), 0, 4);
            }
        }

        private static void WriteMsgpackNil(MemoryStream stream)
        {
            stream.WriteByte(0xc0);
        }

        private static void WriteMsgpackArrayHeader(MemoryStream stream, uint count)
        {
            if (count <= 15)
            {
                // Fixarray (0x90 | count)
                stream.WriteByte((byte)(0x90 | count));
            }
            else if (count <= 0xFFFF)
            {
                // Array 16
                stream.WriteByte(0xdc);
                stream.Write(BitConverter.GetBytes((ushort)count).Reverse().ToArray(), 0, 2);
            }
            else
            {
                // Array 32
                stream.WriteByte(0xdd);
                stream.Write(BitConverter.GetBytes((uint)count).Reverse().ToArray(), 0, 4);
            }
        }

        /// <summary>
        /// Manually serializes the ModpkgMetadata object into Msgpack format bytes.
        /// This is necessary for compatibility with the Rust application's rmp_serde deserializer.
        /// </summary>
        private byte[] SerializeMetadataMsgpack(ModpkgMetadata metadata)
        {
            // This is a complex manual serialization focusing on the snake_case keys and types expected by Rust.
            using var stream = new MemoryStream();

            // Fixed fields count (9 mandatory keys used in the Msgpack structure)
            // schema_version, name, display_name, description, version, distributor, authors, license, layers
            WriteMsgpackMapHeader(stream, 9);

            // 1. schema_version (u32)
            WriteMsgpackString(stream, "schema_version");
            WriteMsgpackUInt(stream, metadata.SchemaVersion);

            // 2. name (String)
            WriteMsgpackString(stream, "name");
            WriteMsgpackString(stream, metadata.Name);

            // 3. display_name (String)
            WriteMsgpackString(stream, "display_name");
            WriteMsgpackString(stream, metadata.DisplayName);

            // 4. description (Option<String>)
            WriteMsgpackString(stream, "description");
            if (string.IsNullOrEmpty(metadata.Description))
            {
                WriteMsgpackNil(stream); // Null
            }
            else
            {
                WriteMsgpackString(stream, metadata.Description);
            }

            // 5. version (String - Rust Version is serialised as a String)
            WriteMsgpackString(stream, "version");
            WriteMsgpackString(stream, metadata.Version);

            // 6. distributor (DistributorInfo - now mandatory structure with defaults)
            WriteMsgpackString(stream, "distributor");

            var distributor = metadata.Distributor ?? new DistributorInfo();

            // Distributor is mandatory in Msgpack structure (4 fields)
            WriteMsgpackMapHeader(stream, 5);

            WriteMsgpackString(stream, "site_id");
            WriteMsgpackString(stream, distributor.SiteId);

            WriteMsgpackString(stream, "site_name");
            WriteMsgpackString(stream, distributor.SiteName);

            WriteMsgpackString(stream, "site_url");
            WriteMsgpackString(stream, distributor.SiteUrl);

            WriteMsgpackString(stream, "mod_id");
            WriteMsgpackString(stream, distributor.ModId);

            WriteMsgpackString(stream, "release_id");
            WriteMsgpackString(stream, distributor.ReleaseId);

            // 7. authors (Vec<ModpkgAuthor>)
            WriteMsgpackString(stream, "authors");
            WriteMsgpackArrayHeader(stream, (uint)metadata.Authors.Count);
            foreach (var author in metadata.Authors)
            {
                // ModpkgAuthor is a map with 'name' and 'role'
                WriteMsgpackMapHeader(stream, 2);

                // name
                WriteMsgpackString(stream, "name");
                WriteMsgpackString(stream, author.Name);

                // role (Option<String>)
                WriteMsgpackString(stream, "role");
                if (string.IsNullOrEmpty(author.Role))
                {
                    WriteMsgpackNil(stream);
                }
                else
                {
                    WriteMsgpackString(stream, author.Role);
                }
            }

            // 8. license (Tagged Enum)
            WriteMsgpackString(stream, "license");

            // Rust Tagged Enum serialization uses a single map containing the tag and the data.
            string tag;
            uint mapSize;

            switch (metadata.License.Type)
            {
                case LicenseType.None:
                    tag = "none";
                    mapSize = 1; // Only 'type' field
                    break;
                case LicenseType.Spdx:
                    tag = "spdx";
                    mapSize = 2; // 'type' + 'spdx_id'
                    break;
                case LicenseType.Custom:
                    tag = "custom";
                    mapSize = 3; // 'type' + 'name' + 'url'
                    break;
                default:
                    tag = "none";
                    mapSize = 1;
                    break;
            }

            // Write the license map header
            WriteMsgpackMapHeader(stream, mapSize);

            // Write the tag: "type"
            WriteMsgpackString(stream, "type");
            WriteMsgpackString(stream, tag);

            // Write the data fields
            switch (metadata.License.Type)
            {
                case LicenseType.Spdx:
                    WriteMsgpackString(stream, "spdx_id");
                    WriteMsgpackString(stream, metadata.License.SpdxId);
                    break;
                case LicenseType.Custom:
                    WriteMsgpackString(stream, "name");
                    WriteMsgpackString(stream, metadata.License.Name);

                    WriteMsgpackString(stream, "url");
                    WriteMsgpackString(stream, metadata.License.Url ?? ""); // URL is non-optional in Rust struct but can be empty string
                    break;
            }

            // 9. layers (Vec<ModpkgLayerMetadata>) - currently unsupported/empty in C# structure
            WriteMsgpackString(stream, "layers");
            WriteMsgpackArrayHeader(stream, 0); // Always empty for now

            return stream.ToArray();
        }

        public void Write()
        {
            if (_metadata == null)
                throw new InvalidOperationException("Metadata not set");

            // Use File.Create to get a seekable stream for BinaryWriter
            using var fileStream = File.Create(_outputPath);
            using var writer = new BinaryWriter(fileStream);

            // Prepare index structures and write header (Pass 1 - Header/Index)
            var chunkPaths = _chunksToProcess.Select(c => c.Path)
                                             .Concat(new[] { Constants.METADATA_CHUNK_PATH }) // Include METADATA PATH
                                             .Concat(_otherMetaChunks.Select(c => c.Path))
                                             .Distinct()
                                             .ToList();
            var pathToIndex = chunkPaths.Select((p, i) => (p, i)).ToDictionary(x => x.p, x => (uint)x.i);

            // Total chunks includes content chunks + metadata (1) + other meta chunks
            var totalChunks = _chunksToProcess.Count + 1 + _otherMetaChunks.Count;

            // --- Header ---
            writer.Write(Constants.MAGIC);
            writer.Write(Constants.VERSION);
            var signatureSizePos = writer.BaseStream.Position;
            writer.Write(0U); // signature size placeholder
            var chunkCountPos = writer.BaseStream.Position;
            writer.Write((uint)totalChunks); // chunk count

            // Signature (empty for now)
            writer.Write(Array.Empty<byte>());

            // --- Layers ---
            writer.Write((uint)_layers.Count);
            foreach (var layer in _layers)
            {
                var nameBytes = Encoding.UTF8.GetBytes(layer.Name);
                writer.Write((uint)nameBytes.Length);
                writer.Write(nameBytes);
                writer.Write(layer.Priority);
            }

            // --- Chunk Paths ---
            writer.Write((uint)chunkPaths.Count);
            foreach (var path in chunkPaths)
            {
                writer.Write(Encoding.UTF8.GetBytes(path));
                writer.Write((byte)0); // Null terminator
            }

            // --- Wads (Empty for now, but index tables must be present) ---
            writer.Write(0U); // wad count

            // Align to 8 bytes
            var currentPos = writer.BaseStream.Position;
            var padding = (8 - (currentPos % 8)) % 8;
            for (int i = 0; i < padding; i++)
                writer.Write((byte)0);

            // Reserve space for chunk headers (TOC)
            var chunksHeaderPos = writer.BaseStream.Position;
            writer.BaseStream.Seek(totalChunks * ModpkgChunk.ChunkHeaderSize, SeekOrigin.Current);

            // --- Write Chunk Data (Pass 2 - Data) ---

            // 0. Write Mandatory Meta Chunk (Metadata)
            var metadataBytes = SerializeMetadataMsgpack(_metadata);
            ProcessAndWriteChunk(writer, Constants.METADATA_CHUNK_PATH, metadataBytes, null, pathToIndex, Constants.NO_LAYER_INDEX, Constants.NO_LAYER_HASH, true);

            // 1. Write Other Meta Chunks (Thumbnail, README)
            foreach (var (path, data) in _otherMetaChunks)
            {
                // Meta chunks are always written uncompressed (isMetaChunk=true)
                ProcessAndWriteChunk(writer, path, data, null, pathToIndex, Constants.NO_LAYER_INDEX, Constants.NO_LAYER_HASH, true);
            }

            // 2. Write Content Chunks
            foreach (var (path, data, layerName) in _chunksToProcess)
            {
                var layerIndex = string.IsNullOrEmpty(layerName)
                    ? Constants.NO_LAYER_INDEX
                    : (uint)_layers.FindIndex(l => l.Name == layerName);
                var layerHash = string.IsNullOrEmpty(layerName)
                    ? Constants.NO_LAYER_HASH
                    : ModPkgLib.HashLayerName(layerName);

                ProcessAndWriteChunk(writer, path, data, layerName, pathToIndex, layerIndex, layerHash, false);
            }

            // --- Write Chunk Headers (Pass 3 - TOC Update) ---
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

            // Ensure all buffered content is written to the underlying file stream
            writer.Flush();
        }

        private void ProcessAndWriteChunk(BinaryWriter writer, string path, byte[] data, string layerName,
            Dictionary<string, uint> pathToIndex, uint layerIndex, ulong layerHash, bool isMetaChunk)
        {
            var dataOffset = (ulong)writer.BaseStream.Position;

            // Determine compression
            byte[] compressedData;
            CompressionType compression;

            if (isMetaChunk)
            {
                // Meta chunks are stored uncompressed.
                compressedData = data;
                compression = CompressionType.None;
            }
            else
            {
                // Attempt Compression (Zstd if smaller) for content chunks
                var compressed = ZstdSharp.Zstd.Compress(data);
                if (compressed.Length < data.Length)
                {
                    compressedData = compressed;
                    compression = CompressionType.Zstd;
                }
                else
                {
                    compressedData = data;
                    compression = CompressionType.None;
                }
            }

            // Write chunk data payload
            writer.Write(compressedData);

            // Build ModpkgChunk record
            var chunk = new ModpkgChunk
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
                WadIndex = Constants.NO_WAD_INDEX // WAD support currently not fully implemented in Pack flow
            };

            _finalChunks.Add(chunk);
        }

        public void Dispose()
        {
        }
    }
}