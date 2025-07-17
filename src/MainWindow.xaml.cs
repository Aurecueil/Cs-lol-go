using Microsoft.Win32;
using Microsoft.Windows.Themes;
using ModLoader;
using System;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using DataFormats = System.Windows.DataFormats;
using Path = System.IO.Path;

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
        public bool autodetect_game_path { get; set; } = true;
        public string gamepath { get; set; } = "";
        public bool startup_start { get; set; } = false;
        public bool load_start { get; set; } = false;
        public bool catch_updated { get; set; } = false;  //true laters
        public int import_override { get; set; } = 0;

        public double Tile_height { get; set; } = 60;
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
        public bool reinitialize_mods_before_each_write { get; set; } = false;
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
    }
    


    public partial class MainWindow : Window
    {
        private TrayIcon _trayIcon;
        private ContextMenuWindow _contextMenuWindow;

        private bool display_only_active = false;

        Dictionary<int, HierarchyElement> hierarchyById = new();
        Dictionary<string, Mod> modByFolder = new();
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


        private void show_profile(object sender, RoutedEventArgs e)
        {
            var control = new ProfileNameDialog();

            control.SetPlaceholderText("New Folder Name");
            control.OnProfileCreated += newProfileName =>
            {
                int key = Enumerable.Range(1, int.MaxValue - 1).First(k => !hierarchyById.ContainsKey(k));
                string inner_path = "";
                int temporary_cur_loca = Current_location_folder;
                if (temporary_cur_loca != 0)
                {
                    List<int> pathSegments = new();

                    
                    while (temporary_cur_loca != 0 && hierarchyById.TryGetValue(temporary_cur_loca, out var parentElement))
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
                        SaveModInfo(mod);
                        SaveModDetails(mod);
                    }

                    if (folder != null)
                    {
                        SaveFolder(folder);
                    }
                }
                OverlayHost.Children.Clear();
                OverlayHost2.Children.Clear();
            }
            else if (e.Key == Key.F5 && !ShouldBlockShortcuts())
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

            // If not valid, scan running processes
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
                Resources["ModListEntryPadding"] = new Thickness(RowHeight/10, RowHeight / 10, RowHeight / 10, RowHeight / 10);
            
            if (TryFindResource("ModListEntryMargin") is Thickness margin)
                Resources["ModListEntryMargin"] = new Thickness(RowHeight / 20);
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
                mod.RefreshDisplay();
            }
            foreach (var folder in FolderListEntriesInDisplay)
            {
                folder.SetSelection(check);
                folder.RefreshDisplay();
            }
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                this.Hide();
                this.ShowInTaskbar = false;
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
            this.WindowState = WindowState.Normal;
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
            base.OnClosed(e);
        }

        public void details_colums_change(bool active)
        {
            if (active)
            {
                var width = new GridLength(settings.details_column, GridUnitType.Pixel);
                MainGrid.ColumnDefinitions[2].Width = width;
                MySplitter.IsEnabled = true;
            }
            else
            {
                var width = new GridLength(0, GridUnitType.Pixel);
                MainGrid.ColumnDefinitions[2].Width = width;
                MySplitter.IsEnabled = false;
                save_settings();
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
                Filter = "Supported Files (*.fantome, *.zip, *.wad, *.client)|*.fantome;*.zip;*.wad;*.client|All Files (*.*)|*.*",
                Multiselect = allowMultiple
            };

            bool? result = owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner);

            return result == true ? dialog.FileNames : Array.Empty<string>();
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
            RefreshModListPanel(loc);
        }
        public MainWindow()
        {
            InitializeComponent();
            load_settings();
            colorManager = new Color_menager(settings);
            detectGamePath();
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
               

            Application.Current.Resources["AccentColor"] = settings.theme_color;


            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string relativePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "animegurl.ico");
            if (File.Exists(relativePath))
            {
                this.Icon = BitmapFrame.Create(new Uri(relativePath, UriKind.Absolute));
            }

            _trayIcon = new TrayIcon();
            _trayIcon.ShowTrayIcon("Yamete, mitenai de yo, onii-san!", OnTrayIconDoubleClick, OnTrayIconRightClick, "animegurl.ico");
            this.StateChanged += MainWindow_StateChanged;


            this.SizeChanged += MainWindow_SizeChanged;
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
            ModListEntry.MainWindowInstance = this;
            try
            {
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

                LoadWadFiles();
                LoadFolders();
                LoadMods();
                details_colums_change(settings.detials_column_active);
                RefreshModListPanel(Current_location_folder);
                InitializeSearchBox();
                if (!Directory.Exists(ProfilesFolder))
                {
                    Directory.CreateDirectory(ProfilesFolder);
                }
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




            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading mods:\n" + ex.Message);
            }
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
                MessageBox.Show($"Game path does not exist: {strin_path}");
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
        }
        private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfileComboBox.SelectedItem != null)
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
                int parent = get_parent_from_innerPath(old_entry.Details.InnerPath);
                if (hierarchyById.TryGetValue(parent, out var old_parent))
                {
                    old_parent.Children.RemoveAll(child => child.Item1 == old_entry.ModFolder && child.Item2 == true);
                }
                modByFolder.Remove(mod_folder);


                string relativePath = Path.Combine("installed", mod_folder);

                string fullPath = Path.GetFullPath(relativePath);
                if (Directory.Exists(fullPath))
                {
                    Directory.Delete(fullPath, true); // true = recursive delete
                }

                MainWindow.ProfileEntries.Remove(old_entry.ModFolder);

                RefreshModListPanel(Current_location_folder);
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

        // Method to read current profile (shows message box as requested)
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
            AdjustModListLayout();
            var column = MainGrid.ColumnDefinitions[2];
            settings.details_column = column.Width.Value;
            save_settings();
        }

        public void UpdateDetailsPanel(string details, bool update = true) {
            if (update)
            {
                Details_Panel.Text = GetDetails(details);
            }
            else
            {
                Details_Panel.Text = "";
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
            File.WriteAllText("settings.json", json);
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
                int depthA = string.IsNullOrEmpty(a.InnerPath) ? 0 : a.InnerPath.Split('/').Length;
                int depthB = string.IsNullOrEmpty(b.InnerPath) ? 0 : b.InnerPath.Split('/').Length;
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
        private CancellationTokenSource _modLoadCts;
        private bool _isLoaderRunning = false;
        private readonly object _loaderLock = new object();

        public async void Start_loader(object sender, RoutedEventArgs e)
        {
            True_Start_loader();
        }

        public async void True_Start_loader()
        {
            lock (_loaderLock)
            {
                if (_modLoadCts != null || _isLoaderRunning)
                {
                    return; // Already running or not properly stopped
                }

                _isLoaderRunning = true;
                Load_check_box.IsEnabled = false; // Disable during startup
            }
            Load_check_box.IsChecked = true;
            UpdateContextMenuLoaderState();
            try
            {
                if (settings.reinitialize_mods_before_each_write)
                {
                    LoadMods();
                    ReadCurrentProfile();
                }
                ToggleOverlay(true);
                ToggleFeed(true);
                Load_Mods();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting loader: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                await Stop_loader_internal();
            }
            finally
            {}
            Load_check_box.IsEnabled = true;
            UpdateContextMenuLoaderState();
        }

        public async void Stop_loader(object sender, RoutedEventArgs e)
        {
            await Stop_loader_internal();        
        }

        private async Task Stop_loader_internal()
        {
            ToggleOverlay(true);
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

                // Wait a bit for cancellation to propagate
                await Task.Delay(100);

                ctsToCancel.Dispose();

                
            }
            _currentRunner?.KillProcess();
            _currentRunner = null;
            try
            {
                CSLolManager.Stop(); // Properly stop the CSLol process
            }
            catch (Exception ex)
            {
                // Log or handle CSLol stop errors if needed
                System.Diagnostics.Debug.WriteLine($"Error stopping CSLol: {ex.Message}");
            }
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
                    return; // Already running
                }

                _modLoadCts = new CancellationTokenSource();
                localCts = _modLoadCts;
            }

            var token = localCts.Token;

            try
            {
                await InitializeModsAsync(token);

                // Check if we're still the active cancellation token
                lock (_loaderLock)
                {
                    if (_modLoadCts != localCts)
                    {
                        // We've been cancelled/replaced
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
            }
            WADS = CopyWadsDictionary(EMPTY_WADS);
            await ProcessFolderChildrenAsync(0, token);
            await WriteWads(token);
            ToggleOverlay(false);
        }

        private void StartCSLol(CancellationToken token)
        {
            CSLolManager.Initialize(
                Path.Combine(Directory.GetCurrentDirectory(), "profiles", settings.CurrentProfile),
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
        private ModToolsRunner _currentRunner;
        public async Task WriteWads(CancellationToken token)
        {
            List<string> MODS = new List<string>();
            foreach (string cWAD in WADS.Keys)
            {
                if (WADS[cWAD].Item2.Keys.Count < 1)
                {
                    continue;
                }

                // Otherwise, process
                foreach (var modEntry in WADS[cWAD].Item2)
                {
                    MODS.Add(modEntry.Key);

                }

            }

            for (int i = 0; i < MODS.Count; i++)
            {
                string current = MODS[i];
                int lastIndex = MODS.LastIndexOf(current);
                if (lastIndex != i)
                {
                    MODS.RemoveAt(i);
                    i--; // Adjust index after removal
                }
            }
            string mod_list = $"\"{string.Join("\"/\"", MODS)}\"";
            string mod_list_disp = $"{string.Join("\n", MODS)}"; 
            //CustomMessageBox.Show(mod_list_disp, new[] { "OK" }, "Mod List");
            var runner = new ModToolsRunner(Path.Combine(Directory.GetCurrentDirectory(), "cslol-tools", "mod-tools.exe"));
            _currentRunner = runner;
            string game_path = Path.GetDirectoryName(settings.gamepath);
            var args = $"mkoverlay --src \"installed\" --dst \"{Path.Combine(Directory.GetCurrentDirectory(), "profiles", settings.CurrentProfile)}\" --game:\"{game_path}\" --mods:{mod_list}";

            if (settings.not_tft) 
            {
                args += " --noTFT";
            }

            if (settings.supress_install_confilcts) 
            {
                args += " --ignoreConflict";
            }


            string err_catch = "";
            var outputLines = new List<string>();https://www.facebook.com/messages/t/100010163408225
            var errorLines = new List<string>();
            try
            {
                int result = await runner.RunAsync(args,
                onOutput: line =>
                {
                    outputLines.Add(line);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Feed.Text = line; // update your UI with output line
                    });
                },
                onError: line =>
                {
                    errorLines.Add(line);  // just collect errors, no immediate display
                }
            );

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
            catch (Exception ex)
            {
                Load_check_box.IsChecked = false;
                if (!string.IsNullOrEmpty(err_catch))
                {
                    CustomMessageBox.Show(err_catch, new[] { "OK" }, "Error");
                }
            }
            _currentRunner = null;
        }

        public async Task ProcessFolderChildrenAsync(int folderId, CancellationToken token, bool isRandomElement = false, bool overrride = false)
        {
            if (!hierarchyById.TryGetValue(folderId, out var folder))
                return;

            var rng = new Random(); // Shared random instance
            var workingList = new List<(string Id, int Priority, bool IsMod)>();
            var skipMap = new Dictionary<string, bool>(); // Track which items to skip (by Id)

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

            if (overrride)
            {
                foreach (var (id, priority, isMod) in workingList.OrderBy(x => x.Priority))
                {
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
                                    modsTuple.Item2.Clear();
                                }
                            }
                        }
                    }
                }
            }

            foreach (var (id, priority, isMod) in workingList.OrderBy(x => x.Priority))
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
                                    modsTuple.Item2.Clear();

                                modsTuple.Item2[mod.ModFolder] = mod.has_changed;
                            }
                        }
                    }
                }
                else if (hierarchyById.TryGetValue(int.Parse(id), out var folderr))
                {
                    await ProcessFolderChildrenAsync(int.Parse(id), token, folderr.Random, folderr.override_);
                }
            }
        }

        // Utility function to trim .wad/.client/.locale suffixes
        public string TrimWadName(string wad)
        {
            string trimmed = wad;

            if (trimmed.EndsWith(".client"))
                trimmed = trimmed[..^7];
            if (trimmed.EndsWith(".wad"))
                trimmed = trimmed[..^4];

            var localeMatch = Regex.Match(trimmed, @"\.[a-zA-Z]{2}_[a-zA-Z]{2}$");
            if (localeMatch.Success)
                trimmed = trimmed[..^localeMatch.Length];

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

        public void ToggleFeed(bool show)
        {
            if (MainGrid.RowDefinitions.Count > 3)
            {
                ToggleOverlayRow.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

            }
        }

        public void RefreshModListPanel(int c_location)
        {
            FolderListEntriesInDisplay.Clear();
            modListEntriesInDisplay.Clear();

            string searchString = Global_searchText;
            // Clear all existing UI entries
            ModListPanel.Children.Clear();
            if (!hierarchyById.TryGetValue(c_location, out var root))
            {
                if (!hierarchyById.TryGetValue(Current_location_folder, out var meow))
                {
                    RefreshModListPanel(0);
                }
                else
                {
                    RefreshModListPanel(Current_location_folder);
                }
            }
            if (c_location != 0)
            {
                AddParentFolderEntry(root);
            }
            if (c_location != Current_location_folder)
            {
                Details_Panel.Text = "";
                GlobalselectedEntries.Clear();
            }
            Current_location_folder = c_location;
                       

            // Separate folders and mods, then sort each group alphabetically
            var folders = new List<(string childId, HierarchyElement element)>();
            var mods = new List<(string childId, Mod element)>();

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

            // Sort folders alphabetically by name
            folders.Sort((a, b) => string.Compare(a.element.Name, b.element.Name, StringComparison.OrdinalIgnoreCase));

            // Sort mods alphabetically by name
            mods.Sort((a, b) => string.Compare(a.element.Info.Name, b.element.Info.Name, StringComparison.OrdinalIgnoreCase));

            // Add folders first
            foreach (var (childId, childElement) in folders)
            {
                if (!ProfileEntries.ContainsKey(childId) && display_only_active) { continue; }
                var folderEntry = new ModListEntry(childElement.ID.ToString());
                folderEntry.InitializeWithFolder(childElement);
                folderEntry.FolderDoubleClicked += (folderId) => RefreshModListPanel(folderId);
                FolderListEntriesInDisplay.Add(folderEntry);
                ModListPanel.Children.Add(folderEntry);
                if (GlobalselectedEntries.Contains(childElement.ID.ToString())) { folderEntry.SetSelection(true); folderEntry.RefreshDisplay(); }
            }

            // Add mods second
            foreach (var (childId, childElement) in mods)
            {
                if (!ProfileEntries.ContainsKey(childId) && display_only_active) { continue; }
                var modWads = childElement.Wads; // string list
                var modAuthor = childElement.Info.Author; // string

                var modEntry = new ModListEntry(childElement.ModFolder);
                modEntry.InitializeWithMod(childElement);
                modListEntriesInDisplay.Add(modEntry);
                ModListPanel.Children.Add(modEntry);
                if (GlobalselectedEntries.Contains(childElement.ModFolder)) { modEntry.SetSelection(true); modEntry.RefreshDisplay(); }
            }
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
                foreach (var item in folder.Children)
                {
                    if (!item.Item2) // Folder
                    {
                        if (int.TryParse(item.Item1, out int childId) && hierarchyById.TryGetValue(childId, out var childFolder))
                        {
                            sb.AppendLine(childFolder.Name);
                        }

                        string nested = GetDetails(item.Item1).Trim();
                        if (!string.IsNullOrEmpty(nested))
                        {
                            // Indent nested lines with a dash
                            foreach (var line in nested.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                            {
                                sb.AppendLine($"-- {line.Trim()}");
                            }
                        }
                    }
                }

                foreach (var item in folder.Children)
                {
                    if (item.Item2) // Mod
                    {
                        if (modByFolder.TryGetValue(item.Item1, out var mod))
                        {
                            sb.AppendLine(mod.Info.Name.Trim());
                        }
                    }
                }
            }
            else if (modByFolder.TryGetValue(new_id, out var mod))
            {
                return $"{mod.Info.Name}\n{mod.ModFolder}".Trim();
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
                Folder existing = index >= 0 ? folders[index] : null;

                // If parent changed, recalculate InnerPath
                if (existing != null)
                {
                    if (existing.parent != element.parent)
                    {
                        element.InnerPath = BuildInnerPath(element.parent, hierarchyById);
                    }
                }
                else
                {
                    // New element, always build InnerPath
                    element.InnerPath = BuildInnerPath(element.parent, hierarchyById);
                }

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
                return "";

            List<int> pathSegments = new();
            int currentId = parentId;

            while (currentId != 0 && hierarchyById.TryGetValue(currentId, out var parentElement))
            {
                pathSegments.Add(parentElement.ID);
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
            //int j = 0;
            foreach (string modFolderPath in modFolders)
            {
                //j++;
                string modFolderName = Path.GetFileName(modFolderPath);

                try
                {
                    Mod mod = CreateModFromFolder(modFolderPath);
                    
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading mod from folder '{modFolderName}': {ex.Message}");
                }

                
            }
            //MessageBox.Show($"{j}");
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
            RefreshModListPanel(Current_location_folder);
        }

        public void SaveModDetails(Mod mod, string basePath = null)
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
            RefreshModListPanel(Current_location_folder);
        }

        public void SaveFolder(HierarchyElement _folder)
        {
            if (hierarchyById.TryGetValue(_folder.ID, out var mod_element))
            {
                mod_element = _folder;
                SaveOrUpdateHierarchyElement(_folder, hierarchyById);
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
                }
                catch (Exception ex)
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

            ModDetails modDetails = new ModDetails(); // Default values
            if (File.Exists(detailsPath))
            {
                try
                {
                    string detailsJson = File.ReadAllText(detailsPath);
                    modDetails = JsonSerializer.Deserialize<ModDetails>(detailsJson) ?? new ModDetails();
                    if(override_inner_path){
                        modDetails.InnerPath = BuildInnerPath(Current_location_folder, hierarchyById);
                    }
                }
                catch (Exception ex)
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

        private void DropScrollViewer_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                if (IsOverlayBlocking()) return;

                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    bool very = false;
                    string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                    foreach (string path in paths)
                    {
                        if (IsAcceptedDropItem(path))
                        {
                            very = true;
                            // Call your drop handler here
                            HandleDroppedItem(path);
                        }
                    }
                    if (very)
                    {
                        RefreshModListPanel(Current_location_folder);
                    }
                    else
                    {
                        MessageBox.Show("no valid mods");
                    }

                }
                e.Handled = true;
            }
        }

        private bool IsOverlayBlocking()
        {
            return (OverlayHost.Visibility == Visibility.Visible && OverlayHost.Children.Count > 0) ||
                   (OverlayHost2.Visibility == Visibility.Visible && OverlayHost2.Children.Count > 0);
        }

        private bool IsAcceptedDropItem(string path)
        {
            if (Directory.Exists(path))
                return true;

            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".zip" || ext == ".fantome" || ext == ".wad.client" || ext == ".client";
        }

        private void HandleDroppedItem(string path)
        {
            try
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();

                if (ext == ".fantome" || ext == ".zip")
                {
                    string fileName = Path.GetFileNameWithoutExtension(path);
                    bool isValidZip = false;

                    // Step 1: Validate zip content before extracting
                    try
                    {
                        using (var archive = ZipFile.OpenRead(path))
                        {
                            bool hasWad = archive.Entries.Any(e =>
                                e.FullName.StartsWith("WAD/", StringComparison.OrdinalIgnoreCase) ||
                                e.FullName.StartsWith("WAD\\", StringComparison.OrdinalIgnoreCase));

                            bool hasMetaInfo = archive.Entries.Any(e =>
                                e.FullName.Equals("META/info.json", StringComparison.OrdinalIgnoreCase) ||
                                e.FullName.Equals("META\\info.json", StringComparison.OrdinalIgnoreCase));

                            if (!hasWad || !hasMetaInfo)
                            {
                                MessageBox.Show("Invalid mod archive: Must contain 'WAD' folder and 'META/info.json'", "Invalid Mod", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }

                            isValidZip = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error reading archive:\n{ex.Message}", "Zip Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (!isValidZip)
                        return;

                    string extractTargetDir;

                    if (modByFolder.TryGetValue(fileName, out var mod))
                    {
                        switch (settings.import_override)
                        {
                            case 0:
                                MessageBox.Show($"Mod already EXISTS under this path:\n{mod.ModFolder}", "Import Skipped", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;

                            case 1:
                                extractTargetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "installed", mod.ModFolder);
                                break;

                            case 2:
                                string wadPath = Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "installed", mod.ModFolder), "WAD");
                                if (Directory.Exists(wadPath))
                                    Directory.Delete(wadPath, true);

                                string infoJsonPath = Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "installed", mod.ModFolder), "META", "info.json");
                                if (File.Exists(infoJsonPath))
                                    File.Delete(infoJsonPath);

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
                                MessageBox.Show("Unknown import override setting.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to extract mod:\n{ex.Message}", "Extraction Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        if (Directory.Exists(extractTargetDir))
                            Directory.Delete(extractTargetDir, true);
                    }
                }
                else
                {
                    HandleWadImport(path);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error handling dropped item:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            RefreshModListPanel(Current_location_folder);
        }

        private void HandleWadImport(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path.TrimEnd(Path.DirectorySeparatorChar));
            string tempFolder = Path.Combine(Path.GetTempPath(), $"modtemp_{Guid.NewGuid()}");
            string wadDir = Path.Combine(tempFolder, "WAD");
            string metaDir = Path.Combine(tempFolder, "META");

            try
            {
                Directory.CreateDirectory(wadDir);
                Directory.CreateDirectory(metaDir);

                // Copy .wad.client file or folder into WAD/
                if (File.Exists(path))
                {
                    string destFile = Path.Combine(wadDir, name + ".wad.client");
                    File.Copy(path, destFile, true);
                }
                else if (Directory.Exists(path))
                {
                    string destFolder = Path.Combine(wadDir, name);
                    CopyDirectory(path, destFolder);
                }
                else
                {
                    MessageBox.Show("Path is neither file nor directory", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Create META/info.json
                string infoJson = Path.Combine(metaDir, "info.json");
                string json = $$"""
        {
            "Author": "",
            "Description": "",
            "Heart": "",
            "Home": "",
            "Name": "{{name}}",
            "Version": "1.0.0"
        }
        """;
                File.WriteAllText(infoJson, json);

                // Pack to zip
                string tempZip = Path.Combine(Path.GetTempPath(), $"{name}_temp.zip");
                if (File.Exists(tempZip))
                    File.Delete(tempZip);

                ZipFile.CreateFromDirectory(tempFolder, tempZip);

                // Import it
                HandleDroppedItem(tempZip);

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
