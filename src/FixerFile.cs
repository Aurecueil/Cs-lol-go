using ZstdSharp;
using Jade.Ritobin;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.IO;
using System.IO.Compression;
using System.IO.Hashing;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Xml.Linq;
using static repatheruwu.Repatheruwu;
using static repatheruwu.Repatheruwu.WadExtractor;
using static System.Collections.Specialized.BitVector32;
using static System.Net.Mime.MediaTypeNames;
using SearchOption = System.IO.SearchOption;

namespace repatheruwu
{
    public static class FixerSettings
    {
        // Example settings
        public static string Character { get; set; } = "";
        public static int skinNo { get; set; } = 0;

        public static int HealthbarStyle { get; set; } = 12;
        public static bool verifyHpBar { get; set; } = true;
        public static string inputDir { get; set; } = "TEMP";
        public static string outputDir { get; set; } = ".";
        public static string WADpath { get; set; } = "C:\\Riot Games\\League of Legends\\Game\\DATA\\FINAL";
        public static List<string> AllWadPaths = new List<string>();
        public static string repath_path_path { get; set; } = ".";
        public static bool in_file_path { get; set; } = true;
        public static bool cls_assets { get; set; } = true;
        public static bool KillStaticMat { get; set; } = true;
        public static string gamehashes_path { get; set; } = "cslol-tools\\hashes.game.txt";
        public static List<string> base_wad_path { get; set; }
        public static List<string> OldLookUp { get; set; }
        public static bool keep_Icons { get; set; } = true;
    }

    class Repatheruwu
    {
        public enum Defi : uint
        {
            ContextualActionData = 3476110372,
            VfxSystemDefinitionData = 1171098015,
            ResourceResolver = 4013559603,
            SkinCharacterDataProperties = 2607278582,
            AbilityObject = 3696800942,
            SpellObject = 1585338886,
            RecSpellRankUpInfolist = 1496570494,
            ItemRecommendationContextList = 2188140632,
            JunglePathRecommendation = 226436980,
            ItemRecommendationOverrideSet = 2753712911,
            CharacterRecord = 602544405,
            StatStoneSet = 2524344308,
            StatStoneData = 3978526660,
            StaticMaterialDef = 4288492553,
            GearSkinUpgrade = 668820321,
            AnimationGraphData = 4126869447
        }
        public static void ProcessAviableSkin(List<int> entries, string charra)
        {
            if (entries == null || entries.Count == 0)
            {
                Characters.Enqueue((charra, 0, true));
                return;
            }

            if (entries.Count == 1)
            {
                Characters.Enqueue((charra, entries[0], true));
                return;
            }

            bool isSequential = true;
            for (int i = 0; i < entries.Count - 1; i++)
            {
                if (entries[i + 1] != entries[i] + 1)
                {
                    isSequential = false;
                    break;
                }
            }

            if (isSequential)
            {
                Characters.Enqueue((charra, entries[0], true));
            }
            else
            {
                foreach (var entry in entries)
                {
                    Characters.Enqueue((charra, entry, true));
                }
            }
        }
        static Queue<(string, int, bool)> Characters = new Queue<(string, int, bool)>();
        public void FixSkini(string charra, List<string> input_wads, string outPUut, string AllWadPath)
        {
            string tmp = Path.Combine(Path.GetTempPath(), "cslolgo_fixer_" + Guid.NewGuid().ToString());

            Directory.CreateDirectory(tmp);
            FixerSettings.inputDir = tmp;

            FixerSettings.outputDir = Path.Combine(outPUut, $"{charra}.wad");
            FixerSettings.base_wad_path = input_wads;
            FixerSettings.WADpath = AllWadPath; 
            FixerSettings.OldLookUp = [];

            for (int i = 0; i < FixerSettings.base_wad_path.Count; i++)
            {
                string currentPath = FixerSettings.base_wad_path[i];

                if (Directory.Exists(currentPath))
                {
                    string randomchar = Path.GetRandomFileName().Replace(".", "").Substring(0, 8);

                    string outputWadPath = Path.Combine(FixerSettings.inputDir, $"{randomchar}.wad.client");

                    PackDirectoryToWad(currentPath, outputWadPath);

                    FixerSettings.base_wad_path[i] = outputWadPath;
                }

            }
            List<int> Skins = GetAvailableSkinNumbers(FixerSettings.base_wad_path, charra);
            ProcessAviableSkin(Skins, charra);

            var priorityWads = new[]
            {
                $"{FixerSettings.Character}.wad.client",
            };
            FixerSettings.AllWadPaths = CollectWads(FixerSettings.WADpath, priorityWads);

            while (Characters.Count > 0)
            {
                var (Current_Char, skinNo, HpBar) = Characters.Dequeue();

                FixerSettings.Character = Current_Char;
                FixerSettings.skinNo = skinNo;
                FixerSettings.verifyHpBar = HpBar;
                string binPath = $"data/characters/{FixerSettings.Character}/skins/skin{FixerSettings.skinNo}.bin";
                var check = CheckLinked([binPath]);
                if (check != null) continue;


                var (binentries, concat, staticMat, allStrings, linkedList) = LoadAllBins(binPath);

                allStrings = process(allStrings);


                string conat_path = $"data/{FixerSettings.Character}_skin{FixerSettings.skinNo}_concat.bin";
                var EmptyLinked = new BinList(BinType.String);
                Save_Bin(EmptyLinked, concat, $"{FixerSettings.outputDir}/{conat_path}");
                linkedList.Items.Add(new BinString(conat_path));

                string static_mat_path = $"data/{FixerSettings.Character}_skin{FixerSettings.skinNo}_StaticMat.bin";
                Save_Bin(EmptyLinked, staticMat, $"{FixerSettings.outputDir}/{static_mat_path}");
                if (FixerSettings.KillStaticMat)
                {
                    string static_mat_path_proxy = $"data/{FixerSettings.Character}_skin{FixerSettings.skinNo}_StaticMat_proxy.bin";
                    EmptyLinked.Items.Add(new BinString(static_mat_path));
                    var EmptyEntries = new BinMap(BinType.Hash, BinType.Embed);
                    Save_Bin(EmptyLinked, EmptyEntries, $"{FixerSettings.outputDir}/{static_mat_path_proxy}");
                    linkedList.Items.Add(new BinString(static_mat_path_proxy));
                }
                else
                {
                    linkedList.Items.Add(new BinString(static_mat_path));
                }

                Save_Bin(linkedList, binentries, $"{FixerSettings.outputDir}/{binPath}");

            }
            if (Directory.Exists(FixerSettings.inputDir)) Directory.Delete(FixerSettings.inputDir, true);

        }

