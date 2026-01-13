using Jade.Ritobin;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;
using System.IO.Hashing;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Shapes;
using ZstdSharp;
using static ModManager.Repatheruwu;
using Path = System.IO.Path;
using SearchOption = System.IO.SearchOption;

namespace ModManager
{
    public class FixerSettings
    {
        // NO LONGER STATIC
        public string Character { get; set; } = "";
        public int skinNo { get; set; } = 0;

        public int HealthbarStyle { get; set; } = 12;
        public bool verifyHpBar { get; set; } = true;
        public string inputDir { get; set; } = "TEMP";
        public string outputDir { get; set; } = ".";
        public string WADpath { get; set; } = "C:\\Riot Games\\League of Legends\\Game\\DATA\\FINAL";
        public List<string> AllWadPaths = [];
        public string repath_path_path { get; set; } = ".";
        public bool in_file_path { get; set; } = true;
        public bool cls_assets { get; set; } = true;
        public string gamehashes_path { get; set; } = "cslol-tools\\hashes.game.txt";
        public List<string> base_wad_path { get; set; } = [];
        public List<string> OldLookUp { get; set; } = [];
        public List<string> LangLookUp { get; set; } = [];
        public bool Lang { get; set; } = true; // good

        public bool keep_Icons { get; set; } = true; // good
        public bool KillStaticMat { get; set; } = true; // good
        public bool sfx_events { get; set; } = true; // good
        public bool folder { get; set; } = true; // good
        public bool binless { get; set; } = true; // good
        public bool noskinni { get; set; } = true; // good
        public bool AllAviable { get; set; } = true; // good
        public int SoundOption { get; set; } = 0; // good
        public int AnimOption { get; set; } = 0; // good percent
        public double percent { get; set; } = 80; // good 

        public List<string> Missing_Bins { get; set; } = new List<string>();
        public List<string> Missing_Files { get; set; } = new List<string>();
        public List<string> CharraBlackList = ["viegowraith"];
        public uint bnk_version { get; set; } = 145; 
        public string manifest_145 { get; set; } = "https://lol.secure.dyn.riotcdn.net/channels/public/releases/998BEDBD1E22BD5E.manifest";
    }

    public class Repatheruwu
    {
        Dictionary<string, List<string>> CharacterCases = new Dictionary<string, List<string>>
        {
            ["anivia"] = new List<string> { "aniviaegg", "aniviaiceblock", },
            ["annie"] = new List<string> { "annietibbers", },
            ["aphelios"] = new List<string> { "apheliosturret", },
            ["aurora"] = new List<string> { "auroraspirits", },
            ["azir"] = new List<string> { "azirsoldier", "azirsundisc", "azirtowerclicker", "azirultsoldier", },
            ["bard"] = new List<string> { "bardfollower", "bardhealthshrine", "bardpickup", "bardpickupnoicon", "bardportalclickable", },
            ["bardpickup"] = new List<string> { "bardpickupnoicon", },
            ["belveth"] = new List<string> { "belvethspore", "belvethvoidling", },
            ["caitlyn"] = new List<string> { "caitlyntrap", },
            ["cassiopeia"] = new List<string> { "cassiopeia_death", },
            ["elise"] = new List<string> { "elisespider", "elisespiderling", },
            ["elisespider"] = new List<string> { "elisespiderling", },
            ["fiddlesticks"] = new List<string> { "fiddlestickseffigy", },
            ["fizz"] = new List<string> { "fizzbait", "fizzshark", },
            ["gangplank"] = new List<string> { "gangplankbarrel", },
            ["gnar"] = new List<string> { "gnarbig", },
            ["heimerdinger"] = new List<string> { "heimertblue", "heimertyellow", },
            ["illaoi"] = new List<string> { "illaoiminion", },
            ["irelia"] = new List<string> { "ireliablades", },
            ["ivern"] = new List<string> { "ivernminion", "iverntotem", },
            ["jarvaniv"] = new List<string> { "jarvanivstandard", "jarvanivwall", },
            ["jhin"] = new List<string> { "jhintrap", },
            ["jinx"] = new List<string> { "jinxmine", },
            ["kalista"] = new List<string> { "kalistaaltar", "kalistaspawn", },
            ["kindred"] = new List<string> { "kindredjunglebountyminion", "kindredwolf", },
            ["kled"] = new List<string> { "kledmount", "kledrider", },
            ["kogmaw"] = new List<string> { "kogmawdead", },
            ["lissandra"] = new List<string> { "lissandrapassive", },
            ["lulu"] = new List<string> { "lulufaerie", "lulupolymorphcritter", },
            ["lux"] = new List<string> { "luxair", "luxdark", "luxfire", "luxice", "luxmagma", "luxmystic", "luxnature", "luxstorm", "luxwater", },
            ["malzahar"] = new List<string> { "malzaharvoidling", },
            ["maokai"] = new List<string> { "maokaisproutling", },
            ["milio"] = new List<string> { "miliominion", },
            ["monkeyking"] = new List<string> { "monkeykingclone", "monkeykingflying", },
            ["naafiri"] = new List<string> { "naafiripackmate", },
            ["nasus"] = new List<string> { "nasusult", },
            ["nidalee"] = new List<string> { "nidaleecougar", "nidaleespear", },
            ["nunu"] = new List<string> { "nunusnowball", },
            ["olaf"] = new List<string> { "olafaxe", },
            ["orianna"] = new List<string> { "oriannaball", "oriannanoball", },
            ["ornn"] = new List<string> { "ornnram", },
            ["quinn"] = new List<string> { "quinnvalor", },
            ["rammus"] = new List<string> { "rammusdbc", "rammuspb", },
            ["reksai"] = new List<string> { "reksaitunnel", },
            ["ruby_jinx"] = new List<string> { "ruby_jinx_monkey", },
            ["senna"] = new List<string> { "sennasoul", },
            ["shaco"] = new List<string> { "shacobox", },
            ["shen"] = new List<string> { "shenspirit", },
            ["shyvana"] = new List<string> { "shyvanadragon", },
            ["sona"] = new List<string> { "sonadjgenre01", "sonadjgenre02", "sonadjgenre03", },
            ["strawberry_aurora"] = new List<string> { "strawberry_auroraspirits", },
            ["strawberry_illaoi"] = new List<string> { "strawberry_illaoiminion", },
            ["swain"] = new List<string> { "swaindemonform", },
            ["syndra"] = new List<string> { "syndraorbs", "syndrasphere", },
            ["taliyah"] = new List<string> { "taliyahwallchunk", },
            ["teemo"] = new List<string> { "teemomushroom", },
            ["thresh"] = new List<string> { "threshlantern", },
            ["trundle"] = new List<string> { "trundlewall", },
            ["vi"] = new List<string> { "viego", "viegosoul", "viktor", "viktorsingularity", },
            ["viego"] = new List<string> { "viegosoul", },
            ["viktor"] = new List<string> { "viktorsingularity", },
            ["yorick"] = new List<string> { "yorickbigghoul", "yorickghoulmelee", "yorickwghoul", "yorickwinvisible", },
            ["zac"] = new List<string> { "zacrebirthbloblet", },
            ["zed"] = new List<string> { "zedshadow", },
            ["zoe"] = new List<string> { "zoeorbs", },
            ["zyra"] = new List<string> { "zyragraspingplant", "zyrapassive", "zyraseed", "zyrathornplant", },
        };

        public FixerSettings Settings { get; private set; }
        private WadExtractor _wadExtractor;
        private PathFixer _pathFixer;
        private Hashes _hashes;
        private FixerUI x;

        // Constants for Logging Colors
        private const string CLR_ACT = "#2a84d2";   // Blue
        private const string CLR_ERR = "#f81118";   // Red
        private const string CLR_WARN = "#ecba0f";  // Yellow
        private const string CLR_GOOD = "#2dc55e";  // Green
        private const string CLR_MOD = "#5350b9";   // Purple

        public Repatheruwu()
        {
            Settings = new FixerSettings();
            _wadExtractor = new WadExtractor(Settings);
            _pathFixer = new PathFixer(Settings);
            _hashes = new Hashes(Settings);
        }

