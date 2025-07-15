using ModLoader;
using System;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Path = System.IO.Path;

namespace ModManager
{
    public class HierarchyElement
    {
        public string Name { get; set; }
        public int Override { get; set; }
        public int Priority { get; set; }
        public bool Random { get; set; }
        public int ID { get; set; }
        public int parent { get; set; }

        public bool isActive { get; set; }

        public string InnerPath { get; set; }

        // Tuple<string, bool>: Item ID/name + isMod (true for mod, false for folder)
        public List<Tuple<string, bool>> Children { get; set; } = new();
    }

    public class Settings
    {
        public bool autodetect_game_path { get; set; } = true;
        public string gamepath { get; set; } = "";
        public bool startup_start { get; set; } = false;
        public bool load_start { get; set; } = false;
        public bool catch_updated { get; set; } = true;
        public int import_override { get; set; } = 0;

        public bool reinitialize { get; set; } = false;

        public string CurrentProfile { get; set; } = "Default Profile";

        public Color theme_color { get; set; } = Color.FromRgb(209, 96, 2);
    }
    public class Folder
    {
        public string InnerPath { get; set; } = "";
        public string name { get; set; } = "";
        public int override_ { get; set; }
        public int priority { get; set; }
        public bool random { get; set; }
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
        public string Author { get; set; }
        public string Description { get; set; }
        public string Heart { get; set; }
        public string Home { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
    }
    public class ModDetails
    {
        public int Priority { get; set; } = 10;
        public int Override { get; set; } = 0;
        public string InnerPath { get; set; } = "";

        public bool Random { get; set; } = false;
    }
    


    public partial class MainWindow : Window
    {
        private TrayIcon _trayIcon;
        private ContextMenuWindow _contextMenuWindow;

        Dictionary<int, HierarchyElement> hierarchyById = new();
        Dictionary<string, Mod> modByFolder = new();
        private static readonly string installedPath = "installed";
        List<ModListEntry> modListEntriesInDisplay = new List<ModListEntry>();
        List<ModListEntry> FolderListEntriesInDisplay = new List<ModListEntry>();

        Dictionary<int,List<(string, bool)>> CutEntries = new Dictionary<int, List<(string, bool)>>();

        public static List<string> GlobalselectedEntries = new List<string>();

        public Settings settings = new Settings();

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
                    Override = 0,
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
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                OverlayHost.Children.Clear();
            }
            else if (e.Key == Key.A && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && !SearchBox.IsFocused)
            {
                foreach (var mod in modListEntriesInDisplay)
                {
                    mod.SetSelection(true);
                    mod.RefreshDisplay();
                }
                foreach (var folder in FolderListEntriesInDisplay)
                {
                    folder.SetSelection(true);
                    folder.RefreshDisplay();
                }
            }
            else if (e.Key == Key.D && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && !SearchBox.IsFocused)
            {
                foreach (var mod in modListEntriesInDisplay)
                {
                    mod.SetSelection(false);
                    mod.RefreshDisplay();
                }
                foreach (var folder in FolderListEntriesInDisplay)
                {
                    folder.SetSelection(false);
                    folder.RefreshDisplay();
                }
            }
            else if (e.Key == Key.X && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && !SearchBox.IsFocused)
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
            else if (e.Key == Key.V && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && !SearchBox.IsFocused)
            {
                foreach (int source in CutEntries.Keys)
                {
                    DragHandler(CutEntries[source], (Current_location_folder.ToString(), false), true, source);
                }
                CutEntries.Clear();
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
            }

            // Get cursor position
            var point = GetCursorPosition();
            _contextMenuWindow.ShowAt(point.X, point.Y);
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
        public MainWindow()
        {
            InitializeComponent();
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string relativePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "animegurl.ico");
            if (File.Exists(relativePath))
            {
                this.Icon = BitmapFrame.Create(new Uri(relativePath, UriKind.Absolute));
            }

            _trayIcon = new TrayIcon();
            _trayIcon.ShowTrayIcon("My WPF App", OnTrayIconDoubleClick, OnTrayIconRightClick, "animegurl.ico");
            this.StateChanged += MainWindow_StateChanged;


