using Jade.Ritobin;
using SharpCompress.Common;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.IO.Hashing;
using System.IO.Packaging;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Shapes;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using ZstdSharp;
using static ModManager.Repatheruwu;
using Path = System.IO.Path;
using SearchOption = System.IO.SearchOption;

namespace ModManager
{
    public interface IFixerLogger
    {
        void UpperLog(string text, string hexColor = "#FFFFFF");
        void LowerLog(string text, string hexColor = "#FFFFFF");
    }
    // 2. Updated Dummy logger that saves to a text file
    public class DummyLogger : IFixerLogger
    {
        private readonly string _filePath;

        // You can change the default filename here or pass it in the constructor
        public DummyLogger(string filePath = "fixer_log.txt")
        {
            _filePath = filePath;
        }

        public void UpperLog(string text, string hexColor = "#FFFFFF")
        {
            // WriteToFile(text, hexColor);
        }

        public void LowerLog(string text, string hexColor = "#FFFFFF")
        {
            // WriteToFile(text, hexColor);
        }

        private void WriteToFile(string text, string hexColor)
        {
            try
            {
                // Requested format: color:{text}
                string line = $"{hexColor}:{text}{Environment.NewLine}";

                // Appends the line to the file, creating it if it doesn't exist
                File.AppendAllText(_filePath, line);
            }
            catch
            {
                // Ideally, a logger shouldn't crash the app if file I/O fails
            }
        }
    }

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
        public string shaderhashes_path { get; set; } = "cslol-tools\\hashes.shaders.txt";
        public List<string> base_wad_path { get; set; } = [];
        public List<string> OldLookUp { get; set; } = [];

        public List<string> LangLookUp { get; set; } = [];
        public bool Lang { get; set; } = true; // good

