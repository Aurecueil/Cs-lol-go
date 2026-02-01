using Microsoft.VisualBasic;
using Microsoft.Win32;
using ModLoader;
using ModPkgLibSpace;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using static ModManager.FixerUI;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using DataFormats = System.Windows.DataFormats;
using File = System.IO.File;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;
using SystemColors = System.Windows.SystemColors;

namespace ModManager
{
    public class HierarchyElement
    {
        public string Name { get; set; }
        public bool override_ { get; set; } = false;
        public int Priority { get; set; } = 10;
        public bool Random { get; set; } = false;
        public int ID { get; set; }
        public int parent { get; set; } = 0;

        public bool isActive { get; set; } = false;

        public string InnerPath { get; set; }

        public List<Tuple<string, bool>> Children { get; set; } = new();
    }
    public class Color_menager : INotifyPropertyChanged
    {
        private readonly Settings _settings;
        public Color_menager(Settings settings)
        {
            _settings = settings;
        }
        public Color theme_color
        {
            get => _settings.theme_color;
            set
            {
                if (_settings.theme_color != value)
                {
                    _settings.theme_color = value;
                    OnPropertyChanged(nameof(theme_color));
                    UpdateAccentColorResource(value);
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

        private static DateTime _lastUpdateTime = DateTime.MinValue;
        private static Color? _pendingColor = null;
        private static DispatcherTimer _throttleTimer = null;

        private void UpdateAccentColorResource(Color newColor)
        {
            var now = DateTime.Now;
            var timeSinceLastUpdate = now - _lastUpdateTime;

            if (timeSinceLastUpdate.TotalMilliseconds >= 100)
            {
                _lastUpdateTime = now;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var newBrush = new SolidColorBrush(newColor);
                    Application.Current.Resources["AccentBrush"] = newBrush;
                });

                // Save the color back to settings here
                _settings.theme_color = newColor;
            }
            else
            {
                _pendingColor = newColor;

                if (_throttleTimer == null)
                {
                    _throttleTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(100)
                    };
                    _throttleTimer.Tick += (s, e) =>
                    {
                        if (_pendingColor.HasValue)
                        {
                            _lastUpdateTime = DateTime.Now;
                            Application.Current.Resources["AccentBrush"] =
                                new SolidColorBrush(_pendingColor.Value);

                            // Save the pending color to settings here
                            _settings.theme_color = _pendingColor.Value;

                            _pendingColor = null;
                        }
                        _throttleTimer.Stop();
                    };
                }

                _throttleTimer.Stop();
                _throttleTimer.Start();
            }
        }

    }
    public class Settings
    {
        public bool tft_mode { get; set; } = false;
        public bool hide_on_minimize { get; set; } = false;
        public bool show_path_window { get; set; } = true;
        public bool autodetect_game_path { get; set; } = true;
        public string gamepath { get; set; } = "";
        public bool startup_start { get; set; } = false;
        public bool load_start { get; set; } = false;
        public bool catch_updated { get; set; } = false;
        public int import_override { get; set; } = 0;

        public double Tile_height { get; set; } = 50;
        public int Tile_width { get; set; } = 620;

        public bool reinitialize { get; set; } = false;

        public string CurrentProfile { get; set; } = "Default Profile";

        public Color theme_color { get; set; } = Color.FromRgb(209, 96, 2);

        public double details_column { get; set; } = 200;
        public bool detials_column_active { get; set; } = false;
        public bool update_path_on_active { get; set; } = true;
        public bool details_displ { get; set; } = true;
        public int startup_index {  get; set; } = 0;
        public int start_mode { get; set; } = 0;
        public bool not_tft {  get; set; } = false;
        public bool supress_install_confilcts { get; set; } = false;
        public string default_author { get; set; } = "Unknown";
        public string default_Hearth { get; set; } = "";
        public string default_home { get; set; } = "";

        public bool show_thumbs { get; set; } = true;
        public float thumb_opacity { get; set; } = 0.4f;
        public bool auto_update_hashes { get; set; } = true;
        public int Ailgment { get; set; } = 1;
        public int game_version_major { get; set; } = 0;
        public int game_version_minor { get; set; } = 0;
        public int game_version_tetrary { get; set; } = 0;
        public int PBE_game_version_major { get; set; } = 0;
        public int PBE_game_version_minor { get; set; } = 0;
        public int PBE_game_version_tetrary { get; set; } = 0;
        public double WindowWidth { get; set; } = 1000;
        public double WindowHeight { get; set; } = 700;
        public WindowState WindowState { get; set; } = WindowState.Normal;
    }
    public class Folder
    {
        public string InnerPath { get; set; } = "";
        public string name { get; set; } = "";
        public bool Override { get; set; } = false ;
        public int priority { get; set; } = 10;
        public bool random { get; set; } = false;
        public int ID { get; set; }
        public int parent { get; set; }
    }

    public class Mod
    {
        public ModInfo Info { get; set; }
        public ModDetails Details { get; set; }
        public string ModFolder { get; set; }
        public bool isActive { get; set; }
        public bool has_changed { get; set; } = false;
        public List<string> Wads { get; set; } = new List<string>();
        public string rf_ID { get; set; }
        public string rf_RE { get; set; }

    }
    public class ModInfo
    {
        public string Author { get; set; } = "";
        public string Description { get; set; } = "";
        public string Heart { get; set; } = "";
        public string Home { get; set; } = "";
        public string Name { get; set; } = "";
        public string Version { get; set; } = "1.0.0";
    }
    public class ModDetails
    {
        public int Priority { get; set; } = 10;
        public bool override_ { get; set; } = false;
        public string InnerPath { get; set; } = "";

        public bool Random { get; set; } = false;
        public List<LayerInfo> Layers { get; set; } = new();
        public string layerss { get; set; } = "None";
    }

    


    public partial class MainWindow : Window
    {
        private TrayIcon _trayIcon;
        private ContextMenuWindow _contextMenuWindow;

        private bool display_only_active = false;

        Dictionary<int, HierarchyElement> hierarchyById = new();
        Dictionary<string, Mod> modByFolder = new Dictionary<string, Mod>();
        private static readonly string installedPath = "installed";
        List<ModListEntry> modListEntriesInDisplay = new List<ModListEntry>();
        List<ModListEntry> FolderListEntriesInDisplay = new List<ModListEntry>();

        Dictionary<int,List<(string, bool)>> CutEntries = new Dictionary<int, List<(string, bool)>>();

        public static List<string> GlobalselectedEntries = new List<string>();

        public Settings settings = new Settings();
        public Color_menager colorManager;

        public int Current_location_folder = 0;
        public bool search_flat = false;
        public string Global_searchText = "";
        private static readonly string ProfilesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles");
        public static Dictionary<string, int> ProfileEntries = new();

        void AddChild(int parentId, string childName, bool isMod)
        {
            if (hierarchyById.TryGetValue(parentId, out var parent))
            {
                parent.Children.Add(Tuple.Create(childName, isMod));
            }
            else
            {
                Console.WriteLine($"Parent ID {parentId} not found.");
            }
        }
        // runeforge-mod://install/ba538a07-d743-4a40-830a-5578059f9377/46f99a6e-3219-415f-8d8a-0798356bd222
        public async void EnsureRuneforgeProtocolRegistered()
        {
            const string protocol = "runeforge-mod";
            string exePath = Process.GetCurrentProcess().MainModule.FileName;

            using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{protocol}"))
            {
                key.SetValue("", "URL:Runeforge Protocol");
                key.SetValue("URL Protocol", "");

                using (var shellKey = key.CreateSubKey(@"shell\open\command"))
                {
                    // The "%1" is where the URL is passed to your app
                    shellKey.SetValue("", $"\"{exePath}\" \"%1\"");
                }
            }
        }
        public async void noskin_button_make(object sender, RoutedEventArgs e)
        {
            noskin_button.IsEnabled = false;
            string gameDataPath = Path.Combine(Path.GetDirectoryName(settings.gamepath), "DATA", "FINAL");
            string modDir = Path.Combine("installed", "NoSkin");
            string wadOutputBase = Path.Combine(modDir, "WAD");

            Directory.CreateDirectory(wadOutputBase);

            if (Wad_champs_dict.Count < 10)
                load_champ_wad_names();

            var championsToProcess = Wad_champs_dict
                .Skip(1)          // Skips the first element
                .SkipLast(2)      // Skips the last two elements
                .ToList();

            await Task.Run(() =>
            {
                Parallel.ForEach(championsToProcess, kvp =>
                {
                    Repatheruwu Fixer = new Repatheruwu();

                    Fixer.Settings.Character = kvp.Value;
                    Fixer.Settings.outputDir = Path.Combine(wadOutputBase, $"{Fixer.Settings.Character}.wad");
                    Fixer.Settings.WADpath = gameDataPath;
                    Fixer.Settings.skinNo = 0;
                    Fixer.Settings.SmallMod = true;
                    Fixer.Settings.SkipCheckup = true;
                    Fixer.Settings.noskinni = true;
                    Fixer.Settings.binless = false;
                    Fixer.Settings.folder = false;

                    Fixer.FixiniYoursSkini();
                    Fixer = null;
                    GC.Collect();
                });
            });
            MessageBox.Show("le done");
        }

        public async void open_ds(object sender, RoutedEventArgs e)

        {

            try

            {

                // Open website in default browser

                Process.Start(new ProcessStartInfo

                {

                    FileName = "https://divineskins.gg/explore-mods",

                    UseShellExecute = true

                });

            }

            catch (Exception ex)

            {

                CustomMessageBox.Show("Failed to open Runeforge website: " + ex.Message);

            }

        }
        public async void open_rf(object sender, RoutedEventArgs e)

        {

            try

            {

                // Open website in default browser

                Process.Start(new ProcessStartInfo

                {

                    FileName = "https://runeforge.dev",

                    UseShellExecute = true

                });

            }

            catch (Exception ex)

            {

                CustomMessageBox.Show("Failed to open Runeforge website: " + ex.Message);

            }

        }



        private void Create_Folder(object sender, RoutedEventArgs e)
        {
            var control = new ProfileNameDialog();

            control.SetPlaceholderText("New Folder Name");
            control.OnProfileCreated += newProfileName =>
            {
                int key = new Random().Next(1, int.MaxValue);
                while (hierarchyById.ContainsKey(key))
                    key = new Random().Next(1, int.MaxValue);


                string inner_path = "";
                int temporary_cur_loca = Current_location_folder;
                if (temporary_cur_loca > 0)
                {
                    List<int> pathSegments = new();

                    while (temporary_cur_loca > 0 && hierarchyById.TryGetValue(temporary_cur_loca, out var parentElement))
                    {
                        pathSegments.Add(parentElement.ID);
                        temporary_cur_loca = parentElement.parent;
                    }
                    pathSegments.Reverse(); // Make it top-down
                    inner_path = string.Join("/", pathSegments);
                    
                }

                var element = new HierarchyElement
                {
                    Name = newProfileName,
                    override_ = false,
                    Priority = 10,
                    Random = false,
                    ID = key,
                    parent = Current_location_folder,
                    InnerPath = inner_path,
                };
                hierarchyById[key] = element;
                AddChild(Current_location_folder, key.ToString(), false);

                SaveOrUpdateHierarchyElement(element, hierarchyById);
                RefreshModListPanel(Current_location_folder);

                OverlayHost.Children.Clear();
            };

            control.OnCanceled += () =>
            {
                OverlayHost.Children.Clear();
            };

            OverlayHost.Children.Clear(); // Just in case
            OverlayHost.Children.Add(control);
        }