        public static uint FNV1aHash(string input)
        {
            const uint FNV_OFFSET_BASIS = 0x811C9DC5;
            const uint FNV_PRIME = 0x01000193;

            uint hash = FNV_OFFSET_BASIS;
            byte[] data = Encoding.UTF8.GetBytes(input.ToLowerInvariant());

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

        public enum lang_id : uint
        {
            ar_AE = 3254137205,
            cs_CZ = 877555794,
            de_DE = 4290373403,
            el_GR = 4147287991,
            en_US = 684519430,
            es_ES = 235381821,
            es_MX = 3671217401,
            fr_FR = 323458483,
            hu_HU = 370126848,
            it_IT = 1238911111,
            ja_JP = 2008704848,
            ko_KR = 3391026937,
            pl_PL = 559547786,
            pt_BR = 960403217,
            ro_RO = 4111048996,
            ru_RU = 2577776572,
            th_TH = 3325617959,
            tr_TR = 4036333791,
            vi_VN = 2847887552,
            zh_CN = 3948448560,
            zh_TW = 2983963595
        }

        private Queue<(string, int, bool)> Characters = new Queue<(string, int, bool)>();
        public (int, bool) getSkinInts(string charra)
        {
            List<int> Skins = _wadExtractor.GetAvailableSkinNumbers(Settings.base_wad_path, charra);
            var (i, b) = ProcessAviableSkin2(Skins);
            return (i, b);
        }
        public (int, bool) ProcessAviableSkin2(List<int> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return (0, true);
            }

            if (entries.Count == 1)
            {
                return (entries[0], false);
            }

            bool isSequential = false;

            var sequenceLengths = new List<int>();
            int currentLength = 1;
            for (int i = 0; i < entries.Count - 1; i++)
            {
                if (entries[i + 1] == entries[i] + 1)
                {
                    currentLength++;
                }
                else
                {
                    sequenceLengths.Add(currentLength);
                    currentLength = 1;
                }
            }
            sequenceLengths.Add(currentLength);
            if (sequenceLengths.Count == 1 && sequenceLengths?[0] > 12)
            {
                isSequential = true;
            }
            else
            {
                isSequential = sequenceLengths.Any(len => len < 4);
            }
            if (isSequential)
            {
                return (0, false);
            }
            else
            {
                return (-1, false);
            }
        }

        public void ProcessAviableSkin(List<int> entries, string charra)
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

        public void FixiniYoursSkini(FixerUI ui)
        {
            this.x = ui;
            Settings.AllWadPaths = CollectWads(Settings.WADpath);
            _wadExtractor.x = this.x;
            string tmp = Path.Combine(Path.GetTempPath(), "cslolgo_fixer_" + Guid.NewGuid().ToString());

            Directory.CreateDirectory(tmp);
            Settings.inputDir = tmp;


            for (int i = 0; i < Settings.base_wad_path.Count; i++)
            {
                string currentPath = Settings.base_wad_path[i];

                if (Directory.Exists(currentPath))
                {
                    string randomchar = Path.GetRandomFileName().Replace(".", "").Substring(0, 8);
                    string outputWadPath = Path.Combine(Settings.inputDir, $"{randomchar}.wad.client");

                    // Use instance method
                    _wadExtractor.PackDirectoryToWad(currentPath, outputWadPath);

                    Settings.base_wad_path[i] = outputWadPath;
                }

            }
            if (Settings.AllAviable)
            {
                List<int> Skins = _wadExtractor.GetAvailableSkinNumbers(Settings.base_wad_path, Settings.Character);
                ProcessAviableSkin(Skins, Settings.Character);
            }
            else
            {
                Characters.Enqueue((Settings.Character, Settings.skinNo, true));
            }

            while (Characters.Count > 0)
            {
                var (Current_Char, skinNo, HpBar) = Characters.Dequeue();
                x.LowerLog($"[FIXI] Fixing {Current_Char} skin {skinNo}", CLR_ACT);

                Settings.Character = Current_Char;
                Settings.skinNo = skinNo;
                Settings.verifyHpBar = HpBar;
                string shortChar = Current_Char.Length > 4
    ? Current_Char.Substring(0, 4)
    : Current_Char;
                Settings.repath_path_path = $".{shortChar}{skinNo}_{Guid.NewGuid().ToString("N").Substring(0, 3)}_";
                string binPath = $"data/characters/{Settings.Character}/skins/skin{Settings.skinNo}.bin";
                var check = CheckLinked([binPath]);
                if (check != null)
                {
                    x.LowerLog($"[FAIL] Failed to find {binPath}, Aborting", CLR_ERR);
                    return;
                }


                var (binentries, concat, staticMat, allStrings, linkedList) = LoadAllBins(binPath);
                if (binentries is null)
                {
                    x.LowerLog($"[SKIP] Coudnt Find SkinCharacterProperties, Skipping {Settings.Character} skin {Settings.skinNo}", CLR_WARN);

                }
                x.LowerLog($"[PROC] Processing Assets", CLR_ACT);

                allStrings = process(allStrings);
                // foreach (var item in allStrings) {
                //      x.LowerLog($"[MISS] {item.OriginalPath}", CLR_ERR);
                // }

                var key = CharacterCases.Keys
    .FirstOrDefault(k =>
        string.Equals(k, Current_Char, StringComparison.OrdinalIgnoreCase));

                if (key != null)
                {
                    foreach (var name in CharacterCases[key])
                    {
                        Characters.Enqueue((name, skinNo, false));
                        // linkedList.Items.AdFd(new BinString($"data/{name}_skin{skinNo}_concat.bin"));
                    }
                }

                if (Settings.binless) continue;

                x.LowerLog($"[SAVE] Saving Bins", CLR_ACT);
                string conat_path = $"data/{Settings.Character}_skin{Settings.skinNo}_concat.bin";
                var EmptyLinked = new BinList(BinType.String);
                x.LowerLog($"[SAVE] {conat_path}", CLR_ACT);
                Save_Bin(EmptyLinked, concat, $"{Settings.outputDir}/{conat_path}");
                linkedList.Items.Add(new BinString(conat_path));

                if (staticMat.Items.Count() > 0)
                {
                    string static_mat_path = $"data/{Settings.Character}_skin{Settings.skinNo}_StaticMat.bin";
                    x.LowerLog($"[SAVE] {static_mat_path}", CLR_ACT);
                    Save_Bin(EmptyLinked, staticMat, $"{Settings.outputDir}/{static_mat_path}");
                    if (Settings.KillStaticMat)
                    {
                        string static_mat_path_proxy = $"data/{Settings.Character}_skin{Settings.skinNo}_StaticMat_proxy.bin";
                        EmptyLinked.Items.Add(new BinString(static_mat_path));
                        var EmptyEntries = new BinMap(BinType.Hash, BinType.Embed);
                        x.LowerLog($"[SAVE] {static_mat_path_proxy}", CLR_ACT);
                        Save_Bin(EmptyLinked, EmptyEntries, $"{Settings.outputDir}/{static_mat_path_proxy}");
                        linkedList.Items.Add(new BinString(static_mat_path_proxy));
                    }
                    else
                    {
                        linkedList.Items.Add(new BinString(static_mat_path));
                    }
                }

                x.LowerLog($"[SAVE] {binPath}", CLR_ACT);
                Save_Bin(linkedList, binentries, $"{Settings.outputDir}/{binPath}");
                if (Settings.noskinni && Settings.skinNo == 0)
                {
                    x.LowerLog($"[SKIN] Creating No Skinni Lightinni Italini", CLR_MOD);

                    var skinEntry = binentries.Items.First(x => ((BinEmbed)x.Value).Name.Hash == (uint)Defi.SkinCharacterDataProperties);
                    var skinKeyRef = (BinHash)skinEntry.Key;

                    var rrLinkRef = ((BinEmbed)skinEntry.Value).Items.FirstOrDefault(x => x.Key.Hash == 0x62286e7e)?.Value as BinLink;

                    var rrEntry = binentries.Items.FirstOrDefault(x => ((BinEmbed)x.Value).Name.Hash == (uint)Defi.ResourceResolver);
                    var rrKeyRef = rrEntry.Key != null ? (BinHash)rrEntry.Key : null;

                    foreach (int i in Enumerable.Range(1, 99))
                    {
                        binPath = $"data/characters/{Settings.Character}/skins/skin{i}.bin";

                        uint newSkinHash = FNV1aHash($"Characters/{Settings.Character}/Skins/Skin{i}");
                        uint newRRHash = FNV1aHash($"Characters/{Settings.Character}/Skins/Skin{i}/Resources");

                        skinKeyRef.Value = new FNV1a(newSkinHash);
                        if(rrLinkRef?.Value != null) rrLinkRef.Value = new FNV1a(newRRHash);
                        if(rrKeyRef != null) rrKeyRef.Value = new FNV1a(newRRHash);

                        Save_Bin(linkedList, binentries, $"{Settings.outputDir}/{binPath}");
                    }
                }

            }

            if (!Settings.folder)
            {
                x.LowerLog("[PACK] Packing WAD", CLR_ACT);

                var psi = new ProcessStartInfo
                {
                    FileName = "cslol-tools/wad-make.exe",
                    Arguments = $"\"{Settings.outputDir}\"",
                    CreateNoWindow = true,      // Don't show a console window
                    UseShellExecute = false,    // Required for redirecting output or hiding window
                    RedirectStandardOutput = false,
                    RedirectStandardError = false
                };

                using (var proc = Process.Start(psi))
                {
                    proc.WaitForExit();
                }
                //_wadExtractor.PackDirectoryToWadCompressed(Settings.outputDir, $"{Settings.outputDir}.client");
                Directory.Delete(Settings.outputDir, true);
            }


            if (Directory.Exists(Settings.inputDir)) Directory.Delete(Settings.inputDir, true);

            foreach (string bin in Settings.Missing_Files)
            {
                x.UpperLog($"[MISS] {bin}", CLR_WARN);
            }

            if (Settings.Missing_Bins.Count() > 2)
            {
                x.LowerLog($"[WARN] Done. . . BUT {Settings.Missing_Bins.Count()} bins are missing", CLR_WARN);
                foreach (string bin in Settings.Missing_Bins)
                {
                    x.UpperLog($"{bin}", CLR_ERR);
                }
                x.LowerLog($"[TIP]  Try using Manifest downloader if needed", CLR_WARN);
            }
            else
            {
                foreach (string bin in Settings.Missing_Bins)
                {
                    x.UpperLog($"{bin}", CLR_ERR);
                }
                x.LowerLog($"[DONE] Finished ^^", CLR_GOOD);
                x.UpperLog($"[DONE] Finished ^^", CLR_GOOD);
            }
        }