        public static void Save_Bin(BinList Linked, BinMap entries, string output)
        {
            var newBin = new Bin();
            newBin.Sections["type"] = new BinString("PROP");
            newBin.Sections["version"] = new BinU32(3);

            newBin.Sections["linked"] = Linked;

            newBin.Sections["entries"] = entries;

            var writer = new BinWriter();
            byte[] bytes = writer.Write(newBin);

            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllBytes(output, bytes);
        }

        public static List<Target> process(List<Target> processing)
        {
            var allPaths = new HashSet<string>(
    processing.Select(t => t.OriginalPath),
    StringComparer.OrdinalIgnoreCase
);

            var bnkToWpkMap = new Dictionary<Target, string>();

            foreach (var target in processing.Where(t => t.OriginalPath.IndexOf("_vo_events.bnk", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                string partnerWpk = Regex.Replace(
                    target.OriginalPath,
                    "_vo_events.bnk",
                    "_vo_audio.wpk",
                    RegexOptions.IgnoreCase
                );

                if (allPaths.Contains(partnerWpk))
                {
                    bnkToWpkMap.Add(target, partnerWpk);
                }
            }

            processing.RemoveAll(target =>
                target.OriginalPath.IndexOf("_events.bnk", StringComparison.OrdinalIgnoreCase) >= 0);

            processing = WadExtractor.ExtractAndSwapReferences(FixerSettings.base_wad_path, processing);
            if (processing.Count == 0) return processing;

            var remainingPaths = new HashSet<string>(
                processing.Select(t => t.OriginalPath),
                StringComparer.OrdinalIgnoreCase
            );
            processing.RemoveAll(target =>
                string.Equals(Path.GetExtension(target.OriginalPath), ".wpk", StringComparison.OrdinalIgnoreCase));
            processing.RemoveAll(target =>
                string.Equals(Path.GetExtension(target.OriginalPath), ".bnk", StringComparison.OrdinalIgnoreCase));
            foreach (var pair in bnkToWpkMap)
            {
                Target bnkTarget = pair.Key;
                string wpkPath = pair.Value;

                if (!remainingPaths.Contains(wpkPath))
                {
                    processing.Add(bnkTarget);
                }
            }

            processing.RemoveAll(target =>
                string.Equals(Path.GetExtension(target.OriginalPath), ".anm", StringComparison.OrdinalIgnoreCase));


            processing = WadExtractor.ExtractAndSwapReferences(FixerSettings.OldLookUp, processing);
            if (processing.Count() == 0) return processing;
            processing = WadExtractor.ExtractAndSwapReferences(FixerSettings.AllWadPaths, processing);
            if (processing.Count() == 0) return processing;
            processing = Hashes.FindMatches(processing);
            processing.RemoveAll(t =>
            {
                if (t.Hashes.Count == 0)
                {
                    return true;
                }
                return false;
            });
            processing = WadExtractor.ExtractAndSwapReferences(FixerSettings.AllWadPaths, processing);
            if (processing.Count() == 0) return processing;
            foreach (var item in processing)
            {
                Console.WriteLine($"MISS: {item.OriginalPath}");
            }
            return processing;
        }

        public static uint FNV1aHash(string input)
        {
            const uint FNV_OFFSET_BASIS = 0x811C9DC5; // 2166136261
            const uint FNV_PRIME = 0x01000193;        // 16777619

            uint hash = FNV_OFFSET_BASIS;
            byte[] data = Encoding.UTF8.GetBytes(input.ToLowerInvariant()); // lowercase

            foreach (byte b in data)
            {
                hash ^= b;
                hash *= FNV_PRIME;
            }

            return hash;
        }

        public static ulong HashPath(string path, bool not_x16 = false)
        {
            string norm = path.Replace('\\', '/').ToLowerInvariant(); ;

            byte[] data = Encoding.UTF8.GetBytes(norm);

            ulong h = XxHash64.HashToUInt64(data, seed: 0);

            return h;
        }
        static Bin LoadBin(string path)
        {
            if (File.Exists($"{FixerSettings.inputDir}/{path}"))
            {
                var data = File.ReadAllBytes($"{FixerSettings.inputDir}/{path}");
                return new BinReader(data).Read();
            }
            else
            {
                string hashed = $"{HashPath(path).ToString("x16")}.bin";
                if (!File.Exists($"{FixerSettings.inputDir}/{hashed}")) return null;
                var data = File.ReadAllBytes($"{FixerSettings.inputDir}/{hashed}");
                return new BinReader(data).Read();
            }
        }
        static List<string> CheckLinked(List<string> bins_to_check)
        {
            var bins_hashed = new List<WadExtractor.Target>();
            foreach (string path in bins_to_check)
            {
                if (File.Exists($"{FixerSettings.inputDir}/{path}")) continue;
                string hashed = $"{HashPath(path).ToString("x16")}.bin";
                if (File.Exists($"{FixerSettings.inputDir}/{hashed}")) continue;

                Target found = bins_hashed.FirstOrDefault(t => t.OriginalPath == hashed);
                if (found == null)
                {
                    bins_hashed.Add(new WadExtractor.Target
                    {
                        Hashes = new List<string> { path },
                        OutputPath = FixerSettings.inputDir,
                        OutputString = hashed,
                        BinStringRef = null,
                        OriginalPath = path,
                    });
                }
            }
            if (bins_hashed.Count() < 1) return null;

            var left = WadExtractor.ExtractAndSwapReferences(FixerSettings.base_wad_path, bins_hashed);

            if (left.Count() < 1) return null;
            left = WadExtractor.ExtractAndSwapReferences(FixerSettings.OldLookUp, left);

            if (left.Count() < 1) return null;
            left = WadExtractor.ExtractAndSwapReferences(FixerSettings.AllWadPaths, left);

            if (left.Count() < 1) return null;
            left = Hashes.FindMatches(left, false);
            // Console.WriteLine($"----------------------------------");
            left.RemoveAll(t =>
            {
                if (t.Hashes.Count == 0)
                {
                    Console.WriteLine($"Missing CAC linked bin: {t.OriginalPath}");
                    return true; // Remove this item
                }
                // Console.WriteLine($"Possible linked bin: {t.OriginalPath}");
                // foreach (var hash in t.Hashes)
                // {
                //     Console.WriteLine($"Possible linked bin: {hash}");
                // }
                // Console.WriteLine($"----------------------------------");
                return false; // Keep this item
            });
            left = WadExtractor.ExtractAndSwapReferences(FixerSettings.AllWadPaths, left);
            if (left.Count() < 1) return null;
            List<string> returning = new List<string>();
            foreach (Target tar in left)
            {
                returning.Add(tar.OriginalPath);
            }
            return returning;
        }

        static bool ShouldSkipFile(string path)
        {
            // data/characters/<folder>/<file>.bin
            var dir = Path.GetDirectoryName(path);
            if (dir == null)
                return false;

            var folder = Path.GetFileName(dir);
            if (folder == null)
                return false;

            var parent = Path.GetFileName(Path.GetDirectoryName(dir));
            if (!parent.Equals("characters", StringComparison.OrdinalIgnoreCase))
                return false;

            var grandParent = Path.GetFileName(
                Path.GetDirectoryName(Path.GetDirectoryName(dir))
            );
            if (!grandParent.Equals("data", StringComparison.OrdinalIgnoreCase))
                return false;

            var fileStem = Path.GetFileNameWithoutExtension(path);

            return folder.Equals(fileStem, StringComparison.OrdinalIgnoreCase);
        }
        static void repathIcon(string charbnin)
        {
            var notfound = CheckLinked([charbnin]);
            if (notfound != null) return;

            var bin = LoadBin(charbnin);
            var Elements = new Dictionary<uint, KeyValuePair<BinValue, BinValue>>();
            if (bin.Sections.TryGetValue("entries", out BinValue entriesSection) && entriesSection is BinMap entriesMap)
            {
                foreach (var kvp in entriesMap.Items)
                {
                    var entryKey = (BinHash)kvp.Key;
                    var entryData = (BinEmbed)kvp.Value;
                    uint hash = entryKey.Value.Hash;

                    if (hash == 0) continue;
                    Dictionary<uint, KeyValuePair<BinValue, BinValue>> targetDict;

                    Elements[hash] = kvp;
                }

            }

            var collectedIcons = new List<Target>();
            foreach (var kvp in Elements.Values)
            {
                FindStringsRecursive(kvp.Value, collectedIcons);
            }

            // Console.WriteLine($"Found Files: {collectedIcons.Count()}");
            collectedIcons.RemoveAll(target =>
    !target.OriginalPath.Contains("icons2d", StringComparison.OrdinalIgnoreCase));

            foreach (var tar in collectedIcons)
            {
                tar.OutputString = tar.OriginalPath;
                tar.BinStringRef = null;
            }
            // Console.WriteLine($"Found icons: {collectedIcons.Count()}");
            WadExtractor.ExtractAndSwapReferences(FixerSettings.base_wad_path, collectedIcons);

        }
        static (BinMap, BinMap, BinMap, List<Target>, BinList) LoadAllBins(string rootBinPath)
        {
            var SkinDataEntries = new Dictionary<uint, KeyValuePair<BinValue, BinValue>>();
            var VFXEntries = new Dictionary<uint, KeyValuePair<BinValue, BinValue>>();
            var CACEntries = new Dictionary<uint, KeyValuePair<BinValue, BinValue>>();
            var RREntries = new Dictionary<uint, KeyValuePair<BinValue, BinValue>>();
            var GearEntries = new Dictionary<uint, KeyValuePair<BinValue, BinValue>>();
            var StaticMatEntries = new Dictionary<uint, KeyValuePair<BinValue, BinValue>>();
            var AnimEntries = new Dictionary<uint, KeyValuePair<BinValue, BinValue>>();
            var GameplayEntries = new Dictionary<uint, KeyValuePair<BinValue, BinValue>>();
            var OtherEntries = new Dictionary<uint, KeyValuePair<BinValue, BinValue>>();

            var collectedStrings = new List<Target>();

            var loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var loaded_linked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<string>();

            var linkedListtoReturn = new BinList(BinType.String);

            queue.Enqueue(rootBinPath);
            List<string> bins_to_check = new List<string>();

            while (queue.Count > 0)
            {
                var path = queue.Dequeue();

                if (loaded.Contains(path)) continue;

                loaded.Add(path);

                var bin = LoadBin(path);
                if (bin == null) continue;

                Console.WriteLine($"Loaded: {path}");

                if (bin.Sections.TryGetValue("linked", out var linkedVal) && linkedVal is BinList linkedList)
                {
                    foreach (var item in linkedList.Items)
                    {
                        if (item is BinString s)
                        {
                            if (loaded_linked.Contains(s.Value)) continue;
                            loaded_linked.Add(s.Value);

                            if (!ShouldSkipFile(s.Value))
                            {
                                queue.Enqueue(s.Value);
                                bins_to_check.Add(s.Value);
                            }
                            else
                            {
                                if (FixerSettings.keep_Icons) repathIcon(s.Value);
                                linkedListtoReturn.Items.Add(s);
                            }
                        }
                    }
                }

                if (bins_to_check.Count() > 0)
                {
                    var notfound = CheckLinked(bins_to_check);
                    if (notfound != null)
                    {
                        foreach (var left in notfound)
                        {
                            linkedListtoReturn.Items.Add(new BinString(left));
                        }
                    }
                    bins_to_check.Clear();
                }
                if (bin.Sections.TryGetValue("entries", out BinValue entriesSection) && entriesSection is BinMap entriesMap)
                {
                    foreach (var kvp in entriesMap.Items)
                    {
                        var entryKey = (BinHash)kvp.Key;
                        var entryData = (BinEmbed)kvp.Value;
                        uint hash = entryKey.Value.Hash;

                        if (hash == 0) continue;
                        Dictionary<uint, KeyValuePair<BinValue, BinValue>> targetDict;

                        switch ((Defi)entryData.Name.Hash)
                        {
                            case Defi.SkinCharacterDataProperties:
                                targetDict = SkinDataEntries;
                                break;

                            case Defi.VfxSystemDefinitionData:
                                targetDict = VFXEntries;
                                break;

                            case Defi.ContextualActionData:
                                targetDict = CACEntries;
                                break;

                            case Defi.ResourceResolver:
                                targetDict = RREntries;
                                break;

                            case Defi.GearSkinUpgrade:
                                targetDict = GearEntries;
                                break;
                            case Defi.StaticMaterialDef:
                                targetDict = StaticMatEntries;
                                break;

                            case Defi.AnimationGraphData:
                                targetDict = AnimEntries;
                                break;

                            case Defi.AbilityObject:
                            case Defi.CharacterRecord:
                            case Defi.SpellObject:
                            case Defi.RecSpellRankUpInfolist:
                            case Defi.ItemRecommendationContextList:
                            case Defi.JunglePathRecommendation:
                            case Defi.ItemRecommendationOverrideSet:
                            case Defi.StatStoneSet:
                            case Defi.StatStoneData:
                                targetDict = GameplayEntries;
                                break;

                            default:
                                targetDict = OtherEntries;
                                break;
                        }
                        targetDict[hash] = kvp;
                    }


                }
            }

            if (SkinDataEntries.Count > 1)
            {
                Validate(SkinDataEntries, [FNV1aHash($"Characters/{FixerSettings.Character}/Skins/Skin{FixerSettings.skinNo}")]);
            }

            var mainEntry = (BinEmbed)SkinDataEntries.Values.First().Value;

            BinValue? GetField(BinEmbed embed, uint hash)
                => embed.Items.FirstOrDefault(f => f.Key.Hash == hash)?.Value;

            uint CAC_name = (GetField(mainEntry, 0xd8f64a0d) as BinLink)?.Value.Hash ?? 0;
            uint RR_name = FNV1aHash($"Characters/{FixerSettings.Character}/Skins/Skin{FixerSettings.skinNo}/Resources"); // (GetField(mainEntry, 0x62286e7e) as BinLink)?.Value.Hash ?? 0;
            var GearUpgrades = new List<uint>();
            uint anmgraph_name = 0;

            if (GetField(mainEntry, 0x426d89a3) is BinEmbed subEmbed)
            {
                if (GetField(subEmbed, 0xf5fb07c7) is BinLink link)
                {
                    anmgraph_name = link.Value.Hash;
                }
            }

            var materialEmbedVal = GetField(mainEntry, 0x68f2b69c);

            if (materialEmbedVal is BinEmbed materialEmbed)
            {
                var materialListVal = GetField(materialEmbed, 0xcb522723);

                if (materialListVal is BinList materialList)
                {
                    foreach (var item in materialList.Items)
                    {
                        if (item is BinLink linkItem)
                        {
                            GearUpgrades.Add(linkItem.Value.Hash);
                        }
                    }
                }
            }
            List<string> ExtraCharactersToLoad = new List<string>();
            var tagListField = GetField(mainEntry, 0x660c8b4e);
            if (tagListField is BinList tagBinList)
            {
                foreach (var item in tagBinList.Items)
                {
                    if (item is BinString strVal)
                    {
                        ExtraCharactersToLoad.Add(strVal.Value);
                    }
                }
            }
            if (FixerSettings.verifyHpBar)
            {
                var targetField = mainEntry.Items.FirstOrDefault(f => f.Key.Hash == 0x51c83af8);

                if (targetField != null && targetField.Value is BinEmbed targetEmbed)
                {
                    var u8Field = targetEmbed.Items.FirstOrDefault(f => f.Key.Hash == 0x3fcb5693);

                    if (u8Field != null && u8Field.Value is BinU8 valU8)
                    {
                        if (valU8.Value != (byte)FixerSettings.HealthbarStyle)
                        {
                            valU8.Value = (byte)FixerSettings.HealthbarStyle;
                        }
                    }
                    else
                    {
                        targetEmbed.Items.Add(new BinField(new FNV1a(0x3fcb5693), new BinU8((byte)FixerSettings.HealthbarStyle)));
                    }
                }
                else
                {
                    // Console.WriteLine("Creating missing 0x51c83af8 element.");

                    var newEmbed = new BinEmbed(new FNV1a(0x11b71b5e));

                    newEmbed.Items.Add(new BinField(new FNV1a(0x4d5ff2d7), new BinString("Buffbone_Cstm_Healthbar")));
                    newEmbed.Items.Add(new BinField(new FNV1a(0x3fcb5693), new BinU8((byte)FixerSettings.HealthbarStyle)));

                    mainEntry.Items.Add(new BinField(new FNV1a(0x51c83af8), newEmbed));
                }
            }


            if (RREntries.Count > 1)
            {
                Validate(RREntries, [RR_name]);
            }
            Console.WriteLine(CACEntries.Count());
            if (CACEntries.Count > 1)
            {
                Validate(CACEntries, [CAC_name]);
            }
            if (AnimEntries.Count > 1)
            {
                Validate(AnimEntries, [anmgraph_name]);
            }
            Validate(GearEntries, GearUpgrades);

            var rrValues = new List<uint>();

            if (RREntries.Count > 0)
            {
                var rrEntry = (BinEmbed)RREntries.Values.First().Value;

                var mapField = rrEntry.Items.FirstOrDefault(f => f.Key.Hash == 0xd2f58721);

                if (mapField != null && mapField.Value is BinMap rrMap)
                {
                    foreach (var kvp in rrMap.Items)
                    {
                        if (kvp.Value is BinLink linkVal)
                        {
                            if (linkVal.Value.Hash == 0) continue;
                            rrValues.Add(linkVal.Value.Hash);
                        }
                    }
                }
            }
            foreach (var kvp in GearEntries)
            {
                if (kvp.Value.Value is BinEmbed gearEmbed)
                {
                    var ptr1 = GetField(gearEmbed, 0x639b0013);
                    if (ptr1 is BinPointer binPtr1)
                    {
                        var ptr2Field = binPtr1.Items.FirstOrDefault(f => f.Key.Hash == 0x5f8284a2);

                        if (ptr2Field != null && ptr2Field.Value is BinPointer binPtr2)
                        {
                            var mapField = binPtr2.Items.FirstOrDefault(f => f.Key.Hash == 0xd2f58721);

                            if (mapField != null && mapField.Value is BinMap gearMap)
                            {
                                foreach (var mapKvp in gearMap.Items)
                                {
                                    if (mapKvp.Value is BinLink linkVal)
                                    {
                                        rrValues.Add(linkVal.Value.Hash);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            Validate(VFXEntries, rrValues);

            void ScanStrings(Dictionary<uint, KeyValuePair<BinValue, BinValue>> source)
            {
                foreach (var kvp in source.Values)
                {
                    FindStringsRecursive(kvp.Value, collectedStrings);
                }
            }

            ScanStrings(SkinDataEntries);
            ScanStrings(RREntries);
            ScanStrings(GearEntries);
            ScanStrings(StaticMatEntries);
            ScanStrings(VFXEntries);
            ScanStrings(CACEntries);
            ScanStrings(GameplayEntries);
            ScanStrings(AnimEntries);
            ScanStrings(OtherEntries);

            foreach (string characterToLoad in ExtraCharactersToLoad)
            {
                Characters.Enqueue((characterToLoad, FixerSettings.skinNo, false));
            }

            var finalMap = new BinMap(BinType.Hash, BinType.Embed);
            var finalMap2 = new BinMap(BinType.Hash, BinType.Embed);
            var finalMap3 = new BinMap(BinType.Hash, BinType.Embed);

            void MergeIntoFinal(Dictionary<uint, KeyValuePair<BinValue, BinValue>> source)
            {
                foreach (var kvp in source.Values)
                {
                    finalMap.Items.Add(kvp);
                }
            }
            void MergeIntoFinal2(Dictionary<uint, KeyValuePair<BinValue, BinValue>> source)
            {
                foreach (var kvp in source.Values)
                {
                    finalMap2.Items.Add(kvp);
                }
            }
            void MergeIntoFinal3(Dictionary<uint, KeyValuePair<BinValue, BinValue>> source)
            {
                foreach (var kvp in source.Values)
                {
                    finalMap3.Items.Add(kvp);
                }
            }

            MergeIntoFinal(SkinDataEntries);
            MergeIntoFinal(RREntries);
            MergeIntoFinal2(GearEntries);
            MergeIntoFinal3(StaticMatEntries);
            MergeIntoFinal2(VFXEntries);
            MergeIntoFinal2(CACEntries);
            MergeIntoFinal2(GameplayEntries);
            MergeIntoFinal2(AnimEntries);
            MergeIntoFinal2(OtherEntries);

            return (finalMap, finalMap2, finalMap3, collectedStrings, linkedListtoReturn);
        }
        private static void Validate(Dictionary<uint, KeyValuePair<BinValue, BinValue>> RREntries, List<uint> RR_names)
        {
            foreach (var rr in RR_names)
            {
                if (!RREntries.ContainsKey(rr))
                {
                    // TODO: prompt user instead of throwing if desired
                    // throw new InvalidOperationException($"Resource Resolver '{rr}' was not found.");
                }
            }

            var keysToRemove = RREntries.Keys
                .Where(k => !RR_names.Contains(k))
                .ToList();

            foreach (var key in keysToRemove)
            {
                RREntries.Remove(key);
            }
        }

        public static void FindStringsRecursive(BinValue value, List<Target> results)
        {
            if (value == null) return;

            // Recursive Action helper
            void Recurse(BinValue v) => FindStringsRecursive(v, results);

            switch (value)
            {
                case BinString str:
                    string s = str.Value;
                    if (!string.IsNullOrWhiteSpace(s) && s.Contains('.'))
                    {
                        int lastDot = s.LastIndexOf('.');
                        if (lastDot < s.Length - 1 && (s.Length - lastDot) <= 6)
                        {
                            var string_out = PathFixer.FixPath(s);
                            var hashes = new List<string> { s };

                            if (s.EndsWith(".tex", StringComparison.OrdinalIgnoreCase))
                            {
                                hashes.Add(Path.ChangeExtension(s, ".dds"));
                            }
                            else if (s.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
                            {
                                hashes.Add(Path.ChangeExtension(s, ".tex"));
                            }
                            Target found = results.FirstOrDefault(t => t.OriginalPath == s);
                            if (found != null)
                            {
                                found.BinStringRef.Add(str);
                            }
                            else
                            {
                                results.Add(new Target
                                {
                                    BinStringRef = [str],
                                    OriginalPath = s,
                                    Hashes = hashes,
                                    OutputPath = FixerSettings.outputDir,
                                    OutputString = string_out,
                                });
                            }
                        }
                    }
                    break;

                // --- Container Traversal ---
                case BinEmbed embed:
                    foreach (var f in embed.Items) Recurse(f.Value);
                    break;

                case BinPointer ptr:
                    foreach (var f in ptr.Items) Recurse(f.Value);
                    break;

                case BinList list:
                    foreach (var item in list.Items) Recurse(item);
                    break;

                case BinList2 list2:
                    foreach (var item in list2.Items) Recurse(item);
                    break;

                case BinOption opt:
                    foreach (var item in opt.Items) Recurse(item);
                    break;

                case BinMap map:
                    foreach (var kvp in map.Items)
                    {
                        Recurse(kvp.Key);
                        Recurse(kvp.Value);
                    }
                    break;
            }
        }
        public static List<string> CollectWads(string rootFolder, IEnumerable<string> priorityWadNames)
        {
            // 1. Map priority names to their index for O(1) lookup.
            //    We use OrdinalIgnoreCase so we don't have to manually normalize strings.
            var priorityMap = (priorityWadNames ?? Enumerable.Empty<string>())
                .Select((name, index) => (Name: name, Index: index))
                .ToDictionary(x => x.Name, x => x.Index, StringComparer.OrdinalIgnoreCase);

            // 2. Enumerate, Sort, and Materialize.
            return Directory
                .EnumerateFiles(rootFolder, "*.wad.client", SearchOption.AllDirectories)
                .Select(Path.GetFullPath)
                .OrderBy(path =>
                {
                    var fileName = Path.GetFileName(path);

                    // If the file is in the priority map, use its index (0, 1, 2...).
                    // If not, assign int.MaxValue so it is sorted to the very end.
                    return priorityMap.TryGetValue(fileName, out int index)
                        ? index
                        : int.MaxValue;
                })
                .ToList();
        }
        public class WadExtractor
        {
            public static List<int> GetAvailableSkinNumbers(List<string> wadPaths, string character)
            {
                var foundSkins = new HashSet<int>();

                // Pre-calculate hashes for skins 0-99 to avoid re-hashing inside the file loop
                var skinHashes = new Dictionary<ulong, int>();
                for (int i = 0; i < 100; i++)
                {
                    // Format: data/characters/{Character}/skins/skin{No}.bin
                    // We force lowercase/normalization if your HashPath expects it
                    string path = $"data/characters/{character}/skins/skin{i}.bin";
                    skinHashes[HashPath(path)] = i;
                }

                byte[] entryBuffer = new byte[32];

                foreach (var wadPath in wadPaths)
                {
                    if (!File.Exists(wadPath)) continue;

                    using (var fs = new FileStream(wadPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var br = new BinaryReader(fs))
                    {
                        if (fs.Length < 272) continue;

                        // Jump to file count
                        fs.Seek(268, SeekOrigin.Begin);
                        uint fileCount = br.ReadUInt32();

                        // Scan all entries in this WAD
                        for (int i = 0; i < fileCount; i++)
                        {
                            if (fs.Read(entryBuffer, 0, 32) != 32) break;

                            ulong pathHash = BitConverter.ToUInt64(entryBuffer, 0);

                            if (skinHashes.TryGetValue(pathHash, out int skinNo))
                            {
                                foundSkins.Add(skinNo);
                            }
                        }
                    }
                }

                var result = foundSkins.ToList();
                result.Sort();
                return result;
            }

            struct WadEntryInfo
            {
                public string FilePath;
                public ulong PathHash;
                public ulong DataChecksum;
                public uint Size;
            }

            public static void PackDirectoryToWad(string sourceDirectory, string outputWadPath)
            {
                if (!Directory.Exists(sourceDirectory))
                    throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory}");

                string outputDir = Path.GetDirectoryName(outputWadPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

                // 1. GATHER FILES
                var files = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
                var entries = new WadEntryInfo[files.Length];

                // 2. PARALLEL PROCESSING (Hashing & Checksums)
                // We use Parallel.For to maximize SSD/CPU usage for checksum calculation
                Parallel.For(0, files.Length, i =>
                {
                    string file = files[i];

                    // A. Path Hashing (XXH64 of relative path)
                    string relativePath = Path.GetRelativePath(sourceDirectory, file);
                    string wadPath = relativePath.Replace('\\', '/').ToLowerInvariant();

                    // B. Data Checksum (XXH64 of file content)
                    // Reading file to compute checksum is necessary for a valid WAD.
                    // We reuse a buffer per thread or use XxHash64.Hash(byte[]) if available.
                    byte[] fileBytes = File.ReadAllBytes(file);

                    entries[i] = new WadEntryInfo
                    {
                        FilePath = file,
                        PathHash = HashPath(wadPath), // Uses your existing HashPath helper
                        DataChecksum = BitConverter.ToUInt64(XxHash64.Hash(fileBytes)), // Using System.IO.Hashing
                        Size = (uint)fileBytes.Length
                    };
                });

                // 3. SORT BY HASH (Standard convention)
                Array.Sort(entries, (a, b) => a.PathHash.CompareTo(b.PathHash));

                // 4. CALCULATE HEADER CHECKSUM (XOR of all entry checksums)
                ulong tocChecksum = 0;
                foreach (var e in entries) tocChecksum ^= e.DataChecksum;

                // 5. WRITE WAD
                using (var fs = new FileStream(outputWadPath, FileMode.Create, FileAccess.Write))
                using (var bw = new BinaryWriter(fs))
                {
                    // --- HEADER ---
                    bw.Write(new char[] { 'R', 'W' });  // Magic
                    bw.Write((byte)3);                  // Major
                    bw.Write((byte)4);                  // Minor
                    bw.Write(new byte[256]);            // ECDSA Signature (Padding)
                    bw.Write(tocChecksum);              // TOC Checksum (64-bit)
                    bw.Write((uint)entries.Length);     // File Count

                    // Calculate Start of Data Block
                    // Header (272) + (Count * EntrySize(32))
                    uint dataStartOffset = 272 + ((uint)entries.Length * 32);
                    uint currentOffset = 0; // Offsets are relative to the start of the Data Block? 
                                            // NO, in WADs, offsets are usually relative to start of file.
                                            // BUT based on your Python script:
                                            // f.write((section.offset + data_offset_base).to_bytes(...))
                                            // So the Python script calculates the absolute offset.

                    uint absoluteOffset = dataStartOffset;

                    // --- ENTRY TABLE ---
                    foreach (var entry in entries)
                    {
                        bw.Write(entry.PathHash);       // 00-08: Path Hash
                        bw.Write(absoluteOffset);       // 08-12: Offset
                        bw.Write(entry.Size);           // 12-16: Compressed Size (Uncompressed, so same)
                        bw.Write(entry.Size);           // 16-20: Uncompressed Size
                        bw.Write((byte)0);              // 20-21: Type (0 = Uncompressed)
                        bw.Write((byte)0);              // 21-22: SubChunk High (unused)
                        bw.Write((ushort)0);            // 22-24: SubChunk Low (unused)
                        bw.Write(entry.DataChecksum);   // 24-32: XXHash64 of Data (CRITICAL FIX)

                        absoluteOffset += entry.Size;
                    }

                    // --- DATA BLOCK ---
                    // Copy files directly to stream (Fastest, low RAM usage)
                    byte[] copyBuffer = new byte[81920]; // 80KB buffer
                    foreach (var entry in entries)
                    {
                        using (var inputFile = new FileStream(entry.FilePath, FileMode.Open, FileAccess.Read))
                        {
                            int bytesRead;
                            while ((bytesRead = inputFile.Read(copyBuffer, 0, copyBuffer.Length)) > 0)
                            {
                                bw.Write(copyBuffer, 0, bytesRead);
                            }
                        }
                    }
                }
            }
            public class Target
            {
                public List<BinString> BinStringRef { get; set; }
                public string OriginalPath { get; set; }
                public List<string> Hashes { get; set; }
                public string OutputPath { get; set; }
                public string OutputString { get; set; }
            }

            // Simple struct to hold extraction jobs so we can sort them
            private struct ExtractionJob
            {
                public ulong Hash;
                public uint Offset;
                public uint CompressedSize;
                public uint UncompressedSize; // Often useful if available, otherwise 0
                public byte Type;
                public Target Target;
                public string Extension;
            }

            public static List<Target> ExtractAndSwapReferences(List<string> wadPaths, List<Target> targets)
            {
                if (targets == null || targets.Count == 0) return targets;

                // 1. Modified Lookup: Stores Priority Index (int)
                // lookup: Hash -> (Target, Extension, Priority)
                var lookup = new Dictionary<ulong, (Target target, string ext, int priority)>(); // <--- CHANGED
                int pendingCount = 0;

                foreach (var t in targets)
                {
                    bool added = false;
                    // Iterate with index to capture priority
                    for (int i = 0; i < t.Hashes.Count; i++) // <--- CHANGED
                    {
                        var h = t.Hashes[i];
                        ulong hashVal = HashPath(h);
                        if (!lookup.ContainsKey(hashVal))
                        {
                            // Store index 'i' as priority (lower is better)
                            lookup[hashVal] = (t, Path.GetExtension(h), i); // <--- CHANGED
                            added = true;
                        }
                    }
                    if (added) pendingCount++;
                }

                var createdDirectories = new HashSet<string>();
                byte[] entryBuffer = new byte[32];

                foreach (var wadPath in wadPaths)
                {
                    if (lookup.Count == 0) break;
                    if (!File.Exists(wadPath)) continue;

                    using (var fs = new FileStream(wadPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var br = new BinaryReader(fs))
                    {
                        if (fs.Length < 272) continue;

                        fs.Seek(268, SeekOrigin.Begin);
                        uint fileCount = br.ReadUInt32();

                        // Temporary buffer to hold the best candidate for each target found in THIS WAD
                        var bestCandidates = new Dictionary<Target, (ExtractionJob job, int priority)>(); // <--- NEW

                        for (int i = 0; i < fileCount; i++)
                        {
                            if (fs.Read(entryBuffer, 0, 32) != 32) break;
                            ulong pathHash = BitConverter.ToUInt64(entryBuffer, 0);

                            if (lookup.TryGetValue(pathHash, out var entry))
                            {
                                // Create potential job
                                var newJob = new ExtractionJob
                                {
                                    Hash = pathHash,
                                    Target = entry.target,
                                    Extension = entry.ext,
                                    Offset = BitConverter.ToUInt32(entryBuffer, 8),
                                    CompressedSize = BitConverter.ToUInt32(entryBuffer, 12),
                                    Type = entryBuffer[20]
                                };

                                // LOGIC: Only keep if it's the first time seeing this target,
                                // OR if this hash has a better (lower) priority index than what we found before.
                                if (!bestCandidates.ContainsKey(entry.target) || entry.priority < bestCandidates[entry.target].priority)
                                {
                                    bestCandidates[entry.target] = (newJob, entry.priority);
                                }
                            }
                        }

                        if (bestCandidates.Count == 0) continue;

                        // Transfer the winners to the actual job list
                        var jobs = bestCandidates.Values.Select(x => x.job).ToList(); // <--- NEW

                        // Remove found hashes from lookup to prevent extraction from subsequent WADs
                        // (Assumes First WAD Wins policy for global order)
                        foreach (var job in jobs)
                        {
                            // We must remove ALL hashes belonging to this Target from the lookup
                            // so we don't try to find "LowRes" in WAD 2 after finding "HighRes" in WAD 1.
                            foreach (var h in job.Target.Hashes)
                                lookup.Remove(HashPath(h));
                        }

                        if (jobs.Count == 0) continue;

                        // --- PASS 2: EXTRACT DATA (Sequential Read) ---
                        // Sorting by offset minimizes disk seeking time
                        jobs.Sort((a, b) => a.Offset.CompareTo(b.Offset));

                        foreach (var job in jobs)
                        {
                            fs.Seek(job.Offset, SeekOrigin.Begin);

                            // Rent a buffer to avoid GC allocation
                            byte[] poolBuffer = ArrayPool<byte>.Shared.Rent((int)job.CompressedSize);

                            try
                            {
                                // Read directly into the pooled buffer
                                int bytesRead = fs.Read(poolBuffer, 0, (int)job.CompressedSize);
                                var rawSpan = new ReadOnlySpan<byte>(poolBuffer, 0, bytesRead);

                                byte[] finalData;

                                // Check Signature (Magic Numbers)
                                // We check the signature first as it is more reliable than the 'Type' byte
                                if (IsZstd(rawSpan) || job.Type == 3)
                                {
                                    try { finalData = DecompressZstd(poolBuffer, bytesRead); }
                                    catch { finalData = rawSpan.ToArray(); } // Fallback
                                }
                                else if (IsGzip(rawSpan) || job.Type == 1)
                                {
                                    finalData = DecompressGzip(poolBuffer, bytesRead);
                                }
                                else
                                {
                                    // If uncompressed, we must copy only the specific bytes read
                                    finalData = rawSpan.ToArray();
                                }

                                // --- WRITE TO DISK ---
                                string final_out = Path.ChangeExtension(job.Target.OutputString, job.Extension);
                                if (string.IsNullOrEmpty(Path.GetFileNameWithoutExtension(final_out))) final_out = Path.Combine(Path.GetDirectoryName(final_out) ?? "", $"dot_{Guid.NewGuid().ToString().Substring(0, 4)}{job.Extension}");
                                if (!string.IsNullOrEmpty(job.Target.OutputPath))
                                {
                                    string outPath = Path.Combine(job.Target.OutputPath, final_out);
                                    string dir = Path.GetDirectoryName(outPath);

                                    // Cache directory creation
                                    if (!createdDirectories.Contains(dir))
                                    {
                                        Directory.CreateDirectory(dir);
                                        createdDirectories.Add(dir);
                                    }

                                    File.WriteAllBytes(outPath, finalData);
                                }

                                // --- UPDATE REFERENCES ---
                                if (job.Target.BinStringRef != null)
                                {
                                    string outRef = final_out;
                                    foreach (BinString s in job.Target.BinStringRef)
                                    {
                                        s.Value = outRef;
                                    }
                                }

                                // Mark target as processed for the return list
                                // (We modify the original list at the very end or track 'found' items)
                                targets.Remove(job.Target);
                            }
                            finally
                            {
                                // Always return buffer to pool
                                ArrayPool<byte>.Shared.Return(poolBuffer);
                            }
                        }
                    }
                }

                return targets;
            }

            // --- HELPERS ---

            // Optimized Magic Number Checks using Spans
            private static bool IsZstd(ReadOnlySpan<byte> data) =>
        data.Length >= 4 &&
        data[0] == 0x28 && data[1] == 0xB5 && data[2] == 0x2F && data[3] == 0xFD;

            private static bool IsGzip(ReadOnlySpan<byte> data) =>
              data.Length >= 2 &&
              data[0] == 0x1F && data[1] == 0x8B;

            private static byte[] DecompressGzip(byte[] data, int length)
            {
                using var ms = new MemoryStream(data, 0, length);
                using var gs = new GZipStream(ms, CompressionMode.Decompress);
                using var outMs = new MemoryStream();
                gs.CopyTo(outMs);
                return outMs.ToArray();
            }

            private static byte[] DecompressZstd(byte[] data, int length)
            {
                // Assuming you are using a library like ZstdNet
                // We pass the specific length because the buffer is pooled and might be larger than data
                var decompressor = new Decompressor();
                var span = new ReadOnlySpan<byte>(data, 0, length);
                return decompressor.Unwrap(span.ToArray()).ToArray();
            }

        }

        public class PathFixer
        {
            static readonly string[] Roots = { "assets", "data" };

            static readonly string[] Categories =
            {
        "characters", "items", "loadouts", "maps", "particles",
        "perks", "rewards", "shared", "sounds", "spells", "ux"
    };

            public static string FixPath(string finalPath)
            {
                if (FixerSettings.cls_assets)
                    finalPath = CleanRootPath(finalPath);

                string norm = finalPath.Replace("\\", "/");
                string[] parts = norm.Split('/', StringSplitOptions.RemoveEmptyEntries);

                string firstFolder = parts.Length > 0 ? parts[0].ToLower() : "";
                string ext = parts.Length > 0
                    ? Path.GetExtension(parts[^1]).ToLower()
                    : "";

                string repath = FixerSettings.repath_path_path;
                bool inFilePath = FixerSettings.in_file_path;

                // ---------------------------
                // CASE 1 — data/... or assets/...
                // ---------------------------
                if (firstFolder == "data" || firstFolder == "assets")
                {
                    string root = firstFolder == "data" ? "DATA" : "ASSETS";

                    if (inFilePath)
                    {
                        string prefix = $"{root}/{repath}{parts.ElementAtOrDefault(1)}";
                        string rest = parts.Length > 2
                            ? string.Join("/", parts.Skip(2))
                            : "";

                        return rest.Length > 0 ? $"{prefix}/{rest}" : prefix;
                    }
                    else
                    {
                        parts[0] = $"{root}/{repath}";
                        return string.Join("/", parts);
                    }
                }

                // ---------------------------
                // CASE 2 — generic path
                // ---------------------------
                bool isFileOnly = parts.Length == 1;

                // Single file → always ASSETS
                if (isFileOnly)
                {
                    return $"ASSETS/{repath}/{parts[0]}";
                }

                // Multi-folder
                string prefixRoot = (ext == ".bin" || ext == "")
                    ? "DATA"
                    : "ASSETS";

                if (inFilePath)
                {
                    return $"{prefixRoot}/{repath}{parts[0]}/" +
                           string.Join("/", parts.Skip(1));
                }
                else
                {
                    return $"{prefixRoot}/{repath}/" +
                           string.Join("/", parts);
                }
            }

            // ---------------------------------
            // CleanRootPath (C# port)
            // ---------------------------------
            static string CleanRootPath(string path)
            {
                string pathNorm = path.Replace("\\", "/").ToLower();
                string[] parts = pathNorm.Split('/', StringSplitOptions.RemoveEmptyEntries);

                string root = null;
                int rootIndex = -1;

                // 1) find root
                for (int i = 0; i < parts.Length; i++)
                {
                    foreach (string r in Roots)
                    {
                        if (parts[i] == r || parts[i].Contains(r))
                        {
                            root = r;
                            rootIndex = i;
                            break;
                        }
                    }
                    if (root != null)
                        break;
                }

                if (root == null)
                    return path;

                // 2) find category
                string category = null;
                int categoryIndex = -1;

                for (int j = rootIndex + 1; j < parts.Length; j++)
                {
                    string cleaned = new string(parts[j].Where(char.IsLetter).ToArray());
                    if (Categories.Contains(cleaned))
                    {
                        category = cleaned;
                        categoryIndex = j;
                        break;
                    }
                }

                if (category == null)
                    return path;

                // 3) build cleaned path
                var rest = parts.Skip(categoryIndex + 1);
                return string.Join("/", new[] { root, category }.Concat(rest));
            }
        }

        public static class Hashes
        {
            private static readonly Lazy<List<string>> _cachedPaths =
                new Lazy<List<string>>(() => LoadPathsOnly(FixerSettings.gamehashes_path));

            private static List<string> LoadPathsOnly(string path)
            {
                var paths = new List<string>();

                if (!File.Exists(path)) return paths;


                foreach (var line in File.ReadLines(path))
                {
                    // Skip the hash, take everything after the first space
                    int spaceIndex = line.IndexOf(' ');
                    if (spaceIndex >= 0 && spaceIndex < line.Length - 1)
                    {
                        paths.Add(line.Substring(spaceIndex + 1).Trim());
                    }
                }

                return paths;
            }

            public static List<Target> FindMatches(List<Target> targets, bool useBaseName = true)
            {
                // 1. Get the list of all paths (Lazy loaded)
                var loadedPaths = _cachedPaths.Value;

                // 2. Iterate through each target
                foreach (var target in targets)
                {
                    // Clear the list for this target
                    target.Hashes = new List<string>();

                    if (string.IsNullOrEmpty(target.OriginalPath)) continue;

                    string searchTerm = useBaseName
                        ? GetBaseName(target.OriginalPath)
                        : GetDataRelativePath(target.OriginalPath);

                    if (string.IsNullOrWhiteSpace(searchTerm)) continue;

                    string targetExt = Path.GetExtension(target.OriginalPath).ToLowerInvariant();

                    foreach (var path in loadedPaths)
                    {
                        if (path.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) < 0)
                            continue;

                        string pathExt = Path.GetExtension(path).ToLowerInvariant();

                        // Allow match if extensions are identical
                        // Or if it's a dds ↔ tex match
                        if (pathExt == targetExt ||
                            (targetExt == ".dds" && pathExt == ".tex") ||
                            (targetExt == ".tex" && pathExt == ".dds"))
                        {
                            target.Hashes.Add(path);
                        }
                    }
                    if (!useBaseName && target.Hashes.Count > 1)
                    {
                        target.Hashes.Sort((a, b) => b.Length.CompareTo(a.Length));
                    }
                }

                return targets;
            }

            private static string GetBaseName(string path)
            {
                return path
                    .Replace("\\", "/")
                    .Split('/')
                    .Last()
                    .Split('.')
                    .First()
                    .ToLowerInvariant();
            }

            private static string GetDataRelativePath(string path)
            {
                string p = path.Replace("\\", "/").ToLowerInvariant();
                int idx = p.IndexOf("data/", StringComparison.OrdinalIgnoreCase);

                string relative = (idx != -1) ? p.Substring(idx + 5) : p;

                if (relative.Length == 0) return "";

                int cutoff = (relative.Length * 2 + 2) / 3;

                return relative.Substring(0, cutoff);
            }
        }

    }

}