        public void CatchSelectedEntries(List<ModListEntry> selected)
        {
            GlobalselectedEntries.Clear();
            foreach (var item in selected)
            {
                GlobalselectedEntries.Add(item.identifier);
            }
        }
        private bool ShouldBlockShortcuts()
        {
            return OverlayHost2.Visibility == Visibility.Visible
                || OverlayHost.Children.Count > 0;
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                var metaEditor = OverlayHost.Children
        .OfType<MetaEdior>()
        .FirstOrDefault();

                if (metaEditor != null)
                {
                    var mod = metaEditor.ModElement;
                    var folder = metaEditor.FolderElement;

                    if (mod != null)
                    {
                        List<string> wads = new List<string>();
                        string wadPath = Path.Combine(installedPath, mod.ModFolder, "WAD");
                        if (Directory.Exists(wadPath))
                        {
                            try
                            {
                                string[] wadEntries = Directory.GetFileSystemEntries(wadPath);
                                foreach (string entry in wadEntries)
                                {
                                    var lastPart = entry.Split('\\').Last();
                                    wads.Add(Path.GetFileName(lastPart));

                                }
                            }
                            catch (UnauthorizedAccessException)
                            {
                                Console.WriteLine("Access denied ");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Failed to read WAD entries during shortcut execution", ex);
                            }
                        }
                        mod.Wads = wads;
                        if (metaEditor.image_path != "")
                        {
                            try
                            {
                                using (var img = System.Drawing.Image.FromFile(metaEditor.image_path))
                                {
                                    img.Save(Path.Combine(installedPath, mod.ModFolder, "META", "image.png"), System.Drawing.Imaging.ImageFormat.Png);
                                }
                            }
                            catch (Exception ex)
                            {
                                CustomMessageBox.Show(
                                    "Failed to change the image.\n\nError: " + ex.Message,
                                    new[] { "OK" },
                                    "Mod List"
                                );
                            }

                        }
                        SaveModInfo(mod);
                        SaveModDetails(mod, null, true);
                    }
                    

                        if (folder != null)
                    {
                        SaveFolder(folder);
                    }
                }
                var settings = OverlayHost.Children
        .OfType<SettingsOverlay>()
        .FirstOrDefault();

                if (settings != null)
                { save_settings(); }
                OverlayHost.Children.Clear();
                OverlayHost2.Children.Clear();
            }
            else if (e.Key == Key.F5 && !ShouldBlockShortcuts() && refreshButton.IsEnabled)
            {
                Internal_restart(Current_location_folder);
            }
            else if (e.Key == Key.R && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && !(SearchBox.IsFocused) && !ShouldBlockShortcuts())
            {
                Internal_restart(Current_location_folder);
            }
            else if (e.Key == Key.A && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && !(SearchBox.IsFocused) && !ShouldBlockShortcuts())
            {
                Change_State_allButtons(true);
            }
            else if (e.Key == Key.D && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && !(SearchBox.IsFocused) && !ShouldBlockShortcuts())
            {
                Change_State_allButtons(false);
            }
            else if (e.Key == Key.X && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && !(SearchBox.IsFocused) && !ShouldBlockShortcuts())
            {
                foreach (var mod in modListEntriesInDisplay)
                {
                    CutEntries.Clear();
                    List<ModListEntry> temp = mod.ReadSelect();
                    int origin = 0;
                    foreach (var entry in temp)
                    {
                        if (entry.IsMod)
                        {
                            string path = entry.ModElement.Details.InnerPath;

                            if (!string.IsNullOrEmpty(path))
                            {
                                string lastPart = path.Contains('/') ? path.Split('/').Last() : path;

                                if (!int.TryParse(lastPart, out origin))
                                    origin = 0; // fallback if not a valid int
                            }
                        }
                        else
                        {
                            origin = entry.FolderElement.parent;
                        }
                        if (!CutEntries.ContainsKey(origin))
                        {
                            CutEntries[origin] = new List<(string, bool)>();
                        }
                        CutEntries[origin].Add((entry.identifier,entry.IsMod));
                    }
                    break;
                }
            }
            else if (e.Key == Key.V && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && !(SearchBox.IsFocused) && !ShouldBlockShortcuts())
            {
                foreach (int source in CutEntries.Keys)
                {
                    DragHandler(CutEntries[source], (Current_location_folder.ToString(), false), true, source);
                }
                CutEntries.Clear();
            }
        }
        public void detectGamePath()
        {
            if (IsValidGamePath(settings.gamepath))
                return;


            // Try to get path based on current running league process
            var targetNames = new[] { "LeagueClient.exe", "League of Legends.exe" };

            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (!targetNames.Contains(process.ProcessName + ".exe", StringComparer.OrdinalIgnoreCase))
                        continue;

                    string path = GetMainModuleFilePath(process);
                    if (path.Contains("LeagueClient.exe"))
                        path = path.Replace("LeagueClient.exe", "Game\\League of Legends.exe");
                    if (IsValidGamePath(path))
                    {
                        settings.gamepath = path;
                        save_settings();
                        return;
                    }
                }
                catch
                {
                    // Access denied or other issues, skip
                }
            }

            // Fallback: try to parse the path from 'ProgramData\Riot Games\RiotClientInstalls.json'
            string riotInstallsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Riot Games",
                "RiotClientInstalls.json"
            );

            if (File.Exists(riotInstallsPath))
            {
                try
                {
                    var jsonContents = File.ReadAllText(riotInstallsPath);
                    using var document = JsonDocument.Parse(jsonContents);

                    var path = document.RootElement
                        .GetProperty("associated_client")
                        .EnumerateObject()
                        .FirstOrDefault()
                        .Name;

                    if (!string.IsNullOrEmpty(path))
                    {
                        path = path.Replace('/', '\\'); // Normalize to Windows path format for consistency.
                        path = Path.Combine(path, @"Game\League of Legends.exe");
                    }

                    if (IsValidGamePath(path))
                    {
                        settings.gamepath = path;
                        save_settings();
                        return;
                    }
                }
                catch
                {
                }
            }
        }

        private static bool IsValidGamePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            return File.Exists(path)
                && Path.GetFileName(path).Equals("League of Legends.exe", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetMainModuleFilePath(Process process)
        {
            try
            {
                return process.MainModule.FileName;
            }
            catch
            {
                // Try native method for access-restricted processes
                return GetExecutablePathViaQueryFullProcessImageName(process.Handle);
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags, System.Text.StringBuilder text, ref int size);

        private static string GetExecutablePathViaQueryFullProcessImageName(IntPtr hProcess)
        {
            int capacity = 1024;
            var buffer = new System.Text.StringBuilder(capacity);
            if (QueryFullProcessImageName(hProcess, 0, buffer, ref capacity))
            {
                return buffer.ToString();
            }
            return "";
        }

        public void update_tile_contrains()
        {
            MinColumnWidth = settings.Tile_width;
            RowHeight = settings.Tile_height;
            
            if (TryFindResource("ModListEntryHeight") is double height)
                Resources["ModListEntryHeight"] = settings.Tile_height;

            if (TryFindResource("ModListEntryPadding") is Thickness padding)
            {
                // Compute new padding
                double value = RowHeight / 10;

                // Clamp top and bottom
                double constrainted = Math.Min(Math.Max(value, 1), 20);

                Resources["ModListEntryPadding"] = new Thickness(constrainted, constrainted, constrainted, constrainted);
            }

            if (TryFindResource("ModListEntryMargin") is Thickness margin)
            {
                double value = RowHeight / 20;

                double constrainted = Math.Min(Math.Max(value, 2), 5);

                Resources["ModListEntryMargin"] = new Thickness(constrainted, constrainted, constrainted, constrainted);
            }
            AdjustModListLayout();
        }

        private void ModListPanel_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Source == sender)
            {
                Change_State_allButtons(false);
            }
        }

        private void Grid_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Only trigger if the click was directly on the Grid background
            if (e.OriginalSource == sender)
            {
                Change_State_allButtons(false);
            }
        }






        private void Change_State_allButtons(bool check)
        {
            foreach (var mod in modListEntriesInDisplay)
            {
                mod.SetSelection(check);
                mod.RefreshDisplay(false, true);
            }
            foreach (var folder in FolderListEntriesInDisplay)
            {
                folder.SetSelection(check);
                folder.RefreshDisplay(false, true);
            }
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == System.Windows.WindowState.Minimized && settings.hide_on_minimize)
            {
                this.Hide();
                this.ShowInTaskbar = false;
            }
        }
        private readonly SemaphoreSlim _queueSemaphore = new SemaphoreSlim(1, 1);
        public void TriggerQueueProcessing()
        {
            _ = ProcessProtocolQueueAsync();
        }

        private async Task ProcessProtocolQueueAsync()
        {
            if (!await _queueSemaphore.WaitAsync(0)) return;

            try
            {
                while (Globals.ProtocolQueue.TryDequeue(out string args))
                {
                    await ProcessArguments(args);
                }
            }
            finally
            {
                _queueSemaphore.Release();
            }
        }

        public async Task ProcessArguments(string args)
        {
            var splitArgs = SplitArgs(args);

            bool hasStart = false;
            bool hasStop = false;
            string spValue = null;

            for (int i = 0; i < splitArgs.Count; i++)
            {
                string arg = splitArgs[i];

                if (arg == "--start")
                {
                    hasStart = true;
                }
                else if (arg == "--sp" && i + 1 < splitArgs.Count)
                {
                    spValue = splitArgs[i + 1];
                    i++; 
                }
                else if (arg == "--stop")
                {
                    hasStop = true;
                }
                else if (arg.StartsWith("runeforge-mod://"))
                {
                    HandleRuneforgeProtocol(arg);
                }
            }

            if (hasStop)
            {
                Load_check_box.IsEnabled = false;
                await Stop_loader_internal();
                Load_check_box.IsEnabled = true;
            }
            if (spValue != null)
            {
                if (ProfileExists(spValue))
                {
                    ProfileComboBox.SelectedItem = spValue;
                    settings.CurrentProfile = spValue;
                    save_settings();
                    ReadCurrentProfile();
                }
            }
            if (hasStart)
            {
                True_Start_loader();
            }
        }

        private async Task HandleRuneforgeProtocol(string url)
        {
            try
            {
                ToggleFeed(true, 3);
                Feed3.Text = "Installing Mod from Runeforge.dev";
                var uri = new Uri(url);
                var segments = uri.AbsolutePath.Trim('/').Split('/');

                string modId = segments[0];
                string releaseId = segments[1];

                DownloadAndInstallMod(modId, releaseId);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to handle Runeforge protocol: " + ex.Message);
                Logger.LogError("Failed to handle Runeforge protocol", ex);
            }
        }

        private async Task DownloadAndInstallMod(string modId, string releaseId)
        {
            try
            {
                string apiUrl = $"https://runeforge.dev/api/mods/{modId}/releases/{releaseId}/download-url";
                using var client = new HttpClient();

                // Get JSON response
                string json = await client.GetStringAsync(apiUrl);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("downloadUrl", out JsonElement urlElement))
                {
                    MessageBox.Show("Download URL not found in response.");
                    return;
                }

                string downloadUrl = urlElement.GetString();
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    MessageBox.Show("Download URL is empty.");
                    return;
                }

                // Determine filename from API or fallback
                string fileName = null;
                if (root.TryGetProperty("filename", out JsonElement filenameElement))
                {
                    fileName = filenameElement.GetString();
                }

                if (string.IsNullOrWhiteSpace(fileName))
                {
                    // Fallback: get filename from URL
                    fileName = Path.GetFileName(new Uri(downloadUrl).AbsolutePath);
                }
                
                fileName = fileName.Split(new string[] { "%2F" }, StringSplitOptions.None).Last();

                Feed3.Text = $"Installing {fileName}";

                // Create temporary directory for Runeforge mods
                string tempDir = Path.Combine(Path.GetTempPath(), "cslolgo");
                Directory.CreateDirectory(tempDir);

                // Full path to target file
                string tempFile = Path.Combine(tempDir, fileName);

                // Download mod
                var bytes = await client.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(tempFile, bytes);

                handle_rf_install(tempFile);

                try
                {
                    File.Delete(tempFile);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to delete temp file when installing from rf", ex);
                }

                // Get the file name without extension
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(tempFile);

                // Build the installed mod folder path
                string installedFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "installed", fileNameWithoutExt);
                try
                {
                    string metaFile = Path.Combine(installedFolder, "META", "rf.json");

                    var meta = new
                    {
                        modId = modId,
                        releaseId = releaseId
                    };

                    string jsones = System.Text.Json.JsonSerializer.Serialize(
                        meta,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
                    );

                    File.WriteAllText(metaFile, jsones);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to write rf.json when installing from rf", ex);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to Download mod from Runeforge API", ex);
                MessageBox.Show("Failed to download/install mod:\n" + ex.Message);
            }
            ToggleFeed(false, 3);
        }

        public static List<string> SplitArgs(string input)
        {
            var args = new List<string>();
            var currentArg = new StringBuilder();
            bool inQuotes = false;

            foreach (char c in input)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (char.IsWhiteSpace(c) && !inQuotes)
                {
                    if (currentArg.Length > 0)
                    {
                        args.Add(currentArg.ToString());
                        currentArg.Clear();
                    }
                }
                else
                {
                    currentArg.Append(c);
                }
            }

            if (currentArg.Length > 0)
                args.Add(currentArg.ToString());

            return args;
        }


        static async Task DownloadCslolDll()
        {
            try
            {
                string url = "https://raw.githubusercontent.com/Aurecueil/Cs-lol-go/main/Tools/cslol-dll.dll";
                string savePath = Path.Combine("cslol-tools", "cslol-dll.dll");

                Directory.CreateDirectory(Path.GetDirectoryName(savePath));

                using HttpClient client = new HttpClient();
                byte[] data = await client.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(savePath, data);
            }
            catch { }
        }


        private void OnTrayIconDoubleClick()
        {
            RestoreWindow();
        }

        private void OnTrayIconRightClick()
        {
            ShowContextMenu();
        }

        private void ShowContextMenu()
        {
            if (_contextMenuWindow == null)
            {
                _contextMenuWindow = new ContextMenuWindow();
                _contextMenuWindow.OnShowClicked += () => RestoreWindow();
                _contextMenuWindow.OnExitClicked += () => Application.Current.Shutdown();

                _contextMenuWindow.OnLoaderClicked += () =>
                {
                    HandleLoaderClick();
                };
            }

            // Update loader state before showing the menu
            bool isRunning = IsLoaderRunning();
            bool isDisabled = !IsLoaderEnabled();
            _contextMenuWindow.UpdateLoaderState(isRunning, isDisabled);

            // Get cursor position
            var point = GetCursorPosition();
            _contextMenuWindow.ShowAt(point.X, point.Y);
        }
        public void UpdateContextMenuLoaderState()
        {
            if (_contextMenuWindow != null)
            {
                bool isRunning = IsLoaderRunning();
                bool isDisabled = !IsLoaderEnabled();
                _contextMenuWindow.UpdateLoaderState(isRunning, isDisabled);
            }
        }

        private void HandleLoaderClick()
        {
            if (IsLoaderRunning())
            {
                Stop_loader(null, null);
            }
            else
            {
                Start_loader(null, null);
            }
        }

        public bool IsLoaderRunning()
        {
            lock (_loaderLock)
            {
                // Check if mod loading process is running
                bool modLoadingRunning = _isLoaderRunning || _modLoadCts != null;

                // Check if CSLol process is running
                bool cslolRunning = CSLolManager.IsRunning();

                return modLoadingRunning || cslolRunning;
            }
        }

        public bool IsLoaderEnabled()
        {
            return Load_check_box.IsEnabled;
        }

        private void RestoreWindow()
        {
            this.Show();
            this.ShowInTaskbar = true;
            this.WindowState = System.Windows.WindowState.Normal;
            this.Activate();
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private POINT GetCursorPosition()
        {
            GetCursorPos(out POINT point);
            return point;
        }

        protected override void OnClosed(EventArgs e)
        {
            _trayIcon?.Dispose();
            _contextMenuWindow?.Close();
            var bounds = RestoreBounds;
            if (WindowState == WindowState.Normal)
            {
                settings.WindowWidth = Width;
                settings.WindowHeight = Height;
            }
            settings.WindowState = WindowState;
            save_settings();
            base.OnClosed(e);
        }

        public async void details_colums_change(bool active)
        {
            if (active)
            {
                var width = new GridLength(settings.details_column, GridUnitType.Pixel);
                MainGrid.ColumnDefinitions[2].Width = width;
                MySplitter.IsEnabled = true;
                await Task.Delay(10);
                AdjustModListLayout();
            }
            else
            {
                var width = new GridLength(0, GridUnitType.Pixel);
                MainGrid.ColumnDefinitions[2].Width = width;
                MySplitter.IsEnabled = false;
                save_settings();
                await Task.Delay(10);
                AdjustModListLayout();
            }
        }
        private void restart_button(object sender, EventArgs e)
        {
            Internal_restart(Current_location_folder);
        }

        private void Import_Mods(object sender, EventArgs e)
        {
            string[] paths = OpenSupportedFileDialog(true);
            foreach (string path in paths)
            {
                if (IsAcceptedDropItem(path))
                {
                    HandleDroppedItem(path);
                }
            }
        }
        public static string[] OpenSupportedFileDialog(bool allowMultiple = false, Window owner = null)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select file(s)",
                Filter = "Supported Files (*.fantome, *.modpkg, *.zip, *.wad, *.client)|*.fantome;*.zip;*.wad;*.client|All Files (*.*)|*.*",
                Multiselect = allowMultiple
            };

            bool? result = owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner);

            return result == true ? dialog.FileNames : Array.Empty<string>();
        }
        private static readonly string[] GitHubUrls =
        {
        "https://api.github.com/repos/CommunityDragon/Data/contents/hashes/lol/hashes.game.txt.0",
        "https://api.github.com/repos/CommunityDragon/Data/contents/hashes/lol/hashes.game.txt.1",
        "https://api.github.com/repos/CommunityDragon/Data/contents/hashes/lol/hashes.binentries.txt"
        };

        private const string DownloadUrl = "https://raw.communitydragon.org/binviewer/hashes/hashes.game.txt";
        private static readonly string BasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cslol-tools");
        private static readonly string HashesFilePath = Path.Combine(BasePath, "hashes.game.txt");
        private static readonly string CheckFilePath = Path.Combine(BasePath, "hashes.check.txt");

        private const string BinEntriesDownloadUrl = "https://raw.githubusercontent.com/CommunityDragon/Data/refs/heads/master/hashes/lol/hashes.binentries.txt";
        private static readonly string BinEntriesFilePath = Path.Combine(BasePath, "hashes.shaders.txt");
        public async Task<string> RunCurlCommandAsync(string url)
        {
            var curlCommand = $"curl -I -H \"User-Agent: cslol-tools\" -H \"Accept: application/vnd.github.v3+json\" \"{url}\"";

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {curlCommand}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = new Process { StartInfo = processStartInfo };
            process.Start();

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();

            process.WaitForExit();
            return output;
        }



        public void StartHashUpdate()
        {
            Task.Run(async () =>
            {
                Dispatcher.Invoke(() => ToggleFeed(true, 2));
                try
                {
                    Dispatcher.Invoke(() => Feed2.Text = "Checking for hash updates...");

                    Directory.CreateDirectory(BasePath);

                    long lastUpdateTicks = 0;
                    if (File.Exists(CheckFilePath))
                        long.TryParse(File.ReadAllText(CheckFilePath).Trim(), out lastUpdateTicks);

                    DateTimeOffset lastUpdate = new DateTimeOffset(lastUpdateTicks, TimeSpan.Zero);
                    bool needsUpdate = !File.Exists(HashesFilePath);
                    if (!needsUpdate) {

                        foreach (var url in GitHubUrls)
                        {
                            Dispatcher.Invoke(() => Feed2.Text = $"Checking {Path.GetFileName(url)}...");

                            var curlOutput = await RunCurlCommandAsync(url);

                            DateTimeOffset? remoteModified = null;
                            if (!string.IsNullOrEmpty(curlOutput))
                            {
                                var lines = curlOutput.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                                var lastModifiedLine = lines.FirstOrDefault(line => line.StartsWith("Last-Modified:", StringComparison.OrdinalIgnoreCase));

                                if (lastModifiedLine != null)
                                {
                                    var lastModifiedValue = lastModifiedLine.Substring("Last-Modified:".Length).Trim();
                                    if (DateTimeOffset.TryParse(lastModifiedValue, out var parsedDate))
                                    {
                                        remoteModified = parsedDate;
                                    }
                                }
                            }

                            if (remoteModified.HasValue)
                            {

                                if (remoteModified.Value > lastUpdate || lastUpdateTicks == 0)
                                {
                                    needsUpdate = true;
                                    Dispatcher.Invoke(() => Feed2.Text = $"Update detected on {Path.GetFileName(url)}.");
                                    break;
                                }
                            }
                            else
                            {
                                Dispatcher.Invoke(() => Feed2.Text = $"Could not check {Path.GetFileName(url)}.");
                            }
                        }


                    }
                    if (!File.Exists(BinEntriesFilePath) || needsUpdate)
                    {
                        using var httpClient = new HttpClient();

                        Dispatcher.Invoke(() => Feed2.Text = "Downloading and filtering shaders...");

                        try
                        {
                            // 1. Download the raw content
                            var binEntriesContent = await httpClient.GetStringAsync(BinEntriesDownloadUrl);

                            // 2. Split into lines
                            var lines = binEntriesContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                            // 3. Filter: Keep only lines where the path (after the hash) starts with "Shaders/"
                            var filteredLines = lines.Where(line =>
                            {
                                // Format is usually: "HASH PATH" (e.g., "12345678 Shaders/MyShader.prop")
                                int spaceIndex = line.IndexOf(' ');
                                if (spaceIndex > -1 && spaceIndex + 1 < line.Length)
                                {
                                    // check the part after the space
                                    string pathPart = line.Substring(spaceIndex + 1);
                                    return pathPart.StartsWith("Shaders/", StringComparison.OrdinalIgnoreCase);
                                }
                                return false;
                            });

                            // 4. Save the filtered result
                            await File.WriteAllLinesAsync(BinEntriesFilePath, filteredLines);
                        }
                        catch (Exception ex)
                        {
                            // Optional: Log specific error for the binentries part so it doesn't fail the whole update
                            Dispatcher.Invoke(() => Feed2.Text = $"Warning: Failed to update shaders. {ex.Message}");
                        }
                    }
                    if (needsUpdate)
                    {
                        using var httpClient = new HttpClient();

                        // --- Existing Logic: Download Game Hashes ---
                        Dispatcher.Invoke(() => Feed2.Text = "Downloading game hashes...");
                        var content = await httpClient.GetStringAsync(DownloadUrl);
                        await File.WriteAllTextAsync(HashesFilePath, content);


                        // --- Finalize ---
                        await File.WriteAllTextAsync(CheckFilePath, DateTimeOffset.UtcNow.Ticks.ToString());
                        Dispatcher.Invoke(() => Feed2.Text = "Hashes updated successfully.");
                    }
                    Dispatcher.Invoke(() => Feed2.Text = "Hashes are up-to-date.");
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => Feed2.Text = $"Error: {ex.Message}");
                }
                finally
                {
                    await Task.Delay(1000);
                    Dispatcher.Invoke(() => ToggleFeed(false, 2));
                }
            });
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            // This fires after window has closed
            _currentRunner?.KillProcess();
        }
        private void Internal_restart(int loc)
        {
            hierarchyById.Clear();
            modByFolder.Clear();
            CutEntries.Clear();
            Stop_loader_internal();
            load_settings();
            var root_folder = new HierarchyElement
            {
                Name = "root",
                override_ = false,
                Priority = 0,
                Random = false,
                ID = 0,
                parent = 0,
                InnerPath = "",
                Children = new List<Tuple<string, bool>>
                {
                }
            };
            hierarchyById[0] = root_folder;
            LoadFolders();
            LoadMods();
            InitializeProfiles();
            LoadWadFiles();
            ReadCurrentProfile();
            RefreshModListPanel(loc, true);
        }
        public Dictionary<int, string> Wad_champs_dict = new Dictionary<int, string>();
        public void load_champ_wad_names()
        {
            string champsPath = Path.Combine(Path.GetDirectoryName(settings.gamepath), "DATA", "FINAL", "champions");

            // Initialize the Dictionary
            var result = new Dictionary<int, string>();

            result.Add(result.Count, "UNKOWN Champion");

            // 1. Add directories found on disk
            if (Directory.Exists(champsPath))
            {
                string[] entries = Directory.GetFileSystemEntries(champsPath);
                foreach (string entry in entries)
                {
                    if (entry.Contains("_")) continue;
                    // Path.GetFileName converts "C:\Path\To\Aatrox" -> "Aatrox"
                    // We use result.Count to auto-assign the next index (0, 1, 2...)
                    result.Add(result.Count, Path.GetFileName(entry).Replace(".wad.client", "", StringComparison.OrdinalIgnoreCase));
                }
            }
            result.Add(result.Count, "SFX");
            result.Add(result.Count, "Announcer");

            // 2. Add your manual entries

            Wad_champs_dict = result;
        }
        private async void awaitKillSwitch()
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    string content = await client.GetStringAsync($"https://raw.githubusercontent.com/Aurecueil/Temp/main/images/good?t={DateTime.UtcNow.Ticks}");
                    string cleanContent = content.Trim();

                    if (!cleanContent.Equals("OKAY", StringComparison.OrdinalIgnoreCase))
                    {
                        CustomMessageBox.Show($"{cleanContent}", null, "Info");
                    }
                }
                catch
                {
                }
            }
        }
        static async void CleanUpOldBackups()
        {
            string path = "backup";
            int days = 7;
            // 1. Verify the directory exists to avoid crashes
            if (!Directory.Exists(path))
            {
                return;
            }

            DirectoryInfo dirInfo = new DirectoryInfo(path);

            // 2. Calculate the cutoff date (Current time minus 7 days)
            DateTime cutoffDate = DateTime.Now.AddDays(-days);

            // 3. Iterate through each immediate subdirectory
            foreach (DirectoryInfo subDir in dirInfo.GetDirectories())
            {
                try
                {
                    // 4. Check Creation Date
                    if (subDir.CreationTime < cutoffDate)
                    {
                        Console.WriteLine($"Deleting: {subDir.FullName} (Created: {subDir.CreationTime})");

                        // 5. Delete recursively (true argument)
                        subDir.Delete(recursive: true);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected error on {subDir.Name}: {ex.Message}");
                }
            }
        }

        public List<Manifest> manifests = null;
        public MainWindow()
        {
            InitializeComponent();
            if (!Globals._hasMutexOwnership) return;
            _trayIcon = new TrayIcon();
            _trayIcon.ShowTrayIcon("Yamete, mitenai de yo, onii-san!", OnTrayIconDoubleClick, OnTrayIconRightClick, "animegurl.ico");
            this.StateChanged += MainWindow_StateChanged;

            this.Closed += MainWindow_Closed;
            this.SizeChanged += MainWindow_SizeChanged;
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;

            CleanUpOldBackups();
            ModListEntry.MainWindowInstance = this;

            SetLoading("Settings", 1, 0.17);
            load_settings();
            colorManager = new Color_menager(settings);
            Application.Current.Resources["AccentColor"] = settings.theme_color;

            Width = settings.WindowWidth;
            Height = settings.WindowHeight;
            WindowState = settings.WindowState;

            string fontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtimes", "RuneforgeFont.ttf");
            var fontFamily = new System.Windows.Media.FontFamily(new Uri(fontPath), "./#Untitled1");

            Rf_Button_Icon.FontFamily = fontFamily;
            DS_Button_Icon.FontFamily = fontFamily;
            deleteteProfileFont.FontFamily = fontFamily;
            SettingsFont.FontFamily = fontFamily;
            tftModButton.FontFamily = fontFamily;

            Loaded += MainWindow_Loaded;
        }
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            EnsureRuneforgeProtocolRegistered();
            await Dispatcher.Yield(); // forces first render
            try
            {
                await CheckForAppUpdates();
            }
            catch { }
            awaitKillSwitch();
            DownloadCslolDll();

            try
            {
                if (!Globals.is_startup)
                {
                    switch (settings.start_mode)
                    {
                        case 1:
                            Globals.StartMinimized = true;
                            break;
                        case 2:
                            Globals.StartWithLoaded = true;
                            break;
                        case 3:
                            Globals.StartMinimized = true;
                            Globals.StartWithLoaded = true;
                            break;
                        default:
                            break;
                    }
                }
                if (!settings.show_path_window)
                {
                    MainGrid.RowDefinitions[1].Height = new GridLength(0);
                    ModListPanel.Margin = new Thickness(10, 10, 10, 0);
                }
                Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string relativePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "animegurl.ico");
                if (File.Exists(relativePath))
                {
                    this.Icon = BitmapFrame.Create(new Uri(relativePath, UriKind.Absolute));
                }

                SetLoading("Leagus Path", 1, 0.34);
                detectGamePath();
                CheckGameVersion();
                var root_folder = new HierarchyElement
                {
                    Name = "LOL",
                    override_ = false,
                    Priority = 0,
                    Random = false,
                    ID = 0,
                    parent = 0,
                    InnerPath = "",
                    Children = new List<Tuple<string, bool>>
                    {
                    }
                };
                hierarchyById[0] = root_folder;

                var tft_root_folder = new HierarchyElement
                {
                    Name = "TFT",
                    override_ = false,
                    Priority = 0,
                    Random = false,
                    ID = -1,
                    parent = -1,
                    InnerPath = "",
                    Children = new List<Tuple<string, bool>>
                    {
                    }
                };
                hierarchyById[-1] = tft_root_folder;

                if (settings.auto_update_hashes)
                {
                    StartHashUpdate();
                }
                SetLoading("WAD Index", 1, 0.51);
                await Task.Run(() => LoadWadFiles());
                SetLoading("Folder Index", 1, 0.68);
                await Task.Run(() => LoadFolders());
                SetLoading("Mods Index", 1, 0.85);
                await Task.Run(() => LoadMods());
                details_colums_change(settings.detials_column_active);
                if (settings.tft_mode)
                {
                    tftModButton.IsChecked= true;
                    Current_location_folder = -1;
                    lastbeen = 0;
                }
                RefreshModListPanel(Current_location_folder);
                InitializeSearchBox();

                tftModButton.Checked += tft_mode_enable;
                tftModButton.Unchecked += tft_mode_disable;
                if (!Directory.Exists(ProfilesFolder))
                {
                    Directory.CreateDirectory(ProfilesFolder);
                }
                SetLoading("Profiles", 1, 1);
                InitializeProfiles();
                if (Globals.StartMinimized)
                {
                    this.Hide();
                    this.ShowInTaskbar = false;
                }

                if (Globals.StartWithLoaded)
                {
                    Load_check_box.IsChecked = true;
                    True_Start_loader();
                }
                update_tile_contrains();

                await PreCacheAllUIElements();
                ToggleOverlayRow.SizeChanged += (s, e) => SetProgress();


                ShowBreadcrumb(Current_location_folder);
                EnsureRuneforgeProtocolRegistered();

                OverlayHost3.Visibility = Visibility.Collapsed;
                OverlayHost3.IsHitTestVisible = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading mods:\n" + ex.Message);
            }
            Globals.IsMainLoaded = true;
            TriggerQueueProcessing();
        }
        private void SetLoading(string text, int progress, double stage)
        {
            LoadingText.Text = text;

            switch (progress)
            {
                case 1: Stage1.Width = 300 * stage; Stage1_b.Width = 300 * stage; break;
                case 2: Stage2.Width = 300 * stage; Stage2_b.Width = 300 * stage; break;
                case 3: Stage3.Width = 300 * stage; break;
            }
            Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
        }

        private List<string> GetAllProfiles()
        {
            var profileFiles = Directory.GetFiles(ProfilesFolder, "*.profile");
            return profileFiles.Select(Path.GetFileNameWithoutExtension).ToList();
        }
        public static void SaveProfileEntriesToFile()
        {
            var main = Application.Current.MainWindow as MainWindow;
            if (main == null || string.IsNullOrWhiteSpace(main.settings.CurrentProfile))
                return;

            string profilePath = Path.Combine(ProfilesFolder, $"{main.settings.CurrentProfile}.profile");

            try
            {
                File.WriteAllLines(profilePath, ProfileEntries.Keys);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save profile file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private double currentProgress = 0;
        public void SetProgress()
        {
            double totalWidth = ToggleOverlayRow.ActualWidth;
            ProgressBarFill.Width = totalWidth * currentProgress;
        }


        private void CreateEmptyProfile(string profileName)
        {
            string filePath = Path.Combine(ProfilesFolder, $"{profileName}.profile");
            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, ""); // Create empty profile file
            }
            else
            {
                MessageBox.Show("This Profile alrady exist bicz");
            }
        }
        private bool ProfileExists(string profileName)
        {
            string filePath = Path.Combine(ProfilesFolder, $"{profileName}.profile");
            return File.Exists(filePath);
        }


        private void InitializeProfiles()
        {
            // Load all profiles into combo box
            List<string> profiles = GetAllProfiles();

            // If the current profile is set but doesn't exist, clear it
            if (!string.IsNullOrEmpty(settings.CurrentProfile) && !ProfileExists(settings.CurrentProfile))
            {
                settings.CurrentProfile = null;
            }

            // Handle profile initialization
            if (string.IsNullOrEmpty(settings.CurrentProfile))
            {
                if (profiles.Count > 0)
                {
                    // Use the first existing profile
                    settings.CurrentProfile = profiles[0];
                }
                else
                {
                    // Create a new empty profile
                    settings.CurrentProfile = "Default";
                    CreateEmptyProfile(settings.CurrentProfile);
                    profiles = GetAllProfiles(); // Refresh after creation
                }
            }

            // Set combo box items and selected item
            ProfileComboBox.ItemsSource = profiles;
            ProfileComboBox.SelectedItem = settings.CurrentProfile;
        }

        public void LoadWadFiles()
        {
            // Clear existing entries
            EMPTY_WADS.Clear();

            // MessageBox.Show($"{settings.gamepath}");

            string strin_path = Path.GetDirectoryName(settings.gamepath);

            if (!Directory.Exists(strin_path))
            {
                //MessageBox.Show($"Game path does not exist: {strin_path}");
                return;
            }

            try
            {
                // Get all .wad.client files recursively
                string[] wadFiles = Directory.GetFiles(strin_path, "*.wad.client", SearchOption.AllDirectories);

                foreach (string filePath in wadFiles)
                {
                    // Get filename without extension
                    string fileName = Path.GetFileNameWithoutExtension(filePath);

                    // Get relative path from game path
                    string relativePath = Path.GetRelativePath(strin_path, filePath);

                    string trimmedWad = fileName;
                    if (trimmedWad.EndsWith(".client"))
                        trimmedWad = trimmedWad.Substring(0, trimmedWad.Length - 7);

                    if (trimmedWad.EndsWith(".wad"))
                        trimmedWad = trimmedWad.Substring(0, trimmedWad.Length - 4);

                    EMPTY_WADS[trimmedWad] = new Tuple<string, Dictionary<string, bool>>(relativePath, new Dictionary<string, bool>());
                }

                Console.WriteLine($"Loaded {EMPTY_WADS.Count} WAD files");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading WAD files: {ex.Message}");
            }
            EMPTY_WADS = EMPTY_WADS
    .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
    .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        }
        private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfileComboBox.SelectedItem is not null)
            {
                settings.CurrentProfile = ProfileComboBox.SelectedItem.ToString();
                save_settings();
                ReadCurrentProfile();
            }
        }
        public void DeleteMod(string mod_folder)
        {
            if (modByFolder.TryGetValue(mod_folder, out var old_entry))
            {
                try
                {
                    string basePath = Path.GetFullPath("installed");
                    string fullPath = Path.GetFullPath(Path.Combine(basePath, mod_folder));

                    // Get the relative path from basePath to fullPath
                    string relativeToBase = Path.GetRelativePath(basePath, fullPath);

                    // If relative path goes outside (e.g., contains '..'), it's not inside installed
                    if (!relativeToBase.StartsWith("..") && Directory.Exists(fullPath))
                    {
                        Task.Run(() =>
                        {
                            Directory.Delete(fullPath, true); // true = recursive delete
                            string backup = Path.GetFullPath(Path.Combine("backup", mod_folder));
                            if (Directory.Exists(backup)) { Directory.Delete(backup, true); }
                        });
                    }


                    int parent = get_parent_from_innerPath(old_entry.Details.InnerPath);
                    if (hierarchyById.TryGetValue(parent, out var old_parent))
                    {
                        old_parent.Children.RemoveAll(child => child.Item1 == old_entry.ModFolder && child.Item2 == true);
                    }
                    modByFolder.Remove(mod_folder);

                    cachedUIElements.Remove((true, mod_folder));

                    MainWindow.ProfileEntries.Remove(old_entry.ModFolder);

                    RefreshModListPanel(Current_location_folder);
                } catch (Exception ex)
                {
                    Logger.LogError("Error while deleting mod: -->   ", ex);
                }
            }
        }

        public void DeleteFolderElement(int id_of_folder_to_del, bool delete_subdirs)
        {

            if (!hierarchyById.TryGetValue(id_of_folder_to_del, out var element_to_del))
            {
                return;
            }

            if (delete_subdirs)
            {
                var childrenCopy = new List<Tuple<string, bool>>(element_to_del.Children);

                foreach (Tuple<string, bool> child in childrenCopy)
                {
                    if (child.Item2)
                    {
                        DeleteMod(child.Item1);
                    }
                    else
                    {
                        // Add validation before parsing
                        if (int.TryParse(child.Item1, out int childId))
                        {
                            DeleteFolderElement(childId, true);
                        }
                    }
                }

                if (hierarchyById.TryGetValue(element_to_del.parent, out var parentElement))
                {
                    parentElement.Children.RemoveAll(t => t.Item1 == id_of_folder_to_del.ToString() && t.Item2 == false);
                }

                cachedUIElements.Remove((false, id_of_folder_to_del.ToString()));

                RemoveHierarchyElement(id_of_folder_to_del, hierarchyById);

                hierarchyById.Remove(id_of_folder_to_del);

                RefreshModListPanel(Current_location_folder);
            }
            else
            {
                var childrenCopy = new List<Tuple<string, bool>>(element_to_del.Children);

                foreach (Tuple<string, bool> child in childrenCopy)
                {
                    if (child.Item2)
                    {
                        if (modByFolder.TryGetValue(child.Item1, out var mod_element))
                        {
                            mod_element.Details.InnerPath = BuildInnerPath(element_to_del.parent, hierarchyById);
                            AddChild(element_to_del.parent, mod_element.ModFolder, true);
                            SaveModDetails(mod_element);

                        }

                    }
                    else
                    {
                        if (hierarchyById.TryGetValue(int.Parse(child.Item1), out var folder_element))
                        {
                            folder_element.InnerPath = BuildInnerPath(element_to_del.parent, hierarchyById);
                            folder_element.parent = element_to_del.parent;
                            AddChild(element_to_del.parent, folder_element.ID.ToString(), false);
                            SaveOrUpdateHierarchyElement(folder_element, hierarchyById);

                        }
                    }
                }
                cachedUIElements.Remove((false, id_of_folder_to_del.ToString()));

                RemoveHierarchyElement(id_of_folder_to_del, hierarchyById);

                hierarchyById.Remove(id_of_folder_to_del);

                RefreshModListPanel(Current_location_folder);
            }
        }

        private void CreateProfile_Click(object sender, RoutedEventArgs e)
        {
            var control = new ProfileNameDialog();

            control.OnProfileCreated += newProfileName =>
            {
                if (!ProfileExists(newProfileName))
                {
                    CreateEmptyProfile(newProfileName);
                    ReadCurrentProfile();

                    var profiles = GetAllProfiles();
                    ProfileComboBox.ItemsSource = profiles;
                    ProfileComboBox.SelectedItem = newProfileName;

                    settings.CurrentProfile = newProfileName;
                    save_settings();
                }
                else
                {
                    MessageBox.Show($"Profile '{newProfileName}' already exists!", "Profile Exists",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                OverlayHost.Children.Clear();
            };

            control.OnCanceled += () =>
            {
                OverlayHost.Children.Clear();
            };

            OverlayHost.Children.Clear(); // Clear previous overlay if any
            OverlayHost.Children.Add(control);
        }
        

        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (ProfileComboBox.SelectedItem != null)
            {
                string currentProfile = ProfileComboBox.SelectedItem.ToString();

                string q = $"Are you sure you want to delete profile '{currentProfile}'?";
                string title = "meow meow meow";
                IEnumerable<string> options = new[] { "Yes", "No" };

                string result = CustomMessageBox.Show(q , options, title);

                if (result == "Yes")
                {
                    DeleteProfile(currentProfile);

                    // Refresh combo box
                    var profiles = GetAllProfiles();
                    ProfileComboBox.ItemsSource = profiles;

                    // Select first available profile or create default
                    if (profiles.Count > 0)
                    {
                        ProfileComboBox.SelectedItem = profiles[0];
                        settings.CurrentProfile = profiles[0];
                    }
                    else
                    {
                        CreateEmptyProfile("Default");
                        profiles = GetAllProfiles();
                        ProfileComboBox.ItemsSource = profiles;
                        ProfileComboBox.SelectedItem = "Default";
                        settings.CurrentProfile = "Default";
                    }

                    save_settings();
                }
            }
        }

        private static string NormalizeVersion(string versionString)
        {
            if (string.IsNullOrEmpty(versionString))
            {
                return "0.0.0";
            }

            string cleanedString = Regex.Replace(versionString, @"[^0-9.]", "");

            var parts = cleanedString
                .Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            if (!parts.Any())
            {
                return "0.0.0";
            }
            var finalParts = parts.Take(3).ToList();
            while (finalParts.Count < 3)
            {
                finalParts.Add("0");
            }
            return string.Join(".", finalParts);
        }
        private void ReadCurrentProfile()
        {
            if (ProfileComboBox.SelectedItem != null)
            {
                string currentProfile = ProfileComboBox.SelectedItem.ToString();
                string profilePath = Path.Combine(ProfilesFolder, $"{currentProfile}.profile");

                if (File.Exists(profilePath))
                {
                    ProfileEntries.Clear();
                    foreach (var element in modByFolder.Values)
                    {
                        element.isActive = false;
                    }

                    foreach (var element in hierarchyById.Values)
                    {
                        element.isActive = false;
                    }

                    foreach (var line in File.ReadAllLines(profilePath))
                    {
                        
                        if (!string.IsNullOrWhiteSpace(line))
                        {

                            if (modByFolder.TryGetValue(line, out var cchildElement))
                            {
                                cchildElement.isActive = true;
                                ProfileEntries[line] = cchildElement.Details.Priority;
                            }
                            else if (int.TryParse(line, out int id) && hierarchyById.TryGetValue(id, out var childElement))
                            {
                                childElement.isActive = true;
                                ProfileEntries[line] = childElement.Priority;
                            }


                        }
                    }

                    RefreshModListPanel(Current_location_folder);
                    RefreshAllCachedElementsDisplay(false, true);
                }
                else
                {
                    MessageBox.Show($"Profile file '{currentProfile}.profile' not found.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void DeleteProfile(string profileName)
        {
            string filePath = Path.Combine(ProfilesFolder, $"{profileName}.profile");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            AdjustModListLayout();
        }
        void AdjustModListLayout()
        {
            if (ModListPanel == null) return;

            double panelWidth = ModListPanel.ActualWidth;

            int columns = Math.Max(1, (int)(panelWidth / MinColumnWidth));
            ModListPanel.Columns = columns;

            double columnWidth = panelWidth / columns;

            foreach (UIElement child in ModListPanel.Children)
            {
                if (child is FrameworkElement fe)
                {
                    fe.Width = columnWidth;
                    fe.Height = RowHeight;

                    fe.HorizontalAlignment = HorizontalAlignment.Stretch;
                }
            }
        }
        private async void MySplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            await Task.Delay(1);
            var column = MainGrid.ColumnDefinitions[2];
            settings.details_column = column.Width.Value;
            save_settings();
            AdjustModListLayout();
        }

        public void UpdateDetailsPanel(string details, bool update = true) {
            if (update)
            {
                if (settings.detials_column_active)
                {
                    Details_Panel.Text = GetDetails(details);
                }
            }
            else
            {
                Details_Panel.Text = "";
            }

            }

        public async Task Export_Mod(ModListEntry ModElement, List<ModListEntry> Additional_Mods = null)
        {
            // --- UI THREAD WORK ---
            // 1. Prepare strings and Dialog (Must happen on UI thread)
            string rawName = string.IsNullOrWhiteSpace(ModElement.ModElement.Info.Author)
                ? ModElement.ModElement.Info.Name
                : $"{ModElement.ModElement.Info.Name} by {ModElement.ModElement.Info.Author}";

            string sanitized = new string(rawName
                .Where(c => !Path.GetInvalidFileNameChars().Contains(c))
                .ToArray())
                .Trim();

            var saveDialog = new SaveFileDialog
            {
                Title = "Export Mod",
                Filter = "Fantome Files (*.fantome)|*.fantome|Mod Package (*.modpkg)|*.modpkg",
                FileName = $"{sanitized}.fantome",
                DefaultExt = ".fantome"
            };

            if (saveDialog.ShowDialog() != true) {
                ModElement.set_export(false);
                if (Additional_Mods is null) return;
                foreach (var mod in Additional_Mods)
                { mod.set_export(false); }
                    return;
            }

                
            string targetPath = saveDialog.FileName;

            // Capture simple variables needed for the background thread to avoid 
            // threading issues if ModElement accesses UI controls.
            bool isFantome = Path.GetExtension(targetPath).Equals(".fantome", StringComparison.OrdinalIgnoreCase);
            string sourceFolder = Path.Combine("installed", ModElement.ModElement.ModFolder);

            await Task.Run(() =>
            {
                actually_export_mod(targetPath, isFantome, sourceFolder, ModElement.ModElement);
            });
            ModElement.set_export(false);
            if (Additional_Mods is not null)
            {
                string directoryPath = Path.GetDirectoryName(targetPath);
                foreach (var mod in Additional_Mods)
                {
                    rawName = string.IsNullOrWhiteSpace(mod.ModElement.Info.Author)
                ? mod.ModElement.Info.Name
                : $"{mod.ModElement.Info.Name} by {mod.ModElement.Info.Author}";

                    sanitized = new string(rawName
                        .Where(c => !Path.GetInvalidFileNameChars().Contains(c))
                        .ToArray())
                        .Trim();
                    sourceFolder = Path.Combine("installed", mod.ModElement.ModFolder);
                    targetPath = Path.Combine(directoryPath,$"{sanitized}.{(isFantome ? "fantome" : "modpkg")}");
                    await Task.Run(() =>
                    {
                        actually_export_mod(targetPath, isFantome, sourceFolder, mod.ModElement);
                    });
                    mod.set_export(false);
                }
            }
        }

        private async void actually_export_mod(string targetPath, bool isFantome, string sourceFolder, Mod ModElement)
        {
            try
            {
                string wadSource = Path.Combine(sourceFolder, "wad");
                string metaSource = Path.Combine(sourceFolder, "meta");

                if (isFantome)
                {
                    string tempExportDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                    Directory.CreateDirectory(tempExportDir);

                    // 1️⃣ META folder
                    string metaTarget = Path.Combine(tempExportDir, "META");
                    Directory.CreateDirectory(metaTarget);
                    if (Directory.Exists(metaSource))
                        CopyDirectory(metaSource, metaTarget);

                    string wadTarget = Path.Combine(tempExportDir, "WAD");
                    Directory.CreateDirectory(wadTarget);

                    if (Directory.Exists(wadSource))
                    {
                        CopyDirectory(wadSource, wadTarget);
                    }
                    else
                    {
                        string? copiedBaseFolder = Directory.GetDirectories(sourceFolder, "WAD_base")
                            .OrderBy(d => d)
                            .FirstOrDefault();
                        if (copiedBaseFolder != null)
                        {
                            CopyDirectory(copiedBaseFolder, wadTarget);
                        }
                    }

                    foreach (var layer in ModElement.Details.Layers)
                    {
                        if (layer.Name == "base") continue;
                        string dir = Path.Combine(sourceFolder, layer.folder_name);
                        string dest = Path.Combine(tempExportDir, layer.folder_name);
                        CopyDirectory(dir, dest);
                    }

                    var exportDetails = new ModDetails();
                    exportDetails.Layers = ModElement.Details.Layers;
                    exportDetails.Priority = ModElement.Details.Priority;
                    exportDetails.override_ = false;
                    exportDetails.InnerPath = "";
                    exportDetails.Random = false;

                    string detailsJsonTarget = Path.Combine(metaTarget, "details.json");
                    File.WriteAllText(detailsJsonTarget, JsonSerializer.Serialize(exportDetails, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));

                    string tempZip = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".zip");
                    ZipFile.CreateFromDirectory(tempExportDir, tempZip);

                    if (File.Exists(targetPath))
                        File.Delete(targetPath);
                    File.Move(tempZip, targetPath);

                    if (Directory.Exists(tempExportDir))
                    {
                        try { Directory.Delete(tempExportDir, true); }
                        catch (Exception ex) { Logger.LogError("Error cleanup: --> ", ex); }
                    }
                }
                else
                {
                    // .modpkg export
                    var layers = new List<(string relativePath, string folderName, int number)>();
                    foreach (var layer in ModElement.Details.Layers)
                    {
                        layers.Add(($"installed/{ModElement.ModFolder}/{layer.folder_name}", layer.Name, layer.Priority));
                    }

                    var metadata = new ModpkgMetadata
                    {
                        Name = ModElement.ModFolder,
                        DisplayName = ModElement.Info.Name,
                        Description = ModElement.Info.Description,
                        Version = NormalizeVersion(ModElement.Info.Version),
                    };

                    if (!string.IsNullOrWhiteSpace(ModElement.Info.Author))
                    {
                        var authorNames = ModElement.Info.Author.Split(',')
                            .Select(a => a.Trim())
                            .Where(a => !string.IsNullOrEmpty(a));
                        foreach (var name in authorNames)
                        {
                            metadata.Authors.Add(new ModpkgAuthor(name));
                        }
                    }

                    string thumb_path = Path.GetFullPath(Path.Combine(metaSource, "image.png"));
                    var dyustry = new DistributorInfo
                    {
                        SiteId = "cslol-go",
                        SiteName = "cslol-go manager",
                        SiteUrl = "https://github.com/Aurecueil/Cs-lol-go/releases/latest",
                        ModId = "0",
                        ReleaseId = "0",
                    };

                    string rf_path = Path.Combine(metaSource, "rf.json");
                    if (File.Exists(rf_path))
                    {
                        string json = File.ReadAllText(rf_path);
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        JsonElement root = doc.RootElement;
                        dyustry.ModId = root.GetProperty("modId").GetString();
                        dyustry.ReleaseId = root.GetProperty("releaseId").GetString();
                        dyustry.SiteName = "RuneForge";
                        dyustry.SiteUrl = "https://runeforge.dev/mods/";
                        dyustry.SiteId = "runeforge";
                    }

                    ModPkgLib.Pack(layers, metadata, targetPath, thumb_path, dyustry);
                }
            }
            catch (Exception ex)
            {
                // Use Application.Current.Dispatcher to show UI elements from background thread
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
                Logger.LogError("Export Fail: -->   ", ex);
            }
        }

        public void CloseSettingsOverlay()
        {
            OverlayHost.Children.Clear();
        }
        private void AddMod_diag(object sender, RoutedEventArgs e)
        {
            // Ensure only one settings overlay is open
            foreach (UIElement child in OverlayHost.Children)
            {
                if (child is ModCreatorUserControl)
                    return;
            }

            OverlayHost.Children.Add(new ModCreatorUserControl());
        }
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // Ensure only one settings overlay is open
            foreach (UIElement child in OverlayHost.Children)
            {
                if (child is SettingsOverlay)
                    return;
            }

            OverlayHost.Children.Add(new SettingsOverlay());
        }
        private int lastbeen = -1;
        public async void tft_mode_enable(object sender, RoutedEventArgs e)
        {
            int to_ref = lastbeen;
            lastbeen = Current_location_folder;
            settings.tft_mode = true;
            RefreshModListPanel(to_ref);
            Change_State_allButtons(false);
            save_settings();
        }
        public async void tft_mode_disable(object sender, RoutedEventArgs e)
        {
            int to_ref = lastbeen;
            lastbeen = Current_location_folder;
            settings.tft_mode = false;
            RefreshModListPanel(to_ref);
            Change_State_allButtons(false);
            save_settings();
        }
        public void load_settings()
        {
            string filePath = "settings.json";

            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        // Deserialize, with custom converter for Color
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            Converters = { new ColorJsonConverter() }
                        };

                        Settings? loadedSettings = JsonSerializer.Deserialize<Settings>(json, options);
                        if (loadedSettings != null)
                        {
                            settings = loadedSettings;
                            return;
                        }
                    }
            }
            else
            {
                CreateStartMenuShortcut();
            }

            save_settings();
        }
        public void save_settings()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new ColorJsonConverter() }
            };

            string json = JsonSerializer.Serialize(settings, options);
            try
            {
                File.WriteAllText("settings.json", json);
            }
            catch (Exception ex) { Logger.LogError("Error Saving Settings",ex); }
        }

        private void LoadFolders()
        {
            string filePath = "folders.json";

            // Create file if it doesn't exist
            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, "[]");
            }

            // Read and deserialize
            List<Folder> folders;
            try
            {
                string json = File.ReadAllText(filePath);
                folders = JsonSerializer.Deserialize<List<Folder>>(json) ?? new List<Folder>();
            }
            catch
            {
                folders = new List<Folder>();
            }

            // Sort based on depth of inner_path
            folders.Sort((a, b) =>
            {
                int GetDepth(string innerPath) // TFT mode folder fallback
                {
                    if (string.IsNullOrEmpty(innerPath))
                        return 0;

                    var parts = innerPath.Split('/');

                    int depth = parts.Length;

                    // add +1 if first part is NOT "0" or "-1"
                    if (parts[0] != "0" && parts[0] != "-1")
                        depth += 1;

                    return depth;
                }

                int depthA = GetDepth(a.InnerPath);
                int depthB = GetDepth(b.InnerPath);

                return depthA.CompareTo(depthB);
            });


            // Iterate
            foreach (var folder in folders)
            {
                bool was_added = false;

                if (folder.InnerPath == "")
                {
                    var element = new HierarchyElement
                    {
                        Name = folder.name,
                        override_ = folder.Override,
                        Priority = folder.priority,
                        Random = folder.random,
                        ID = folder.ID,
                        parent = 0,
                        InnerPath = folder.InnerPath,
                    };
                    hierarchyById[folder.ID] = element;
                    was_added = true;
                    AddChild(0, folder.ID.ToString(), false);
                    continue;
                }
                string innerPath = folder.InnerPath;

                string[] parts = innerPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                for (int i = parts.Length - 1; i >= 0; i--)
                {
                    int part = int.Parse(parts[i]);
                    if (hierarchyById.ContainsKey(part))
                    {
                        var element = new HierarchyElement
                        {
                            Name = folder.name,
                            override_ = folder.Override,
                            Priority = folder.priority,
                            Random = folder.random,
                            ID = folder.ID,
                            parent = part,
                            InnerPath = folder.InnerPath,
                        };
                        hierarchyById[folder.ID] = element;
                        was_added = true;
                        AddChild(part, folder.ID.ToString(), false);
                        break;
                    }
                }

                if (was_added == false) {
                    var element = new HierarchyElement
                    {
                        Name = folder.name,
                        override_ = folder.Override,
                        Priority = folder.priority,
                        Random = folder.random,
                        ID = folder.ID,
                        parent = 0,
                        InnerPath = folder.InnerPath,
                    };
                    hierarchyById[folder.ID] = element;
                    was_added = true;
                    AddChild(0, folder.ID.ToString(), false);
                    continue;
                }
            }

        }


        private async void YasuoButtons_click(object sender, EventArgs e)
        {
            string exePath = @"cslol-tools\cslol-diag.exe";
            string modListDisp;

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    CreateNoWindow = true,
                };

                using (var process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();
                }
            }
            catch (Exception ex)
            {
                modListDisp = $"Failed to run diagnostic tool.\n\nError: {ex.Message}";
            }
        }
    







        private CancellationTokenSource _modLoadCts;
        private bool _isLoaderRunning = false;
        private readonly object _loaderLock = new object();

        public async void Start_loader(object sender, RoutedEventArgs e)
        {
            True_Start_loader();
        }

        public async void True_Start_loader()
        {
            if (!File.Exists(settings.gamepath))
            {
                CustomMessageBox.Show("Set Gamepath in settings first");
                return;
            }
            ToggleOverlay(true);
            Feed.Text = "[INF] Checking Game Version";
            await CheckGameVersion();
            currentProgress = 0;
            SetProgress();
            ProfileComboBox.IsEnabled = false;
            refreshButton.IsEnabled = false;
            CreateProfile.IsEnabled = false;
            deleteteProfile.IsEnabled = false;
            lock (_loaderLock)
            {
                if (_modLoadCts != null || _isLoaderRunning)
                {
                    return;
                }

                _isLoaderRunning = true;
                Load_check_box.IsEnabled = false;
            }
            Load_check_box.IsChecked = true;
            UpdateContextMenuLoaderState();
            try
            {
                ToggleFeed(true);
                Load_Mods();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting loader: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("Error starting loader: -->   ", ex);
                await Stop_loader_internal();
            }
            finally
            {}
            Load_check_box.IsEnabled = true;
            UpdateContextMenuLoaderState();
        }

        public async void Stop_loader(object sender, RoutedEventArgs e)
        {
            Stop_loader_internal();        
        }

        private async Task Stop_loader_internal()
        {
            try
            {
                await Task.Run(() => CSLolManager.Stop()); // Move heavy sync work off UI thread
            }
            catch (Exception ex)
            {
                Logger.LogError("Error stopping cslol: -->   ", ex);
            }

            Load_check_box.IsEnabled = false;
            UpdateContextMenuLoaderState();

            CancellationTokenSource ctsToCancel = null;

            lock (_loaderLock)
            {
                if (_modLoadCts != null)
                {
                    ctsToCancel = _modLoadCts;
                    _modLoadCts = null;
                }
                _isLoaderRunning = false;
            }

            if (ctsToCancel != null)
            {
                ctsToCancel.Cancel();
                await Task.Delay(100);
                ctsToCancel.Dispose();
            }

            if (_currentRunner != null)
            {
                await Task.Run(() => _currentRunner.KillProcess()); // Also offload this
                _currentRunner = null;
            }

            ProfileComboBox.IsEnabled = true;
            refreshButton.IsEnabled = true;
            CreateProfile.IsEnabled = true;
            deleteteProfile.IsEnabled = true;
            ClearPaintActiveMods();
            Load_check_box.IsChecked = false;
            ToggleOverlay(false);
            ToggleFeed(false);
            Load_check_box.IsEnabled = true;
            UpdateContextMenuLoaderState();
        }

        public async void Load_Mods()
        {
            CancellationTokenSource localCts = null;

            lock (_loaderLock)
            {
                if (_modLoadCts != null)
                {
                    return;
                }

                _modLoadCts = new CancellationTokenSource();
                localCts = _modLoadCts;
            }

            var token = localCts.Token;

            try
            {
                await InitializeModsAsync(token);

                lock (_loaderLock)
                {
                    if (_modLoadCts != localCts)
                    {
                        return;
                    }
                }

                StartCSLol(token);
            }
            catch (OperationCanceledException)
            {
                // Don't show message box for expected cancellation
                System.Diagnostics.Debug.WriteLine("Mod loading was cancelled.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading mods: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (!token.IsCancellationRequested)
                {
                    ToggleOverlay(false);
                }

                // Clean up if we're still the active token
                lock (_loaderLock)
                {
                    if (_modLoadCts == localCts)
                    {
                        _modLoadCts = null;
                        _isLoaderRunning = false;
                    }
                }
            }
        }

        public Dictionary<string, Tuple<string, Dictionary<string, bool>>> WADS =
    new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, Tuple<string, Dictionary<string, bool>>> EMPTY_WADS =
            new(StringComparer.OrdinalIgnoreCase);

        public static Dictionary<string, Tuple<string, Dictionary<string, bool>>> CopyWadsDictionary(
    Dictionary<string, Tuple<string, Dictionary<string, bool>>> source)
        {
            var copy = new Dictionary<string, Tuple<string, Dictionary<string, bool>>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in source)
            {
                var innerCopy = new Dictionary<string, bool>(kvp.Value.Item2);
                copy[kvp.Key] = Tuple.Create(kvp.Value.Item1, innerCopy);
            }
            return copy;
        }


        private async Task InitializeModsAsync(CancellationToken token)
        {
            ToggleOverlay(true);
            if (settings.update_path_on_active)
            {
                string old_path = settings.gamepath;
                settings.gamepath = "";
                detectGamePath();
                if (!IsValidGamePath(settings.gamepath))
                {
                    settings.gamepath = old_path;
                }
                save_settings();
                LoadWadFiles();
            }
            else if (!EMPTY_WADS.ContainsKey("Common"))
            {
                string old_path = settings.gamepath;
                settings.gamepath = "";
                detectGamePath();
                if (!IsValidGamePath(settings.gamepath))
                {
                    settings.gamepath = old_path;
                }
                save_settings();
                LoadWadFiles();
            }
            WADS = CopyWadsDictionary(EMPTY_WADS);
            mods_loaded_in.Clear();
            folders_loaded_in.Clear();
            if (settings.tft_mode)
            {
                await ProcessFolderChildrenAsync(-1, token);
            }
            else
            {
                await ProcessFolderChildrenAsync(0, token);
            }
            await WriteWads(token);
            ToggleOverlay(false);
        }


        private void StartCSLol(CancellationToken token)
        {
            CSLolManager.Initialize(
    Path.Combine(Directory.GetCurrentDirectory(), "profiles", settings.CurrentProfile)
        + (settings.gamepath?.EndsWith(@"(PBE)\Game\League of Legends.exe", StringComparison.OrdinalIgnoreCase) == true
            ? "‗PBE‗profile"
            : ""),
    token,
                text => Application.Current.Dispatcher.Invoke(() => Feed.Text = text),
                () => Application.Current.Dispatcher.Invoke(() =>
                {
                    ToggleFeed(false);
                    _isLoaderRunning = false;
                    _modLoadCts = null;
                }),
                // Game status changed callback - reinitialize mods
                () => Application.Current.Dispatcher.Invoke(async () =>
                {
                    if (!token.IsCancellationRequested)
                    {
                        if (settings.reinitialize) { 
                            try
                            {
                                Feed.Text = "Reinitializing mods...";
                                ToggleOverlay(true);
                                ClearPaintActiveMods();
                                await InitializeModsAsync(token);
                                Feed.Text = "Mods reinitialized. Waiting for game to start...";
                            }
                            catch (OperationCanceledException)
                            {
                                // Expected when cancellation is requested
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Error reinitializing mods: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                }),
                errorMsg => Application.Current.Dispatcher.Invoke(() =>
                {
                    ToggleFeed(false);
                    _isLoaderRunning = false;
                    MessageBox.Show(errorMsg, "CSLol Error", MessageBoxButton.OK, MessageBoxImage.Error);
                })
            );
        }
        private void OnDropMode(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(ModListEntry)))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true; // Prevent ScrollViewer from handling
            }
            if (e.Data.GetDataPresent("ModListEntries"))
            {
                var draggedItems = e.Data.GetData("ModListEntries") as List<ModListEntry>;
                if (draggedItems != null && draggedItems.Count > 0)
                {
                    // Call the drop handler
                    HandleItemsDropMode(draggedItems);
                }
            }
        }

        private void OnDragEnterMode(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("ModListEntries"))
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void OnDragOverMode(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(ModListEntry)))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true; // Prevent ScrollViewer from handling
            }
            OnDragEnterMode(sender, e);
        }
        private void HandleItemsDropMode(List<ModListEntry> draggedItems)
        {
            var draggedElements = new List<(string, bool)>();
            foreach (var item in draggedItems)
            {
                if (item.IsMod && item.ModElement != null)
                {
                    draggedElements.Add((item.ModElement.ModFolder, true));
                    item.SetSelection(false);
                }
                else if (!item.IsMod && !item.IsParentFolder && item.FolderElement != null)
                {
                    draggedElements.Add((item.FolderElement.ID.ToString(), false));
                    item.SetSelection(false);
                }
            }

            if (draggedElements.Count > 0)
            {
                DragHandler(draggedElements, (lastbeen.ToString(), false));
            }
                     
        }

        private async void PaintActiveMods()
        {
            foreach(string mod in mods_loaded_in)
            {
                if (cachedUIElements.TryGetValue((true, mod), out var entry))
                {
                    entry.SetStatus(1);
                }
            }
            foreach (int folder in folders_loaded_in)
            {
                if (cachedUIElements.TryGetValue((false, folder.ToString()), out var entry))
                {
                    entry.SetStatus(1);
                }
            }
        }

        private async void ClearPaintActiveMods()
        {
            foreach ((string s, Mod modd) in modByFolder)
            {
                if (cachedUIElements.TryGetValue((true, modd.ModFolder), out var entry))
                {
                    entry.SetStatus(0);
                }
            }
            foreach ((int s, HierarchyElement fold) in hierarchyById)
            {
                if (cachedUIElements.TryGetValue((false, fold.ID.ToString()), out var entry))
                {
                    entry.SetStatus(0);
                }
            }
        }

        private async Task CheckGameVersion()
        {
            if (File.Exists(settings.gamepath))
            {
                try
                {
                    int old_major = settings.game_version_major;
                    int old_minor = settings.game_version_minor;
                    int old_tetrary = settings.game_version_tetrary;
                    bool pbe = settings.gamepath.Contains("(PBE)", StringComparison.OrdinalIgnoreCase);
                    if (pbe)
                    {
                        old_major = settings.PBE_game_version_major;
                        old_minor = settings.PBE_game_version_minor;
                        old_tetrary = settings.PBE_game_version_tetrary;
                    }

                    var parentDir = Directory.GetParent(settings.gamepath);
                    if (parentDir == null)
                        return;

                    string VersionJson = Path.Combine(parentDir.FullName, "content-metadata.json");

                    string json = File.ReadAllText(VersionJson);

                    using JsonDocument doc = JsonDocument.Parse(json);
                    string version = doc.RootElement.GetProperty("version").GetString()!;

                    string[] parts = version.Split('.', StringSplitOptions.RemoveEmptyEntries);

                    int major = int.Parse(parts[0]);
                    int minor = int.Parse(parts[1]);
                    int tetrary = int.Parse(parts[2].Split('+', StringSplitOptions.RemoveEmptyEntries)[0]);
                    if (old_major == 0 && old_minor == 0 && old_tetrary == 0)
                    {
                        if (pbe)
                        {
                            settings.PBE_game_version_major = major;
                            settings.PBE_game_version_minor = minor;
                            settings.PBE_game_version_tetrary = tetrary;
                        }
                        else
                        {
                            settings.game_version_major = major;
                            settings.game_version_minor = minor;
                            settings.game_version_tetrary = tetrary;
                        }
                        return;
                    }
                    if (old_major != major || old_minor != minor || old_tetrary != tetrary)
                    {
                        string profilesPath = "profiles";

                        if (!Directory.Exists(profilesPath))
                            return;
                        if (pbe)
                        {
                            foreach (var dir in Directory.GetDirectories(profilesPath))
                            {
                                if (dir.Contains("‗PBE‗profile", StringComparison.OrdinalIgnoreCase)){

                                    try
                                    {
                                        Directory.Delete(dir, recursive: true);
                                    }
                                    catch
                                    {
                                    }
                                }
                            }
                        }
                        else
                        {
                            foreach (var dir2 in Directory.GetDirectories(profilesPath))
                            {
                                if (!dir2.Contains("‗PBE‗profile", StringComparison.OrdinalIgnoreCase))
                                {

                                    try
                                    {
                                        Directory.Delete(dir2, recursive: true);
                                    }
                                    catch
                                    {
                                    }
                                }
                            }
                        }

                        if (old_major != major || old_minor != minor)
                        {
                            CustomMessageBox.Show($"Due to new {(pbe ? "PBE " : "")}patch, some mods might have broken now. Check in pratice tool if everything works, before playing rankeds.\n\nMods that are very likely broken are:\n- ALL map mods\n- Emerald gains (or other gold retextures)\n- Moga's UI mods\n- No-skin", null, $"New {(pbe ? "PBE " : "")}Patch {major}.{minor}, check ur mods");
                        }
                        if (pbe)
                        {
                            settings.PBE_game_version_major = major;
                            settings.PBE_game_version_minor = minor;
                            settings.PBE_game_version_tetrary = tetrary;
                        }
                        else
                        {
                            settings.game_version_major = major;
                            settings.game_version_minor = minor;
                            settings.game_version_tetrary = tetrary;
                        }
                    }
                }
                catch { }
            }
        }

        private ModToolsRunner _currentRunner;

        HashSet<string> mods_loaded_in = new HashSet<string>();
        HashSet<string> mods_loaded_in_over = new HashSet<string>();
        List<int> folders_loaded_in = new List<int>();

        public async Task WriteWads(CancellationToken token)
        {
            mods_loaded_in.UnionWith(mods_loaded_in_over);
            string mod_list = $"\"{string.Join("\"/\"", mods_loaded_in)}\"";
            string mod_list_disp = $"{string.Join("\n", mods_loaded_in)}";
            // CustomMessageBox.Show(mod_list_disp, new[] { "OK" }, "Mod List");
            Logger.Log("-- WRITING MODS --");
            Logger.Log(mod_list_disp);
            Logger.Log("-- ------------ --");
            var runner = new ModToolsRunner(Path.Combine(Directory.GetCurrentDirectory(), "cslol-tools", "mod-tools.exe"));
            _currentRunner = runner;
            string game_path = Path.GetDirectoryName(settings.gamepath);

            bool test = false;
            if (test)
            {
                OverlayTool ovrl = new OverlayTool();
                ovrl.MkOverlay("installed", $"{Path.Combine(Directory.GetCurrentDirectory(), "profiles", settings.CurrentProfile) + (settings.gamepath?.EndsWith(@"(PBE)\Game\League of Legends.exe", StringComparison.OrdinalIgnoreCase) == true ? "‗PBE‗profile" : "")}", game_path, mods_loaded_in);
            }
            else
            {



                var args = $"mkoverlay --src \"installed\" --dst \"{Path.Combine(Directory.GetCurrentDirectory(), "profiles", settings.CurrentProfile) + (settings.tft_mode == true ? "‗TFT" : "") + (settings.gamepath?.EndsWith(@"(PBE)\Game\League of Legends.exe", StringComparison.OrdinalIgnoreCase) == true ? "‗PBE‗profile" : "")}\" --game:\"{game_path}\" --mods:{mod_list}";


                if (settings.not_tft)
                {
                    args += " --noTFT";
                }

                args += " --ignoreConflict";


                string err_catch = "";
                var outputLines = new List<string>();
                var errorLines = new List<string>();
                try
                {
                    int result = await runner.RunAsync(args,
                    onOutput: line =>
                    {
                        outputLines.Add(line);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Feed.Text = line;
                            string lastPart = line.Split('/').Last();
                            string trimmedWad = lastPart.Replace(".wad.client", "");
                            if (EMPTY_WADS.TryGetValue(trimmedWad, out var wadInfo))
                            {
                                int position = EMPTY_WADS.Keys
                                    .Select((key, index) => new { key, index })
                                    .FirstOrDefault(x => x.key.Equals(trimmedWad, StringComparison.OrdinalIgnoreCase))?.index ?? -1;

                                if (position >= 0)
                                {
                                    int total = EMPTY_WADS.Count;
                                    currentProgress = (double)(position + 1) / total;
                                    SetProgress();


                                }
                            }
                        });
                    },
                    onError: line =>
                    {
                        errorLines.Add(line);  // just collect errors, no immediate display
                    }
                );
                    PaintActiveMods();

                    // After RunAsync completes, display all error lines if any
                    if (errorLines.Count > 0)
                    {
                        string allErrors = string.Join(Environment.NewLine, errorLines);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            string allErrors = string.Join(Environment.NewLine, errorLines);
                            _modLoadCts?.Cancel();
                            ToggleFeed(false);
                            err_catch = allErrors;
                            _currentRunner = null;
                            throw new InvalidOperationException(allErrors);
                        });
                    }
                }
                catch (Exception)
                {
                    Load_check_box.IsChecked = false;
                    if (!string.IsNullOrEmpty(err_catch))
                    {
                        CustomMessageBox.Show(err_catch, new[] { "OK" }, "Error");
                    }
                }
            }
            currentProgress = 1;
            SetProgress();
            _currentRunner = null;
        }




        public async Task ProcessFolderChildrenAsync(int folderId, CancellationToken token, bool isRandomElement = false, bool overrride = false)
        {
            if (!hierarchyById.TryGetValue(folderId, out var folder))
                return;

            var rng = new Random(); // Shared random instance
            var workingList = new List<(string Id, int Priority, bool IsMod)>();
            var skipMap = new Dictionary<string, bool>();

            foreach (var (childId, isMod) in folder.Children)
            {
                token.ThrowIfCancellationRequested();

                if (ProfileEntries.TryGetValue(childId, out int priority))
                {
                    workingList.Add((childId, priority, isMod));
                }
            }

            if (isRandomElement && workingList.Any())
            {
                var randomIndex = rng.Next(workingList.Count);
                var randomElement = workingList[randomIndex];
                workingList.Clear();
                workingList.Add(randomElement);
            }

            // Pre-roll skip/keep decisions
            foreach (var (id, priority, isMod) in workingList)
            {
                bool skip = false;

                if (isMod && modByFolder.TryGetValue(id, out var mod) && mod.Details.Random)
                {
                    skip = rng.Next(2) == 0; // 50/50
                }

                skipMap[id] = skip;
            }

            workingList.Sort((a, b) => a.Priority.CompareTo(b.Priority));

            foreach (var (id, priority, isMod) in workingList)
            {
                token.ThrowIfCancellationRequested();

                if (skipMap.TryGetValue(id, out var skip) && skip)
                    continue;

                if (isMod)
                {
                    if (modByFolder.TryGetValue(id, out var mod))
                    {
                        foreach (string wad in mod.Wads)
                        {
                            string trimmedWad = TrimWadName(wad);
                            if (WADS.TryGetValue(trimmedWad, out var modsTuple))
                            {
                                if (mod.Details.override_)
                                {
                                    var clearedFolders = modsTuple.Item2.Keys.ToList();
                                    mods_loaded_in.RemoveWhere(m => clearedFolders.Contains(m));
                                    modsTuple.Item2.Clear();
                                }

                                modsTuple.Item2[mod.ModFolder] = mod.has_changed;
                            }
                            if (overrride)
                            {
                                mods_loaded_in.Add(mod.ModFolder);
                            }
                            else
                            {
                                mods_loaded_in_over.Add(mod.ModFolder);
                            }
                        }
                    }
                }
                else if (hierarchyById.TryGetValue(int.Parse(id), out var folderr))
                {
                    folders_loaded_in.Add(int.Parse(id));
                    await ProcessFolderChildrenAsync(int.Parse(id), token, folderr.Random, folderr.override_);
                }
            }
        }

        // Utility function to trim .wad/.client/.locale suffixes
        public string TrimWadName(string wad, bool trimm_locale = true)
        {
            string trimmed = wad;

            if (trimmed.EndsWith(".client"))
                trimmed = trimmed[..^7];
            if (trimmed.EndsWith(".wad"))
                trimmed = trimmed[..^4];
            if (trimm_locale)
            {
                var localeMatch = Regex.Match(trimmed, @"\.[a-zA-Z]{2}_[a-zA-Z]{2}$");
                if (localeMatch.Success)
                    trimmed = trimmed[..^localeMatch.Length];
            }

            return trimmed;
        }

        private void ToggleOverlay(bool show)
        {
            if (show)
            {
                // Make overlay block everything
                OverlayHost2.Background = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)); // semi-transparent black
                OverlayHost2.IsHitTestVisible = true;
                OverlayHost2.Visibility = Visibility.Visible;
            }
            else
            {
                OverlayHost2.Visibility = Visibility.Collapsed;
                OverlayHost2.IsHitTestVisible = false;
            }
        }

        public void ToggleFeed(bool show, int id = 0)
        {
            if (id == 0)
            {
                ToggleOverlayRow.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (id == 2)
            {
                ToggleOverlayRow2.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (id == 3)
            {
                ToggleOverlayRow3.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public void ShowBreadcrumb(int currentFolderId)
        {
            BreadcrumbPanel.Children.Clear();

            // Always include root
            List<HierarchyElement> path = new();

            if (currentFolderId <= 0 || !hierarchyById.ContainsKey(currentFolderId))
            {
                if (settings.tft_mode)
                {
                    path.Add(new HierarchyElement { ID = -1, Name = "TFT" });
                }
                else
                {
                    path.Add(new HierarchyElement { ID = 0, Name = "LOL" });
                }
            }
            else
            {
                int id = currentFolderId;
                while (id > 0 && hierarchyById.TryGetValue(id, out var element))
                {
                    path.Add(element);
                    id = element.parent;
                }
                path.Reverse();
                if (settings.tft_mode)
                {
                    path.Insert(0, new HierarchyElement { ID = -1, Name = "TFT" });
                }
                else
                {
                    path.Insert(0, new HierarchyElement { ID = 0, Name = "LOL" });
                }
            }

            // Add buttons and separators
            for (int i = 0; i < path.Count; i++)
            {
                var folder = path[i];

                // Folder button adjusted for 25px panel height
                var btn = new Button
                {
                    Content = folder.Name,
                    Background = Brushes.Transparent,
                    Foreground = (Brush)Application.Current.Resources[SystemColors.ControlTextBrushKey],
                    BorderBrush = (Brush)Application.Current.Resources["AccentBrush"],
                    BorderThickness = new Thickness(1.5),
                    FontSize = 12,
                    Height = 19, // slightly smaller than panel height to fit
                    Padding = new Thickness(5, 0, 5, 0),
                    Margin = new Thickness(1, 1, 1, 1),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Cursor = Cursors.Hand,
                    Style = (Style)Application.Current.Resources["diagwindow_highlight"]
                };

                // Rounded corners for the button
                var borderStyle = new Style(typeof(Border));
                borderStyle.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(4)));
                btn.Resources.Add(typeof(Border), borderStyle);

                int folderId = folder.ID; // capture for lambda
                btn.Click += (s, e) => RefreshModListPanel(folderId);

                BreadcrumbPanel.Children.Add(btn);

                // "/" separator (not clickable)
                if (i < path.Count - 1)
                {
                    var slash = new TextBlock
                    {
                        Text = "/",
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(2, 0, 2, 0),
                        FontSize = 12
                    };
                    BreadcrumbPanel.Children.Add(slash);
                }
            }
        }





        private Dictionary<(bool isMod, string id), ModListEntry> cachedUIElements = new Dictionary<(bool, string), ModListEntry>();

        public void RefreshModListPanel(int c_location, bool rebuild = false)
        {
            FolderListEntriesInDisplay.Clear();
            modListEntriesInDisplay.Clear();
            string searchString = Global_searchText;

            // Clear all existing UI entries
            ModListPanel.Children.Clear();

            // If rebuild is true, clear the cache to force recreation
            if (rebuild)
            {
                cachedUIElements.Clear();
            }

            if (!hierarchyById.TryGetValue(c_location, out var root))
            {
                if (!hierarchyById.TryGetValue(Current_location_folder, out var meow))
                {
                    if (settings.tft_mode)
                    {
                        RefreshModListPanel(-1, rebuild);
                    }
                    else
                    {
                        RefreshModListPanel(0, rebuild);
                    }
                }
                else
                {
                    RefreshModListPanel(Current_location_folder, rebuild);
                }
            }
            
            
            var folders = new List<(string childId, HierarchyElement element)>();
            var mods = new List<(string childId, Mod element)>();

            bool isEmptySearch = string.IsNullOrWhiteSpace(searchString);
            bool isFlatFullModSearch = searchString == "-f";
            bool isFlatFilteredModSearch = searchString.StartsWith("-f ");
            bool isFullModSearch = searchString == "-g";
            bool isFilteredModSearch = searchString.StartsWith("-g ");
            bool isRecursiveModSearch = searchString == "-l";
            bool isFilteredRecursiveModSearch = searchString.StartsWith("-l ");
            string filteredSearchString = isFilteredModSearch ? searchString.Substring(3) :
                                         isFlatFilteredModSearch ? searchString.Substring(3) :
                                         isFilteredRecursiveModSearch ? searchString.Substring(3) : searchString;

            if (isEmptySearch)
            {
                if (c_location > 0)
                {
                    AddParentFolderEntry(root);
                }
                foreach (var childTuple in root.Children)
                {
                    string childId = childTuple.Item1;
                    if (childTuple.Item2 == false) // folder
                    {
                        if (hierarchyById.TryGetValue(int.Parse(childTuple.Item1), out var childElement))
                        {
                            folders.Add((childId, childElement));
                        }
                    }
                    else // mod
                    {
                        if (modByFolder.TryGetValue(childId, out var childElement))
                        {
                            mods.Add((childId, childElement));
                        }
                    }
                }
            }
            else if (isFullModSearch)
            {
                // Add all mods from Dictionary, no folders
                foreach (var modKvp in modByFolder)
                {
                    mods.Add((modKvp.Key, modKvp.Value));
                }
            }
            else if (isFilteredModSearch)
            {
                foreach (var modKvp in modByFolder)
                {
                    if (MatchesSearchCriteria(null, modKvp.Value, filteredSearchString))
                    {
                        mods.Add((modKvp.Key, modKvp.Value));
                    }
                }
            }
            else if (isFlatFullModSearch)
            {
                int loc = 0;
                if (settings.tft_mode) loc = -1;
                if(hierarchyById.TryGetValue(loc, out var search_root))
                {
                    CollectAllModsRecursively(search_root, mods);
                }
            }
            else if (isFlatFilteredModSearch)
            {
                int loc = 0;
                if (settings.tft_mode) loc = -1;
                if (hierarchyById.TryGetValue(loc, out var search_root))
                {
                    CollectAllModsRecursively(search_root, mods, filteredSearchString);
                }
            }
            else if (isRecursiveModSearch)
            {
                CollectAllModsRecursively(root, mods);
            }
            else if (isFilteredRecursiveModSearch)
            {
                CollectAllModsRecursively(root, mods, filteredSearchString);
            }
            else
            {
                if (c_location > 0)
                {
                    AddParentFolderEntry(root);
                }
                foreach (var childTuple in root.Children)
                {
                    string childId = childTuple.Item1;
                    if (childTuple.Item2 == false) // folder
                    {
                        if (hierarchyById.TryGetValue(int.Parse(childTuple.Item1), out var childElement))
                        {
                            if (MatchesSearchCriteria(childElement, null, searchString))
                            {
                                folders.Add((childId, childElement));
                            }
                        }
                    }
                    else // mod
                    {
                        if (modByFolder.TryGetValue(childId, out var childElement))
                        {
                            if (MatchesSearchCriteria(null, childElement, searchString))
                            {
                                mods.Add((childId, childElement));
                            }
                        }
                    }
                }
            }

            

            folders.Sort((a, b) => string.Compare(a.element.Name, b.element.Name, StringComparison.OrdinalIgnoreCase));
            mods.Sort((a, b) => string.Compare(a.element.Info.Name, b.element.Info.Name, StringComparison.OrdinalIgnoreCase));


            if (c_location != Current_location_folder)
            {
                ShowBreadcrumb(c_location);
                Details_Panel.Text = "";
                GlobalselectedEntries.Clear();
            }
            Current_location_folder = c_location;


            foreach (var (childId, childElement) in folders)
            {
                if (!ProfileEntries.ContainsKey(childId) && display_only_active) { continue; }

                var cacheKey = (false, childId); // isMod = false for folders
                ModListEntry folderEntry;

                // Check if element is already cached
                if (cachedUIElements.TryGetValue(cacheKey, out var cachedEntry))
                {
                    // Load from cache
                    folderEntry = cachedEntry;
                }
                else
                {
                    // Create new entry
                    folderEntry = new ModListEntry(childElement.ID.ToString());
                    folderEntry.InitializeWithFolder(childElement);
                    folderEntry.FolderDoubleClicked += (folderId) => RefreshModListPanel(folderId, rebuild);

                    // Add to cache
                    cachedUIElements[cacheKey] = folderEntry;
                }

                FolderListEntriesInDisplay.Add(folderEntry);
                ModListPanel.Children.Add(folderEntry);

                if (GlobalselectedEntries.Contains(childElement.ID.ToString()))
                {
                    folderEntry.SetSelection(true);
                    folderEntry.RefreshDisplay(false, true);
                }
            }

            // Add mods second
            foreach (var (childId, childElement) in mods)
            {
                if (!ProfileEntries.ContainsKey(childId) && display_only_active) { continue; }

                var cacheKey = (true, childElement.ModFolder); // isMod = true for mods
                ModListEntry modEntry;

                // Check if element is already cached
                if (cachedUIElements.TryGetValue(cacheKey, out var cachedEntry))
                {
                    // Load from cache
                    modEntry = cachedEntry;
                }
                else
                {
                    // Create new entry
                    var modWads = childElement.Wads; // string list
                    var modAuthor = childElement.Info.Author; // string
                    modEntry = new ModListEntry(childElement.ModFolder);
                    modEntry.InitializeWithMod(childElement);

                    // Add to cache
                    cachedUIElements[cacheKey] = modEntry;
                }

                modListEntriesInDisplay.Add(modEntry);
                ModListPanel.Children.Add(modEntry);

                if (GlobalselectedEntries.Contains(childElement.ModFolder))
                {
                    modEntry.SetSelection(true);
                    modEntry.RefreshDisplay(false, true);
                }
            }
            DelayedAdjustModListLayout();
            if (rebuild)
            {
                PreCacheAllUIElements();
            }
        }
        static async Task CheckForAppUpdates()
        {
            const string OWNER = "Aurecueil";
            const string REPO = "Cs-lol-go";

            string baseDir = AppContext.BaseDirectory;
            string versionFile = Path.Combine(baseDir, "version.txt");

            string localVersion = File.Exists(versionFile)
                ? File.ReadAllText(versionFile).Trim()
                : "0.0.0";

            using HttpClient http = new();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("cslol-go-check-update");

            string json = await http.GetStringAsync(
                $"https://api.github.com/repos/{OWNER}/{REPO}/releases/latest");

            using JsonDocument doc = JsonDocument.Parse(json);

            string remoteVersion = doc.RootElement
                .GetProperty("tag_name")
                .GetString()!
                .TrimStart('v');

            if (!Version.TryParse(remoteVersion, out var r) ||
                !Version.TryParse(localVersion, out var l) ||
                r <= l)
                return; // no update
            if (!File.Exists("cslol-go.exe")) return;
            try
            {
                CustomMessageBox.Show("Update Found! cslol-go will now close, and auto update", null, "Update");
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cslol-go.exe",
                    Arguments = "-w",
                    UseShellExecute = true
                });
            }
            catch { }
            finally
            {
                Environment.Exit(0);
            }

        }
        private async Task PreCacheAllUIElements()
        {
            double current = 1;
            double max = hierarchyById.Count;
            // Cache all folder entries
            foreach (var kvp in hierarchyById)
            {

                SetLoading(kvp.Value.Name, 2, current / max);
                current += 1;
                int folderId = kvp.Key;
                HierarchyElement folderElement = kvp.Value;

                var cacheKey = (false, folderId.ToString()); // isMod = false for folders

                // Check if element is already cached
                if (!cachedUIElements.ContainsKey(cacheKey))
                {
                    // Create new folder entry
                    var folderEntry = new ModListEntry(folderId.ToString());
                    folderEntry.InitializeWithFolder(folderElement);
                    folderEntry.FolderDoubleClicked += (id) => RefreshModListPanel(id, false);

                    // Add to cache
                    cachedUIElements[cacheKey] = folderEntry;
                }

                await Dispatcher.Yield(DispatcherPriority.Background);
            }
            current = 1;
            max = modByFolder.Count;

            foreach (var kvp in modByFolder)
            {
                string modId = kvp.Key;
                SetLoading(kvp.Value.Info.Name, 3, current / max);
                current += 1;
                Mod modElement = kvp.Value;

                var cacheKey = (true, modId); // isMod = true for mods

                // Check if element is already cached
                if (!cachedUIElements.ContainsKey(cacheKey))
                {
                    // Create new mod entry
                    var modEntry = new ModListEntry(modId);
                    modEntry.InitializeWithMod(modElement);
                    // Add any mod-specific event handlers here if needed

                    // Add to cache
                    cachedUIElements[cacheKey] = modEntry;
                }

                await Dispatcher.Yield(DispatcherPriority.Background);
            }
        }
        private void CollectAllModsRecursively(HierarchyElement currentFolder, List<(string childId, Mod element)> mods, string searchFilter = null)
        {
            foreach (var childTuple in currentFolder.Children)
            {
                string childId = childTuple.Item1;
                if (childTuple.Item2 == false) // folder
                {
                    if (hierarchyById.TryGetValue(int.Parse(childTuple.Item1), out var childFolder))
                    {
                        // Recursively call for this folder
                        CollectAllModsRecursively(childFolder, mods, searchFilter);
                    }
                }
                else // mod
                {
                    if (modByFolder.TryGetValue(childId, out var childMod))
                    {
                        // If no search filter, add all mods; otherwise check criteria
                        if (string.IsNullOrEmpty(searchFilter) || MatchesSearchCriteria(null, childMod, searchFilter))
                        {
                            mods.Add((childId, childMod));
                        }
                    }
                }
            }
        }
        private async void DelayedAdjustModListLayout()
        {
            await Task.Delay(10);
            AdjustModListLayout();
        }

        private void set_active_only_disp(object sender, RoutedEventArgs e)
        {
            display_only_active = only_active_disp_checkbox.IsChecked == true;
            RefreshModListPanel(Current_location_folder);
        }


        private bool MatchesSearchCriteria(HierarchyElement folderElement, Mod modElement, string searchString)
        {
            if (string.IsNullOrWhiteSpace(searchString))
                return true;

            // Split the entire search string by OR groups "||"
            var orGroups = searchString.Split(new[] { "||" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var group in orGroups)
            {
                // Parse group into (prefix, term) pairs using the new method
                var searchGroups = ParseSearchGroups(group);

                bool allTermsMatch = true;

                foreach (var (prefix, term) in searchGroups)
                {
                    bool termMatched = false;

                    if (!string.IsNullOrWhiteSpace(prefix))
                    {
                        // Check prefixes with partial matching

                        if (IsPartialMatch(prefix, "name:", out _)) // prefix here like "a:" or "name:"
                        {
                            if (folderElement != null)
                                termMatched = folderElement.Name.Contains(term, StringComparison.OrdinalIgnoreCase);
                            else if (modElement != null)
                                termMatched = modElement.Info.Name.Contains(term, StringComparison.OrdinalIgnoreCase);
                        }
                        else if (IsPartialMatch(prefix, "author:", out _))
                        {
                            if (modElement != null)
                                termMatched = modElement.Info.Author.Contains(term, StringComparison.OrdinalIgnoreCase);
                        }
                        else if (IsPartialMatch(prefix, "wad:", out _))
                        {
                            if (modElement != null)
                                termMatched = modElement.Wads.Any(wad => wad.Contains(term, StringComparison.OrdinalIgnoreCase));
                        }
                        else
                        {
                            // Unknown prefix — treat as no match
                            termMatched = false;
                        }
                    }
                    else
                    {
                        // No prefix => search only mod/folder names (your requested logic)
                        if (folderElement != null)
                            termMatched = folderElement.Name.Contains(term, StringComparison.OrdinalIgnoreCase);
                        else if (modElement != null)
                            termMatched = modElement.Info.Name.Contains(term, StringComparison.OrdinalIgnoreCase);
                    }

                    if (!termMatched)
                    {
                        allTermsMatch = false;
                        break;
                    }
                }

                if (allTermsMatch)
                    return true; // One OR group matched fully
            }

            return false; // No OR group matched completely
        }



        private List<(string prefix, string term)> ParseSearchGroups(string searchString)
{
    var results = new List<(string prefix, string term)>();
    if (string.IsNullOrWhiteSpace(searchString))
        return results;

    // Split on || first to handle OR groups separately
    var orGroups = searchString.Split(new[] { "||" }, StringSplitOptions.RemoveEmptyEntries);

    foreach (var orGroup in orGroups)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        // Tokenize respecting quotes and spaces
        for (int i = 0; i < orGroup.Length; i++)
        {
            char c = orGroup[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
                // Include the quote in the token for clarity
                // or skip adding quotes (your choice)
                continue;
            }
            else if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }
        if (current.Length > 0)
            tokens.Add(current.ToString());

        // Now group tokens by prefix boundaries
        string currentPrefix = null;
        var currentTerms = new List<string>();

        bool IsPrefix(string token, out string prefix)
        {
            prefix = null;
            int colonIndex = token.IndexOf(':');
            if (colonIndex > 0)
            {
                prefix = token.Substring(0, colonIndex + 1).ToLower(); // include colon
                return true;
            }
            return false;
        }

        void FlushCurrentGroup()
        {
            if (currentTerms.Count > 0)
            {
                string combinedTerm = string.Join(" ", currentTerms);
                results.Add((currentPrefix, combinedTerm));
                currentTerms.Clear();
            }
        }

        foreach (var token in tokens)
        {
            if (IsPrefix(token, out string prefix))
            {
                // Flush previous group before starting new prefix group
                FlushCurrentGroup();

                currentPrefix = prefix;
                // Add rest of token after colon as first term
                var afterColon = token.Substring(prefix.Length);
                if (!string.IsNullOrWhiteSpace(afterColon))
                    currentTerms.Add(afterColon);
            }
            else
            {
                currentTerms.Add(token);
            }
        }

        FlushCurrentGroup();

        // Note: If you want to handle OR groups separately,
        // you could store that info or return a list of lists.
        // For now, just flattening all.
    }

    return results;
}


        private bool IsPartialMatch(string term, string fullPrefix, out string searchValue)
        {
            searchValue = null;

            // Check if term contains a colon
            int colonIndex = term.IndexOf(':');
            if (colonIndex == -1)
                return false;

            string prefixWithColon = term.Substring(0, colonIndex + 1);
            string fullPrefixWithoutColon = fullPrefix.Substring(0, fullPrefix.Length - 1); // Remove colon from fullPrefix

            // Check if the fullPrefix starts with the user's prefix (case insensitive)
            if (fullPrefixWithoutColon.StartsWith(prefixWithColon.Substring(0, colonIndex), StringComparison.OrdinalIgnoreCase) &&
                colonIndex >= 1) // At least 1 character before colon
            {
                searchValue = term.Substring(colonIndex + 1).Trim();
                return true;
            }

            return false;
        }
        private void InitializeSearchBox()
        {
            SearchBox.TextChanged += OnSearchBoxTextChanged;
        }

        private void OnSearchBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            Global_searchText = SearchBox.Text;
            RefreshModListPanel(Current_location_folder);
        }

        public string GetDetails(string new_id)
        {
            var sb = new StringBuilder();

            if (int.TryParse(new_id, out int id) && hierarchyById.TryGetValue(id, out var folder))
            {
                // Folder header
                sb.AppendLine($"📂 Folder: {folder.Name}");
                sb.AppendLine(new string('=', 10));

                // List Mods
                var mods = folder.Children.Where(c => c.Item2).ToList();
                var subfolders = folder.Children.Where(c => !c.Item2).ToList();

                if (mods.Count > 0)
                {
                    sb.AppendLine("Mods in this folder:");
                    foreach (var item in mods)
                    {
                        if (modByFolder.TryGetValue(item.Item1, out var mod))
                        {
                            sb.AppendLine($"- {mod.Info.Name} (v{mod.Info.Version}) {(mod.isActive ? "[Active]" : "[Inactive]")}");
                        }
                    }
                    sb.AppendLine();
                }

                // Recursively list subfolders
                if (subfolders.Count > 0)
                {
                    sb.AppendLine("Subfolders:");
                    foreach (var sub in subfolders)
                    {
                        if (int.TryParse(sub.Item1, out int childId) && hierarchyById.TryGetValue(childId, out var childFolder))
                        {
                            sb.AppendLine($"- {childFolder.Name}");
                        }

                        string nested = GetDetails(sub.Item1).Trim();
                        if (!string.IsNullOrEmpty(nested))
                        {
                            foreach (var line in nested.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                            {
                                sb.AppendLine($"    {line.Trim()}");
                            }
                        }
                    }
                }
            }
            else if (modByFolder.TryGetValue(new_id, out var mod))
            {
                // Detailed Mod Info
                sb.AppendLine($"Mod: {mod.Info.Name}");
                sb.AppendLine();
                sb.AppendLine($"Author: {mod.Info.Author}");
                sb.AppendLine($"Version: {mod.Info.Version}");
                sb.AppendLine($"Heart: {mod.Info.Heart}");
                sb.AppendLine($"Home: {mod.Info.Home}");
                sb.AppendLine();
                sb.AppendLine("Description:");
                sb.AppendLine();
                sb.AppendLine($"Status: {(mod.isActive ? ":white_check_mark: Active" : ":x: Inactive")}");
                sb.AppendLine($"Priority: {mod.Details.Priority}");
                sb.AppendLine($"Overrides: {(mod.Details.override_ ? "Yes" : "No")}");
                sb.AppendLine($"Random Enabled: {(mod.Details.Random ? "Yes" : "No")}");
                sb.AppendLine($"Inner Path: {mod.Details.InnerPath}");
                sb.AppendLine(string.IsNullOrWhiteSpace(mod.Info.Description) ? "No description provided." : mod.Info.Description);
                sb.AppendLine();
                sb.AppendLine("Mod Folder:");
                sb.AppendLine(mod.ModFolder);
                sb.AppendLine();

                if (mod.Wads.Count > 0)
                {
                    sb.AppendLine("Included WAD Files:");
                    foreach (var wad in mod.Wads)
                    {
                        sb.AppendLine($"- {wad}");
                    }
                }

                // Image info
            }

            return sb.ToString().TrimEnd('\n', '\r');
        }




        void AddParentFolderEntry(HierarchyElement currentElement)
        {
            var parentEntry = new ModListEntry("0");

            // Initialize as parent folder entry
            parentEntry.InitializeAsParentFolder(currentElement.parent);

            // Subscribe to double-click event for parent navigation
            parentEntry.FolderDoubleClicked += (parentId) =>
            {
                if (hierarchyById.TryGetValue(currentElement.parent, out var parent))
                {
                    RefreshModListPanel(parent.ID);
                }
                else
                {
                    // If no parent found, go to root
                    RefreshModListPanel(0);
                }
            };

            ModListPanel.Children.Add(parentEntry);
        }

        public void DragHandler(List<(string, bool)> draggedElements, (string, bool) dropTarget,bool override_location = false, int location = 0)
        {
            if (dropTarget.Item2)
                return;
            int dropTargetId = int.Parse(dropTarget.Item1);

            int cureent_location = Current_location_folder;
            if (override_location)
            {
                cureent_location = location;
            }

            if (!hierarchyById.TryGetValue(cureent_location, out var CurrenFolderLocation))
            {
                return;
            }
            if (!hierarchyById.TryGetValue(dropTargetId, out var DropTargetElement))
            {
                return;
            }

            foreach (var draggedElement in draggedElements)
            {
                if (draggedElement.Item1 == dropTarget.Item1)
                {
                    continue;
                }
                // draggedElement is (string name, bool isMod)
                var match = CurrenFolderLocation.Children.FirstOrDefault(child =>
                    child.Item1 == draggedElement.Item1 && child.Item2 == draggedElement.Item2);

                if (match != null)
                {
                    if (dropTarget.Item1 == draggedElement.Item1) continue;

                    CurrenFolderLocation.Children.Remove(match);
                    DropTargetElement.Children.Add(match);



                    if (match.Item2 == true)
                    {
                        if (modByFolder.TryGetValue(match.Item1, out var cchildElement))
                        {
                            cchildElement.Details.InnerPath = BuildInnerPath(int.Parse(dropTarget.Item1), hierarchyById);

                            // Update details.json
                            string detailsPath = Path.Combine("installed", cchildElement.ModFolder, "META", "details.json");

                            try
                            {
                                string json = JsonSerializer.Serialize(cchildElement.Details, new JsonSerializerOptions { WriteIndented = true });
                                File.WriteAllText(detailsPath, json);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Failed to update details.json:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                        

                    }
                    else
                    {
                        if (hierarchyById.TryGetValue(int.Parse(match.Item1), out var cchildElement))
                        {
                            cchildElement.parent = dropTargetId;
                            SaveOrUpdateHierarchyElement(cchildElement, hierarchyById);
                            void updt(HierarchyElement childe)
                            {
                                foreach (var child in childe.Children)
                                {
                                    if (child.Item2 == true) continue;
                                    if (hierarchyById.TryGetValue(int.Parse(child.Item1), out var kid))
                                    {
                                        SaveOrUpdateHierarchyElement(kid, hierarchyById);
                                        updt(kid);
                                    }
                                }
                            }
                            updt(cchildElement);
                        }
                    }



                }
            }
            RefreshModListPanel(Current_location_folder);
        }
        public static void SaveOrUpdateHierarchyElement(
    HierarchyElement element,
    Dictionary<int, HierarchyElement> hierarchyById,
    string filePath = null)
        {
            filePath ??= Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "folders.json");

            try
            {
                List<Folder> folders;

                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    folders = JsonSerializer.Deserialize<List<Folder>>(json) ?? new();
                }
                else
                {
                    folders = new List<Folder>();
                }

                // Check if this ID exists in folders.json
                int index = folders.FindIndex(f => f.ID == element.ID);

                // If parent changed, recalculate InnerPath
                element.InnerPath = BuildInnerPath(element.parent, hierarchyById);

                // Create updated Folder object with parent
                Folder updated = new Folder
                {
                    ID = element.ID,
                    name = element.Name,
                    Override = element.override_,
                    priority = element.Priority,
                    random = element.Random,
                    InnerPath = element.InnerPath,
                    parent = element.parent,
                };

                if (index >= 0)
                    folders[index] = updated;
                else
                    folders.Add(updated);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = null
                };

                string outputJson = JsonSerializer.Serialize(folders, options);
                File.WriteAllText(filePath, outputJson);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating folders.json: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static string BuildInnerPath(int parentId, Dictionary<int, HierarchyElement> hierarchyById)
        {
            if (parentId == 0 || !hierarchyById.ContainsKey(parentId))
            {
                return "0";
            }
            if (parentId == -1 || !hierarchyById.ContainsKey(parentId))
            {
                return "-1";
            }

            List<int> pathSegments = new();
            int currentId = parentId;

            while (hierarchyById.TryGetValue(currentId, out var parentElement))
            {
                pathSegments.Add(parentElement.ID);
                if (currentId <= 0) break;
                currentId = parentElement.parent;
            }

            pathSegments.Reverse(); // From root to parent
            return string.Join("/", pathSegments);
        }


        public void LoadMods()
        {
            if (!Directory.Exists(installedPath))
            {
                Directory.CreateDirectory(installedPath);
                return;
            }

            string[] modFolders = Directory.GetDirectories(installedPath);

            Parallel.ForEach(
                modFolders,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                },
                modFolderPath =>
                {
                    string modFolderName = Path.GetFileName(modFolderPath);

                    try
                    {
                        Mod mod = CreateModFromFolder(modFolderPath);
                    }
                    catch (Exception ex)
                    {
                        // UI access must be marshaled to UI thread
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            CustomMessageBox.Show(
                                $"Error reading mod installed/{modFolderName} : {ex.Message}", null, "Error"
                            );
                        });
                    }
                }
            );
        }

        public void SaveModInfo(Mod mod, string basePath = null)
        {
            basePath ??= Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "installed");

            string infoPath = Path.Combine(basePath, mod.ModFolder,"META" , "info.json");

            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = null };
                string json = JsonSerializer.Serialize(mod.Info, options);

                File.WriteAllText(infoPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving info.json for mod '{mod.ModFolder}': {ex.Message}");
            }
            if (cachedUIElements.TryGetValue((true, mod.ModFolder), out var modEntry))
                modEntry.RefreshDisplay(true);
        }

        public void SaveModDetails(Mod mod, string basePath = null, bool update_mod_image = false)
        {
            if (modByFolder.TryGetValue(mod.ModFolder, out var mod_element))
            {
                mod_element = mod;
            }
            basePath ??= Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "installed");

            string detailsPath = Path.Combine(basePath, mod.ModFolder, "META" , "details.json");

            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = null };
                string json = JsonSerializer.Serialize(mod.Details, options);

                File.WriteAllText(detailsPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving details.json for mod '{mod.ModFolder}': {ex.Message}");
            }
            if (cachedUIElements.TryGetValue((true, mod.ModFolder), out var modEntry))
                modEntry.RefreshDisplay(true, false, update_mod_image, update_mod_image);
        }
        
        public void SaveFolder(HierarchyElement _folder)
        {
            if (hierarchyById.TryGetValue(_folder.ID, out var mod_element))
            {
                mod_element = _folder;
                SaveOrUpdateHierarchyElement(_folder, hierarchyById);
            }
        }
        public void RefreshCachedElementDisplay(bool isMod, string id)
        {
            if (cachedUIElements.TryGetValue((isMod, id), out var element))
                element.RefreshDisplay(true, true, true);
        }
        public void RefreshAllCachedElementsDisplay(bool info = false, bool basee = false, bool image = false)
        {
            foreach (var cachedElement in cachedUIElements.Values)
            {
                cachedElement.RefreshDisplay(info, basee, image);
            }
        }

        public static void RemoveHierarchyElement(int id, Dictionary<int, HierarchyElement> hierarchyById, string filePath = null)
        {
            filePath ??= Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "folders.json");
            if (id == 0) return;
            try
            {
                // Remove from in-memory hierarchy dictionary
                if (hierarchyById.ContainsKey(id))
                {
                    hierarchyById.Remove(id);
                }

                List<Folder> folders;

                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    folders = JsonSerializer.Deserialize<List<Folder>>(json) ?? new();
                }
                else
                {
                    // No file or empty list, nothing to remove
                    return;
                }

                // Remove from folder list by ID
                folders.RemoveAll(f => f.ID == id);

                // Save back updated list
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = null
                };

                string outputJson = JsonSerializer.Serialize(folders, options);
                File.WriteAllText(filePath, outputJson);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error removing element from folders.json: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public Mod CreateModFromFolder(string modFolderPath, bool override_inner_path = false)
        {
            string modFolderName = Path.GetFileName(modFolderPath);
            string metaPath = Path.Combine(modFolderPath, "META");
            string infoPath = Path.Combine(metaPath, "info.json");
            string detailsPath = Path.Combine(metaPath, "details.json");
            string wadPath = Path.Combine(modFolderPath, "WAD");

            if (!Directory.Exists(metaPath))
            {
                Directory.CreateDirectory(metaPath);
            }

            

            ModDetails modDetails = new ModDetails(); // Default values
            if (File.Exists(detailsPath))
            {
                try
                {
                    string detailsJson = File.ReadAllText(detailsPath);
                    modDetails = JsonSerializer.Deserialize<ModDetails>(detailsJson) ?? new ModDetails();
                    if (override_inner_path)
                    {
                        modDetails.InnerPath = BuildInnerPath(Current_location_folder, hierarchyById);
                    }
                }
                catch (Exception)
                {
                    if (override_inner_path)
                    {
                        modDetails.InnerPath = BuildInnerPath(Current_location_folder, hierarchyById);
                    }
                    string defaultDetailsJson = JsonSerializer.Serialize(modDetails, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(detailsPath, defaultDetailsJson);
                }
            }
            else
            {
                if (override_inner_path)
                {
                    modDetails.InnerPath = BuildInnerPath(Current_location_folder, hierarchyById);
                }
                string defaultDetailsJson = JsonSerializer.Serialize(modDetails, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(detailsPath, defaultDetailsJson);
            }

            string wadFolder = Path.Combine(modFolderPath, "WAD_base");
            string wadBaseFolder = Path.Combine(modFolderPath, "WAD");

            // If folder "WAD_base" exists and "WAD" does not → rename
            if (Directory.Exists(wadFolder))
            {
                if (!Directory.Exists(wadBaseFolder))
                {
                    Directory.Move(wadFolder, wadBaseFolder);
                }
                else
                {
                    // If both exist, merge contents of WAD into WAD_base and delete WAD
                    foreach (string file in Directory.GetFiles(wadFolder, "*", SearchOption.AllDirectories))
                    {
                        string relative = Path.GetRelativePath(wadFolder, file);
                        string target = Path.Combine(wadBaseFolder, relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        File.Move(file, target, true);
                    }
                    Directory.Delete(wadFolder, true);
                }
            }

            // Collect all folders that start with WAD_
            var wadFolders = Directory.GetDirectories(modFolderPath, "WAD_*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .ToList();

            // Remove non-existent layers
            modDetails.Layers.RemoveAll(l =>
            {
                return !wadFolders.Contains(l.folder_name);
            });

            string layerName = "base";
            modDetails.Layers.Add(new LayerInfo
            {
                Name = layerName,
                Priority = modDetails.Layers.Count + 1,
                folder_name = "WAD"
            });

            foreach (var wadDir in wadFolders)
            {
                layerName = wadDir.Substring(4);
                if (!modDetails.Layers.Any(l => l.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase)))
                {
                    modDetails.Layers.Add(new LayerInfo
                    {
                        Name = layerName,
                        Priority = modDetails.Layers.Count + 1,
                        folder_name = wadDir
                    });
                }
                else
                {
                    modDetails.Layers.Add(new LayerInfo
                    {
                        Name = $"{layerName}_2",
                        Priority = modDetails.Layers.Count + 1,
                        folder_name = wadDir
                    });
                }
            }

            

            File.WriteAllText(detailsPath, JsonSerializer.Serialize(modDetails, new JsonSerializerOptions { WriteIndented = true }));

            List<string> wads = new List<string>();
            if (Directory.Exists(wadPath))
            {
                try
                {
                    string[] wadEntries = Directory.GetFileSystemEntries(wadPath);
                    foreach (string entry in wadEntries)
                    {
                        var lastPart = entry.Split('\\').Last();
                        wads.Add(Path.GetFileName(lastPart));

                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error reading WAD directory for mod '{modFolderName}': {ex.Message}");
                }
            }
            ModInfo modInfo = new ModInfo();
            if (File.Exists(infoPath))
            {
                try
                {
                    string infoJson = File.ReadAllText(infoPath);
                    modInfo = JsonSerializer.Deserialize<ModInfo>(infoJson);

                    // modInfo.Author ??= settings.default_author;
                    // modInfo.Description ??= "";
                    // modInfo.Heart ??= settings.default_Hearth;
                    // modInfo.Home ??= settings.default_home;
                    // modInfo.Name ??= "null";
                    // modInfo.Version ??= "1.0.0";
                }
                catch (Exception)
                {
                    modInfo.Name = modFolderName;
                    modInfo.Author = settings.default_author;
                    modInfo.Heart = settings.default_Hearth;
                    modInfo.Home = settings.default_home;
                    string defaultDetailsJson = JsonSerializer.Serialize(modInfo, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(infoPath, defaultDetailsJson);
                }
            }
            else
            {
                modInfo.Name = modFolderName;
                modInfo.Author = settings.default_author;
                string defaultDetailsJson = JsonSerializer.Serialize(modInfo, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(infoPath, defaultDetailsJson);
            }
            if (modInfo.Description.Contains("darkseal") || modInfo.Author.Contains("darkseal")) return null;

            // Create and return Mod object
            Mod new_mod_netyr = new Mod
            {
                Info = modInfo,
                Details = modDetails,
                ModFolder = modFolderName,
                Wads = wads
            };
               
            if (modByFolder.TryGetValue(modFolderName, out var old_entry))
            {
                int parent = get_parent_from_innerPath(old_entry.Details.InnerPath);
                if (hierarchyById.TryGetValue(parent, out var old_parent)) {
                    old_parent.Children.RemoveAll(child => child.Item1 == old_entry.ModFolder && child.Item2 == true);
                }
                modByFolder.Remove(modFolderName);
            }
            int new_parent = get_parent_from_innerPath(new_mod_netyr.Details.InnerPath);
            modByFolder[modFolderName] = new_mod_netyr;
            AddChild(new_parent, new_mod_netyr.ModFolder, true);
            return new_mod_netyr;
        }

        public int get_parent_from_innerPath(string innerPath)
        {
            int parent = 0;

            var parts = innerPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            for (int i = parts.Length - 1; i >= 0; i--)
            {
                string possibleParent = parts[i];

                if (!string.IsNullOrWhiteSpace(possibleParent) && int.TryParse(possibleParent, out int id))
                {
                    if (hierarchyById.ContainsKey(id))
                    {
                        parent = id;
                        break;
                    }
                }
            }

            return parent;
        }



        private void DropScrollViewer_DragOver(object sender, DragEventArgs e)
        {
                // Check if data is from Explorer
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    if (IsOverlayBlocking())
                    {
                        e.Effects = DragDropEffects.None;
                        e.Handled = true;
                        return;
                    }

                    if (e.Data.GetDataPresent(DataFormats.FileDrop))
                    {
                        string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                        if (paths.Any(IsAcceptedDropItem))
                        {
                            e.Effects = DragDropEffects.Copy;
                        }
                        else
                        {
                            e.Effects = DragDropEffects.None;
                        }
                    }
                    else
                    {
                        e.Effects = DragDropEffects.None;
                    }

                    e.Handled = true;
                    e.Effects = DragDropEffects.Copy;
                    e.Handled = true;
                }
                else
                {
                    // Let child entries handle internal drags
                    e.Effects = DragDropEffects.None;
                    e.Handled = false;
                }
            
            
        }

        private bool IsOverlayBlocking()
        {
            return (OverlayHost.Visibility == Visibility.Visible && OverlayHost.Children.Count > 0) ||
                   (OverlayHost2.Visibility == Visibility.Visible && OverlayHost2.Children.Count > 0);
        }


        public void CreateStartMenuShortcut()
        {
            // string startMenuProgramsPath = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu) + @"\Programs";
            // string shortcutPath = Path.Combine(startMenuProgramsPath, "ModLoader-cslolgo.url");
            // string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            // 
            // string shortcutContent =
            //     "[InternetShortcut]\n" +
            //     $"URL=file:///{exePath.Replace('\\', '/')}\n" +
            //     "IconIndex=0\n" +
            //     $"IconFile={exePath}\n";
            // 
            // File.WriteAllText(shortcutPath, shortcutContent);
        }



        public async void handle_rf_install(string path)
        {
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(path);
            string extractTargetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "installed", fileNameWithoutExt);
            Directory.CreateDirectory(extractTargetDir);

            string ext = Path.GetExtension(path).ToLowerInvariant();

            if (ext != ".fantome" && ext != ".modpkg")
            {
                ext = ".unk";

                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    byte[] h = new byte[8];
                    fs.Read(h, 0, h.Length);

                    // ZIP → fantome (50 4B 03 04)
                    if (h[0] == 0x50 && h[1] == 0x4B && h[2] == 0x03 && h[3] == 0x04)
                    {
                        ext = ".fantome";
                    }
                    // "_modpkg_" (5F 6D 6F 64 70 6B 67 5F)
                    else if (h[0] == 0x5F && h[1] == 0x6D && h[2] == 0x6F && h[3] == 0x64 &&
                             h[4] == 0x70 && h[5] == 0x6B && h[6] == 0x67 && h[7] == 0x5F)
                    {
                        ext = ".modpkg";
                    }
                }
            }


            if (ext == ".fantome")
            {
                try
                {
                    using (var archive = ZipFile.OpenRead(path))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            string fullPath = Path.Combine(extractTargetDir, entry.FullName);

                            // Create directory if needed
                            if (string.IsNullOrEmpty(entry.Name))
                            {
                                Directory.CreateDirectory(fullPath);
                                continue;
                            }

                            // Ensure directory exists
                            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

                            // Overwrite the file
                            entry.ExtractToFile(fullPath, overwrite: true);
                        }
                    }
                    string folderName = Path.GetFileName(extractTargetDir.TrimEnd(Path.DirectorySeparatorChar));
                    Mod new_mod = CreateModFromFolder(extractTargetDir, true);
                    RefreshModListPanel(Current_location_folder);
                    string metaDir = Path.Combine(extractTargetDir, "meta");
                    Directory.CreateDirectory(metaDir);

                    File.Delete(path);
                }
                catch (Exception ex) { Logger.LogError("Error RF importing (extract): -->   ", ex); }
            }
            else if (ext == ".modpkg")
            {
                install_modpkg(path, true);
            }
            else
            {
                MessageBox.Show($"{Path.GetExtension(path)} is not a currently supported file format");
            }

                
        }


        private bool IsAcceptedDropItem(string path)
        {
            if (Directory.Exists(path))
                return true;

            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".zip" || ext == ".fantome" || ext == ".7fantome" || ext == ".wad.client" || ext == ".client" || ext == ".wad" || ext == ".7z" || ext == ".rar" || ext == ".modpkg";
        }

        private async void DropScrollViewer_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                if (IsOverlayBlocking()) return;

                string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                bool anyValid = false;

                // 1. Open the Feed
                ToggleFeed(true, 3);

                try
                {
                    foreach (string path in paths)
                    {
                        if (IsAcceptedDropItem(path))
                        {
                            anyValid = true;
                            string fileName = Path.GetFileName(path);

                            // 2. Update status text (Must be on UI thread)
                            Feed3.Text = $"importing mod {fileName}";

                            // 3. Await the heavy processing (Non-blocking)
                            await HandleDroppedItem(path);
                        }
                    }

                    if (anyValid)
                    {
                        // Refresh logic is now handled at the end or inside HandleDroppedItem safely
                        RefreshModListPanel(Current_location_folder);
                    }
                    else
                    {
                        // MessageBox.Show("no valid mods");
                    }
                }
                finally
                {
                    // 4. Close the feed when done, even if errors occur
                    ToggleFeed(false, 3);
                }

                e.Handled = true;
            }
        }

        public async Task HandleDroppedItem(string path)
        {
            // Offload the heavy work to a background thread
            await Task.Run(() =>
            {
                try
                {
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    bool isRaw = false;

                    if (ext == ".fantome" || ext == ".zip")
                    {
                        string fileName = Path.GetFileNameWithoutExtension(path);
                        bool isValidZip = false;

                        try
                        {
                            using (var archive = ZipFile.OpenRead(path))
                            {
                                isRaw = archive.Entries.Any(e =>
                                    e.FullName.StartsWith("RAW/", StringComparison.OrdinalIgnoreCase) ||
                                    e.FullName.StartsWith("RAW\\", StringComparison.OrdinalIgnoreCase));

                                bool hasWad = isRaw || archive.Entries.Any(e =>
                                    e.FullName.StartsWith("WAD/", StringComparison.OrdinalIgnoreCase) ||
                                    e.FullName.StartsWith("WAD\\", StringComparison.OrdinalIgnoreCase));

                                bool hasMetaInfo = archive.Entries.Any(e =>
                                    e.FullName.Equals("META/info.json", StringComparison.OrdinalIgnoreCase) ||
                                    e.FullName.Equals("META\\info.json", StringComparison.OrdinalIgnoreCase));

                                if (!hasWad || !hasMetaInfo)
                                {
                                    Application.Current.Dispatcher.Invoke(() =>
                                        MessageBox.Show("Invalid mod archive: Must contain 'WAD' or 'RAW' folder and 'META/info.json'", "Invalid Mod", MessageBoxButton.OK, MessageBoxImage.Error));
                                    return;
                                }

                                isValidZip = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                                MessageBox.Show($"Error reading archive:\n{ex.Message}", "Zip Error", MessageBoxButton.OK, MessageBoxImage.Error));
                            return;
                        }

                        if (!isValidZip) return;

                        string extractTargetDir = "";

                        // Note: Assuming 'settings' and 'installedPath' are thread-safe or static. 
                        // If they are UI controls, capture them before Task.Run.
                        if (modByFolder.TryGetValue(fileName, out var mod))
                        {
                            int overrideSetting = settings.import_override;

                            // bool shouldReturn = false;

                            // UI logic inside background task requires Dispatcher for standard execution flow
                            // However, we can't easily await a Dispatcher call in this structure for logic branching
                            // So we run the logic that determines paths here, but alerts on UI.

                            switch (overrideSetting)
                            {
                                case 0:
                                    Application.Current.Dispatcher.Invoke(() =>
                                        MessageBox.Show($"Mod already EXISTS under this path:\n{mod.ModFolder}", "Import Skipped", MessageBoxButton.OK, MessageBoxImage.Warning));
                                    return;

                                case 1:
                                    extractTargetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "installed", mod.ModFolder);
                                    break;

                                case 2:
                                    string wadPath = Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "installed", mod.ModFolder), "WAD");
                                    if (Directory.Exists(wadPath)) Directory.Delete(wadPath, true);

                                    string infoJsonPath = Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "installed", mod.ModFolder), "META", "info.json");
                                    if (File.Exists(infoJsonPath)) File.Delete(infoJsonPath);

                                    extractTargetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "installed", mod.ModFolder);
                                    Directory.CreateDirectory(extractTargetDir);
                                    break;

                                case 3:
                                    string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "installed", fileName);
                                    int i = 1;
                                    string newPath = basePath + "_" + i;
                                    while (Directory.Exists(newPath))
                                    {
                                        i++;
                                        newPath = basePath + "_" + i;
                                    }
                                    Directory.CreateDirectory(newPath);
                                    extractTargetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "installed", newPath);
                                    break;

                                default:
                                    Application.Current.Dispatcher.Invoke(() =>
                                        MessageBox.Show("Unknown import override setting.", "Error", MessageBoxButton.OK, MessageBoxImage.Error));
                                    return;
                            }
                        }
                        else
                        {
                            extractTargetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "installed", fileName);
                            Directory.CreateDirectory(extractTargetDir);
                        }

                        // Step 3: Extract
                        try
                        {
                            using (var archive = ZipFile.OpenRead(path))
                            {
                                foreach (var entry in archive.Entries)
                                {
                                    string relativePath = entry.FullName;

                                    if (isRaw)
                                    {
                                        if (relativePath.StartsWith("RAW/", StringComparison.OrdinalIgnoreCase) ||
                                            relativePath.StartsWith("RAW\\", StringComparison.OrdinalIgnoreCase))
                                        {
                                            relativePath = Path.Combine("WAD", "raw.wad.client", relativePath.Substring(4));
                                        }
                                    }

                                    string fullPath = Path.Combine(extractTargetDir, relativePath);

                                    if (string.IsNullOrEmpty(entry.Name))
                                    {
                                        Directory.CreateDirectory(fullPath);
                                        continue;
                                    }

                                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                                    entry.ExtractToFile(fullPath, overwrite: true);
                                }
                            }

                            string folderName = Path.GetFileName(extractTargetDir.TrimEnd(Path.DirectorySeparatorChar));
                            var newmod = CreateModFromFolder(extractTargetDir, true);

                            if (newmod == null)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                   CustomMessageBox.Show("skinhacks are not supported, get lost"), null, "No Skins?");
                                return;
                            }

                            SaveModDetails(newmod);

                            Application.Current.Dispatcher.Invoke(() =>
                                RefreshModListPanel(Current_location_folder));
                        }
                        catch (Exception)
                        {
                            if (Directory.Exists(extractTargetDir))
                                Directory.Delete(extractTargetDir, true);
                            // Optionally log error here
                        }
                    }
                    else if (ext == ".modpkg")
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            install_modpkg(path);
                        });
                    }

                    // The catch-all block from original code
                    {
                        HandleWadImport(path);
                    }
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                        MessageBox.Show($"Error handling dropped item:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error));
                }

                // Final UI refresh for this item (wrapped in Dispatcher)
                Application.Current.Dispatcher.Invoke(() =>
                    RefreshModListPanel(Current_location_folder));
            });
        }
        private static readonly Regex modRegex = new Regex(
        @"^(?<name>.+?)_(?<version>\d+\.\d+\.\d+)(\(\d+\))?(\.fantome)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

        private void install_modpkg(string path, bool override_ = false)
        {
            var info = ModPkgLib.GetMetadata(path);

            string folder = (info.Metadata.Name == "" ? $"{Path.GetFileNameWithoutExtension(path)}_modpkg_import" : info.Metadata.Name);
            string installPath = Path.Combine("installed", folder);

            if (Directory.Exists(installPath) && !override_)
            {
                switch (settings.import_override)
                {
                    case 0:
                        MessageBox.Show("This mod is Already installed");
                        break;
                    case 1:
                        // just unpack, keep existing folder
                        break;
                    case 2:
                        // delete folder, then unpack
                        Directory.Delete(installPath, true);
                        break;
                    case 3:
                        // find next available folder with _numb
                        int numb = 2;
                        string newPath;
                        do
                        {
                            newPath = Path.Combine("installed", $"{info.Metadata.Name}_{numb}");
                            numb++;
                        } while (Directory.Exists(newPath));
                        installPath = newPath;
                        break;
                    default:
                        throw new Exception("Invalid import_override setting.");
                }
            }else if (Directory.Exists(installPath) && override_)
            {
                Directory.Delete(installPath, true);
            }
            ModPkgLib.Extract(path, installPath);

            // Ensure META folder exists
            string metaPath = Path.Combine(installPath, "META");
            Directory.CreateDirectory(metaPath);

            if (info.Metadata.Distributor?.SiteId == "runeforge")
            {
                var obj = new
                {
                    modId = info.Metadata.Distributor.ModId,
                    releaseId = info.Metadata.Distributor.ReleaseId,
                };

                string json_rf = JsonSerializer.Serialize(obj, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(Path.Combine(metaPath, "rf.json"), json_rf);
            }

            // Prepare authors as comma-separated string
            string authors = info.Metadata.Authors != null
                ? string.Join(", ", info.Metadata.Authors.Select(a => a?.Name ?? ""))
                : "";

            // Create ModInfo object with safe defaults
            var modInfo = new ModInfo
            {
                Author = authors,
                Description = info.Metadata.Description ?? "",
                Heart = "",
                Home = "",
                Name = info.Metadata.DisplayName ?? "",
                Version = info.Metadata.Version ?? "1.0.0"
            };

            // Serialize to JSON
            string json = System.Text.Json.JsonSerializer.Serialize(modInfo, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            // Save to META/info.json
            File.WriteAllText(Path.Combine(metaPath, "info.json"), json);

            var modDetails = new ModDetails
            {
                Layers = info.Layers // assign the layers directly
            };

            string json2 = System.Text.Json.JsonSerializer.Serialize(modDetails, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            // Save to META/info.json
            File.WriteAllText(Path.Combine(metaPath, "details.json"), json2);

            string folderName = Path.GetFileName(installPath.TrimEnd(Path.DirectorySeparatorChar));
            SaveModDetails(CreateModFromFolder(installPath, true));
            RefreshModListPanel(Current_location_folder);
        }

        private async void HandleWadImport(string path)
        {
            if (Directory.Exists(path))
            {
                string wadFolder = Path.Combine(path, "WAD");
                string metaFolder = Path.Combine(path, "META");

                if (Directory.Exists(wadFolder) && Directory.Exists(metaFolder))
                {
                    string installedPat = Path.Combine("installed", Path.GetFileName(path));

                    // If target already exists, you might want to delete or skip copying
                    if (Directory.Exists(installedPat))
                    {
                        // Optional: delete existing before copy
                        Directory.Delete(installedPat, true);
                    }

                    // Copy directory recursively
                    CopyDirectory(path, installedPat);
                    CreateModFromFolder(installedPat, true);
                    return;
                }
            }

            string name = Path.GetFileName(path);
            if (!path.EndsWith(".wad") && !path.EndsWith(".client") && !EMPTY_WADS.ContainsKey(TrimWadName(name))) { return; };
            string tempFolder = Path.Combine(Path.GetTempPath(), $"modtemp_{Guid.NewGuid()}");
            string wadDir = Path.Combine(tempFolder, "WAD");
            string metaDir = Path.Combine(tempFolder, "META");

            try
            {
                Directory.CreateDirectory(wadDir);
                Directory.CreateDirectory(metaDir);

                if (File.Exists(path))
                {
                    string destFile = Path.Combine(wadDir, TrimWadName(name, false) + ".wad.client");
                    File.Copy(path, destFile, true);
                }
                else if (Directory.Exists(path))
                {
                    string destFolder = Path.Combine(wadDir, TrimWadName(name, false) + ".wad.client");
                    CopyDirectory(path, destFolder);
                }
                else
                {
                    MessageBox.Show("Path is neither file nor directory", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string infoJson = Path.Combine(metaDir, "info.json");
                string json = $$"""
        {
            "Author": "{{settings.default_author}}",
            "Description": "",
            "Heart": "{{settings.default_Hearth}}",
            "Home": "{{settings.default_home}}",
            "Name": "{{name}}",
            "Version": "1.0.0"
        }
        """;
                File.WriteAllText(infoJson, json);

                string tempZip = Path.Combine(Path.GetTempPath(), $"{TrimWadName(name, false)}.zip");
                if (File.Exists(tempZip))
                    File.Delete(tempZip);

                ZipFile.CreateFromDirectory(tempFolder, tempZip);

                await HandleDroppedItem(tempZip);

                // Clean up zip
                File.Delete(tempZip);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import WAD:\n{ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Cleanup temp folder
                if (Directory.Exists(tempFolder))
                    Directory.Delete(tempFolder, true);
            }
        }

        private void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string dest = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, dest, true);
            }
            foreach (var folder in Directory.GetDirectories(sourceDir))
            {
                string dest = Path.Combine(targetDir, Path.GetFileName(folder));
                CopyDirectory(folder, dest);
            }
        }

        public double MinColumnWidth = 620;
        public double RowHeight = 60;

        

    }

    public class ColorJsonConverter : System.Text.Json.Serialization.JsonConverter<Color>
    {
        public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string? colorString = reader.GetString();
            if (!string.IsNullOrEmpty(colorString))
            {
                // Expect string like "#D16002" (ARGB or RGB)
                return (Color)ColorConverter.ConvertFromString(colorString);
            }
            return Colors.Transparent;
        }

        public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
        {
            // Write as hex string e.g. "#D16002"
            string colorString = $"#{value.R:X2}{value.G:X2}{value.B:X2}";
            writer.WriteStringValue(colorString);
        }
    }
}