        public void Save_Bin(BinList Linked, BinMap entries, string output)
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

        public List<WadExtractor.Target> process(List<WadExtractor.Target> processing)
        {
            List<WadExtractor.Target> ToCheckup = new List<WadExtractor.Target>();
            Dictionary<WadExtractor.Target, uint> Event_bnk_lang = new Dictionary<WadExtractor.Target, uint>();
            void check_n_fix_vo()
            {
                ToCheckup = _wadExtractor.FindAndSwapReferences(Settings.AllWadPaths, ToCheckup);
                if (ToCheckup.Count > 0)
                {
                    ToCheckup = _hashes.FindMatches(ToCheckup);
                    ToCheckup.RemoveAll(t =>
                    {
                        if (t.Hashes.Count == 0)
                        {
                            return true;
                        }
                        return false;
                    });
                    ToCheckup = _wadExtractor.FindAndSwapReferences(Settings.AllWadPaths, ToCheckup);
                }
                foreach (var chk in ToCheckup)
                {
                    x.UpperLog($"[MISS] Could not verify path for {chk.OriginalPath}", CLR_ERR);
                    Settings.Missing_Files.Add(chk.OriginalPath);
                }
                Dictionary<string, List<WadExtractor.Target>> Audio_to_dl = new Dictionary<string, List<WadExtractor.Target>>();
                foreach (var kvp in Event_bnk_lang)
                {
                    string wwise_file = Path.Combine(Settings.outputDir, kvp.Key.OutputString);
                    if (!File.Exists(wwise_file)) continue;
                    uint langID = 0;

                    using (var fs = new FileStream(wwise_file, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var br = new BinaryReader(fs))
                    {
                        if (fs.Length >= 20)
                        {
                            fs.Seek(16, SeekOrigin.Begin); // TODO
                            langID = br.ReadUInt32();
                        }
                    }
                    if (kvp.Value != langID)
                    {
                        var new_lang = (lang_id)kvp.Value;
                        if (!Audio_to_dl.ContainsKey(new_lang.ToString()))
                        {
                            Audio_to_dl[new_lang.ToString()] = new List<WadExtractor.Target>();
                        }
                        Audio_to_dl[new_lang.ToString()].Add(kvp.Key);

                        x.LowerLog($"[WRNG] Expected {(lang_id)kvp.Value} but found {(lang_id)langID} in {kvp.Key.OriginalPath}", CLR_WARN);
                    }
                    else
                    {
                        x.LowerLog($"[GOOD] {(lang_id)langID} in {kvp.Key.OriginalPath}", CLR_GOOD);
                    }
                }
                if (Audio_to_dl.Count > 0)
                {
                    foreach (var kv in Audio_to_dl)
                    {
                        string VO_path = Path.Combine("manifest", $".lang_{Settings.bnk_version}_{kv.Key}");
                        string VO_wad = Path.Combine(VO_path, "DATA","FINAL","Champions",$"{Settings.Character}.{kv.Key}.wad.client");
                        if (!File.Exists(VO_wad))
                        {
                            x.LowerLog($"[WAIT] Downloading {Settings.Character}.{kv.Key}.wad.client to fix events.bnk", CLR_MOD);
                            string manifestFilePath = Path.Combine(VO_path, "this.manifest");
                            if (!File.Exists(manifestFilePath))
                            {
                                using (var client = new HttpClient())
                                {
                                    var data = client
                                        .GetByteArrayAsync(Settings.manifest_145)
                                        .GetAwaiter()
                                        .GetResult();
                                    Directory.CreateDirectory(Path.GetDirectoryName(manifestFilePath));
                                    File.WriteAllBytes(manifestFilePath, data);
                                }
                            }


                            var psi = new ProcessStartInfo
                            {
                                FileName = Path.Combine("cslol-tools", "ManifestDownloader.exe"),
                                Arguments = $"\"{manifestFilePath}\" -f {Settings.Character}.{kv.Key}.wad.client -o \"{VO_path}\"",
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };

                            using (var process = Process.Start(psi))
                            {
                                process.WaitForExit();
                            }
                        }
                        if (File.Exists(VO_wad))
                        {
                            x.LowerLog($"[FIXI] Fixing VO", CLR_ACT);
                            var left = _wadExtractor.ExtractAndSwapReferences([VO_wad], kv.Value);
                            if( left.Count != 0)
                            {
                                left = _hashes.FindMatches(left);
                                left = _wadExtractor.ExtractAndSwapReferences([VO_wad], kv.Value);
                            }
                            foreach (var tar in left)
                            {
                                x.LowerLog($"[FAIL] Failed to fill up {kv.Key} events {tar.OriginalPath}", CLR_ERR);
                            }
                        }
                    }
                }
            }
            var allPaths = new HashSet<string>(
                    processing.Select(t => t.OriginalPath),
                    StringComparer.OrdinalIgnoreCase
                );

            var bnkToWpkMap = new Dictionary<WadExtractor.Target, string>();


            if (Settings.SoundOption == 0)
            {
                foreach (var target in processing
                    .Where(t => t.OriginalPath.IndexOf("_vo_events.bnk", StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList())
                {
                    var (ver, id) = _wadExtractor.CheckLanguageID(Settings.base_wad_path, target.OriginalPath);
                    if (ver == 0)
                    {
                        processing.Remove(target);
                        continue;
                    }
                    if (ver < Settings.bnk_version)
                    {
                        Event_bnk_lang.Add(target, id);

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

                        processing.Remove(target);
                    }else if (ver > Settings.bnk_version)
                    {
                        x.LowerLog("[INFO] UR APP NEED UPDATE BTW, DID U KNOW THAT?????", CLR_GOOD);
                    }
                }

                if (!Settings.sfx_events)
                {
                    bool IsSfxEvents(WadExtractor.Target t) =>
    string.Equals(Path.GetExtension(t.OriginalPath), "_sfx_events.bnk", StringComparison.OrdinalIgnoreCase);
                    ToCheckup = processing.Where(IsSfxEvents).ToList();
                    processing.RemoveAll(IsSfxEvents);
                }

            }
            else if (Settings.SoundOption == 2)
            {
                bool IsSound(WadExtractor.Target t)
                {
                    var ext = Path.GetExtension(t.OriginalPath);

                    return string.Equals(ext, ".wpk", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(ext, ".bnk", StringComparison.OrdinalIgnoreCase);
                }
                ToCheckup = processing.Where(IsSound).ToList();
                processing.RemoveAll(IsSound);
            }
            else
            {
                foreach (var target in processing.Where(t => t.OriginalPath.IndexOf("_vo_events.bnk", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    var (ver, id) = _wadExtractor.CheckLanguageID(Settings.base_wad_path, target.OriginalPath);
                    if (ver < Settings.bnk_version)
                    {
                        Event_bnk_lang.Add(target, id);
                    }
                }
            }

            if (Settings.AnimOption == 2)
            {
                processing.RemoveAll(target =>
                string.Equals(Path.GetExtension(target.OriginalPath), ".anm", StringComparison.OrdinalIgnoreCase));
            }
            // Use _wadExtractor instance
            processing = _wadExtractor.ExtractAndSwapReferences(Settings.base_wad_path, processing);
            if (processing.Count == 0)
            {
                check_n_fix_vo();
                return processing;
            }

            var remainingPaths = new HashSet<string>(
                processing.Select(t => t.OriginalPath),
                StringComparer.OrdinalIgnoreCase
                );

            if (Settings.binless)
            {
                x.LowerLog("[CHEK] Double checking files . . .", CLR_ACT);
                processing = _hashes.FindMatches(processing);
                processing.RemoveAll(t =>
                {
                    if (t.Hashes.Count == 0)
                    {
                        return true;
                    }
                    return false;
                });
                processing = _wadExtractor.ExtractAndSwapReferences(Settings.base_wad_path, processing, CLR_MOD);
                processing.Clear();
                if (Settings.SoundOption != 2)
                {
                    foreach (var pair in bnkToWpkMap)
                    {
                        WadExtractor.Target bnkTarget = pair.Key;
                        string wpkPath = pair.Value;

                        if (!remainingPaths.Contains(wpkPath))
                        {
                            processing.Add(bnkTarget);
                        }
                    }
                }
                processing = _wadExtractor.ExtractAndSwapReferences(Settings.AllWadPaths, processing);
                check_n_fix_vo();
                return new List<WadExtractor.Target>();
            }
            if (Settings.SoundOption == 0)
            {
                bool IsSound(WadExtractor.Target t)
                {
                    var ext = Path.GetExtension(t.OriginalPath);

                    return string.Equals(ext, ".wpk", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(ext, ".bnk", StringComparison.OrdinalIgnoreCase);
                }
                ToCheckup = processing.Where(IsSound).ToList();
                processing.RemoveAll(IsSound);
            }

            if (Settings.AnimOption == 0)
            {
                bool IsANM(WadExtractor.Target t) =>
    string.Equals(Path.GetExtension(t.OriginalPath), ".anm", StringComparison.OrdinalIgnoreCase);
                ToCheckup = processing.Where(IsANM).ToList();
                processing.RemoveAll(IsANM);
            }

            processing = _wadExtractor.ExtractAndSwapReferences(Settings.OldLookUp, processing);
            if (processing.Count() == 0)
            {
                check_n_fix_vo();
                return processing;
            }
            if (Settings.SoundOption == 0)
            {
                foreach (var pair in bnkToWpkMap)
                {
                    WadExtractor.Target bnkTarget = pair.Key;
                    string wpkPath = pair.Value;

                    if (!remainingPaths.Contains(wpkPath))
                    {
                        processing.Add(bnkTarget);
                    }
                }
            }

            processing = _wadExtractor.ExtractAndSwapReferences(Settings.AllWadPaths, processing);
            if (processing.Count() == 0)
            {
                check_n_fix_vo();
                return processing;
            }

            // Use _hashes instance
            processing = _hashes.FindMatches(processing);

            processing.RemoveAll(t =>
            {
                if (t.Hashes.Count == 0)
                {
                    return true;
                }
                return false;
            });
            processing = _wadExtractor.ExtractAndSwapReferences(Settings.base_wad_path, processing, CLR_MOD);
            if (processing.Count() == 0)
            {
                check_n_fix_vo();
                return processing;
            }
            processing = _wadExtractor.ExtractAndSwapReferences(Settings.OldLookUp, processing, CLR_MOD);
            if (processing.Count() == 0)
            {
                check_n_fix_vo();
                return processing;
            }
            processing = _wadExtractor.ExtractAndSwapReferences(Settings.AllWadPaths, processing, CLR_MOD);
            if (processing.Count() == 0)
            {
                check_n_fix_vo();
                return processing;
            }
            foreach (var item in processing)
            {
                Settings.Missing_Files.Add(item.OriginalPath);
            }

            check_n_fix_vo();
            return processing;
        }

        Bin LoadBin(string path)
        {
            if (File.Exists($"{Settings.inputDir}/{path}"))
            {
                var data = File.ReadAllBytes($"{Settings.inputDir}/{path}");
                return new BinReader(data).Read();
            }
            else
            {
                string hashed = $"{HashPath(path).ToString("x16")}.bin";
                if (!File.Exists($"{Settings.inputDir}/{hashed}")) return null;
                var data = File.ReadAllBytes($"{Settings.inputDir}/{hashed}");
                return new BinReader(data).Read();
            }
        }

        List<string> CheckLinked(List<string> bins_to_check)
        {
            var bins_hashed = new List<WadExtractor.Target>();
            foreach (string path in bins_to_check)
            {
                if (File.Exists($"{Settings.inputDir}/{path}")) continue;
                string hashed = $"{HashPath(path).ToString("x16")}.bin";
                if (File.Exists($"{Settings.inputDir}/{hashed}")) continue;

                WadExtractor.Target found = bins_hashed.FirstOrDefault(t => t.OriginalPath == hashed);
                if (found == null)
                {
                    bins_hashed.Add(new WadExtractor.Target
                    {
                        Hashes = new List<string> { path },
                        OutputPath = Settings.inputDir,
                        OutputString = hashed,
                        BinStringRef = null,
                        OriginalPath = path,
                    });
                }
            }
            if (bins_hashed.Count() < 1) return null;
            if (!Settings.binless)
            {
                bins_hashed = _wadExtractor.ExtractAndSwapReferences(Settings.base_wad_path, bins_hashed);
                if (bins_hashed.Count() < 1) return null;

                bins_hashed = _wadExtractor.ExtractAndSwapReferences(Settings.OldLookUp, bins_hashed);
                if (bins_hashed.Count() < 1) return null;
            }


            bins_hashed = _wadExtractor.ExtractAndSwapReferences(Settings.AllWadPaths, bins_hashed);
            if (bins_hashed.Count() < 1) return null;
            bins_hashed = _hashes.FindMatches(bins_hashed, false);

            bins_hashed.RemoveAll(t =>
            {
                if (t.Hashes.Count == 0)
                {
                    Console.WriteLine($"Missing CAC linked bin: {t.OriginalPath}");
                    return true; // Remove this item
                }
                return false; // Keep this item
            });
            bins_hashed = _wadExtractor.ExtractAndSwapReferences(Settings.AllWadPaths, bins_hashed);
            if (bins_hashed.Count() < 1) return null;
            List<string> returning = new List<string>();
            foreach (WadExtractor.Target tar in bins_hashed)
            {
                Settings.Missing_Bins.Add($"[Missing] {tar.OriginalPath}");
                returning.Add(tar.OriginalPath);
            }
            return returning;
        }

        bool ShouldSkipFile(string path)
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

        void repathIcon(string charbnin)
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

                    Elements[hash] = kvp;
                }

            }

            var collectedIcons = new List<WadExtractor.Target>();
            foreach (var kvp in Elements.Values)
            {
                FindStringsRecursive(kvp.Value, collectedIcons);
            }

            collectedIcons.RemoveAll(target =>
                 !target.OriginalPath.Contains("icons2d", StringComparison.OrdinalIgnoreCase));

            foreach (var tar in collectedIcons)
            {
                tar.OutputString = tar.OriginalPath;
                tar.BinStringRef = null;
            }
            _wadExtractor.ExtractAndSwapReferences(Settings.base_wad_path, collectedIcons);

        }

        (BinMap, BinMap, BinMap, List<WadExtractor.Target>, BinList) LoadAllBins(string rootBinPath)
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

            var collectedStrings = new List<WadExtractor.Target>();

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
                string trimmedPath = path.Length > 100
    ? "…" + path[^99..]   // keep last 99 chars + ellipsis
    : path;

                x.UpperLog($"[READ] {trimmedPath}", CLR_ACT);

                Bin bin = null;
                try
                {
                    bin = LoadBin(path);
                }
                catch {
                    x.UpperLog($"[FAIL] Failed to read {trimmedPath}", CLR_ERR);
                    Settings.Missing_Bins.Add($"[Read Error] {path}");
                    continue; }
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
                                x.UpperLog($"[SKIP] {path}", CLR_WARN);
                                if (Settings.keep_Icons) repathIcon(s.Value);
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
                            x.LowerLog($"[FAIL] Missing: {path}", CLR_ERR);
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
                Validate(SkinDataEntries, [FNV1aHash($"Characters/{Settings.Character}/Skins/Skin{Settings.skinNo}")]);
            }

            if (SkinDataEntries.Count == 0)
            {
                x.LowerLog($"[MISS] SkinCharacterDataProperties", CLR_ERR);
                return (null, null, null, null, null);
            }
            var mainEntry = (BinEmbed)SkinDataEntries.Values.First().Value;

            BinValue? GetField(BinEmbed embed, uint hash)
                => embed.Items.FirstOrDefault(f => f.Key.Hash == hash)?.Value;

            uint CAC_name = (GetField(mainEntry, 0xd8f64a0d) as BinLink)?.Value.Hash ?? 0;
            uint RR_name = FNV1aHash($"Characters/{Settings.Character}/Skins/Skin{Settings.skinNo}/Resources");
            BinLink? rrLinkRef = GetField(mainEntry, 0x62286e7e) as BinLink;
            if (rrLinkRef != null)
            {
                rrLinkRef.Value = new FNV1a(RR_name);
            }

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
            if (Settings.verifyHpBar)
            {
                var targetField = mainEntry.Items.FirstOrDefault(f => f.Key.Hash == 0x51c83af8);

                if (targetField != null && targetField.Value is BinEmbed targetEmbed)
                {
                    var u8Field = targetEmbed.Items.FirstOrDefault(f => f.Key.Hash == 0x3fcb5693);

                    if (u8Field != null && u8Field.Value is BinU8 valU8)
                    {
                        if (valU8.Value != (byte)Settings.HealthbarStyle)
                        {
                            valU8.Value = (byte)Settings.HealthbarStyle;
                        }
                    }
                    else
                    {
                        targetEmbed.Items.Add(new BinField(new FNV1a(0x3fcb5693), new BinU8((byte)Settings.HealthbarStyle)));
                    }
                }
                else
                {
                    // Console.WriteLine("Creating missing 0x51c83af8 element.");

                    var newEmbed = new BinEmbed(new FNV1a(0x11b71b5e));

                    newEmbed.Items.Add(new BinField(new FNV1a(0x4d5ff2d7), new BinString("Buffbone_Cstm_Healthbar")));
                    newEmbed.Items.Add(new BinField(new FNV1a(0x3fcb5693), new BinU8((byte)Settings.HealthbarStyle)));

                    mainEntry.Items.Add(new BinField(new FNV1a(0x51c83af8), newEmbed));
                }
            }


            if (RREntries.Count > 1)
            {
                Validate(RREntries, [RR_name]);
            }
            if (CACEntries.Count > 1)
            {
                Validate(CACEntries, [CAC_name]);
            }
            if (AnimEntries.Count > 1)
            {
                Validate(AnimEntries, [anmgraph_name]);
            }
            if (Settings.verifyHpBar)
            {
                if (RREntries.Count == 0)
                {
                    x.LowerLog($"[MISS] Resource Resolver", CLR_ERR);
                }
                if (CACEntries.Count == 0)
                {
                    x.LowerLog($"[MISS] Contextual Action Data", CLR_ERR);
                }
                if (AnimEntries.Count == 0)
                {
                    x.LowerLog($"[MISS] Animations Definitions", CLR_ERR);
                }
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
            ScanStrings(AnimEntries);
            ScanStrings(OtherEntries);

            foreach (string characterToLoad in ExtraCharactersToLoad)
            {
                if (!Settings.CharraBlackList.Contains(characterToLoad, StringComparer.OrdinalIgnoreCase)) Characters.Enqueue((characterToLoad, Settings.skinNo, false));  
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
            MergeIntoFinal2(AnimEntries);
            MergeIntoFinal2(OtherEntries);

            return (finalMap, finalMap2, finalMap3, collectedStrings, linkedListtoReturn);
        }

        private void Validate(Dictionary<uint, KeyValuePair<BinValue, BinValue>> RREntries, List<uint> RR_names)
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

        public void FindStringsRecursive(BinValue value, List<WadExtractor.Target> results)
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
                            var string_out = s;
                            if (!Settings.binless)
                            {
                                string_out = _pathFixer.FixPath(s);

                            }
                            var hashes = new List<string> { s };

                            if (s.EndsWith(".tex", StringComparison.OrdinalIgnoreCase))
                            {
                                hashes.Add(Path.ChangeExtension(s, ".dds"));
                            }
                            else if (s.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
                            {
                                hashes.Add(Path.ChangeExtension(s, ".tex"));
                            }
                            WadExtractor.Target found = results.FirstOrDefault(t => t.OriginalPath == s);
                            if (found != null)
                            {
                                found.BinStringRef.Add(str);
                            }
                            else
                            {
                                results.Add(new WadExtractor.Target
                                {
                                    BinStringRef = [str],
                                    OriginalPath = s,
                                    Hashes = hashes,
                                    OutputPath = Settings.outputDir,
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

        public List<string> CollectWads(string rootFolder)
        {
            // Ensure we have a valid string to search for to avoid null reference exceptions
            var searchToken = Settings.Character ?? string.Empty;

            return Directory
                .EnumerateFiles(rootFolder, "*.wad.client", SearchOption.AllDirectories)
                .Select(Path.GetFullPath)
                .OrderBy(path =>
                {
                    var fileName = Path.GetFileName(path);

                    // If the filename contains the character name, return 0 (top priority).
                    // Otherwise, return 1 (bottom priority).
                    return fileName.Contains(searchToken, StringComparison.OrdinalIgnoreCase)
                        ? 0
                        : 1;
                })
                // Optional: Add a secondary sort (e.g., alphabetical) to keep the list stable
                .ThenBy(path => path)
                .ToList();
        }

        public static List<string> bonusPaths = [];
        public class WadExtractor
        {
            private FixerSettings _settings;
            public FixerUI x;
            public Hashes _hash;

            // Reusing log colors from parent
            private const string CLR_ACT = "#2a84d2";
            private const string CLR_GOOD = "#2dc55e";
            private const string CLR_MOD = "#5350b9";


            public WadExtractor(FixerSettings settings)
            {
                _settings = settings;
            }

            public List<int> GetAvailableSkinNumbers(List<string> wadPaths, string character)
            {
                var foundSkins = new HashSet<int>();
                var skinHashes = new Dictionary<ulong, int>();

                // Pre-calculate hashes for WAD file lookups
                for (int i = 0; i < 100; i++)
                {
                    string path = $"data/characters/{character}/skins/skin{i}.bin";
                    skinHashes[Repatheruwu.HashPath(path)] = i;
                }

                byte[] entryBuffer = new byte[32];

                foreach (var wadPath in wadPaths)
                {
                    // --- NEW LOGIC START ---
                    // If the path is a directory, check for loose files
                    if (Directory.Exists(wadPath))
                    {
                        for (int i = 0; i < 100; i++)
                        {
                            // Reconstruct the internal path structure
                            string relativePath = $"data/characters/{character}/skins/skin{i}.bin";

                            // Combine the directory with the relative path
                            string fullPath = Path.Combine(wadPath, relativePath);

                            if (File.Exists(fullPath))
                            {
                                foundSkins.Add(i);
                            }
                        }
                        continue; // Skip the WAD reading logic for this iteration
                    }
                    // --- NEW LOGIC END ---

                    // Existing logic for .wad files
                    if (!File.Exists(wadPath)) continue;

                    using (var fs = new FileStream(wadPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var br = new BinaryReader(fs))
                    {
                        if (fs.Length < 272) continue;
                        fs.Seek(268, SeekOrigin.Begin);
                        uint fileCount = br.ReadUInt32();

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

            public void PackDirectoryToWad(string sourceDirectory, string outputWadPath)
            {
                if (!Directory.Exists(sourceDirectory))
                    throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory}");

                string outputDir = Path.GetDirectoryName(outputWadPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

                var files = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
                var entries = new WadEntryInfo[files.Length];

                var tempPaths = new ConcurrentBag<string>();

                Parallel.For(0, files.Length, i =>
                {
                    string file = files[i];
                    string relativePath = Path.GetRelativePath(sourceDirectory, file);
                    string wadPath = relativePath.Replace('\\', '/').ToLowerInvariant();
                    tempPaths.Add(wadPath);
                    byte[] fileBytes = File.ReadAllBytes(file);

                    entries[i] = new WadEntryInfo
                    {
                        FilePath = file,
                        PathHash = Repatheruwu.HashPath(wadPath),
                        DataChecksum = BitConverter.ToUInt64(XxHash64.Hash(fileBytes)),
                        Size = (uint)fileBytes.Length
                    };
                });
                bonusPaths.AddRange(tempPaths);

                Array.Sort(entries, (a, b) => a.PathHash.CompareTo(b.PathHash));
                ulong tocChecksum = 0;
                foreach (var e in entries) tocChecksum ^= e.DataChecksum;

                using (var fs = new FileStream(outputWadPath, FileMode.Create, FileAccess.Write))
                using (var bw = new BinaryWriter(fs))
                {
                    bw.Write(new char[] { 'R', 'W' });
                    bw.Write((byte)3);
                    bw.Write((byte)4);
                    bw.Write(new byte[256]);
                    bw.Write(tocChecksum);
                    bw.Write((uint)entries.Length);

                    uint dataStartOffset = 272 + ((uint)entries.Length * 32);
                    uint absoluteOffset = dataStartOffset;

                    foreach (var entry in entries)
                    {
                        bw.Write(entry.PathHash);
                        bw.Write(absoluteOffset);
                        bw.Write(entry.Size);
                        bw.Write(entry.Size);
                        bw.Write((byte)0);
                        bw.Write((byte)0);
                        bw.Write((ushort)0);
                        bw.Write(entry.DataChecksum);

                        absoluteOffset += entry.Size;
                    }

                    byte[] copyBuffer = new byte[81920];
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

            public (uint version, uint id) CheckLanguageID(List<string> wadPaths, string target)
            {
                ulong targetHash = Repatheruwu.HashPath(target);
                byte[] entryBuffer = new byte[32];

                foreach (var wadPath in wadPaths)
                {
                    if (!File.Exists(wadPath)) continue;

                    using (var fs = new FileStream(wadPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var br = new BinaryReader(fs))
                    {
                        if (fs.Length < 272) continue;

                        // Read File Count
                        fs.Seek(268, SeekOrigin.Begin);
                        uint fileCount = br.ReadUInt32();

                        for (int i = 0; i < fileCount; i++)
                        {
                            if (fs.Read(entryBuffer, 0, 32) != 32) break;

                            ulong pathHash = BitConverter.ToUInt64(entryBuffer, 0);

                            if (pathHash == targetHash)
                            {
                                // Found the specific file entry
                                uint offset = BitConverter.ToUInt32(entryBuffer, 8);
                                uint compressedSize = BitConverter.ToUInt32(entryBuffer, 12);
                                byte type = entryBuffer[20];

                                // Read the file data
                                fs.Seek(offset, SeekOrigin.Begin);
                                byte[] fileData = new byte[compressedSize];
                                if (fs.Read(fileData, 0, (int)compressedSize) != compressedSize) continue;

                                // Handle Decompression (Zstd/Gzip/Raw)
                                byte[] rawData;
                                var rawSpan = new ReadOnlySpan<byte>(fileData);

                                if (IsZstd(rawSpan) || type == 3)
                                {
                                    try { rawData = DecompressZstd(fileData, fileData.Length); }
                                    catch { continue; }
                                }
                                else if (IsGzip(rawSpan) || type == 1)
                                {
                                    try { rawData = DecompressGzip(fileData, fileData.Length); }
                                    catch { continue; }
                                }
                                else
                                {
                                    rawData = fileData;
                                }

                                // --- READ VERSION AND ID ---
                                // We need at least 20 bytes (reading up to offset 16 + 4 bytes)
                                if (rawData.Length >= 20)
                                {
                                    uint version = BitConverter.ToUInt32(rawData, 8);  // 0x08
                                    uint id = BitConverter.ToUInt32(rawData, 16);      // 0x10
                                    return (version, id);
                                }

                                return (0, 0); // Found but too small
                            }
                        }
                    }
                }

                return (0, 0);
            }
            public class Target
            {
                public List<BinString> BinStringRef { get; set; }
                public string OriginalPath { get; set; }
                public List<string> Hashes { get; set; }
                public string OutputPath { get; set; }
                public string OutputString { get; set; }
            }

            private struct ExtractionJob
            {
                public ulong Hash;
                public uint Offset;
                public uint CompressedSize;
                public uint UncompressedSize;
                public byte Type;
                public Target Target;
                public string Extension;
            }

            public List<Target> ExtractAndSwapReferences(List<string> wadPaths, List<Target> targets, string logColor = CLR_GOOD)
            {
                if (targets == null || targets.Count == 0) return targets;
                if (wadPaths.Count == 0) return targets;

                var lookup = new Dictionary<ulong, (Target target, string ext, int priority)>();
                int pendingCount = 0;

                foreach (var t in targets)
                {
                    bool added = false;
                    for (int i = 0; i < t.Hashes.Count; i++)
                    {
                        var h = t.Hashes[i];
                        ulong hashVal = Repatheruwu.HashPath(h);
                        if (!lookup.ContainsKey(hashVal))
                        {
                            lookup[hashVal] = (t, Path.GetExtension(h), i);
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

                        var bestCandidates = new Dictionary<Target, (ExtractionJob job, int priority)>();

                        for (int i = 0; i < fileCount; i++)
                        {
                            if (fs.Read(entryBuffer, 0, 32) != 32) break;
                            ulong pathHash = BitConverter.ToUInt64(entryBuffer, 0);

                            if (lookup.TryGetValue(pathHash, out var entry))
                            {
                                var newJob = new ExtractionJob
                                {
                                    Hash = pathHash,
                                    Target = entry.target,
                                    Extension = entry.ext,
                                    Offset = BitConverter.ToUInt32(entryBuffer, 8),
                                    CompressedSize = BitConverter.ToUInt32(entryBuffer, 12),
                                    Type = entryBuffer[20]
                                };

                                if (!bestCandidates.ContainsKey(entry.target) || entry.priority < bestCandidates[entry.target].priority)
                                {
                                    bestCandidates[entry.target] = (newJob, entry.priority);
                                }
                            }
                        }

                        if (bestCandidates.Count == 0) continue;

                        var jobs = bestCandidates.Values.Select(x => x.job).ToList();

                        foreach (var job in jobs)
                        {
                            foreach (var h in job.Target.Hashes)
                                lookup.Remove(Repatheruwu.HashPath(h));
                        }

                        if (jobs.Count == 0) continue;

                        jobs.Sort((a, b) => a.Offset.CompareTo(b.Offset));

                        foreach (var job in jobs)
                        {
                            fs.Seek(job.Offset, SeekOrigin.Begin);
                            byte[] poolBuffer = ArrayPool<byte>.Shared.Rent((int)job.CompressedSize);

                            try
                            {
                                int bytesRead = fs.Read(poolBuffer, 0, (int)job.CompressedSize);
                                var rawSpan = new ReadOnlySpan<byte>(poolBuffer, 0, bytesRead);

                                byte[] finalData;

                                if (IsZstd(rawSpan) || job.Type == 3)
                                {
                                    try { finalData = DecompressZstd(poolBuffer, bytesRead); }
                                    catch { finalData = rawSpan.ToArray(); }
                                }
                                else if (IsGzip(rawSpan) || job.Type == 1)
                                {
                                    finalData = DecompressGzip(poolBuffer, bytesRead);
                                }
                                else
                                {
                                    finalData = rawSpan.ToArray();
                                }
                                string final_out = job.Target.OutputString;
                                if (!_settings.binless)
                                {
                                    final_out = Path.ChangeExtension(job.Target.OutputString, job.Extension);
                                }
                                if (string.IsNullOrEmpty(Path.GetFileNameWithoutExtension(final_out))) final_out = Path.Combine(Path.GetDirectoryName(final_out) ?? "", $"dot_{Guid.NewGuid().ToString().Substring(0, 4)}{job.Extension}");
                                if (!string.IsNullOrEmpty(job.Target.OutputPath))
                                {
                                    string outPath = Path.Combine(job.Target.OutputPath, final_out);
                                    string dir = Path.GetDirectoryName(outPath);

                                    if (!createdDirectories.Contains(dir))
                                    {
                                        Directory.CreateDirectory(dir);
                                        createdDirectories.Add(dir);
                                    }

                                    File.WriteAllBytes(outPath, finalData);
                                }

                                if (job.Target.BinStringRef != null)
                                {
                                    string left = job.Target.OriginalPath.Length > 55
    ? $"{job.Target.OriginalPath[..26]}...{job.Target.OriginalPath[^26..]}"
    : job.Target.OriginalPath;

                                    string right = final_out.Length > 55
                                        ? $"{final_out[..26]}...{final_out[^26..]}"
                                        : final_out;

                                    // Determine if extension changed for color coding
                                    bool extChanged = !string.Equals(Path.GetExtension(job.Target.OriginalPath), Path.GetExtension(final_out), StringComparison.OrdinalIgnoreCase);
                                    string logTag = extChanged ? "[FIXD]" : "[GOOD]";
                                    string log_c = extChanged ? CLR_MOD : logColor;
                                    x.UpperLog($"{logTag} {left,-55} --> {right,-55}", log_c);

                                    string outRef = final_out;
                                    foreach (BinString s in job.Target.BinStringRef)
                                    {
                                        s.Value = outRef;
                                    }
                                }
                                targets.Remove(job.Target);
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(poolBuffer);
                            }
                        }
                    }
                }


                return targets;
            }

            public List<Target> FindAndSwapReferences(List<string> wadPaths, List<Target> targets)
            {
                if (targets == null || targets.Count == 0) return targets;
                if (wadPaths.Count == 0) return targets;

                // Dictionary to map Hash -> (Target Object, Index in the Hashes List)
                var lookup = new Dictionary<ulong, (Target target, int index)>();
                int pendingCount = 0;

                foreach (var t in targets)
                {
                    bool added = false;
                    // Iterate through all possible hashes for this target
                    for (int i = 0; i < t.Hashes.Count; i++)
                    {
                        var h = t.Hashes[i];
                        ulong hashVal = Repatheruwu.HashPath(h);

                        // Only add if not already present (prioritizing the first occurrence if duplicates exist)
                        if (!lookup.ContainsKey(hashVal))
                        {
                            lookup[hashVal] = (t, i);
                            added = true;
                        }
                    }
                    if (added) pendingCount++;
                }

                // Buffer to read directory entries (32 bytes per file entry)
                byte[] entryBuffer = new byte[32];

                foreach (var wadPath in wadPaths)
                {
                    // If we have found everything, stop looking
                    if (lookup.Count == 0) break;
                    if (!File.Exists(wadPath)) continue;

                    using (var fs = new FileStream(wadPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var br = new BinaryReader(fs))
                    {
                        if (fs.Length < 272) continue;

                        // Jump to file count in WAD header
                        fs.Seek(268, SeekOrigin.Begin);
                        uint fileCount = br.ReadUInt32();

                        // Store the best match found in this specific WAD
                        // Key: Target, Value: Index in the Hashes list (lower is better priority)
                        var bestCandidates = new Dictionary<Target, int>();

                        for (int i = 0; i < fileCount; i++)
                        {
                            // Read the 32-byte entry
                            if (fs.Read(entryBuffer, 0, 32) != 32) break;

                            // The first 8 bytes are the Path Hash
                            ulong pathHash = BitConverter.ToUInt64(entryBuffer, 0);

                            if (lookup.TryGetValue(pathHash, out var entry))
                            {
                                // If this target isn't in candidates yet, OR this new match has a higher priority (lower index)
                                if (!bestCandidates.ContainsKey(entry.target) || entry.index < bestCandidates[entry.target])
                                {
                                    bestCandidates[entry.target] = entry.index;
                                }
                            }
                        }

                        if (bestCandidates.Count == 0) continue;

                        // Process the matches found in this WAD
                        foreach (var candidate in bestCandidates)
                        {
                            Target t = candidate.Key;
                            int hashIndex = candidate.Value;

                            // Retrieve the actual string that exists in the WAD
                            string foundString = t.Hashes[hashIndex];

                            // Remove all hashes for this target from lookup so we don't process it again in other WADs
                            foreach (var h in t.Hashes)
                            {
                                lookup.Remove(Repatheruwu.HashPath(h));
                            }

                            if (t.BinStringRef != null)
                            {
                                // Update the references to the string we actually found
                                foreach (BinString s in t.BinStringRef)
                                {
                                    s.Value = foundString;
                                }

                                // --- Logging (Reusing your style) ---
                                string left = t.OriginalPath.Length > 55
                                    ? $"{t.OriginalPath[..26]}...{t.OriginalPath[^26..]}"
                                    : t.OriginalPath;

                                string right = foundString.Length > 55
                                    ? $"{foundString[..26]}...{foundString[^26..]}"
                                    : foundString;

                                // Determine if the path changed (e.g. extension fix or hash fallback)
                                bool pathChanged = !string.Equals(t.OriginalPath, foundString, StringComparison.OrdinalIgnoreCase);

                                // Assuming 'x' is your logger instance from the original scope
                                x.UpperLog($"[UPDT] {left,-55} --> {right,-55}", CLR_MOD);
                            }

                            // Finally, remove the processed target from the list
                            targets.Remove(t);
                        }
                    }
                }

                return targets;
            }

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
                var decompressor = new Decompressor();
                var span = new ReadOnlySpan<byte>(data, 0, length);
                return decompressor.Unwrap(span.ToArray()).ToArray();
            }
            public void PackDirectoryToWadCompressed(string sourceDirectory, string outputWadPath)
            {
                if (!Directory.Exists(sourceDirectory))
                    throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory}");

                string outputDir = Path.GetDirectoryName(outputWadPath);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

                var files = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);

                // We use a temporary container to hold the compressed data in memory 
                // because we need exact sizes for the TOC before writing the file body.
                var entryData = new ConcurrentDictionary<ulong, byte[]>();
                var entries = new WadEntryInfo[files.Length];

                // 1. Compress and Hash in Parallel
                Parallel.For(0, files.Length, i =>
                {
                    string file = files[i];
                    string relativePath = Path.GetRelativePath(sourceDirectory, file);
                    string wadPath = relativePath.Replace('\\', '/').ToLowerInvariant();

                    byte[] originalBytes = File.ReadAllBytes(file);
                    ulong pathHash = Repatheruwu.HashPath(wadPath);
                    ulong dataChecksum = BitConverter.ToUInt64(XxHash64.Hash(originalBytes)); // Checksum is usually of the original data

                    byte[] finalData;

                    byte type;

                    // Attempt Compression (Zstd Level 22 is usually 'Ultra')
                    // Using a disposable compressor context if your library requires it
                    using (var compressor = new Compressor(22))
                    {
                        byte[] compressedBytes = compressor.Wrap(originalBytes).ToArray();

                        // If compression actually saves space, use it. Otherwise, store raw.
                        if (compressedBytes.Length < originalBytes.Length)
                        {
                            finalData = compressedBytes;
                            type = 3; // ZSTD Type
                        }
                        else
                        {
                            finalData = originalBytes;
                            type = 0; // Uncompressed
                        }
                    }

                    // Store the data for the writing phase
                    entryData[pathHash] = finalData;

                    // Fill metadata (Size = Compressed Size, UncompressedSize = Original)
                    entries[i] = new WadEntryInfo
                    {
                        FilePath = file, // Kept for reference, though we use entryData now
                        PathHash = pathHash,
                        DataChecksum = dataChecksum,
                        Size = (uint)finalData.Length,
                        // We'll store the Type in the unused high bits or just handle it during write. 
                        // Since the struct doesn't have a Type field, let's piggyback or just rely on the logic below.
                        // However, the cleanest way without changing your struct is to assume 
                        // if Size < OriginalSize (we need to track OriginalSize somewhere).
                        // Let's assume we modify WadEntryInfo or use a Tuple. 
                        // For this snippet, I will modify the write logic to look up the type from the data comparison
                        // or simpler: I'll assume you add a 'Type' field or 'UncompressedSize' to WadEntryInfo. 
                        // But sticking to your provided struct:
                    };

                    // HACK: Since WadEntryInfo definition in your snippet is fixed, 
                    // and we need to pass UncompressedSize and Type to the writer:
                    // I will use a local dictionary to store the extra metadata needed for the TOC.
                });

                // 2. Sort entries to match standard WAD structure (by PathHash)
                Array.Sort(entries, (a, b) => a.PathHash.CompareTo(b.PathHash));

                ulong tocChecksum = 0;
                foreach (var e in entries) tocChecksum ^= e.DataChecksum;

                using (var fs = new FileStream(outputWadPath, FileMode.Create, FileAccess.Write))
                using (var bw = new BinaryWriter(fs))
                {
                    // --- Header ---
                    bw.Write(new char[] { 'R', 'W' });
                    bw.Write((byte)3);
                    bw.Write((byte)4);
                    bw.Write(new byte[256]);
                    bw.Write(tocChecksum);
                    bw.Write((uint)entries.Length);

                    // Calculate Start of Data Area
                    uint dataStartOffset = 272 + ((uint)entries.Length * 32);
                    uint absoluteOffset = dataStartOffset;

                    // --- Write TOC ---
                    foreach (var entry in entries)
                    {
                        byte[] data = entryData[entry.PathHash];

                        // We need to retrieve the uncompressed size. 
                        // Since we don't have it in the struct, we can infer it or (better) 
                        // re-read the original file size or store it in a parallel list. 
                        // For safety/speed here, let's grab the file info.
                        uint uncompressedSize = (uint)new FileInfo(entry.FilePath).Length;

                        // Determine type based on data comparison
                        byte compressionType = (data.Length < uncompressedSize) ? (byte)3 : (byte)0;

                        bw.Write(entry.PathHash);        // 0-7
                        bw.Write(absoluteOffset);        // 8-11: Offset
                        bw.Write((uint)data.Length);     // 12-15: Compressed Size
                        bw.Write(uncompressedSize);      // 16-19: Uncompressed Size
                        bw.Write(compressionType);       // 20: Type (3 = Zstd, 0 = None)
                        bw.Write((byte)0);               // 21
                        bw.Write((ushort)0);             // 22-23
                        bw.Write(entry.DataChecksum);    // 24-31

                        absoluteOffset += (uint)data.Length;
                    }

                    // --- Write Data ---
                    foreach (var entry in entries)
                    {
                        byte[] data = entryData[entry.PathHash];
                        bw.Write(data);

                        // Help GC slightly by removing reference (optional)
                        entryData.TryRemove(entry.PathHash, out _);
                    }
                }
            }
        }

        public class PathFixer
        {
            private FixerSettings _settings;
            public PathFixer(FixerSettings settings)
            {
                _settings = settings;
            }

            static readonly string[] Roots = { "assets", "data" };

            static readonly string[] Categories =
            {
                "characters", "items", "loadouts", "maps", "particles",
                "perks", "rewards", "shared", "sounds", "spells", "ux"
            };

            public string FixPath(string finalPath)
            {
                if (_settings.cls_assets)
                    finalPath = CleanRootPath(finalPath);

                string norm = finalPath.Replace("\\", "/");
                string[] parts = norm.Split('/', StringSplitOptions.RemoveEmptyEntries);

                string firstFolder = parts.Length > 0 ? parts[0].ToLower() : "";
                string ext = parts.Length > 0
                    ? Path.GetExtension(parts[^1]).ToLower()
                    : "";

                string repath = _settings.repath_path_path;
                bool inFilePath = _settings.in_file_path;

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

                bool isFileOnly = parts.Length == 1;

                if (isFileOnly)
                {
                    return $"ASSETS/{repath}/{parts[0]}";
                }

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

            static string CleanRootPath(string path)
            {
                string pathNorm = path.Replace("\\", "/").ToLower();
                string[] parts = pathNorm.Split('/', StringSplitOptions.RemoveEmptyEntries);

                string root = null;
                int rootIndex = -1;

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

                var rest = parts.Skip(categoryIndex + 1);
                return string.Join("/", new[] { root, category }.Concat(rest));
            }
        }

        public class Hashes
        {
            private FixerSettings _settings;
            // Cached paths are now per-instance to allow settings isolation
            private List<string> _cachedPaths;
            public FixerUI x;

            public Hashes(FixerSettings settings)
            {
                _settings = settings;
            }

            private List<string> LoadPathsOnly(string path)
            {
                var paths = new List<string>();

                if (!File.Exists(path)) return paths;

                foreach (var line in File.ReadLines(path))
                {
                    int spaceIndex = line.IndexOf(' ');
                    if (spaceIndex >= 0 && spaceIndex < line.Length - 1)
                    {
                        paths.Add(line.Substring(spaceIndex + 1).Trim());
                    }
                }

                return paths;
            }

            private List<string> GetCachedPaths()
            {
                if (_cachedPaths == null)
                {
                    _cachedPaths = LoadPathsOnly(_settings.gamehashes_path);
                }
                return _cachedPaths;
            }

            public List<WadExtractor.Target> FindMatches(List<WadExtractor.Target> targets, bool useBaseName = true)
            {
                var loadedPaths = GetCachedPaths();
                loadedPaths.AddRange(bonusPaths);

                foreach (var target in targets)
                {
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

            private string GetDataRelativePath(string path)
            {
                string p = path.Replace("\\", "/").ToLowerInvariant();
                int idx = p.IndexOf("data/", StringComparison.OrdinalIgnoreCase);

                string relative = (idx != -1) ? p.Substring(idx + 5) : p;
                if (relative.Length == 0) return "";

                int cutoff = (int)Math.Round(relative.Length * (_settings.percent / 100.0));
                return relative.Substring(0, cutoff);
            }

        }

    }
}