        public bool keep_Icons { get; set; } = true; // good
        public bool KillStaticMat { get; set; } = false; // good
        public bool sfx_events { get; set; } = false; // good
        public bool folder { get; set; } = true; // good
        public bool binless { get; set; } = false; // good
        public bool SmallMod { get; set; } = true; // good
        public bool SkipCheckup { get; set; } = false; // good
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
        public List<ShaderEntry> shaders { get; set; } = null;
        public string ManfiestDL { get; set; } = Path.Combine("cslol-tools", "ManifestDownloader.exe");
    }
    public class ShaderEntry
    {
        public uint Hash { get; set; }
        public string Path { get; set; }
        public bool Exists { get; set; } = false; // Default to false until verified
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
        private IFixerLogger x;

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
        private Dictionary<uint, ShaderEntry> _byHash = new Dictionary<uint, ShaderEntry>();
        private Dictionary<string, List<(string, ShaderEntry)>> _byPath = new Dictionary<string, List<(string, ShaderEntry)>>();
        private void FetchShaders()
        {
            string shaderbinpath = "data/shaders/shaders.bin";
            Settings.shaders = new List<ShaderEntry>();

            if (!File.Exists(Settings.shaderhashes_path))
            {
                Console.WriteLine($"[EROR] Shader Hashes are Missing");
                return;
            }

            // Read all lines at once
            string[] lines = File.ReadAllLines(Settings.shaderhashes_path);

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Split into [HashString, Path]
                string[] parts = line.Trim().Split(new[] { ' ' }, 2);
                if (parts.Length < 2) continue;

                if (!uint.TryParse(parts[0], NumberStyles.HexNumber, null, out uint hash))
                {
                    // Skip if the hash isn't a valid hex number
                    continue;
                }

                string path = parts[1];

                var entry = new ShaderEntry { Hash = hash, Path = path, Exists = false };

                // 3. STORE
                if (!_byHash.ContainsKey(hash)) _byHash.Add(hash, entry);
            }




            WadExtractor.Target Shader = new WadExtractor.Target
            {
                Hashes = new List<string> { shaderbinpath },
                OutputPath = Settings.inputDir,
                OutputString = $"{HashPath(shaderbinpath).ToString("x16")}.bin",
                BinStringRef = null,
                OriginalPath = shaderbinpath,
            };
            _wadExtractor.ExtractAndSwapReferences([Path.Combine(Settings.WADpath, "Shaders", "Shaders.wad.client")], [Shader]);
            Bin bin = null;
            try
            {
                bin = LoadBin(shaderbinpath);
            }
            catch (Exception e)
            {
                x.UpperLog($"[FAIL] Failed to load {shaderbinpath}, {e}", CLR_WARN);
                return;
            }
            if (bin == null) return;

            HashSet<uint> existingHashes = new HashSet<uint>();
            if (bin.Sections.TryGetValue("entries", out BinValue entriesSection) && entriesSection is BinMap entriesMap)
            {
                foreach (var kvp in entriesMap.Items)
                {
                    var entryKey = (BinHash)kvp.Key;
                    var entryData = (BinEmbed)kvp.Value;
                    uint hash = entryKey.Value.Hash;
                
                    if (hash == 0) continue;
                
                    if (entryData.Name.Hash == 0x205255d6) // CustomShaderDef
                    {
                        existingHashes.Add(entryKey.Value.Hash);
                    }
                }
            }
            foreach (var kvp in _byHash)
            {
                if (existingHashes.Contains(kvp.Key))
                {
                    kvp.Value.Exists = true;
                    string[] parts = kvp.Value.Path.Split("/");
                    if (parts.Length < 3) continue;
                    if (!_byPath.ContainsKey(parts[1]))
                    {
                        _byPath.Add(parts[1], [(parts[2], kvp.Value)]);
                    }
                    else
                    {
                        _byPath[parts[1]].Add((parts[2], kvp.Value));
                    }
                }
            }
        }

        public (uint, string, string) GetShaderOrFallback(uint targetHash)
        {
            if (!_byHash.TryGetValue(targetHash, out ShaderEntry originalPath))
            {
                x.LowerLog($"[FAIL] Unknown Shader hash uint:{targetHash}");
                return (targetHash, "", "");
            }
            if (originalPath.Exists)
            {
                return (targetHash, originalPath.Path, ""); ;
            }
            else
            {
                x.LowerLog($"[FIXI] Updating Shader: {originalPath.Path}", CLR_MOD);
                return FindFallbackHash(originalPath);
            }

        }

        public (uint, string, string) FindFallbackHash(ShaderEntry Shader)
        {
            string[] parts = Shader.Path.Split("/");
            if (parts.Length < 3) return (Shader.Hash, Shader.Path, "");

            if (!_byPath.TryGetValue(parts[1], out var candidates))
            {
                return (Shader.Hash, Shader.Path, "");
            }

            HashSet<string> targetTokens = new HashSet<string>(parts[2].Split('_'));

            ShaderEntry bestMatch = null;
            int bestMutualCount = -1;
            int bestNonMutualCount = int.MaxValue;

            foreach (var (candName, candEntry) in candidates)
            {
                string[] candidateTokens = candName.Split('_');

                int mutual = 0;
                int nonMutual = 0;

                foreach (var token in candidateTokens)
                {
                    if (targetTokens.Contains(token))
                    {
                        mutual++;
                    }
                    else
                    {
                        nonMutual++;
                    }
                }

                if (mutual == 0) continue;

                bool isBetter = false;

                if (mutual > bestMutualCount)
                {
                    isBetter = true;
                }
                else if (mutual == bestMutualCount)
                {
                    if (nonMutual < bestNonMutualCount)
                    {
                        isBetter = true;
                    }
                }
                if (isBetter)
                {
                    bestMutualCount = mutual;
                    bestNonMutualCount = nonMutual;
                    bestMatch = candEntry;
                }
            }
            if (bestMatch is not null)
            {
                return (bestMatch.Hash, Shader.Path, bestMatch.Path);
            }
            else
            {
                return (Shader.Hash, Shader.Path,"");
            }
        }

        public void FixiniYoursSkini(IFixerLogger ui = null)
        {
            this.x = ui ?? new DummyLogger();
            Settings.AllWadPaths = CollectWads(Settings.WADpath);
            _wadExtractor.x = this.x;
            string tmp = Path.Combine(Path.GetTempPath(), "cslolgo_fixer_" + Guid.NewGuid().ToString());

            Directory.CreateDirectory(tmp);
            Settings.inputDir = tmp;

            if (Settings.binless) Settings.percent = 100;

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
            FetchShaders();
            List<string> seen = new List<string>();
            while (Characters.Count > 0)
            {
                var (Current_Char, skinNo, HpBar) = Characters.Dequeue();
                x.LowerLog($"[FIXI] Fixing {Current_Char} skin {skinNo}", CLR_ACT);

                if (seen.Contains($"{Current_Char}{skinNo}", StringComparer.OrdinalIgnoreCase)) continue;
                seen.Add($"{Current_Char}{skinNo}");

                Settings.Character = Current_Char;
                Settings.skinNo = skinNo;
                Settings.verifyHpBar = HpBar;
                string shortChar = Current_Char.Length > 4
    ? Current_Char.Substring(0, 4)
    : Current_Char;
                Settings.repath_path_path = $".{shortChar}{skinNo}_";
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
                    continue;
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
                    if (!Settings.KillStaticMat)
                    {
                        linkedList.Items.Add(new BinString(static_mat_path));
                    }
                    // else
                    // {
                    //     string static_mat_path_proxy = $"data/{Settings.Character}_skin{Settings.skinNo}_StaticMat_proxy.bin";
                    //     EmptyLinked.Items.Add(new BinString(static_mat_path));
                    //     var EmptyEntries = new BinMap(BinType.Hash, BinType.Embed);
                    //     x.LowerLog($"[SAVE] {static_mat_path_proxy}", CLR_ACT);
                    //     Save_Bin(EmptyLinked, EmptyEntries, $"{Settings.outputDir}/{static_mat_path_proxy}");
                    //     linkedList.Items.Add(new BinString(static_mat_path_proxy));
                    // }
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

                    List<int> tonoskin = _wadExtractor.GetAvailableSkinNumbers(Settings.AllWadPaths.Where(s => s.Contains($"{Current_Char}.wad.client", StringComparison.OrdinalIgnoreCase)).ToList(), Current_Char);

                    foreach (int i in tonoskin.Skip(1))
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
                _wadExtractor.PackDirectoryToWadCompressed(Settings.outputDir, $"{Settings.outputDir}.client");
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
                if (!Settings.SkipCheckup)
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
                        string VO_path = Path.Combine("manifests", $".lang_{Settings.bnk_version}_{kv.Key}");
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
                                FileName = Settings.ManfiestDL,
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

                        if (allPaths.Contains(partnerWpk, StringComparer.OrdinalIgnoreCase))
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
    t.OriginalPath.EndsWith("_sfx_events.bnk", StringComparison.OrdinalIgnoreCase);
                    ToCheckup.AddRange(processing.Where(IsSfxEvents));
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
                ToCheckup.AddRange(processing.Where(IsSound));
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
                bool IsANM(WadExtractor.Target t) =>
    string.Equals(Path.GetExtension(t.OriginalPath), ".anm", StringComparison.OrdinalIgnoreCase);
                ToCheckup.AddRange(processing.Where(IsANM));
                processing.RemoveAll(IsANM);
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

                        if (!remainingPaths.Contains(wpkPath, StringComparer.OrdinalIgnoreCase))
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
                ToCheckup.AddRange(processing.Where(IsSound));
                processing.RemoveAll(IsSound);
            }

            if (Settings.AnimOption == 0)
            {
                bool IsANM(WadExtractor.Target t) =>
    string.Equals(Path.GetExtension(t.OriginalPath), ".anm", StringComparison.OrdinalIgnoreCase);
                ToCheckup.AddRange(processing.Where(IsANM));
                processing.RemoveAll(IsANM);
            }

            processing = _wadExtractor.ExtractAndSwapReferences(Settings.OldLookUp, processing);
            if (processing.Count() == 0)
            {
                check_n_fix_vo();
                return processing;
            }
            if (!Settings.SmallMod)
            {
                if (Settings.SoundOption == 0)
                {
                    foreach (var pair in bnkToWpkMap)
                    {
                        WadExtractor.Target bnkTarget = pair.Key;
                        string wpkPath = pair.Value;

                        if (!remainingPaths.Contains(wpkPath, StringComparer.OrdinalIgnoreCase))
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
            }
            else
            {
                List<WadExtractor.Target> bnk = new List<WadExtractor.Target>();
                if (Settings.SoundOption == 0)
                {
                    foreach (var pair in bnkToWpkMap)
                    {
                        WadExtractor.Target bnkTarget = pair.Key;
                        string wpkPath = pair.Value;

                        if (!remainingPaths.Contains(wpkPath, StringComparer.OrdinalIgnoreCase))
                        {
                            bnk.Add(bnkTarget);
                        }
                    }
                    _wadExtractor.ExtractAndSwapReferences(Settings.AllWadPaths, bnk);
                }
                ToCheckup.AddRange(processing);
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
            if (parent is null || !parent.Equals("characters", StringComparison.OrdinalIgnoreCase))
                return false;

            var grandParent = Path.GetFileName(
                Path.GetDirectoryName(Path.GetDirectoryName(dir))
            );
            if (grandParent is null || !grandParent.Equals("data", StringComparison.OrdinalIgnoreCase))
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
            loaded_linked.Add(rootBinPath);
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
                catch (Exception e){
                    x.UpperLog($"[FAIL] Failed to read {trimmedPath}, {e}", CLR_ERR);
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
                        foreach (string left in notfound)
                        {
                            x.LowerLog($"[FAIL] Missing: {left}", CLR_ERR);
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

            foreach (var entry in StaticMatEntries)
            {
                if (entry.Value.Value is BinEmbed materialEmbed2)
                {
                    var embedListField = materialEmbed2.Items.FirstOrDefault(f => f.Key.Hash == 0x0a6f0eb5);

                    if (embedListField != null && embedListField.Value is BinList2 embedList)
                    {
                        // "for each 0x0904b150 in it" -> Iterate the Embeds inside the list
                        foreach (var listItem in embedList.Items)
                        {
                            if (listItem is BinEmbed innerEmbed)
                            {
                                // Iterate over the fields in the inner embed to find 0x0904b150
                                foreach (var field in innerEmbed.Items)
                                {
                                    if (field.Value is BinString strVal)
                                    {
                                        if (strVal.Value.Contains("."))
                                        {
                                            // Set key to 0x02e7fb4c
                                            field.Key = new FNV1a(0xf0a363e3);
                                        }
                                        else
                                        {
                                            // Set key to 0xb311d4ef
                                            field.Key = new FNV1a(0xb311d4ef);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    var techniquesField = materialEmbed2.Items.FirstOrDefault(f => f.Key.Hash == 0x844f384e);

                    if (techniquesField?.Value is BinList techniquesList)
                    {
                        foreach (var techniqueItem in techniquesList.Items)
                        {
                            if (techniqueItem is BinEmbed techniqueEmbed)
                            {
                                // 2. Find the "passes" list inside the technique
                                var passesField = techniqueEmbed.Items.FirstOrDefault(f => f.Key.Hash == 0x623cd25c);

                                if (passesField?.Value is BinList passesList)
                                {
                                    foreach (var passItem in passesList.Items)
                                    {
                                        if (passItem is BinEmbed passEmbed)
                                        {
                                            // 3. Find the "shader" link inside the pass (Hash: 0x355d5568)
                                            var shaderField = passEmbed.Items.FirstOrDefault(f => f.Key.Hash == 0x355d5568);

                                            if (shaderField?.Value is BinLink shaderLink)
                                            {
                                                // Capture the struct
                                                var fnv = shaderLink.Value;

                                                // Calculate the new hash
                                                uint originalHash = fnv.Hash;
                                                var (newHash, oldName, newName) = GetShaderOrFallback(originalHash);

                                                // Apply changes only if necessary
                                                if (originalHash != newHash)
                                                {
                                                    fnv.Hash = newHash;
                                                    fnv.String = null;

                                                    shaderLink.Value = fnv;
                                                    x.LowerLog($"[FIXD] Shader link {oldName} to {newName}", CLR_GOOD);
                                                }
                                                else
                                                {
                                                    x.LowerLog($"[GOOD] Shader link {oldName}", CLR_GOOD);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            foreach (var entry in VFXEntries)
            {
                // Ensure we are working with a BinEmbed (the root definition)
                if (entry.Value.Value is not BinEmbed vfxEmbed)
                    continue;
            
                uint[] validHashes = { 0x6781e762, 0x868eb76a };
            
                // 1. Find the fields that contain the Emitter Lists (0x868eb76a, etc.)
                // The user's snippet iterated Items directly, but the data dump shows these are Lists.
                // We iterate the fields of the main Embed.
                foreach (var listField in vfxEmbed.Items.Where(f => validHashes.Contains(f.Key.Hash)).ToList())
                {
                    x.UpperLog($"Found valid list hash: 0x{listField.Key.Hash:x8}");
            
                    // We expect the value to be a List of Pointers (The Emitters)
                    if (listField.Value is not BinList binList) continue;
            
                    // 2. Iterate through every Emitter in that list
                    foreach (var emitterValue in binList.Items)
                    {
                        if (emitterValue is not BinPointer emitterPointer) continue;
            
                        x.UpperLog($"Processing Emitter Pointer: {emitterPointer.Name}");
            
                        // 3. Find the Shape Attribute (0x9dc3d926) inside the Emitter
                        var shapeField = emitterPointer.Items.FirstOrDefault(f => f.Key.Hash == 0x9dc3d926);
            
                        if (shapeField == null) continue;
            
                        // The shape is typically an Embed or Pointer containing data
                        // We need to check if it has content.
                        List<BinField>? shapeItems = null;
                        if (shapeField.Value is BinEmbed shapeEmbed) shapeItems = shapeEmbed.Items;
                        else if (shapeField.Value is BinPointer shapePtr) shapeItems = shapePtr.Items;
            
                        if (shapeItems == null || shapeItems.Count == 0) continue;
            
                        x.UpperLog($"Found Shape Attribute 0x9dc3d926 with {shapeItems.Count} items.");
            
                        // Initialize the "ShitDict" logic state
                        bool emitRotationAnglesKeyValues = false;
                        bool emitRotationAxesShit = false;
                        bool flags = false;
                        bool keepItAs0x4f4e2ed7 = false;
            
                        float radiusVal = 0;
                        float heightVal = 0;
            
                        // We must iterate a Copy or use a for-loop because we might modify shapeItems inside
                        for (int i = 0; i < shapeItems.Count; i++)
                        {
                            var insideOfShape = shapeItems[i];
            
                            // --- Logic: 0xff7d0e41 (BirthTranslation handling) ---
                            if (insideOfShape.Key.Hash == 0xff7d0e41)
                            {
                                // Check contents of this inner structure
                                List<BinField>? innerItems = GetItemsFromBinValue(insideOfShape.Value);
                                if (innerItems != null)
                                {
                                    var vec3Field = innerItems.FirstOrDefault(f => f.Key.Hash == 0xb4b427aa && f.Value.Type == BinType.Vec3);
            
                                    if (vec3Field != null && vec3Field.Value is BinVec3 vec3Val)
                                    {
                                        x.UpperLog("Moving BirthTranslation to Emitter parent.");
            
                                        // Create new Embed for the Emitter
                                        var birthTranslation = new BinEmbed(new FNV1a(0x68dc32b6)); // hash_type
            
                                        // Add the vec3 to the new embed
                                        birthTranslation.Items.Add(new BinField(vec3Field.Key, new BinVec3(vec3Val.Value)));
            
                                        // Add this new field to the EMITTER (Parent of Shape)
                                        // hash = 0x563d4a22
                                        emitterPointer.Items.Add(new BinField(new FNV1a(0x563d4a22), birthTranslation));
            
                                        // Clear the data of the current shape item (inside_of_shape.data = [])
                                        if (insideOfShape.Value is BinEmbed innerEmbed) innerEmbed.Items.Clear();
                                        else if (insideOfShape.Value is BinPointer innerPtr) innerPtr.Items.Clear();
            
                                        // Python did a break here, implying specific handling for the first match
                                        // logic usually continues to next shape item though.
                                    }
                                }
                            }
            
                            // --- Logic: 0xe5f268dd (Extracting Radius/Height) ---
                            else if (insideOfShape.Key.Hash == 0xe5f268dd)
                            {
                                List<BinField>? innerItems = GetItemsFromBinValue(insideOfShape.Value);
                                if (innerItems != null)
                                {
                                    foreach (var item in innerItems)
                                    {
                                        if (item.Key.Hash == 0xb4b427aa && item.Value is BinVec3 v3)
                                        {
                                            radiusVal = v3.Value.X;
                                            heightVal = v3.Value.Y; // "lmao?"
                                            x.UpperLog($"Extracted Radius: {radiusVal}, Height: {heightVal}");
                                        }
                                        else if (item.Key.Hash == 0xbc037de7)
                                        {
                                            // Drill down: inside_of_emitoffset -> table_data -> shit -> smoll_shit
                                            AnalyzeNestedTable(item.Value, ref flags, ref keepItAs0x4f4e2ed7);
                                        }
                                    }
                                }
                            }
            
                            // --- Logic: 0x07f41838 ---
                            else if (insideOfShape.Key.Hash == 0x07f41838)
                            {
                                List<BinField>? innerItems = GetItemsFromBinValue(insideOfShape.Value);
                                if (innerItems != null)
                                {
                                    foreach (var valFloat in innerItems)
                                    {
                                        // "for stuff in value_float.data" implies structure depth
                                        List<BinField>? stuffItems = GetItemsFromBinValue(valFloat.Value);
                                        if (stuffItems == null) continue;
            
                                        foreach (var stuff in stuffItems)
                                        {
                                            if (stuff.Key.Hash == 0xbc037de7)
                                            {
                                                AnalyzeNestedTableForRotation(stuff.Value, ref emitRotationAnglesKeyValues);
                                            }
                                        }
                                    }
                                }
                            }
            
                            // --- Logic: 0xd1789c65 ---
                            else if (insideOfShape.Key.Hash == 0xd1789c65)
                            {
                                List<BinField>? innerItems = GetItemsFromBinValue(insideOfShape.Value);
                                // Looking for list length 2
                                // Python: list[vec3] = { { 0, 1, 0 } { 0, 0, 1 } }
                                // In Ritobin, a List BinValue works differently than Embed items.
                                // If `insideOfShape.Value` is a BinList:
                                if (insideOfShape.Value is BinList vecList && vecList.Items.Count == 2)
                                {
                                    if (vecList.Items[0] is BinVec3 v1 && vecList.Items[1] is BinVec3 v2)
                                    {
                                        if ((int)v1.Value.Y == 1 && (int)v2.Value.Z == 1)
                                        {
                                            emitRotationAxesShit = true;
                                            x.UpperLog("EmitRotationAxesShit set to True");
                                        }
                                    }
                                }
                            }
                        }
            
                        // --- Final Reconstruction Logic ---
            
                        // Python: shape.hash = 0x3bf0b4ed (This actually changes the KEY of the field in the parent list)
                        // But usually in these scripts, it implies transforming the Object Type Hash (Name).
                        // However, looking at the result logic:
                        // It constructs a new Object.
            
                        if (!keepItAs0x4f4e2ed7 && emitRotationAnglesKeyValues && emitRotationAxesShit)
                        {
                            x.UpperLog("Transforming Shape to 0x3dbe415d");
            
                            // Create new Pointer
                            var newPointer = new BinPointer(new FNV1a(0x3dbe415d)); // Hash Type
            
                            // Add Radius (0x0dba4cb3)
                            newPointer.Items.Add(new BinField(new FNV1a(0x0dba4cb3), new BinF32(radiusVal)));
            
                            // Add Height (0xd5bdbb42) if exists (check logic: python says if shit_dict.get("Height"))
                            // Note: heightVal is float, check if non-zero or logic requires specific check? 
                            // Assuming non-zero based on generic extraction logic
                            if (heightVal != 0)
                            {
                                newPointer.Items.Add(new BinField(new FNV1a(0xd5bdbb42), new BinF32(heightVal)));
                            }
            
                            // Add Flags (0x9c677a2c)
                            if (flags)
                            {
                                newPointer.Items.Add(new BinField(new FNV1a(0x9c677a2c), new BinU8(1)));
                            }
            
                            // Update the Field Key and Value
                            shapeField.Key = new FNV1a(0x3bf0b4ed);
                            shapeField.Value = newPointer;
                        }
                        else
                        {
                            // Else logic
                            // Check if shapeItems has 1 item, hash is 0xe5f268dd, and contains a Vector
                            bool isSimpleVec3 = false;
                            BinVec3? constantVec3 = null;
            
                            if (shapeItems.Count == 1 && shapeItems[0].Key.Hash == 0xe5f268dd)
                            {
                                // "isinstance(shape.data[0].data[0].data, Vector)"
                                // We need to check if the INNER item is a Vec3
                                List<BinField>? inner = GetItemsFromBinValue(shapeItems[0].Value);
                                if (inner != null && inner.Count > 0 && inner[0].Value is BinVec3 v3)
                                {
                                    isSimpleVec3 = true;
                                    constantVec3 = v3;
                                }
                            }
            
                            if (isSimpleVec3 && constantVec3 != null)
                            {
                                x.UpperLog("Transforming Shape to 0xee39916f (Simple Vec3)");
            
                                // Transform to Embed with type ee39916f
                                var newEmbed = new BinEmbed(new FNV1a(0xee39916f));
            
                                // Flatten: The field 0xe5f268dd now directly contains the vec3
                                newEmbed.Items.Add(new BinField(new FNV1a(0xe5f268dd), new BinVec3(constantVec3.Value)));
            
                                shapeField.Value = newEmbed;
                            }
                            else
                            {
                                x.UpperLog("Defaulting Shape to 0x4f4e2ed7");
                                // Default 0x4f4e2ed7
                                // In Ritobin, the "hash_type" is the Name of the Embed/Pointer
                                if (shapeField.Value is BinEmbed existingEmbed)
                                {
                                    existingEmbed.Name = new FNV1a(0x4f4e2ed7);
                                }
                                else if (shapeField.Value is BinPointer existingPtr)
                                {
                                    existingPtr.Name = new FNV1a(0x4f4e2ed7);
                                }
                            }
                        }
                    }
                }
            }


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
        private List<BinField>? GetItemsFromBinValue(BinValue value)
        {
            if (value is BinEmbed embed) return embed.Items;
            if (value is BinPointer ptr) return ptr.Items;
            return null; // Lists or primitives don't have BinField items directly
        }

        // "Drill down" logic for 0xbc037de7 -> 0xa7084719 -> smoll_shit
        private void AnalyzeNestedTable(BinValue rootValue, ref bool flags, ref bool keepIt)
        {
            // rootValue is the 0xbc037de7 container
            List<BinField>? tableData = GetItemsFromBinValue(rootValue);
            if (tableData == null) return;
        
            foreach (var td in tableData)
            {
                if (td.Key.Hash == 0xa7084719) // "list[pointer]" usually
                {
                    // The value here is likely a BinList containing items
                    if (td.Value is BinList list)
                    {
                        foreach (var shit in list.Items)
                        {
                            List<BinField>? smollShits = GetItemsFromBinValue(shit);
                            if (smollShits == null) continue;
        
                            foreach (var smoll_shit in smollShits)
                            {
                                if (smoll_shit.Key.Hash == 0xe44b7382)
                                {
                                    // Check data: python expects list[f32]
                                    if (smoll_shit.Value is BinList f32List && f32List.Items.Count >= 2)
                                    {
                                        float d0 = (f32List.Items[0] as BinF32)?.Value ?? -999;
                                        float d1 = (f32List.Items[1] as BinF32)?.Value ?? -999;
        
                                        if (d0 == 0 && d1 >= 1)
                                        {
                                            flags = true;
                                            x.UpperLog("Flags detected via nested table");
                                        }
                                        else if (d0 == -1 && d1 == 1)
                                        {
                                            keepIt = true;
                                            x.UpperLog("KeepItAs0x4f4e2ed7 detected via nested table");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        
        private void AnalyzeNestedTableForRotation(BinValue rootValue, ref bool emitRotation)
        {
            // rootValue is 0xbc037de7 container
            List<BinField>? tableData = GetItemsFromBinValue(rootValue);
            if (tableData == null) return;
        
            foreach (var td in tableData)
            {
                if (td.Key.Hash == 0xa7084719)
                {
                    if (td.Value is BinList list)
                    {
                        foreach (var shit in list.Items)
                        {
                            List<BinField>? smollShits = GetItemsFromBinValue(shit);
                            if (smollShits == null) continue;
        
                            foreach (var smoll_shit in smollShits)
                            {
                                if (smoll_shit.Key.Hash == 0xe44b7382)
                                {
                                    if (smoll_shit.Value is BinList f32List && f32List.Items.Count >= 2)
                                    {
                                        float d0 = (f32List.Items[0] as BinF32)?.Value ?? -999;
                                        float d1 = (f32List.Items[1] as BinF32)?.Value ?? -999;
        
                                        if (d0 == 0 && d1 > 1)
                                        {
                                            emitRotation = true;
                                            x.UpperLog("EmitRotationAnglesKeyValues detected");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
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
                            WadExtractor.Target found = results.FirstOrDefault(t => string.Equals(t.OriginalPath, s, StringComparison.OrdinalIgnoreCase));
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
            public IFixerLogger x;
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
                public uint UncompressedSize;
                public byte CompressionType;
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

                                    if (!createdDirectories.Contains(dir, StringComparer.OrdinalIgnoreCase))
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
                                x.UpperLog($"[UPDT] {left,-55} --> {right,-55}", pathChanged ? CLR_MOD : CLR_GOOD);
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
            private class ProcessedEntry
            {
                public WadEntryInfo Info;
                public byte[] Data;
            }

            public void PackDirectoryToWadCompressed(string sourceDirectory, string outputWadPath)
            {
                if (!Directory.Exists(sourceDirectory))
                    throw new DirectoryNotFoundException(sourceDirectory);

                string outputDir = Path.GetDirectoryName(outputWadPath);
                if (!string.IsNullOrEmpty(outputDir)) Directory.CreateDirectory(outputDir);

                var files = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);

                // FIX: Use an array of objects instead of a Dictionary to avoid threading crashes
                var processedEntries = new ProcessedEntry[files.Length];

                // -------------------------------
                // 1. Compress + hash (parallel)
                // -------------------------------
                Parallel.For(0, files.Length, i =>
                {
                    string file = files[i];
                    string relativePath = Path.GetRelativePath(sourceDirectory, file)
                        .Replace('\\', '/')
                        .ToLowerInvariant();

                    // cslol-tools uses XXH64 for path hashing
                    ulong pathHash = Repatheruwu.HashPath(relativePath);
                    byte[] originalBytes = File.ReadAllBytes(file);

                    // Logic matches cslol-tools (Magic bytes are safer, but extension works for basic mods)
                    bool rawOnly = relativePath.EndsWith(".bnk") || relativePath.EndsWith(".wpk");

                    byte[] finalData;
                    byte compressionType;

                    if (rawOnly)
                    {
                        finalData = originalBytes;
                        compressionType = 0; // Raw
                    }
                    else
                    {
                        // Zstd Level 0 in C++ maps to default level 3
                        using var compressor = new Compressor(3);
                        finalData = compressor.Wrap(originalBytes).ToArray();
                        compressionType = 3; // Zstd
                    }

                    // XXH3 Checksum of the FINAL (compressed) data
                    ulong checksum = XxHash3.HashToUInt64(finalData);

                    // Store directly in array slot 'i' (Thread-Safe)
                    processedEntries[i] = new ProcessedEntry
                    {
                        Info = new WadEntryInfo
                        {
                            FilePath = file,
                            PathHash = pathHash,
                            Size = (uint)finalData.Length,
                            UncompressedSize = (uint)originalBytes.Length,
                            CompressionType = compressionType,
                            DataChecksum = checksum
                        },
                        Data = finalData
                    };
                });

                // -------------------------------
                // 2. Sort by hash (WAD standard)
                // -------------------------------
                // We sort the combined array so Data stays with Info
                Array.Sort(processedEntries, (a, b) => a.Info.PathHash.CompareTo(b.Info.PathHash));

                // -------------------------------
                // 3.4 Requirement: TOC Checksum
                // -------------------------------
                ulong headerChecksum = CalculateTocChecksum(processedEntries);

                // -------------------------------
                // 3. Write WAD (Version 3.4)
                // -------------------------------
                using var fs = new FileStream(outputWadPath, FileMode.Create, FileAccess.Write);
                using var bw = new BinaryWriter(fs);

                // ---- Header (v3.4) ----
                bw.Write(new[] { 'R', 'W' });
                bw.Write((byte)3); // Major
                bw.Write((byte)4); // Minor (Updated to 4)
                bw.Write(new byte[256]); // ECDSA Signature (Empty)
                bw.Write(headerChecksum); // 3.4 Required Checksum
                bw.Write((uint)processedEntries.Length);

                // Calculate Data Start Offset
                // Header (272) + Entries (Count * 32 bytes)
                uint currentOffset = 272 + (uint)(processedEntries.Length * 32);

                // Deduplication Map: Maps DataChecksum -> FileOffset
                var writtenOffsets = new Dictionary<ulong, uint>();

                // ---- TOC ----
                foreach (var entry in processedEntries)
                {
                    // Deduplication: If we already wrote this data, point to it
                    if (!writtenOffsets.TryGetValue(entry.Info.DataChecksum, out uint entryOffset))
                    {
                        entryOffset = currentOffset;
                        writtenOffsets[entry.Info.DataChecksum] = currentOffset;
                        currentOffset += entry.Info.Size;
                    }

                    bw.Write(entry.Info.PathHash);
                    bw.Write(entryOffset);
                    bw.Write(entry.Info.Size);
                    bw.Write(entry.Info.UncompressedSize);

                    // v3.4 Entry: Type (4 bits) | SubChunkCount (4 bits)
                    // Usually count is 0.
                    bw.Write((byte)(entry.Info.CompressionType & 0xF));

                    // v3.4 Entry: SubChunkIndex (3 bytes / 24-bit)
                    // Writing 3 zeros
                    bw.Write((byte)0);
                    bw.Write((byte)0);
                    bw.Write((byte)0);

                    bw.Write(entry.Info.DataChecksum);
                }

                // ---- Data ----
                // Write distinct data chunks
                var writtenChecksums = new HashSet<ulong>();
                foreach (var entry in processedEntries)
                {
                    if (writtenChecksums.Add(entry.Info.DataChecksum))
                    {
                        bw.Write(entry.Data);
                    }
                }
            }

            // -------------------------------
            // Helper for WAD 3.4 Checksum
            // -------------------------------
            private ulong CalculateTocChecksum(ProcessedEntry[] sortedEntries)
            {
                var hasher = new XxHash3();
                // 3.4 Header Magic+Ver
                hasher.Append(new byte[] { (byte)'R', (byte)'W', 3, 4 });

                foreach (var e in sortedEntries)
                {
                    hasher.Append(BitConverter.GetBytes(e.Info.PathHash));
                    hasher.Append(BitConverter.GetBytes(e.Info.DataChecksum));
                }

                // GetCurrentHash() returns byte[], convert to ulong
                return BitConverter.ToUInt64(hasher.GetCurrentHash());
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
                    if (Categories.Contains(cleaned, StringComparer.OrdinalIgnoreCase))
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