            this.SizeChanged += MainWindow_SizeChanged;
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
            ModListEntry.MainWindowInstance = this;
            try
            {
                var root_folder = new HierarchyElement
                {
                    Name = "root",
                    Override = 0,
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
                RefreshModListPanel(Current_location_folder);
                InitializeSearchBox();
                load_settings();
                if (!Directory.Exists(ProfilesFolder))
                {
                    Directory.CreateDirectory(ProfilesFolder);
                }
                InitializeProfiles();
                LoadWadFiles();

                if (Globals.StartMinimized)
                {
                    this.Hide();
                    this.ShowInTaskbar = false;
                }

                if (Globals.StartWithLoaded)
                {
                    True_Start_loader();
                }



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
            var profiles = GetAllProfiles();
            ProfileComboBox.ItemsSource = profiles;

            // Check if current profile exists, if not create it
            if (!string.IsNullOrEmpty(settings.CurrentProfile))
            {
                if (!ProfileExists(settings.CurrentProfile))
                {
                    CreateEmptyProfile(settings.CurrentProfile);
                    // Refresh the list to include the new profile
                    profiles = GetAllProfiles();
                    ProfileComboBox.ItemsSource = profiles;
                }
                ProfileComboBox.SelectedItem = settings.CurrentProfile;
            }
            else if (profiles.Count > 0)
            {
                ProfileComboBox.SelectedItem = profiles[0];
                settings.CurrentProfile = profiles[0];
            }
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
            if (modByFolder.TryGetValue(mod_folder, out var childElement))
            {
                var innerPath = childElement.Details.InnerPath ?? "";
                var segments = innerPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                int parID = 0;
                if (segments.Length > 0)
                {
                    if (!int.TryParse(segments[^1], out int parentId))
                    {
                        parID = parentId;
                    }
                }

                if (!hierarchyById.TryGetValue(parID, out var parentElement))
                {
                    return;
                }

                parentElement.Children.RemoveAll(t => t.Item1 == mod_folder && t.Item2 == true);

                modByFolder.Remove(mod_folder);

                string relativePath = Path.Combine("installed", mod_folder);

                string fullPath = Path.GetFullPath(relativePath);
                Directory.Delete(fullPath, true);

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

        public void ElementsSettings(string element, bool isMod)
        {
            if (isMod)
            {
                if (modByFolder.TryGetValue(element, out var mod_element))
                {
                    OverlayHost.Children.Add(new HierarchyElementEditor(mod_element, mod_element.Details.Random));
                }
            }
            else
            {
                if (hierarchyById.TryGetValue(int.Parse(element), out var folder_element))
                {
                    OverlayHost.Children.Add(new HierarchyElementEditor(folder_element, folder_element.Random));
                }
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

                var result = MessageBox.Show($"Are you sure you want to delete profile '{currentProfile}'?",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
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
        public void CloseSettingsOverlay()
        {
            OverlayHost.Children.Clear();
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
                        Override = folder.override_,
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
                            Override = folder.override_,
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
                        Override = folder.override_,
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

            try
            {
                ToggleOverlay(true);
                ToggleFeed(true);
                Load_Mods(); // Call without await since it's async void
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting loader: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                await Stop_loader_internal();
            }
            finally
            {
                Load_check_box.IsEnabled = true;
            }
        }

        public async void Stop_loader(object sender, RoutedEventArgs e)
        {
            Load_check_box.IsEnabled = false;
            await Stop_loader_internal();            
            Load_check_box.IsEnabled = true;
        }

        private async Task Stop_loader_internal()
        {
            // Note: Don't disable checkbox here since Stop_loader handles it

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

            ToggleOverlay(false);
            ToggleFeed(false);
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

        public Dictionary<string, Tuple<string, Dictionary<string, bool>>> WADS = new();
        public static Dictionary<string, Tuple<string, Dictionary<string, bool>>> EMPTY_WADS = new();

        public static Dictionary<string, Tuple<string, Dictionary<string, bool>>> CopyWadsDictionary(
            Dictionary<string, Tuple<string, Dictionary<string, bool>>> source)
        {
            var copy = new Dictionary<string, Tuple<string, Dictionary<string, bool>>>();
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
            string mod_list = $"\"{string.Join("\"/\"", MODS)}\"";
            string mod_list_disp = $"{string.Join("\n", MODS)}";
            // MessageBox.Show(mod_list_disp);
            var runner = new ModToolsRunner(Path.Combine(Directory.GetCurrentDirectory(), "cslol-tools", "mod-tools.exe"));
            _currentRunner = runner;
            string game_path = Path.GetDirectoryName(settings.gamepath);

            var args = $"mkoverlay --src \"installed\" --dst \"{Path.Combine(Directory.GetCurrentDirectory(), "profiles", settings.CurrentProfile)}\" --game:\"{game_path}\" --mods:{mod_list} --noTFT --ignoreConflict";

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
                if (err_catch != "") { MessageBox.Show($"{err_catch}", "Error"); }
            }
            _currentRunner = null;
        }

        public async Task ProcessFolderChildrenAsync(int folderId, CancellationToken token, bool isRandomElement = false, int overrride = 0)
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

            // Handle override clearing
            if (overrride == 1)
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
                    else if (hierarchyById.TryGetValue(int.Parse(id), out var folderr))
                    {
                        await ProcessFolderChildrenAsync(int.Parse(id), token, folderr.Random, folderr.Override);
                    }
                }
            }

            // Main mod writing
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
                                if (mod.Details.Override == 1)
                                    modsTuple.Item2.Clear();

                                modsTuple.Item2[mod.ModFolder] = mod.has_changed;
                            }
                        }
                    }
                }
                else if (hierarchyById.TryGetValue(int.Parse(id), out var folderr))
                {
                    await ProcessFolderChildrenAsync(int.Parse(id), token, folderr.Random, folderr.Override);
                }
            }
        }

        // Utility function to trim .wad/.client/.locale suffixes
        private string TrimWadName(string wad)
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



        void RefreshModListPanel(int c_location)
        {
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
                // Collect additional mod data for future filtering
                var modWads = childElement.Wads; // string list
                var modAuthor = childElement.Info.Author; // string

                var modEntry = new ModListEntry(childElement.ModFolder);
                modEntry.InitializeWithMod(childElement);
                modListEntriesInDisplay.Add(modEntry);
                ModListPanel.Children.Add(modEntry);
                if (GlobalselectedEntries.Contains(childElement.ModFolder)) { modEntry.SetSelection(true); modEntry.RefreshDisplay(); }
            }
        }

        private bool MatchesSearchCriteria(HierarchyElement folderElement, Mod modElement, string searchString)
        {
            if (string.IsNullOrWhiteSpace(searchString))
                return true;

            // Parse search string for quoted terms and regular terms
            var searchTerms = ParseSearchString(searchString);

            foreach (var term in searchTerms)
            {
                bool termMatched = false;

                // Check if term is a specific field search (name:, author:, wad:)
                if (IsPartialMatch(term, "name:", out string nameSearch))
                {
                    if (folderElement != null)
                    {
                        termMatched = folderElement.Name.Contains(nameSearch, StringComparison.OrdinalIgnoreCase);
                    }
                    else if (modElement != null)
                    {
                        termMatched = modElement.Info.Name.Contains(nameSearch, StringComparison.OrdinalIgnoreCase);
                    }
                }
                else if (IsPartialMatch(term, "author:", out string authorSearch))
                {
                    if (modElement != null)
                    {
                        termMatched = modElement.Info.Author.Contains(authorSearch, StringComparison.OrdinalIgnoreCase);
                    }
                }
                else if (IsPartialMatch(term, "wad:", out string wadSearch))
                {
                    if (modElement != null)
                    {
                        termMatched = modElement.Wads.Any(wad => wad.Contains(wadSearch, StringComparison.OrdinalIgnoreCase));
                    }
                }
                else
                {
                    // General search - check all applicable fields
                    if (folderElement != null)
                    {
                        termMatched = folderElement.Name.Contains(term, StringComparison.OrdinalIgnoreCase);
                    }
                    else if (modElement != null)
                    {
                        termMatched = modElement.Info.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                     modElement.Info.Author.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                     modElement.Wads.Any(wad => wad.Contains(term, StringComparison.OrdinalIgnoreCase));
                    }
                }

                // If any term doesn't match, this item doesn't match
                if (!termMatched)
                    return false;
            }

            return true;
        }

        private List<string> ParseSearchString(string searchString)
        {
            var terms = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < searchString.Length; i++)
            {
                char c = searchString[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ' ' && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        terms.Add(current.ToString().Trim());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
            {
                terms.Add(current.ToString().Trim());
            }

            return terms.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
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
                    override_ = element.Override,
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
                Console.WriteLine($"Directory '{installedPath}' does not exist.");
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
                    if (mod != null)
                    {
                        modByFolder[modFolderName] = mod;
                        Console.WriteLine($"Successfully loaded mod: {mod.Info?.Name ?? modFolderName}");
                        bool was_added = false;
                        string innerPath = mod.Details.InnerPath;
                        if (innerPath == "")
                        {
                            was_added = true;
                            AddChild(0, modFolderName, true);
                        }
                        else { 
                            string[] parts = innerPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                            for (int i = parts.Length - 1; i >= 0; i--)
                            {
                                int part = int.Parse(parts[i]);
                                if (hierarchyById.ContainsKey(part))
                                {
                                    was_added = true;
                                    AddChild(part, modFolderName, true);
                                    break;
                                }
                            }
                        }
                        if (was_added == false)
                        {
                            AddChild(0, modFolderName, true);
                        }


                    }
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
                ProfileEntries[mod.ModFolder] = mod.Details.Priority;
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
        public void ModInfoEdit(string Modfolder)
        {
            if (modByFolder.TryGetValue(Modfolder, out var mod_element))
                {

                    ModDetailsEditor editor = new ModDetailsEditor(mod_element);
                    OverlayHost.Children.Add(editor);
                }

        }
        public void SaveFolder(HierarchyElement _folder)
        {
            if (hierarchyById.TryGetValue(_folder.ID, out var mod_element))
            {
                mod_element = _folder;
                ProfileEntries[mod_element.ID.ToString()] = mod_element.Priority;
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

        private static Mod CreateModFromFolder(string modFolderPath)
        {
            string modFolderName = Path.GetFileName(modFolderPath);
            string metaPath = Path.Combine(modFolderPath, "META");
            string infoPath = Path.Combine(metaPath, "info.json");
            string detailsPath = Path.Combine(metaPath, "details.json");
            string wadPath = Path.Combine(modFolderPath, "WAD");

            // Check if META directory exists
            if (!Directory.Exists(metaPath))
            {
                Console.WriteLine($"META directory not found for mod: {modFolderName}");
                return null;
            }

            // Load ModInfo
            ModInfo modInfo = null;
            if (File.Exists(infoPath))
            {
                try
                {
                    string infoJson = File.ReadAllText(infoPath);
                    modInfo = JsonSerializer.Deserialize<ModInfo>(infoJson);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading info.json for mod '{modFolderName}': {ex.Message}");
                    return null;
                }
            }
            else
            {
                Console.WriteLine($"info.json not found for mod: {modFolderName}");
                return null;
            }

            // Load or create ModDetails
            ModDetails modDetails = new ModDetails(); // Default values
            if (File.Exists(detailsPath))
            {
                try
                {
                    string detailsJson = File.ReadAllText(detailsPath);
                    modDetails = JsonSerializer.Deserialize<ModDetails>(detailsJson) ?? new ModDetails();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading details.json for mod '{modFolderName}': {ex.Message}");
                    // Use default values if reading fails
                }
            }
            else
            {
                // Create details.json with default values
                try
                {
                    string defaultDetailsJson = JsonSerializer.Serialize(modDetails, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(detailsPath, defaultDetailsJson);
                    Console.WriteLine($"Created default details.json for mod: {modFolderName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating details.json for mod '{modFolderName}': {ex.Message}");
                }
            }

            // Load WAD files/folders
            List<string> wads = new List<string>();
            if (Directory.Exists(wadPath))
            {
                try
                {
                    string[] wadEntries = Directory.GetFileSystemEntries(wadPath);
                    foreach (string entry in wadEntries)
                    {
                        wads.Add(Path.GetFileName(entry));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading WAD directory for mod '{modFolderName}': {ex.Message}");
                }
            }
            // Create and return Mod object
            return new Mod
            {
                Info = modInfo,
                Details = modDetails,
                ModFolder = modFolderName,
                Wads = wads
            };
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
                        Mod new_mod = CreateModFromFolder(extractTargetDir);
                        if (new_mod != null)
                        {
                            Console.WriteLine($"Successfully loaded mod: {new_mod.Info?.Name ?? folderName}");
                            bool was_added = false;
                            if (modByFolder.ContainsKey(folderName))
                            { was_added = true;}
                            modByFolder[folderName] = new_mod;
                            string innerPath = new_mod.Details.InnerPath;
                            if (was_added == false)
                            {
                                if (innerPath == "")
                                {
                                    was_added = true;
                                    AddChild(Current_location_folder, folderName, true);
                                }
                                else
                                {
                                    string[] parts = innerPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                                    for (int i = parts.Length - 1; i >= 0; i--)
                                    {
                                        int part = int.Parse(parts[i]);
                                        if (hierarchyById.ContainsKey(part))
                                        {
                                            was_added = true;
                                            AddChild(part, folderName, true);
                                            break;
                                        }
                                    }
                                }
                            }
                        }

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

        const double MinColumnWidth = 620;
        const double RowHeight = 60;

        void ModListPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
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
