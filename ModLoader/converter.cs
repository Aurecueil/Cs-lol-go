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
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
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

    public class ConvertedStringsTracker
    {
        public ConcurrentBag<string> GameFiles { get; } = new();
        public ConcurrentBag<string> BinEntries { get; } = new();
        public ConcurrentBag<string> UnconvertedTempStrings { get; } = new();
    }

    public partial class BinFieldConverter
    {
        private readonly Dictionary<(uint ClassHash, uint FieldHash), (ConversionRule Rule, Action<BinField, ConvertedStringsTracker?> TransformAction)> _fieldTransformers = new();
        public IReadOnlyDictionary<(uint ClassHash, uint FieldHash), (ConversionRule Rule, Action<BinField, ConvertedStringsTracker?> TransformAction)> Transformers => _fieldTransformers;

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

        public void ConvertBinWithTracking(Bin bin, ConvertedStringsTracker tracker) => ConvertBinInternal(bin, tracker);

        private void ConvertBinInternal(Bin bin, ConvertedStringsTracker? tracker)
        {
            if (bin.Sections.TryGetValue("entries", out var entriesVal) && entriesVal is BinMap entriesMap)
            {
                foreach (var kvp in entriesMap.Items)
                {
                    if (kvp.Value is BinEmbed entryEmbed)
                        TransformEmbed(entryEmbed, tracker);
                }
            }

            if (bin.Sections.TryGetValue("patches", out var patchesVal) && patchesVal is BinMap patchesMap)
            {
                foreach (var kvp in patchesMap.Items)
                {
                    if (kvp.Value is BinEmbed patchEmbed)
                        TransformEmbed(patchEmbed, tracker);
                }
            }
        }

        public void ConvertBinMap(BinMap bin, ConvertedStringsTracker? tracker) => ConvertBinInternalMap(bin, tracker);
        private void ConvertBinInternalMap(BinMap bin, ConvertedStringsTracker? tracker)
        {
            foreach (var kvp in bin.Items)
            {
                if (kvp.Value is BinEmbed entryEmbed)
                    TransformEmbed(entryEmbed, tracker);
            }
        }

        private void TransformEmbed(BinEmbed embed, ConvertedStringsTracker? tracker)
        {
            uint classHash = embed.Name.Hash;

            foreach (var field in embed.Items)
            {
                if (_fieldTransformers.TryGetValue((classHash, field.Key.Hash), out var mapping))
                {
                    mapping.TransformAction(field, tracker);
                }
                else
                {
                    CollectRawStrings(field.Value, tracker?.UnconvertedTempStrings);
                }

                TransformNestedValue(field.Value, tracker);
            }
        }

        private void TransformPointer(BinPointer ptr, ConvertedStringsTracker? tracker)
        {
            uint classHash = ptr.Name.Hash;

            foreach (var field in ptr.Items)
            {
                if (_fieldTransformers.TryGetValue((classHash, field.Key.Hash), out var mapping))
                {
                    mapping.TransformAction(field, tracker);
                }
                else
                {
                    CollectRawStrings(field.Value, tracker?.UnconvertedTempStrings);
                }

                TransformNestedValue(field.Value, tracker);
            }
        }

        private void TransformNestedValue(BinValue val, ConvertedStringsTracker? tracker)
        {
            switch (val)
            {
                case BinEmbed e:
                    TransformEmbed(e, tracker);
                    break;

                case BinPointer p:
                    TransformPointer(p, tracker);
                    break;

                case BinList l:
                    foreach (var item in l.Items)
                        TransformNestedValue(item, tracker);
                    break;

                case BinList2 l2:
                    foreach (var item in l2.Items)
                        TransformNestedValue(item, tracker);
                    break;

                case BinOption opt:
                    foreach (var item in opt.Items)
                        TransformNestedValue(item, tracker);
                    break;

                case BinMap map:
                    foreach (var kvp in map.Items)
                    {
                        TransformNestedValue(kvp.Key, tracker);
                        TransformNestedValue(kvp.Value, tracker);
                    }
                    break;
            }
        }

        public static void CollectRawStrings(BinValue val, ConcurrentBag<string>? bag)
        {
            if (bag == null) return;

            switch (val)
            {
                case BinString s when !string.IsNullOrWhiteSpace(s.Value):
                    bag.Add(s.Value.Trim());
                    break;
                case BinHash h when !string.IsNullOrWhiteSpace(h.Value.String):
                    bag.Add(h.Value.String.Trim());
                    break;
                case BinFile f when !string.IsNullOrWhiteSpace(f.Value.String):
                    bag.Add(f.Value.String.Trim());
                    break;

                case BinEmbed embed:
                    foreach (var field in embed.Items)
                        CollectRawStrings(field.Value, bag);
                    break;
                case BinPointer ptr:
                    foreach (var field in ptr.Items)
                        CollectRawStrings(field.Value, bag);
                    break;
                case BinList l:
                    foreach (var item in l.Items)
                        CollectRawStrings(item, bag);
                    break;
                case BinList2 l2:
                    foreach (var item in l2.Items)
                        CollectRawStrings(item, bag);
                    break;
                case BinOption opt:
                    foreach (var item in opt.Items)
                        CollectRawStrings(item, bag);
                    break;
                case BinMap map:
                    foreach (var kvp in map.Items)
                    {
                        CollectRawStrings(kvp.Key, bag);
                        CollectRawStrings(kvp.Value, bag);
                    }
                    break;
            }
        }

        private void RegisterRule(ConversionRule rule)
        {
            uint classHash = ParseHashOrName(rule.ClassName);
            uint fieldHash = ParseHashOrName(rule.FieldName);

            _fieldTransformers[(classHash, fieldHash)] = (rule, (field, tracker) =>
            {
                ApplyRuleToField(field, rule, tracker);
            }
            );
        }

        private void ApplyRuleToField(BinField field, ConversionRule rule, ConvertedStringsTracker? tracker)
        {
            string fromType = rule.From.Type;
            string toType = rule.To.Type;

            // Handle string-to-file/hash conversions
            if (fromType == "String")
            {
                if (toType == "File" && field.Value is BinString sFile)
                {
                    if (!string.IsNullOrWhiteSpace(sFile.Value))
                        tracker?.GameFiles.Add(sFile.Value.Trim());

                    field.Value = ConvertStringToBinFile(sFile.Value);
                    return;
                }
                if (toType == "Hash" && field.Value is BinString sHash)
                {
                    if (!string.IsNullOrWhiteSpace(sHash.Value))
                        tracker?.BinEntries.Add(sHash.Value.Trim());

                    field.Value = ConvertStringToBinHash(sHash.Value);
                    return;
                }
            }

            if (fromType == "Hash" && toType == "File")
            {
                if (field.Value is BinHash h)
                {
                    if (!string.IsNullOrWhiteSpace(h.Value.String))
                        tracker?.GameFiles.Add(h.Value.String.Trim());

                    field.Value = new BinFile(new XXH64(h.Value.Hash, h.Value.String));
                }
                return;
            }

            if (fromType == "Embed" && toType == "Pointer")
            {
                if (field.Value is BinEmbed embed)
                {
                    var ptr = new BinPointer(embed.Name);
                    ptr.Items.AddRange(embed.Items);
                    field.Value = ptr;
                }
                return;
            }

            // Handle numeric, vector, matrix, and color primitives conversion
            if (TryParseBinType(fromType, out _) && TryParseBinType(toType, out BinType targetBinType))
            {
                try
                {
                    field.Value = BinTypeConverter.ConvertValue(field.Value, targetBinType);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[BinConverter] Failed to convert field {rule.FieldName} from {fromType} to {toType}: {ex.Message}");
                }
                return;
            }

            if ((fromType is "List" or "List2" or "Container" or "UnorderedContainer") && field.Value is BinList list)
            {
                ApplyListConversion(list, rule.To.ValueType, tracker);
                return;
            }
            if ((fromType is "List" or "List2" or "Container" or "UnorderedContainer") && field.Value is BinList2 list2)
            {
                ApplyList2Conversion(list2, rule.To, tracker);
                return;
            }

            if ((fromType is "Option" or "Optional") && field.Value is BinOption opt)
            {
                ApplyOptionConversion(opt, rule.To.ValueType, tracker);
                return;
            }

            if (fromType == "Map" && field.Value is BinMap map)
            {
                ApplyMapConversion(map, rule.To, tracker);
                return;
            }
        }

        private static bool TryParseBinType(string typeName, out BinType binType)
        {
            binType = typeName.ToLowerInvariant() switch
            {
                "i8" => BinType.I8,
                "u8" => BinType.U8,
                "i16" => BinType.I16,
                "u16" => BinType.U16,
                "i32" or "int" => BinType.I32,
                "u32" or "uint" => BinType.U32,
                "i64" => BinType.I64,
                "u64" => BinType.U64,
                "f32" or "float" => BinType.F32,
                "vec2" => BinType.Vec2,
                "vec3" => BinType.Vec3,
                "vec4" => BinType.Vec4,
                "mtx44" or "mat44" => BinType.Mtx44,
                "rgba" or "color" => BinType.Rgba,
                _ => BinType.None
            };
            return binType != BinType.None;
        }


        private void ApplyListListInternal(IList<BinValue> items, string? targetValueType, ConvertedStringsTracker? tracker)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] is BinString s)
                {
                    if (!string.IsNullOrWhiteSpace(s.Value))
                    {
                        if (targetValueType == "File") tracker?.GameFiles.Add(s.Value.Trim());
                        else if (targetValueType == "Hash") tracker?.BinEntries.Add(s.Value.Trim());
                    }

                    if (targetValueType == "File") items[i] = ConvertStringToBinFile(s.Value);
                    else if (targetValueType == "Hash") items[i] = ConvertStringToBinHash(s.Value);
                }
                else if (items[i] is BinHash h && targetValueType == "File")
                {
                    if (!string.IsNullOrWhiteSpace(h.Value.String)) tracker?.GameFiles.Add(h.Value.String.Trim());
                    items[i] = new BinFile(new XXH64(h.Value.Hash, h.Value.String));
                }
            }
        }

        private void ApplyListConversion(BinList list, string? targetValueType, ConvertedStringsTracker? tracker)
        {
            if (targetValueType == "File") list.ValueType = BinType.File;
            else if (targetValueType == "Hash") list.ValueType = BinType.Hash;
            ApplyListListInternal(list.Items, targetValueType, tracker);
        }

        private void ApplyList2Conversion(BinList2 list2, TypeDescriptor toDesc, ConvertedStringsTracker? tracker)
        {
            string? targetValueType = toDesc.ValueType;
            if (targetValueType == "File") list2.ValueType = BinType.File;
            else if (targetValueType == "Hash") list2.ValueType = BinType.Hash;

            ApplyListListInternal(list2.Items, targetValueType, tracker);

            if (targetValueType == "Embed" && !string.IsNullOrEmpty(toDesc.ClassName))
            {
                uint newClassHash = ParseHashOrName(toDesc.ClassName);
                foreach (var item in list2.Items)
                {
                    if (item is BinEmbed e)
                        e.Name = new FNV1a(newClassHash);
                }
            }
        }

        private void ApplyOptionConversion(BinOption opt, string? targetValueType, ConvertedStringsTracker? tracker)
        {
            if (targetValueType == "File") opt.ValueType = BinType.File;
            else if (targetValueType == "Hash") opt.ValueType = BinType.Hash;
            ApplyListListInternal(opt.Items, targetValueType, tracker);
        }

        private void ApplyMapConversion(BinMap map, TypeDescriptor toDesc, ConvertedStringsTracker? tracker)
        {
            string? targetKeyType = toDesc.KeyType;
            string? targetValueType = toDesc.ValueType;

            if (targetKeyType == "File") map.KeyType = BinType.File;
            else if (targetKeyType == "Hash") map.KeyType = BinType.Hash;

            if (targetValueType == "File") map.ValueType = BinType.File;
            else if (targetValueType == "Hash") map.ValueType = BinType.Hash;

            for (int i = 0; i < map.Items.Count; i++)
            {
                var kvp = map.Items[i];
                BinValue newKey = kvp.Key;
                BinValue newVal = kvp.Value;

                if (targetKeyType == "File" && kvp.Key is BinString ks)
                {
                    if (!string.IsNullOrWhiteSpace(ks.Value)) tracker?.GameFiles.Add(ks.Value.Trim());
                    newKey = ConvertStringToBinFile(ks.Value);
                }
                else if (targetKeyType == "Hash" && kvp.Key is BinString kh)
                {
                    if (!string.IsNullOrWhiteSpace(kh.Value)) tracker?.BinEntries.Add(kh.Value.Trim());
                    newKey = ConvertStringToBinHash(kh.Value);
                }

                if (targetValueType == "File" && kvp.Value is BinString vs)
                {
                    if (!string.IsNullOrWhiteSpace(vs.Value)) tracker?.GameFiles.Add(vs.Value.Trim());
                    newVal = ConvertStringToBinFile(vs.Value);
                }
                else if (targetValueType == "Hash" && kvp.Value is BinString vh)
                {
                    if (!string.IsNullOrWhiteSpace(vh.Value)) tracker?.BinEntries.Add(vh.Value.Trim());
                    newVal = ConvertStringToBinHash(vh.Value);
                }

                map.Items[i] = new KeyValuePair<BinValue, BinValue>(newKey, newVal);
            }
        }

        #region Hashing & Parsing Helpers

        public static ulong HashPath(string path)
        {
            string norm = path.Replace('\\', '/').ToLowerInvariant().Trim();
            byte[] data = Encoding.UTF8.GetBytes(norm);
            return XxHash64.HashToUInt64(data, seed: 0);
        }

        public static uint HashEntryFnv1a(string text)
        {
            return FNV1a.Calculate(text.Trim());
        }

        public static BinFile ConvertStringToBinFile(string path)
        {
            ulong hash = HashPath(path);
            return new BinFile(new XXH64(hash, path));
        }

        public static BinHash ConvertStringToBinHash(string text)
        {
            uint hash = HashEntryFnv1a(text);
            return new BinHash(new FNV1a(hash, text));
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
        private MainWindow _main;

        public WadBatchProcessor(WadExtractor extractor, BinFieldConverter converter, MainWindow? main = null)
        {
            _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
            _converter = converter ?? throw new ArgumentNullException(nameof(converter));
            _main = main;
        }
        private void ReportProgress(double progress)
        {
            if (_main == null) return;

            double clamped = Math.Clamp(progress, 0.0, 1.0);

            if (_main.Dispatcher.CheckAccess())
                _main.SetProgress3(clamped);
            else
                _main.Dispatcher.InvokeAsync(() => _main.SetProgress3(clamped));
        }
        public async Task ProcessFolderAsync(string modpath, CancellationToken ct = default)
        {
            string targetDirectory = Path.Combine(modpath, "WAD");
            if (!Directory.Exists(targetDirectory))
                throw new DirectoryNotFoundException($"Target directory not found: {targetDirectory}");
            ReportProgress(0.0);
            var tracker = new ConvertedStringsTracker();
            string hash_path = Path.Combine(modpath, "META", "hashes", "game.hashes.txt");
            if (File.Exists(hash_path))
            {
                foreach (string line in File.ReadLines(hash_path))
                {
                    tracker.GameFiles.Add(line.Trim());
                }
            }
            ReportProgress(0.02);
            var extractedWadFolders = await ProcessAllWadsInDirectoryAsync(targetDirectory, ct);

            bool hasNewPaths = true;
            int pass = 0;

            while (hasNewPaths && pass < 5)
            {
                pass++;
                hasNewPaths = false;

                ConvertAllBinsInDirectory(targetDirectory, tracker);

                var allDiscovered = tracker.GameFiles
                    .Concat(tracker.BinEntries)
                    .Concat(tracker.UnconvertedTempStrings)
                    .Distinct()
                    .ToList();

                if (allDiscovered.Count > 0)
                {
                    await HashMaster.AddTemporaryHashesAsync(allDiscovered, ct);
                }

                foreach (var wadFolder in extractedWadFolders)
                {
                    if (Directory.Exists(wadFolder))
                    {
                        int resolved = await ResolveDirectHashedFilesInFolderAsync(wadFolder, ct);
                        if (resolved > 0) hasNewPaths = true;
                    }
                }
            }

            ReportProgress(1);
            SaveTrackedStrings(targetDirectory, tracker);
        }

        public async Task RunRecoveryPipelineAsync(string targetDirectory, CancellationToken ct = default)
        {
            if (!Directory.Exists(targetDirectory))
                throw new DirectoryNotFoundException($"Target directory not found: {targetDirectory}");

            string metaDir = Path.GetFullPath(Path.Combine(targetDirectory, "..", "meta"));
            string oldMetaFile = Path.Combine(metaDir, "files.txt");

            if (File.Exists(oldMetaFile))
            {
                var lines = File.ReadAllLines(oldMetaFile);
                var stringList = new List<string>();

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    int firstSpace = line.IndexOfAny(new[] { ' ', '\t' });
                    string text = firstSpace > 0 ? line[(firstSpace + 1)..].Trim() : line.Trim();
                    if (!string.IsNullOrEmpty(text)) stringList.Add(text);
                }

                if (stringList.Count > 0)
                {
                    await HashMaster.AddTemporaryHashesAsync(stringList, ct);
                }
            }
            var tracker = new ConvertedStringsTracker();
            var allFiles = Directory.GetFiles(targetDirectory, "*", SearchOption.AllDirectories);

            foreach (var filePath in allFiles)
            {
                ct.ThrowIfCancellationRequested();

                if (IsInAssetsFolder(filePath)) continue;
                if (!IsBinFile(filePath)) continue;

                try
                {
                    byte[] rawBytes = File.ReadAllBytes(filePath);
                    var reader = new BinReader(rawBytes);
                    Bin bin = reader.Read();

                    await RecoverBinInternalAsync(bin, tracker, ct);

                    var writer = new BinWriter();
                    File.WriteAllBytes(filePath, writer.Write(bin));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Recovery] Error processing {filePath}: {ex.Message}");
                }
            }

            var allDiscovered = tracker.GameFiles
                .Concat(tracker.BinEntries)
                .Concat(tracker.UnconvertedTempStrings)
                .Distinct()
                .ToList();

            if (allDiscovered.Count > 0)
            {
                await HashMaster.AddTemporaryHashesAsync(allDiscovered, ct);
            }

            var wadFolders = Directory.GetDirectories(targetDirectory, "*", SearchOption.AllDirectories)
                                      .Where(d => d.EndsWith(".wad", StringComparison.OrdinalIgnoreCase) ||
                                                  d.EndsWith(".wad.client", StringComparison.OrdinalIgnoreCase) ||
                                                  File.Exists(Path.Combine(d, "..", Path.GetFileName(d) + ".client")));

            foreach (var folder in wadFolders)
            {
                await ResolveDirectHashedFilesInFolderAsync(folder, ct);
            }

            SaveTrackedStrings(targetDirectory, tracker);
        }

        private async Task RecoverBinInternalAsync(Bin bin, ConvertedStringsTracker tracker, CancellationToken ct)
        {
            if (bin.Sections.TryGetValue("entries", out var entriesVal) && entriesVal is BinMap entriesMap)
            {
                foreach (var kvp in entriesMap.Items)
                {
                    if (kvp.Value is BinEmbed entryEmbed)
                        await RecoverEmbedAsync(entryEmbed, tracker, ct);
                }
            }

            if (bin.Sections.TryGetValue("patches", out var patchesVal) && patchesVal is BinMap patchesMap)
            {
                foreach (var kvp in patchesMap.Items)
                {
                    if (kvp.Value is BinEmbed patchEmbed)
                        await RecoverEmbedAsync(patchEmbed, tracker, ct);
                }
            }
        }

        private async Task RecoverEmbedAsync(BinEmbed embed, ConvertedStringsTracker tracker, CancellationToken ct)
        {
            uint classHash = embed.Name.Hash;

            foreach (var field in embed.Items)
            {
                if (_converter.Transformers.TryGetValue((classHash, field.Key.Hash), out var mapping))
                {
                    await RecoverFieldAsync(field, mapping.Rule, tracker, ct);
                }
                else
                {
                    BinFieldConverter.CollectRawStrings(field.Value, tracker.UnconvertedTempStrings);
                }

                await RecoverNestedValueAsync(field.Value, tracker, ct);
            }
        }

        private async Task RecoverPointerAsync(BinPointer ptr, ConvertedStringsTracker tracker, CancellationToken ct)
        {
            uint classHash = ptr.Name.Hash;

            foreach (var field in ptr.Items)
            {
                if (_converter.Transformers.TryGetValue((classHash, field.Key.Hash), out var mapping))
                {
                    await RecoverFieldAsync(field, mapping.Rule, tracker, ct);
                }
                else
                {
                    BinFieldConverter.CollectRawStrings(field.Value, tracker.UnconvertedTempStrings);
                }

                await RecoverNestedValueAsync(field.Value, tracker, ct);
            }
        }

        private async Task RecoverNestedValueAsync(BinValue val, ConvertedStringsTracker tracker, CancellationToken ct)
        {
            switch (val)
            {
                case BinEmbed e:
                    await RecoverEmbedAsync(e, tracker, ct);
                    break;
                case BinPointer p:
                    await RecoverPointerAsync(p, tracker, ct);
                    break;
                case BinList l:
                    foreach (var item in l.Items) await RecoverNestedValueAsync(item, tracker, ct);
                    break;
                case BinList2 l2:
                    foreach (var item in l2.Items) await RecoverNestedValueAsync(item, tracker, ct);
                    break;
                case BinOption opt:
                    foreach (var item in opt.Items) await RecoverNestedValueAsync(item, tracker, ct);
                    break;
                case BinMap map:
                    foreach (var kvp in map.Items)
                    {
                        await RecoverNestedValueAsync(kvp.Key, tracker, ct);
                        await RecoverNestedValueAsync(kvp.Value, tracker, ct);
                    }
                    break;
            }
        }

        private static async Task RecoverFieldAsync(BinField field, ConversionRule rule, ConvertedStringsTracker tracker, CancellationToken ct)
        {
            string toType = rule.To.Type;
            string? valType = rule.To.ValueType;
            string? keyType = rule.To.KeyType;

            if (toType == "File" && field.Value is BinFile fileVal)
            {
                string? str = await ResolveFileStringAsync(fileVal, ct);
                if (!string.IsNullOrEmpty(str))
                    tracker.GameFiles.Add(str);
                return;
            }

            if (toType == "Hash")
            {
                if (field.Value is BinFile errFile)
                {
                    string? str = await ResolveFileStringAsync(errFile, ct);
                    if (!string.IsNullOrEmpty(str))
                    {
                        field.Value = BinFieldConverter.ConvertStringToBinHash(str);
                        tracker.BinEntries.Add(str);
                    }
                }
                else if (field.Value is BinHash bh && !string.IsNullOrEmpty(bh.Value.String))
                {
                    tracker.BinEntries.Add(bh.Value.String);
                }
                return;
            }

            if (valType == "File")
            {
                if (field.Value is BinList l) foreach (var item in l.Items.OfType<BinFile>()) await ProcessFileItemAsync(item, tracker, ct);
                if (field.Value is BinList2 l2) foreach (var item in l2.Items.OfType<BinFile>()) await ProcessFileItemAsync(item, tracker, ct);
                if (field.Value is BinOption opt) foreach (var item in opt.Items.OfType<BinFile>()) await ProcessFileItemAsync(item, tracker, ct);
                if (field.Value is BinMap m) foreach (var kvp in m.Items.Where(x => x.Value is BinFile).Select(x => (BinFile)x.Value)) await ProcessFileItemAsync(kvp, tracker, ct);
            }
            else if (valType == "Hash")
            {
                if (field.Value is BinList l) await ProcessListHashRecoveryAsync(l.Items, tracker, ct);
                if (field.Value is BinList2 l2) await ProcessListHashRecoveryAsync(l2.Items, tracker, ct);
                if (field.Value is BinOption opt) await ProcessListHashRecoveryAsync(opt.Items, tracker, ct);
            }

            if (keyType == "File" && field.Value is BinMap mapKF)
            {
                foreach (var kvp in mapKF.Items.Where(x => x.Key is BinFile).Select(x => (BinFile)x.Key))
                    await ProcessFileItemAsync(kvp, tracker, ct);
            }
            else if (keyType == "Hash" && field.Value is BinMap mapKH)
            {
                for (int i = 0; i < mapKH.Items.Count; i++)
                {
                    var kvp = mapKH.Items[i];
                    if (kvp.Key is BinFile errFile)
                    {
                        string? str = await ResolveFileStringAsync(errFile, ct);
                        if (!string.IsNullOrEmpty(str))
                        {
                            mapKH.Items[i] = new KeyValuePair<BinValue, BinValue>(BinFieldConverter.ConvertStringToBinHash(str), kvp.Value);
                            tracker.BinEntries.Add(str);
                        }
                    }
                    else if (kvp.Key is BinHash bh && !string.IsNullOrEmpty(bh.Value.String))
                    {
                        tracker.BinEntries.Add(bh.Value.String);
                    }
                }
            }
        }

        private static async Task ProcessFileItemAsync(BinFile file, ConvertedStringsTracker tracker, CancellationToken ct)
        {
            string? str = await ResolveFileStringAsync(file, ct);
            if (!string.IsNullOrEmpty(str)) tracker.GameFiles.Add(str);
        }

        private static async Task ProcessListHashRecoveryAsync(IList<BinValue> items, ConvertedStringsTracker tracker, CancellationToken ct)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] is BinFile errFile)
                {
                    string? str = await ResolveFileStringAsync(errFile, ct);
                    if (!string.IsNullOrEmpty(str))
                    {
                        items[i] = BinFieldConverter.ConvertStringToBinHash(str);
                        tracker.BinEntries.Add(str);
                    }
                }
                else if (items[i] is BinHash bh && !string.IsNullOrEmpty(bh.Value.String))
                {
                    tracker.BinEntries.Add(bh.Value.String);
                }
            }
        }

        private static async Task<string?> ResolveFileStringAsync(BinFile fileVal, CancellationToken ct)
        {
            if (!string.IsNullOrEmpty(fileVal.Value.String))
                return fileVal.Value.String;

            var unhashed = await HashMaster.UnhashBatchAsync(new[] { fileVal.Value.Hash }, ct);
            if (unhashed.TryGetValue(fileVal.Value.Hash, out var resolved) && !string.IsNullOrEmpty(resolved))
                return resolved;

            return null;
        }

        private static async Task<int> ResolveDirectHashedFilesInFolderAsync(string wadFolder, CancellationToken ct)
        {
            var allFiles = Directory.GetFiles(wadFolder, "*", SearchOption.AllDirectories);
            var hashedFiles = new List<(string FilePath, ulong Hash)>();

            foreach (var file in allFiles)
            {
                string nameNoExt = Path.GetFileNameWithoutExtension(file);

                if (nameNoExt.Length == 16 && ulong.TryParse(nameNoExt, NumberStyles.HexNumber, null, out ulong hash))
                {
                    hashedFiles.Add((file, hash));
                }
            }

            if (hashedFiles.Count == 0) return 0;

            var unhashedMap = await HashMaster.UnhashBatchAsync(hashedFiles.Select(x => x.Hash).Distinct(), ct);
            int resolvedCount = 0;

            foreach (var (filePath, hash) in hashedFiles)
            {
                if (unhashedMap.TryGetValue(hash, out string? resolvedPath) && !string.IsNullOrEmpty(resolvedPath))
                {
                    string targetRel = resolvedPath.Replace('/', Path.DirectorySeparatorChar)
                                                   .Replace('\\', Path.DirectorySeparatorChar)
                                                   .TrimStart(Path.DirectorySeparatorChar);

                    string targetFull = Path.Combine(wadFolder, targetRel);
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
                    resolvedCount++;
                }
            }

            return resolvedCount;
        }

        private void ConvertAllBinsInDirectory(string rootDirectory, ConvertedStringsTracker tracker)
        {
            var allFiles = Directory.GetFiles(rootDirectory, "*", SearchOption.AllDirectories);

            foreach (var filePath in allFiles)
            {
                if (IsInAssetsFolder(filePath)) continue;
                if (!IsBinFile(filePath)) continue;

                try
                {
                    byte[] rawBytes = File.ReadAllBytes(filePath);
                    var reader = new BinReader(rawBytes);
                    Bin bin = reader.Read();

                    _converter.ConvertBinWithTracking(bin, tracker);

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

        private static bool IsInAssetsFolder(string filePath)
        {
            string norm = filePath.Replace('/', '\\').ToLowerInvariant();
            return norm.Contains("\\assets\\") || norm.StartsWith("assets\\");
        }

        private static void SaveTrackedStrings(string rootDirectory, ConvertedStringsTracker tracker)
        {
            string hashesDir = Path.GetFullPath(Path.Combine(rootDirectory, "..", "META", "hashes"));
            if (!Directory.Exists(hashesDir))
                Directory.CreateDirectory(hashesDir);

            string gameHashesPath = Path.Combine(hashesDir, "game.hashes.txt");
            string binEntriesPath = Path.Combine(hashesDir, "binentries.hashes.txt");

            var filteredPaths = HashMaster.FilterNonGamePathsAsync(tracker.GameFiles).GetAwaiter().GetResult();
            while (tracker.GameFiles.TryTake(out _)) { }
            foreach (var path in filteredPaths)
            {
                tracker.GameFiles.Add(path);
            }

            if (tracker.GameFiles.Count > 0) WriteUniqueStringsToFile(gameHashesPath, tracker.GameFiles);
            if (tracker.BinEntries.Count > 0) WriteUniqueStringsToFile(binEntriesPath, tracker.BinEntries);
        }

        private static void WriteUniqueStringsToFile(string filePath, IEnumerable<string> strings)
        {
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(filePath))
            {
                foreach (var line in File.ReadAllLines(filePath))
                {
                    var trimmed = line.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        existing.Add(trimmed);
                }
            }

            foreach (var s in strings)
            {
                var trimmed = s.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    existing.Add(trimmed);
            }

            File.WriteAllLines(filePath, existing.OrderBy(x => x, StringComparer.OrdinalIgnoreCase), Encoding.UTF8);
        }

        private async Task<List<string>> ProcessAllWadsInDirectoryAsync(string directory, CancellationToken ct)
        {
            var wadFiles = Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".wad", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".wad.client", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var extractedFolders = new List<string>();
            int section = 0;
            int sections = wadFiles.Count();
            foreach (var wadPath in wadFiles)
            {
                double progress = 0.02 + (0.93 / sections * section);
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

                    await ExtractWadFileAsync(tempWadPath, outputExtractFolder, ct, progress, 0.93 / sections);
                    extractedFolders.Add(outputExtractFolder);
                }
                finally
                {
                    if (File.Exists(tempWadPath))
                    {
                        File.Delete(tempWadPath);
                    }
                }
                section += 1;
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

        private static byte[] DecompressDeflateOrZlib(byte[] data, int length)
        {
            if (IsGzip(data, length))
            {
                try
                {
                    using var ms = new MemoryStream(data, 0, length);
                    using var gs = new GZipStream(ms, CompressionMode.Decompress);
                    using var outMs = new MemoryStream();
                    gs.CopyTo(outMs);
                    return outMs.ToArray();
                }
                catch { }
            }

            try
            {
                using var ms = new MemoryStream(data, 0, length);
                using var zs = new ZLibStream(ms, CompressionMode.Decompress);
                using var outMs = new MemoryStream();
                zs.CopyTo(outMs);
                return outMs.ToArray();
            }
            catch { }

            try
            {
                using var ms = new MemoryStream(data, 0, length);
                using var ds = new DeflateStream(ms, CompressionMode.Decompress);
                using var outMs = new MemoryStream();
                ds.CopyTo(outMs);
                return outMs.ToArray();
            }
            catch { }

            if (length > 2 && data[0] == 0x78)
            {
                try
                {
                    using var ms = new MemoryStream(data, 2, length - 2);
                    using var ds = new DeflateStream(ms, CompressionMode.Decompress);
                    using var outMs = new MemoryStream();
                    ds.CopyTo(outMs);
                    return outMs.ToArray();
                }
                catch { }
            }

            byte[] fallback = new byte[length];
            Array.Copy(data, fallback, length);
            return fallback;
        }

        private static byte[] DecompressZstd(byte[] data, int length, uint uncompressedSize)
        {
            if (uncompressedSize > 0)
            {
                try
                {
                    using var decompressor = new Decompressor();
                    byte[] outBuffer = new byte[uncompressedSize];
                    int written = decompressor.Unwrap(data.AsSpan(0, length), outBuffer.AsSpan());
                    if (written == (int)uncompressedSize)
                        return outBuffer;
                }
                catch { }
            }

            using var inMs = new MemoryStream(data, 0, length);
            using var zs = new ZstdSharp.DecompressionStream(inMs);
            using var outMs = uncompressedSize > 0 ? new MemoryStream((int)uncompressedSize) : new MemoryStream();
            zs.CopyTo(outMs);
            return outMs.ToArray();
        }

        private static byte[] DecompressEntry(byte[] compBuffer, int bytesRead, RawWadEntry entry)
        {
            if (bytesRead == 0 || entry.CompressedSize == 0)
                return Array.Empty<byte>();

            byte compType = (byte)(entry.Type & 0x0F);

            if (compType == 3 || compType == 4 || IsZstd(compBuffer, bytesRead))
            {
                try
                {
                    return DecompressZstd(compBuffer, bytesRead, entry.UncompressedSize);
                }
                catch
                {
                    byte[] fallback = new byte[bytesRead];
                    Array.Copy(compBuffer, fallback, bytesRead);
                    return fallback;
                }
            }
            else if (compType == 1 || IsGzip(compBuffer, bytesRead) || (bytesRead >= 2 && compBuffer[0] == 0x78))
            {
                try
                {
                    return DecompressDeflateOrZlib(compBuffer, bytesRead);
                }
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

        private async Task ExtractWadFileAsync(string wadFilePath, string outputDir, CancellationToken ct, double current, double max)
        {
            var entries = new List<RawWadEntry>();

            using (var fs = new FileStream(wadFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var br = new BinaryReader(fs))
            {
                if (fs.Length < 4) return;

                byte[] magic = br.ReadBytes(2);
                if (magic.Length < 2 || magic[0] != 'R' || magic[1] != 'W') return;

                byte major = br.ReadByte();
                byte minor = br.ReadByte();

                uint fileCount = 0;
                int entrySize = 32;

                if (major == 3)
                {
                    if (fs.Length < 272) return;
                    fs.Seek(268, SeekOrigin.Begin);
                    fileCount = br.ReadUInt32();
                    entrySize = 32;
                }
                else if (major == 2)
                {
                    if (fs.Length < 103) return;
                    fs.Seek(95, SeekOrigin.Begin);
                    ushort tocStartOffset = br.ReadUInt16();
                    entrySize = br.ReadUInt16();
                    fileCount = br.ReadUInt32();
                    fs.Seek(tocStartOffset, SeekOrigin.Begin);
                }
                else if (major == 1)
                {
                    if (fs.Length < 12) return;
                    fs.Seek(4, SeekOrigin.Begin);
                    ushort tocStartOffset = br.ReadUInt16();
                    entrySize = br.ReadUInt16();
                    fileCount = br.ReadUInt32();
                    fs.Seek(tocStartOffset, SeekOrigin.Begin);
                }
                else
                {
                    return;
                }

                byte[] entryBuffer = new byte[entrySize];

                for (int i = 0; i < fileCount; i++)
                {
                    if (fs.Read(entryBuffer, 0, entrySize) != entrySize) break;

                    ulong pathHash = BitConverter.ToUInt64(entryBuffer, 0);
                    uint offset = BitConverter.ToUInt32(entryBuffer, 8);
                    uint compSize = BitConverter.ToUInt32(entryBuffer, 12);
                    uint uncompSize = BitConverter.ToUInt32(entryBuffer, 16);
                    byte compType = (byte)(entryBuffer[20] & 0x0F);

                    entries.Add(new RawWadEntry
                    {
                        PathHash = pathHash,
                        Offset = offset,
                        CompressedSize = compSize,
                        UncompressedSize = uncompSize,
                        Type = compType
                    });
                }
            }

            if (entries.Count == 0) return;

            var uniqueHashes = entries.Select(e => e.PathHash).Distinct();
            var unhashedMap = await HashMaster.UnhashBatchAsync(uniqueHashes, ct);

            using (var fs = new FileStream(wadFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int entryIndex = 1;
                int entryIndexMax = entries.Count();
                foreach (var entry in entries)
                {
                    ct.ThrowIfCancellationRequested();

                    try
                    {
                        if (entry.CompressedSize == 0)
                        {
                            string emptyPath = ResolveDestinationPath(outputDir, entry, unhashedMap, Array.Empty<byte>());
                            EnsureDirectoryExists(emptyPath);
                            File.WriteAllBytes(emptyPath, Array.Empty<byte>());
                            continue;
                        }

                        if ((ulong)entry.Offset + entry.CompressedSize > (ulong)fs.Length)
                        {
                            Debug.WriteLine($"[WadExtract] Chunk {entry.PathHash:x16} bounds exceed file length. Skipped.");
                            continue;
                        }

                        fs.Seek(entry.Offset, SeekOrigin.Begin);
                        byte[] compBuffer = ArrayPool<byte>.Shared.Rent((int)entry.CompressedSize);

                        try
                        {
                            int bytesRead = fs.Read(compBuffer, 0, (int)entry.CompressedSize);
                            byte[] decompressed = DecompressEntry(compBuffer, bytesRead, entry);

                            string fullOutPath = ResolveDestinationPath(outputDir, entry, unhashedMap, decompressed);
                            EnsureDirectoryExists(fullOutPath);

                            File.WriteAllBytes(fullOutPath, decompressed);
                        }
                        finally
                        {
                            ReportProgress(current + (max / entryIndexMax * entryIndex));
                            ArrayPool<byte>.Shared.Return(compBuffer);
                            entryIndex += 1;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WadExtract] Skipped entry {entry.PathHash:x16}: {ex.Message}");
                    }
                }
            }
        }

        private static string ResolveDestinationPath(string outputDir, RawWadEntry entry, IReadOnlyDictionary<ulong, string> unhashedMap, byte[] decompressed)
        {
            string relativePath;
            if (unhashedMap.TryGetValue(entry.PathHash, out string? resolved) && !string.IsNullOrWhiteSpace(resolved))
            {
                relativePath = resolved;
            }
            else
            {
                string ext = GuessExtension(decompressed);
                relativePath = $"{entry.PathHash:x16}{ext}";
            }

            relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar)
                                       .Replace('\\', Path.DirectorySeparatorChar)
                                       .TrimStart(Path.DirectorySeparatorChar);

            char[] invalidChars = Path.GetInvalidPathChars();
            if (relativePath.IndexOfAny(invalidChars) >= 0)
            {
                var sb = new StringBuilder(relativePath.Length);
                foreach (char c in relativePath)
                {
                    sb.Append(invalidChars.Contains(c) || c == ':' || c == '*' || c == '?' || c == '"' || c == '<' || c == '>' || c == '|' ? '_' : c);
                }
                relativePath = sb.ToString();
            }

            return Path.Combine(outputDir, relativePath);
        }

        private static void EnsureDirectoryExists(string filePath)
        {
            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
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
            if (data == null || data.Length == 0)
                return ".bin";

            if (data.Length >= 10 && Encoding.ASCII.GetString(data, 0, 10) == "#PROP_text")
            {
                return ".py";
            }

            int firstNonWhitespace = -1;
            for (int i = 0; i < Math.Min(data.Length, 64); i++)
            {
                char c = (char)data[i];
                if (!char.IsWhiteSpace(c))
                {
                    firstNonWhitespace = i;
                    break;
                }
            }

            if (firstNonWhitespace != -1)
            {
                char startChar = (char)data[firstNonWhitespace];
                if (startChar == '{' || startChar == '[')
                {
                    return ".json";
                }
            }

            if (data.Length >= 4)
            {
                string magic = Encoding.ASCII.GetString(data, 0, 4);
                if (magic is "PROP" or "PTCH") return ".bin";
                if (magic == "BKHD") return ".bnk";
                if (magic == "DDS ") return ".dds";
                if (magic == "OggS") return ".ogg";
                if (magic == "%PDF") return ".pdf";
                if (magic.StartsWith("r3d")) return ".scb";
            }

            if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
                return ".jpg";

            if (data.Length >= 3 && data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46)
                return ".gif";

            if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
                return ".png";

            return ".bin";
        }
    }
}