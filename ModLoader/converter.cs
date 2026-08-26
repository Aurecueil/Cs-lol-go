using Jade.Ritobin;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.IO.Hashing;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ZstdSharp;
using static ModManager.Repatheruwu;
using Path = System.IO.Path;
using SearchOption = System.IO.SearchOption;

namespace ModManager
{
    #region JSON Schema Models

    public class ConversionRule
    {
        [JsonPropertyName("class")]
        public string ClassName { get; set; } = string.Empty;

        [JsonPropertyName("field")]
        public string FieldName { get; set; } = string.Empty;

        [JsonPropertyName("from")]
        public TypeDescriptor From { get; set; } = new();

        [JsonPropertyName("to")]
        public TypeDescriptor To { get; set; } = new();

        [JsonPropertyName("conversion")]
        public string Conversion { get; set; } = string.Empty;
    }

    public class TypeDescriptor
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("class")]
        public string? ClassName { get; set; }

        [JsonPropertyName("value")]
        public string? ValueType { get; set; }

        [JsonPropertyName("key")]
        public string? KeyType { get; set; }

        [JsonPropertyName("size")]
        public int? Size { get; set; }
    }

    #endregion

    public partial class BinFieldConverter
    {
        private readonly Dictionary<(uint ClassHash, uint FieldHash), (ConversionRule Rule, Action<BinField> TransformAction)> _fieldTransformers = new();

        public BinFieldConverter() { }

        public BinFieldConverter(string rulesFilePath)
        {
            LoadRulesFromFile(rulesFilePath);
        }

        public void LoadRulesFromFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                var content = File.ReadAllText(filePath);
                LoadRulesFromJson(content);
            }
        }

        public void LoadRulesFromJson(string jsonContent)
        {
            var trimmed = jsonContent.Trim();
            if (trimmed.StartsWith("["))
            {
                var rules = JsonSerializer.Deserialize<List<ConversionRule>>(trimmed);
                if (rules != null)
                {
                    foreach (var rule in rules) RegisterRule(rule);
                }
            }
            else
            {
                using var reader = new StringReader(jsonContent);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var rule = JsonSerializer.Deserialize<ConversionRule>(line);
                        if (rule != null) RegisterRule(rule);
                    }
                    catch { }
                }
            }
        }

        public void ConvertBin(Bin bin) => ConvertBinInternal(bin, null);

        public void ConvertBinWithTracking(Bin bin, ConcurrentBag<string> trackedStrings) => ConvertBinInternal(bin, trackedStrings);

        private void ConvertBinInternal(Bin bin, ConcurrentBag<string>? trackedStrings)
        {
            if (bin.Sections.TryGetValue("entries", out var entriesVal) && entriesVal is BinMap entriesMap)
            {
                foreach (var kvp in entriesMap.Items)
                {
                    if (kvp.Value is BinEmbed entryEmbed)
                        TransformEmbed(entryEmbed, trackedStrings);
                }
            }

            if (bin.Sections.TryGetValue("patches", out var patchesVal) && patchesVal is BinMap patchesMap)
            {
                foreach (var kvp in patchesMap.Items)
                {
                    if (kvp.Value is BinEmbed patchEmbed)
                        TransformEmbed(patchEmbed, trackedStrings);
                }
            }
        }

        private void TransformEmbed(BinEmbed embed, ConcurrentBag<string>? trackedStrings)
        {
            uint classHash = embed.Name.Hash;

            foreach (var field in embed.Items)
            {
                if (_fieldTransformers.TryGetValue((classHash, field.Key.Hash), out var mapping))
                {
                    if (trackedStrings != null)
                    {
                        CollectStringValues(field.Value, trackedStrings);
                    }
                    mapping.TransformAction(field);
                }

                TransformNestedValue(field.Value, trackedStrings);
            }
        }

        private void TransformPointer(BinPointer ptr, ConcurrentBag<string>? trackedStrings)
        {
            uint classHash = ptr.Name.Hash;

            foreach (var field in ptr.Items)
            {
                if (_fieldTransformers.TryGetValue((classHash, field.Key.Hash), out var mapping))
                {
                    if (trackedStrings != null)
                    {
                        CollectStringValues(field.Value, trackedStrings);
                    }
                    mapping.TransformAction(field);
                }

                TransformNestedValue(field.Value, trackedStrings);
            }
        }

        private void TransformNestedValue(BinValue val, ConcurrentBag<string>? trackedStrings)
        {
            switch (val)
            {
                case BinEmbed e:
                    TransformEmbed(e, trackedStrings);
                    break;

                case BinPointer p:
                    TransformPointer(p, trackedStrings);
                    break;

                case BinList l:
                    foreach (var item in l.Items)
                        TransformNestedValue(item, trackedStrings);
                    break;

                case BinList2 l2:
                    foreach (var item in l2.Items)
                        TransformNestedValue(item, trackedStrings);
                    break;

                case BinOption opt:
                    foreach (var item in opt.Items)
                        TransformNestedValue(item, trackedStrings);
                    break;

                case BinMap map:
                    foreach (var kvp in map.Items)
                    {
                        TransformNestedValue(kvp.Key, trackedStrings);
                        TransformNestedValue(kvp.Value, trackedStrings);
                    }
                    break;
            }
        }

        private static void CollectStringValues(BinValue val, ConcurrentBag<string> bag)
        {
            switch (val)
            {
                case BinString s:
                    if (!string.IsNullOrEmpty(s.Value)) bag.Add(s.Value);
                    break;
                case BinList l:
                    foreach (var item in l.Items) CollectStringValues(item, bag);
                    break;
                case BinList2 l2:
                    foreach (var item in l2.Items) CollectStringValues(item, bag);
                    break;
                case BinOption opt:
                    foreach (var item in opt.Items) CollectStringValues(item, bag);
                    break;
                case BinMap map:
                    foreach (var kvp in map.Items)
                    {
                        CollectStringValues(kvp.Key, bag);
                        CollectStringValues(kvp.Value, bag);
                    }
                    break;
            }
        }

        private void RegisterRule(ConversionRule rule)
        {
            uint classHash = ParseHashOrName(rule.ClassName);
            uint fieldHash = ParseHashOrName(rule.FieldName);

            _fieldTransformers[(classHash, fieldHash)] = (rule, (field) =>
            {
                ApplyRuleToField(field, rule);
            }
            );
        }

        private void ApplyRuleToField(BinField field, ConversionRule rule)
        {
            // 1. String -> File ("hash_value")
            if (rule.From.Type == "String" && rule.To.Type == "File")
            {
                if (field.Value is BinString s)
                    field.Value = ConvertStringToBinFile(s.Value);
                return;
            }

            // 2. Hash -> File ("rehash"): Keep existing FNV1a hash zero-extended in XXH64
            if (rule.From.Type == "Hash" && rule.To.Type == "File")
            {
                if (field.Value is BinHash h)
                    field.Value = new BinFile(new XXH64(h.Value.Hash, h.Value.String));
                return;
            }

            // 3. Embed -> Pointer ("none"): Swap type
            if (rule.From.Type == "Embed" && rule.To.Type == "Pointer")
            {
                if (field.Value is BinEmbed embed)
                {
                    var ptr = new BinPointer(embed.Name);
                    ptr.Items.AddRange(embed.Items);
                    field.Value = ptr;
                }
                return;
            }

            // 4. List conversion
            if (rule.From.Type == "List" && field.Value is BinList list)
            {
                if (rule.To.ValueType == "File")
                {
                    list.ValueType = BinType.File;
                    for (int i = 0; i < list.Items.Count; i++)
                    {
                        if (list.Items[i] is BinString s)
                            list.Items[i] = ConvertStringToBinFile(s.Value);
                    }
                }
                return;
            }

            // 5. List2 conversion
            if (rule.From.Type == "List2" && field.Value is BinList2 list2)
            {
                if (rule.To.ValueType == "File")
                {
                    list2.ValueType = BinType.File;
                    for (int i = 0; i < list2.Items.Count; i++)
                    {
                        if (list2.Items[i] is BinString s)
                            list2.Items[i] = ConvertStringToBinFile(s.Value);
                    }
                }
                else if (rule.To.ValueType == "Embed" && !string.IsNullOrEmpty(rule.To.ClassName))
                {
                    uint newClassHash = ParseHashOrName(rule.To.ClassName);
                    foreach (var item in list2.Items)
                    {
                        if (item is BinEmbed e)
                            e.Name = new FNV1a(newClassHash);
                    }
                }
                return;
            }

            // 6. Option conversion
            if (rule.From.Type == "Option" && field.Value is BinOption opt)
            {
                if (rule.To.ValueType == "File")
                {
                    opt.ValueType = BinType.File;
                    for (int i = 0; i < opt.Items.Count; i++)
                    {
                        if (opt.Items[i] is BinString s)
                            opt.Items[i] = ConvertStringToBinFile(s.Value);
                    }
                }
                return;
            }

            // 7. Map conversion
            if (rule.From.Type == "Map" && field.Value is BinMap map)
            {
                // Value conversion (hash_value: String -> File)
                if (rule.To.ValueType == "File")
                {
                    map.ValueType = BinType.File;
                    for (int i = 0; i < map.Items.Count; i++)
                    {
                        var kvp = map.Items[i];
                        if (kvp.Value is BinString s)
                        {
                            map.Items[i] = new KeyValuePair<BinValue, BinValue>(kvp.Key, ConvertStringToBinFile(s.Value));
                        }
                    }
                }

                // Key conversion (hash_key: Hash -> File)
                if (rule.To.KeyType == "File")
                {
                    map.KeyType = BinType.File;
                    for (int i = 0; i < map.Items.Count; i++)
                    {
                        var kvp = map.Items[i];
                        if (kvp.Key is BinHash h)
                        {
                            var fileKey = new BinFile(new XXH64(h.Value.Hash, h.Value.String));
                            map.Items[i] = new KeyValuePair<BinValue, BinValue>(fileKey, kvp.Value);
                        }
                    }
                }
            }
        }

        #region Hashing & Parsing Helpers

        public static ulong HashPath(string path)
        {
            string norm = path.Replace('\\', '/').ToLowerInvariant();
            byte[] data = Encoding.UTF8.GetBytes(norm);
            return XxHash64.HashToUInt64(data, seed: 0);
        }

        private static BinFile ConvertStringToBinFile(string path)
        {
            ulong hash = HashPath(path);
            return new BinFile(new XXH64(hash, path));
        }

        private static uint ParseHashOrName(string token)
        {
            if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return uint.Parse(token[2..], NumberStyles.HexNumber);
            }
            return FNV1a.Calculate(token);
        }

        #endregion
    }

    public class WadBatchProcessor
    {
        private readonly WadExtractor _extractor;
        private readonly BinFieldConverter _converter;

        public WadBatchProcessor(WadExtractor extractor, BinFieldConverter converter)
        {
            _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
            _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        }

        public async Task ProcessFolderAsync(string targetDirectory, CancellationToken ct = default)
        {
            if (!Directory.Exists(targetDirectory))
                throw new DirectoryNotFoundException($"Target directory not found: {targetDirectory}");

            // 1. Pre-seed standard character & skin bin paths (skin0..skin100) based on folder/wad names
            await PreseedCommonSkinHashesAsync(targetDirectory, ct);

            // 2. Unpack all .wad / .wad.client archives into their respective folders
            var extractedWadFolders = await ProcessAllWadsInDirectoryAsync(targetDirectory, ct);

            // 3. Scan & Convert all .bin files in each extracted folder, collecting internal string references
            var convertedStrings = new ConcurrentBag<string>();
            ConvertAllBinsInDirectory(targetDirectory, convertedStrings);

            // 4. Register newly discovered strings from .bin files into HashMaster
            if (!convertedStrings.IsEmpty)
            {
                await HashMaster.AddTemporaryHashesAsync(convertedStrings, ct);
            }

            // 5. Pass 2: Rename remaining {hash:x16}.* files strictly INSIDE their respective WAD folders
            foreach (var wadFolder in extractedWadFolders)
            {
                if (Directory.Exists(wadFolder))
                {
                    await ResolveRemainingHashedFilesInWadFolderAsync(wadFolder, ct);
                }
            }

            // 6. Save all converted/discovered strings formatted to ../meta/files.txt
            SaveConvertedStrings(targetDirectory, convertedStrings);
        }

        private async Task PreseedCommonSkinHashesAsync(string rootDirectory, CancellationToken ct)
        {
            var candidateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var wadFiles = Directory.GetFiles(rootDirectory, "*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".wad", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".wad.client", StringComparison.OrdinalIgnoreCase));

            foreach (var f in wadFiles)
            {
                string name = Path.GetFileName(f);
                int firstDot = name.IndexOf('.');
                string champ = firstDot > 0 ? name[..firstDot] : name;
                if (!string.IsNullOrWhiteSpace(champ)) candidateNames.Add(champ.ToLowerInvariant());
            }

            foreach (var dir in Directory.GetDirectories(rootDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                string dirName = Path.GetFileName(dir);
                int firstDot = dirName.IndexOf('.');
                string champ = firstDot > 0 ? dirName[..firstDot] : dirName;
                if (!string.IsNullOrWhiteSpace(champ)) candidateNames.Add(champ.ToLowerInvariant());
            }

            var generatedPaths = new List<string>();
            foreach (var champ in candidateNames)
            {
                generatedPaths.Add($"data/characters/{champ}/{champ}.bin");
                generatedPaths.Add($"data/characters/{champ}/skins/root.bin");

                for (int i = 0; i < 100; i++)
                {
                    generatedPaths.Add($"data/characters/{champ}/skins/skin{i}.bin");
                    generatedPaths.Add($"data/characters/{champ}/animations/skin{i}.bin");
                }
            }

            if (generatedPaths.Count > 0)
            {
                await HashMaster.AddTemporaryHashesAsync(generatedPaths, ct);
            }
        }

        private async Task<List<string>> ProcessAllWadsInDirectoryAsync(string directory, CancellationToken ct)
        {
            var wadFiles = Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".wad", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".wad.client", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var extractedFolders = new List<string>();

            foreach (var wadPath in wadFiles)
            {
                ct.ThrowIfCancellationRequested();

                string? wadDir = Path.GetDirectoryName(wadPath);
                string wadFileName = Path.GetFileName(wadPath);
                string tempWadPath = Path.Combine(wadDir ?? directory, $"{wadFileName}.{Guid.NewGuid():N}.tmp");
                string outputExtractFolder = Path.Combine(wadDir ?? directory, wadFileName);

                File.Move(wadPath, tempWadPath);

                try
                {
                    if (!Directory.Exists(outputExtractFolder))
                        Directory.CreateDirectory(outputExtractFolder);

                    await ExtractWadFileAsync(tempWadPath, outputExtractFolder, ct);
                    extractedFolders.Add(outputExtractFolder);
                }
                finally
                {
                    if (File.Exists(tempWadPath))
                    {
                        File.Delete(tempWadPath);
                    }
                }
            }

            return extractedFolders;
        }

        private struct RawWadEntry
        {
            public ulong PathHash;
            public uint Offset;
            public uint CompressedSize;
            public uint UncompressedSize;
            public byte Type;
        }

        private static bool IsZstd(byte[] data, int length) =>
            length >= 4 &&
            data[0] == 0x28 && data[1] == 0xB5 && data[2] == 0x2F && data[3] == 0xFD;

        private static bool IsGzip(byte[] data, int length) =>
            length >= 2 &&
            data[0] == 0x1F && data[1] == 0x8B;

        private static byte[] DecompressGzip(byte[] data, int length)
        {
            using var ms = new MemoryStream(data, 0, length);
            using var gs = new GZipStream(ms, CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            gs.CopyTo(outMs);
            return outMs.ToArray();
        }

        private static byte[] DecompressZstd(byte[] data, int length, uint uncompressedSize)
        {
            using var decompressor = new Decompressor();
            if (uncompressedSize > 0)
            {
                byte[] outBuffer = new byte[uncompressedSize];
                int written = decompressor.Unwrap(data.AsSpan(0, length), outBuffer.AsSpan());
                if (written == uncompressedSize) return outBuffer;
                return outBuffer.AsSpan(0, written).ToArray();
            }
            return decompressor.Unwrap(data.AsSpan(0, length)).ToArray();
        }

        private static byte[] DecompressEntry(byte[] compBuffer, int bytesRead, RawWadEntry entry)
        {
            if (IsZstd(compBuffer, bytesRead) || entry.Type == 3)
            {
                try { return DecompressZstd(compBuffer, bytesRead, entry.UncompressedSize); }
                catch
                {
                    byte[] fallback = new byte[bytesRead];
                    Array.Copy(compBuffer, fallback, bytesRead);
                    return fallback;
                }
            }
            else if (IsGzip(compBuffer, bytesRead) || entry.Type == 1)
            {
                try { return DecompressGzip(compBuffer, bytesRead); }
                catch
                {
                    byte[] fallback = new byte[bytesRead];
                    Array.Copy(compBuffer, fallback, bytesRead);
                    return fallback;
                }
            }
            else
            {
                byte[] raw = new byte[bytesRead];
                Array.Copy(compBuffer, raw, bytesRead);
                return raw;
            }
        }

        private async Task ExtractWadFileAsync(string wadFilePath, string outputDir, CancellationToken ct)
        {
            var entries = new List<RawWadEntry>();
            byte[] entryBuffer = new byte[32];

            using (var fs = new FileStream(wadFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var br = new BinaryReader(fs))
            {
                if (fs.Length < 272) return;

                byte[] magic = br.ReadBytes(2);
                if (magic.Length < 2 || magic[0] != 'R' || magic[1] != 'W') return;

                byte major = br.ReadByte();
                byte minor = br.ReadByte();

                fs.Seek(268, SeekOrigin.Begin);
                uint fileCount = br.ReadUInt32();

                for (int i = 0; i < fileCount; i++)
                {
                    if (fs.Read(entryBuffer, 0, 32) != 32) break;

                    entries.Add(new RawWadEntry
                    {
                        PathHash = BitConverter.ToUInt64(entryBuffer, 0),
                        Offset = BitConverter.ToUInt32(entryBuffer, 8),
                        CompressedSize = BitConverter.ToUInt32(entryBuffer, 12),
                        UncompressedSize = BitConverter.ToUInt32(entryBuffer, 16),
                        Type = entryBuffer[20]
                    });
                }
            }

            if (entries.Count == 0) return;

            var uniqueHashes = entries.Select(e => e.PathHash).Distinct();
            var unhashedMap = await HashMaster.UnhashBatchAsync(uniqueHashes, ct);

            using (var fs = new FileStream(wadFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                foreach (var entry in entries)
                {
                    ct.ThrowIfCancellationRequested();

                    fs.Seek(entry.Offset, SeekOrigin.Begin);
                    byte[] compBuffer = ArrayPool<byte>.Shared.Rent((int)entry.CompressedSize);

                    try
                    {
                        int bytesRead = fs.Read(compBuffer, 0, (int)entry.CompressedSize);
                        byte[] decompressed = DecompressEntry(compBuffer, bytesRead, entry);

                        string relativePath;
                        if (unhashedMap.TryGetValue(entry.PathHash, out string? resolvedPath) && !string.IsNullOrEmpty(resolvedPath))
                        {
                            relativePath = resolvedPath;
                        }
                        else
                        {
                            string ext = GuessExtension(decompressed);
                            relativePath = $"{entry.PathHash:x16}{ext}";
                        }

                        // Ensure paths extract STRICTLY inside outputDir
                        relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar)
                                                   .Replace('\\', Path.DirectorySeparatorChar)
                                                   .TrimStart(Path.DirectorySeparatorChar);

                        string fullOutPath = Path.Combine(outputDir, relativePath);
                        string? dir = Path.GetDirectoryName(fullOutPath);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }

                        File.WriteAllBytes(fullOutPath, decompressed);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(compBuffer);
                    }
                }
            }
        }

        private void ConvertAllBinsInDirectory(string rootDirectory, ConcurrentBag<string> convertedStrings)
        {
            var allFiles = Directory.GetFiles(rootDirectory, "*", SearchOption.AllDirectories);

            foreach (var filePath in allFiles)
            {
                if (!IsBinFile(filePath)) continue;

                try
                {
                    byte[] rawBytes = File.ReadAllBytes(filePath);
                    var reader = new BinReader(rawBytes);
                    Bin bin = reader.Read();

                    HarvestAllStringsFromBin(bin, convertedStrings);
                    _converter.ConvertBinWithTracking(bin, convertedStrings);

                    var writer = new BinWriter();
                    byte[] outputBytes = writer.Write(bin);
                    File.WriteAllBytes(filePath, outputBytes);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[BinConverter] Skipped {filePath}: {ex.Message}");
                }
            }
        }

        private static void HarvestAllStringsFromBin(Bin bin, ConcurrentBag<string> bag)
        {
            if (bin.Sections.TryGetValue("linked", out var linkedVal) && linkedVal is BinList linkedList)
            {
                foreach (var item in linkedList.Items)
                {
                    if (item is BinString s && !string.IsNullOrEmpty(s.Value))
                        bag.Add(s.Value);
                }
            }

            if (bin.Sections.TryGetValue("entries", out var entriesVal) && entriesVal is BinMap entriesMap)
            {
                foreach (var kvp in entriesMap.Items)
                {
                    CollectAllStrings(kvp.Key, bag);
                    CollectAllStrings(kvp.Value, bag);
                }
            }

            if (bin.Sections.TryGetValue("patches", out var patchesVal) && patchesVal is BinMap patchesMap)
            {
                foreach (var kvp in patchesMap.Items)
                {
                    CollectAllStrings(kvp.Key, bag);
                    CollectAllStrings(kvp.Value, bag);
                }
            }
        }

        private static void CollectAllStrings(BinValue val, ConcurrentBag<string> bag)
        {
            switch (val)
            {
                case BinString s:
                    if (!string.IsNullOrEmpty(s.Value)) bag.Add(s.Value);
                    break;
                case BinEmbed e:
                    foreach (var f in e.Items) CollectAllStrings(f.Value, bag);
                    break;
                case BinPointer p:
                    foreach (var f in p.Items) CollectAllStrings(f.Value, bag);
                    break;
                case BinList l:
                    foreach (var item in l.Items) CollectAllStrings(item, bag);
                    break;
                case BinList2 l2:
                    foreach (var item in l2.Items) CollectAllStrings(item, bag);
                    break;
                case BinOption opt:
                    foreach (var item in opt.Items) CollectAllStrings(item, bag);
                    break;
                case BinMap m:
                    foreach (var kvp in m.Items)
                    {
                        CollectAllStrings(kvp.Key, bag);
                        CollectAllStrings(kvp.Value, bag);
                    }
                    break;
            }
        }

        private static async Task ResolveRemainingHashedFilesInWadFolderAsync(string wadExtractFolder, CancellationToken ct)
        {
            var files = Directory.GetFiles(wadExtractFolder, "*", SearchOption.AllDirectories);
            var hashedFiles = new List<(string FilePath, ulong Hash, string Ext)>();

            foreach (var file in files)
            {
                string nameNoExt = Path.GetFileNameWithoutExtension(file);
                if (nameNoExt.Length == 16 && ulong.TryParse(nameNoExt, NumberStyles.HexNumber, null, out ulong hash))
                {
                    hashedFiles.Add((file, hash, Path.GetExtension(file)));
                }
            }

            if (hashedFiles.Count == 0) return;

            var unhashedMap = await HashMaster.UnhashBatchAsync(hashedFiles.Select(x => x.Hash).Distinct(), ct);

            foreach (var (filePath, hash, ext) in hashedFiles)
            {
                if (unhashedMap.TryGetValue(hash, out string? resolvedPath) && !string.IsNullOrEmpty(resolvedPath))
                {
                    string targetRel = resolvedPath.Replace('/', Path.DirectorySeparatorChar)
                                                   .Replace('\\', Path.DirectorySeparatorChar)
                                                   .TrimStart(Path.DirectorySeparatorChar);

                    // Place the resolved file INSIDE the wad extract folder
                    string targetFull = Path.Combine(wadExtractFolder, targetRel);

                    string? targetDir = Path.GetDirectoryName(targetFull);
                    if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    if (File.Exists(targetFull))
                    {
                        File.Delete(targetFull);
                    }

                    File.Move(filePath, targetFull);
                }
            }
        }

        private static bool IsBinFile(string filePath)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (fs.Length < 4) return false;

                byte[] magic = new byte[4];
                int read = fs.Read(magic, 0, 4);
                if (read < 4) return false;

                string magicStr = Encoding.ASCII.GetString(magic);
                return magicStr is "PROP" or "PTCH";
            }
            catch
            {
                return false;
            }
        }

        private static string GuessExtension(byte[] data)
        {
            if (data.Length >= 4)
            {
                string magic = Encoding.ASCII.GetString(data, 0, 4);
                if (magic is "PROP" or "PTCH") return ".bin";
                if (magic == "BKHD") return ".bnk";
                if (magic == "DDS ") return ".dds";
                if (magic == "OggS") return ".ogg";
                if (magic.StartsWith("r3d")) return ".scb";
            }
            if (data.Length >= 3 && data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46) return ".gif";
            if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47) return ".png";
            return ".bin";
        }

        private static void SaveConvertedStrings(string rootDirectory, ConcurrentBag<string> strings)
        {
            string metaDir = Path.GetFullPath(Path.Combine(rootDirectory, "..", "meta"));
            if (!Directory.Exists(metaDir))
                Directory.CreateDirectory(metaDir);

            string metaFilePath = Path.Combine(metaDir, "files.txt");

            var entries = new SortedDictionary<ulong, string>();

            if (File.Exists(metaFilePath))
            {
                foreach (var line in File.ReadAllLines(metaFilePath))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    int firstSpace = line.IndexOfAny(new[] { ' ', '\t' });
                    if (firstSpace > 0 && firstSpace < line.Length - 1)
                    {
                        var hexSpan = line.AsSpan(0, firstSpace);
                        if (ulong.TryParse(hexSpan, NumberStyles.HexNumber, null, out ulong hash))
                        {
                            entries[hash] = line.Substring(firstSpace + 1).Trim();
                        }
                    }
                }
            }

            foreach (var s in strings)
            {
                if (string.IsNullOrWhiteSpace(s)) continue;

                string normPath = s.Replace('\\', '/').Trim();
                ulong hash = BinFieldConverter.HashPath(normPath);
                entries[hash] = normPath;
            }

            var outputLines = entries.Select(kvp => $"{kvp.Key:x16} {kvp.Value}");
            File.WriteAllLines(metaFilePath, outputLines, Encoding.UTF8);
        }
    }
}