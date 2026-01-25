using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ModManager
{
    public partial class FixerUI : UserControl, INotifyPropertyChanged, IFixerLogger
    {
        public ModListEntry CallerModListEntry { get; set; }
        public Mod ModElement { get; private set; }
        public Repatheruwu Fixer { get; private set; }
        public MainWindow Main { get; set; }

        // INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public FixerUI(MainWindow main, Mod mod, Repatheruwu Fixi)
        {
            InitializeComponent();
            this.Main = main;
            this.ModElement = mod;
            this.Fixer = Fixi;
            this.DataContext = this;

            LoadTopazImage();
            PopulateCharacters();

            // Set Default UI states based on Fixer default settings (optional, but good for sync)
            chkKeepIcons.IsChecked = true;
            chkKillStatic.IsChecked = false;

            CheckSkinNo();
            CheckInitialBackupState();
            LoadManifests();

            HookUpEvents();

            UpdateNoSkinLightVisibility();
        }

        private void HookUpEvents()
        {
            // Trigger visibility check when these change
            cmbCharacter.SelectionChanged += (s, e) => UpdateNoSkinLightVisibility();
            chkBinsless.Click += (s, e) => UpdateNoSkinLightVisibility();
            chkAllSkins.Click += (s, e) => UpdateNoSkinLightVisibility();
        }
        public class Manifest
        {
            [JsonPropertyName("version")]
            public string Version { get; set; } = string.Empty;

            [JsonPropertyName("link")]
            public string Link { get; set; } = string.Empty;

            [JsonIgnore]
            public int X { get; private set; }

            [JsonIgnore]
            public int Y { get; private set; }

            [JsonIgnore]
            public int Z { get; private set; }

            [JsonIgnore]
            public string DisplayName => $"Patch {X}.{Y} Snapshot {Z}";

            public void ParseVersion()
            {
                var parts = Version.Split('.');
                if (parts.Length != 3)
                    throw new FormatException($"Invalid version format: {Version}");

                X = int.Parse(parts[0]);
                Y = int.Parse(parts[1]);
                Z = int.Parse(parts[2]);
            }
        }

        private Manifest? GetSelectedManifest()
        {
            return cmbManifestVersion.SelectedItem as Manifest;
        }
        private ObservableCollection<Manifest> _manifestList;
        private ICollectionView _manifestView;
        private Manifest? _lastManifestSelection = null;

        private void LoadManifests()
        {
            string path = Path.Combine("cslol-tools", "manifests.json");
            if (!File.Exists(path))
                return;

            string json = File.ReadAllText(path);
            if (Main.manifests == null)
            {
                Main.manifests = JsonSerializer.Deserialize<List<Manifest>>(json) ?? new List<Manifest>();
                foreach (var m in Main.manifests)
                    m.ParseVersion();
            }

            _manifestList = new ObservableCollection<Manifest>(Main.manifests);
            _manifestView = CollectionViewSource.GetDefaultView(_manifestList);
            cmbManifestVersion.ItemsSource = _manifestView;
            cmbManifestVersion.DisplayMemberPath = nameof(Manifest.DisplayName);

            if (_manifestList.Count > 0)
            {
                cmbManifestVersion.SelectedIndex = 0;
                _lastManifestSelection = _manifestList[0];
            }
        }

        private void cmbManifestVersion_Loaded(object sender, RoutedEventArgs e)
        {
            if (cmbManifestVersion.Template.FindName("PART_EditableTextBox", cmbManifestVersion) is TextBox tb)
            {
                tb.GotFocus += ManifestTextBox_GotFocus;
                tb.LostFocus += ManifestTextBox_LostFocus;
                tb.TextChanged += ManifestTextBox_TextChanged;
            }

            cmbManifestVersion.SelectionChanged += cmbManifestVersion_SelectionChanged;
        }

        private void ManifestTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.Text = string.Empty; // clear for search
                cmbManifestVersion.IsDropDownOpen = true;
                tb.CaretIndex = 0;
            }
        }

        private void ManifestTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (cmbManifestVersion.SelectedItem == null && _lastManifestSelection != null)
            {
                cmbManifestVersion.Text = _lastManifestSelection.DisplayName;
            }
        }

        private void ManifestTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                _manifestView.Filter = item =>
                {
                    if (item is Manifest m)
                        return m.DisplayName.IndexOf(tb.Text, StringComparison.InvariantCultureIgnoreCase) >= 0;
                    return false;
                };
                _manifestView.Refresh();
            }
        }

        private void cmbManifestVersion_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbManifestVersion.SelectedItem is Manifest m)
                _lastManifestSelection = m;
        }


        private void UpdateNoSkinLightVisibility()
        {
            // 1. Get the actual selected string object
            var selectedItem = cmbCharacter.SelectedItem as string;

            // 2. Find the index in the ORIGINAL, UNFILTERED list (_characterList)
            // If _characterList is null, default to -1
            int realIndex = (_characterList != null && selectedItem != null)
                            ? _characterList.IndexOf(selectedItem)
                            : -1;

            // 3. Get the total count from the original list
            int totalItems = _characterList?.Count ?? 0;

            // 4. Perform your logic using the REAL index
            // Check realIndex != -1 to ensure we actually found the item
            bool isSpecialIndex = (realIndex != -1) && (
                                  (realIndex == 0) ||
                                  (realIndex == totalItems - 1) ||
                                  (realIndex == totalItems - 2));

            bool shouldCollapse = (SkinNumberValue != 0) ||
                                  (chkBinsless.IsChecked == true) ||
                                  (chkAllSkins.IsChecked == true) ||
                                  isSpecialIndex;

            // MessageBox.Show($"{shouldCollapse} - RealIdx: {realIndex} - Total: {totalItems}");

            chkNoSkinLight.Visibility = shouldCollapse ? Visibility.Collapsed : Visibility.Visible;

            if (shouldCollapse) chkNoSkinLight.IsChecked = false;
        }

        private void CheckSkinNo()
        {
            string Target = Path.Combine("backup", ModElement.ModFolder, "WAD");
            string Target2 = Path.Combine("installed", ModElement.ModFolder, "WAD");

            if (!Directory.Exists(Target))
            {
                if (Directory.Exists(Target2))
                    Target = Target2;
                else
                {
                    cmbCharacter.SelectedIndex = 0;
                    return;
                }
            }

            List<string> fileSysEntries = Directory.GetFileSystemEntries(Target).ToList();

            //                                                              var pattern = new Regex(@".+\.wad\.client-([a-fA-F0-9]+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            //                                                              string randomFolderName = Path.GetRandomFileName().Replace(".", "");
            //                                                              string destDir = Path.Combine(Target, $"{randomFolderName}.wad");
            //                                                              
            //                                                              Directory.CreateDirectory(destDir);
            //                                                              
            //                                                              fileSysEntries.Add(destDir);
            //                                                              foreach (string entryPath in fileSysEntries.ToList())
            //                                                              {
            //                                                                  // Skip directories or non-existent files
            //                                                                  if (!File.Exists(entryPath)) continue;
            //                                                              
            //                                                                  string fileName = Path.GetFileName(entryPath);
            //                                                                  Match match = pattern.Match(fileName);
            //                                                              
            //                                                                  if (match.Success)
            //                                                                  {
            //                                                                      string hash = match.Groups[1].Value;
            //                                                                      string extension = ".bin"; // Default
            //                                                              
            //                                                                      try
            //                                                                      {
            //                                                                          byte[] buffer = new byte[16];
            //                                                                          using (FileStream fs = new FileStream(entryPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            //                                                                          {
            //                                                                              int bytesRead = fs.Read(buffer, 0, buffer.Length);
            //                                                              
            //                                                                              string header = System.Text.Encoding.ASCII.GetString(buffer, 0, bytesRead);
            //                                                              
            //                                                                              if (header.TrimStart().StartsWith("["))
            //                                                                              {
            //                                                                                  continue;
            //                                                                              }
            //                                                                              else if (header.TrimStart().StartsWith("{"))
            //                                                                              {
            //                                                                                  extension = ".json";
            //                                                                              }
            //                                                                              else if (header.StartsWith("#PROP_text"))
            //                                                                              {
            //                                                                                  extension = ".py";
            //                                                                              }
            //                                                                          }
            //                                                              
            //                                                                          string destFileName = $"{hash}{extension}";
            //                                                                          string destFilePath = Path.Combine(destDir, destFileName);
            //                                                                          File.Copy(entryPath, destFilePath);
            //                                                                          if (extension == ".json" || extension == ".py")
            //                                                                          {
            //                                                                              // Define the output path with .bin extension
            //                                                                              string binOutput = Path.ChangeExtension(destFilePath, ".bin");
            //                                                              
            //                                                                              // Path to your executable (ensure this is reachable or provide full path)
            //                                                                              string cliPath = "cslol-tools/ritobin_cli.exe";
            //                                                              
            //                                                                              ProcessStartInfo psi = new ProcessStartInfo
            //                                                                              {
            //                                                                                  FileName = cliPath,
            //                                                                                  // Arguments: "input_path" "output_path"
            //                                                                                  Arguments = $"\"{destFilePath}\" \"{binOutput}\"",
            //                                                                                  UseShellExecute = false,
            //                                                                                  CreateNoWindow = true,
            //                                                                                  RedirectStandardOutput = true,
            //                                                                                  RedirectStandardError = true
            //                                                                              };
            //                                                              
            //                                                                              using (Process p = Process.Start(psi))
            //                                                                              {
            //                                                                                  p.WaitForExit();
            //                                                              
            //                                                                                  // Optional: Check for errors
            //                                                                                  if (p.ExitCode != 0)
            //                                                                                  {
            //                                                                                      string error = p.StandardError.ReadToEnd();
            //                                                                                      Console.WriteLine($"Error converting {destFileName}: {error}");
            //                                                                                  }
            //                                                                              }
            //                                                                          }
            //                                                                          File.Delete(destFilePath);
            //                                                              
            //                                                                      }
            //                                                                      catch
            //                                                                      {
            //                                                                      }
            //                                                                  }
            //                                                              }
            int foundKey = -1;
            string foundValue = null;

            foreach (var kvp in Main.Wad_champs_dict)
            {
                bool matchFound = fileSysEntries.Any(entryPath =>
                string.Equals(
                    Path.GetFileName(entryPath).Split(".")[0],
                    kvp.Value,
                    StringComparison.OrdinalIgnoreCase
                )
            );


                if (matchFound)
                {
                    foundKey = kvp.Key;
                    foundValue = kvp.Value;
                    break;
                }
            }

            if (foundKey == -1)
            {
                // Fallback checks (common, map11)
                bool hasCommon = fileSysEntries.Any(entryPath => Path.GetFileName(entryPath).IndexOf("common", StringComparison.OrdinalIgnoreCase) >= 0);

                if (hasCommon)
                {
                    var sfxEntry = Main.Wad_champs_dict.FirstOrDefault(x => x.Value.Equals("SFX", StringComparison.OrdinalIgnoreCase));
                    if (sfxEntry.Value != null) { foundKey = sfxEntry.Key; foundValue = sfxEntry.Value; }
                }
                else
                {
                    bool hasMap11 = fileSysEntries.Any(entryPath => Path.GetFileName(entryPath).IndexOf("map11", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (hasMap11)
                    {
                        var announcerEntry = Main.Wad_champs_dict.FirstOrDefault(x => x.Value.Equals("Announcer", StringComparison.OrdinalIgnoreCase));
                        if (announcerEntry.Value != null) { foundKey = announcerEntry.Key; foundValue = announcerEntry.Value; }
                    }
                }
            }

            if (foundKey != -1)
            {
                cmbCharacter.SelectedIndex = foundKey;
                Fixer.Settings.base_wad_path = fileSysEntries;
                var (i, b) = Fixer.getSkinInts(foundValue);

                if (i == -1)
                {
                    chkAllSkins.IsChecked = true;
                    // Trigger update manually since we set it via code
                    UpdateNoSkinLightVisibility();
                    return;
                }

                chkBinsless.IsChecked = b;
                SkinNumberValue = i; // This triggers OnPropertyChanged -> UpdateNoSkinLightVisibility
            }
            else
            {
                cmbCharacter.SelectedIndex = 0;
                SkinNumberValue = 0;
            }
        }

        private int _skinNumberValue;
        public int SkinNumberValue
        {
            get { return _skinNumberValue; }
            set
            {
                if (_skinNumberValue != value)
                {
                    _skinNumberValue = value;
                    OnPropertyChanged();
                    // Trigger visibility check when value changes
                    UpdateNoSkinLightVisibility();
                }
            }
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space) e.Handled = true;
        }

        private void TextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(String)))
            {
                String text = (String)e.DataObject.GetData(typeof(String));
                Regex regex = new Regex("[^0-9]+");
                if (regex.IsMatch(text)) e.CancelCommand();
            }
            else
            {
                e.CancelCommand();
            }
        }
        private ObservableCollection<string> _characterList;
        private ICollectionView _characterView;
        private string _lastSelection = null;

        private void PopulateCharacters()
        {
            if (Main.Wad_champs_dict.Count < 10)
                Main.load_champ_wad_names();

            _characterList = new ObservableCollection<string>(Main.Wad_champs_dict.Values);
            _characterView = CollectionViewSource.GetDefaultView(_characterList);
            cmbCharacter.ItemsSource = _characterView;
        }

        private void cmbCharacter_Loaded(object sender, RoutedEventArgs e)
        {
            if (cmbCharacter.Template.FindName("PART_EditableTextBox", cmbCharacter) is TextBox tb)
            {
                tb.GotFocus += EditableTextBox_GotFocus;
                tb.LostFocus += EditableTextBox_LostFocus;
                tb.TextChanged += EditableTextBox_TextChanged;
            }

            // remember initial selection
            if (cmbCharacter.SelectedItem != null)
                _lastSelection = cmbCharacter.SelectedItem.ToString();
        }

        private void EditableTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                // Clear the text for search
                tb.Text = string.Empty;

                // Open dropdown
                cmbCharacter.IsDropDownOpen = true;

                // Move caret to start
                tb.CaretIndex = 0;
            }
        }

        private void EditableTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (cmbCharacter.SelectedItem == null && _lastSelection != null)
            {
                cmbCharacter.Text = _lastSelection;
            }
        }

        private void EditableTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                _characterView.Filter = item =>
                {
                    if (item == null) return false;
                    return item.ToString().IndexOf(tb.Text, StringComparison.InvariantCultureIgnoreCase) >= 0;
                };
                _characterView.Refresh();
            }
        }

        private void cmbCharacter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbCharacter.SelectedItem is not null)
            {
                _lastSelection = cmbCharacter.SelectedItem.ToString();
            }
            btnStart.IsEnabled = cmbCharacter.SelectedIndex > 0;
        }

        private void ToggleAdvanced_Click(object sender, RoutedEventArgs e)
        {
            if (btnToggleAdvanced.IsChecked == true)
                pnlAdvancedContent.Visibility = Visibility.Visible;
            else
                pnlAdvancedContent.Visibility = Visibility.Collapsed;
        }

        private void CheckInitialBackupState()
        {
            bool backupExists = Directory.Exists(Path.Combine("backup", ModElement.ModFolder));
            UpdateBackupUI(backupExists);
        }

        private void UpdateBackupUI(bool backupFound)
        {
            if (backupFound)
            {
                txtBackupState.Text = "Backup Found";
                txtBackupState.Foreground = Brushes.LightGreen;
                btnCreateBackup.Visibility = Visibility.Collapsed;
                pnlBackupControls.Visibility = Visibility.Visible;
            }
            else
            {
                txtBackupState.Text = "No Backup";
                txtBackupState.Foreground = Brushes.Gray;
                btnCreateBackup.Visibility = Visibility.Visible;
                pnlBackupControls.Visibility = Visibility.Collapsed;
            }
        }

        private void BackupAction_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            string content = btn.Content.ToString();

            if (content == "Create")
            {
                create_backup();
                UpdateBackupUI(true);
            }
            else if (content == "Restore")
            {
                restore_backup();
                string backupDir = Path.Combine("backup", ModElement.ModFolder);
                if (Directory.Exists(backupDir)) Directory.Delete(backupDir, true);
                UpdateBackupUI(false);
                CheckSkinNo();
                CheckInitialBackupState();
            }
            else if (content == "Delete")
            {
                if (CustomMessageBox.Show("Delete backup?", new[] { "Yes", "No" }, "Confirm") == "Yes")
                {
                    string backupDir = Path.Combine("backup", ModElement.ModFolder);
                    if (Directory.Exists(backupDir)) Directory.Delete(backupDir, true);
                    UpdateBackupUI(false);
                }
                CheckSkinNo();
                CheckInitialBackupState();
            }
        }

        private void restore_backup()
        {
            string modDir = Path.Combine("installed", ModElement.ModFolder);
            string backupDir = Path.Combine("backup", ModElement.ModFolder);
            if (Directory.Exists(modDir))
            {
                Directory.Delete(modDir, true);
                CopyDirectory(backupDir, modDir);
            }
        }
        private async void LoadTopazImage()
        {
            // 1. Get the directory where the .exe is running
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // 2. Combine with your specific folder and filename
            string fullPath = Path.Combine(baseDir, "runtimes", "topaz.png");

            // 3. Check if file exists to prevent crashing
            if (File.Exists(fullPath))
            {
                // Create the bitmap from the file
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // Loads image into memory and releases the file handle
                bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                bitmap.EndInit();

                // 4. Create an ImageBrush and paint the Border's background
                ImageBrush brush = new ImageBrush();
                brush.ImageSource = bitmap;
                brush.Stretch = Stretch.Fill; // Matches your previous "Stretch=Fill"

                TopazBorder.Background = brush;
            }
        }

        private void create_backup()
        {
            string modDir = Path.Combine("installed", ModElement.ModFolder);
            string backupDir = Path.Combine("backup", ModElement.ModFolder);
            if (Directory.Exists(modDir))
            {
                CopyDirectory(modDir, backupDir);
            }
        }

        static void CopyDirectory(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var f in Directory.GetFiles(src))
                File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
            foreach (var d in Directory.GetDirectories(src))
                CopyDirectory(d, Path.Combine(dst, Path.GetFileName(d)));
        }
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Main.OverlayHost.Children.Clear();
            if (CallerModListEntry.fixerRunning == false)
                CallerModListEntry.null_fixer();
        }
        private async void StartFixer_Click(object sender, RoutedEventArgs e)
        {
            if (cmbCharacter.SelectedItem != null)
                Fixer.Settings.Character = cmbCharacter.SelectedItem.ToString();
            if (Fixer.Settings.Character.Contains("UNKOWN"))
            {
                CustomMessageBox.Show("UNKOWN champions is not real champin bud");
                return;
            }
            if (Fixer.Settings.Character.Contains("SFX") || Fixer.Settings.Character.Contains("Announcer"))
            {
                CustomMessageBox.Show("this is not yet implemented TT");
                return;
            }
            string modDir = Path.Combine("installed", ModElement.ModFolder);
            string backupDir = Path.Combine("backup", ModElement.ModFolder);

            string modMeta = Path.Combine(modDir, "META");
            string modWad = Path.Combine(modDir, "WAD");
            string bakMeta = Path.Combine(backupDir, "META");
            string bakWad = Path.Combine(backupDir, "WAD");

            try
            {
                if (!Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);

                    if (Directory.Exists(modMeta))
                        CopyDirectory(modMeta, bakMeta);

                    if (Directory.Exists(modWad))
                    {
                        if (Directory.Exists(bakWad))
                            Directory.Delete(bakWad, true);

                        Directory.Move(modWad, bakWad);
                    }
                    Directory.CreateDirectory(modWad);
                    LowerLog("[INFO] Created Backup (just in case)", "#2dc55e");
                }
                else
                {
                    if (Directory.Exists(modWad)) Directory.Delete(modWad, true);
                    Directory.CreateDirectory(modWad);
                }
            }
            catch (Exception ex)
            {
                return;
                CustomMessageBox.Show($"{ex}", null, "ERROR");
            }
            CallerModListEntry.set_fixer(true);
            close.Visibility = Visibility.Visible;

            // --- 1. UI PHASE (Main Thread) ---
            // Prepare the UI
            MainBorder.Width = 900;
            SettingsView.Visibility = Visibility.Collapsed;
            OutputView.Visibility = Visibility.Visible;
            btnStart.Visibility = Visibility.Collapsed;

            // Capture all UI values NOW. 
            // We cannot access UI elements (CheckBoxes/ComboBoxes) inside Task.Run later.

            // Paths

            // Capture Settings into your Fixer object


            // Ensure directory existence for Settings path calculation
            string gameDataPath = Path.Combine(Path.GetDirectoryName(Main.settings.gamepath), "DATA", "FINAL");

            // Store settings
            bool folder = chkKeepFolder.IsChecked == false;
            Fixer.Settings.folder = folder;
            if (folder)
            {
                Fixer.Settings.outputDir = Path.Combine(modDir, "WAD", $"{Fixer.Settings.Character}.wad.client");
            }
            else
            {
                Fixer.Settings.outputDir = Path.Combine(modDir, "WAD", $"{Fixer.Settings.Character}.wad");
            }
            Fixer.Settings.WADpath = gameDataPath;
            Fixer.Settings.skinNo = SkinNumberValue;
            Fixer.Settings.AllAviable = chkAllSkins.IsChecked == true;
            Fixer.Settings.binless = chkBinsless.IsChecked == true;

            // Visibility check logic needs to be resolved to a simple bool here
            bool isNoSkinLightVisible = chkNoSkinLight.Visibility == Visibility.Visible;
            Fixer.Settings.noskinni = isNoSkinLightVisible && (chkNoSkinLight.IsChecked == true);

            Fixer.Settings.sfx_events = chkKeepSFX.IsChecked == true;
            Fixer.Settings.KillStaticMat = chkKillStatic.IsChecked == true;
            Fixer.Settings.keep_Icons = chkKeepIcons.IsChecked == true;
            Fixer.Settings.SoundOption = cmbSound.SelectedIndex;
            Fixer.Settings.AnimOption = cmbAnim.SelectedIndex;
            Fixer.Settings.percent = sliderValue.Value;
            Fixer.Settings.SmallMod = chkSmallMod.IsChecked == true;

            // Capture booleans for logic inside the thread
            bool doKeepUI = chkKeepUI.IsChecked == true;
            bool doAddStuff = chkAddStuff.IsChecked == true;
            bool doManifest = chkManifest.IsChecked == true;

            // Capture Manifest Data
            Manifest selectedManifest = GetSelectedManifest();

            // --- 2. BACKGROUND PHASE (Worker Thread) ---
            // We await the Task.Run, which keeps the UI responsive while waiting.
            await Task.Run(async () =>
            {
                    // Update UI about backup success safely
                    Dispatcher.Invoke(() => UpdateBackupUI(true));

                    // Reload file entries from backup for the Fixer
                    List<string> fileSysEntries = Directory.GetFileSystemEntries(bakWad).Reverse().ToList();
                    Fixer.Settings.base_wad_path = fileSysEntries;

                    // Keep UI Logic
                    if (doKeepUI)
                    {
                        string[] uiTargets = { "ui.wad", "ui.wad.client" };
                        foreach (var target in uiTargets)
                        {
                            string sourcePath = Path.Combine(bakWad, target);
                            string destPath = Path.Combine(modWad, target);

                            if (Directory.Exists(sourcePath))
                            {
                                CopyDirectory(sourcePath, destPath);
                                break;
                            }
                            else if (File.Exists(sourcePath))
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                                File.Copy(sourcePath, destPath, true);
                                break;
                            }
                        }
                    }

                    // Add Stuff Logic
                    if (doAddStuff)
                    {
                        string[] targets = { "common.wad", "common.wad.client", "map11.wad", "map11.wad.client" };
                        foreach (var target in targets)
                        {
                            string sourcePath = Path.Combine(bakWad, target);
                            string destPath = Path.Combine(modWad, target);

                            if (Directory.Exists(sourcePath))
                            {
                                CopyDirectory(sourcePath, destPath);
                                break;
                            }
                            else if (File.Exists(sourcePath))
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                                File.Copy(sourcePath, destPath, true);
                                break;
                            }
                        }
                    }


                    // Manifest Logic
                    if (doManifest && selectedManifest != null)
                    {
                        LowerLog($"[DWNL] Downloading Manifest {selectedManifest.Version}", "#5350b9");
                        List<string> downloadedFiles = new List<string>();

                        string manifestFolder = Path.Combine("manifests", selectedManifest.Version);
                        Directory.CreateDirectory(manifestFolder);

                        string characterFile = $"{Fixer.Settings.Character}.wad.client";
                        string expectedFilePath = Directory
                            .EnumerateFiles(manifestFolder, characterFile, SearchOption.AllDirectories)
                            .FirstOrDefault();

                        if (expectedFilePath == null)
                        {
                            var manifestTimer = System.Diagnostics.Stopwatch.StartNew();
                            string manifestFilePath = Path.Combine(manifestFolder, "this.manifest");

                            if (!File.Exists(manifestFilePath))
                            {
                                using (var client = new HttpClient())
                                {
                                    var data = await client.GetByteArrayAsync(selectedManifest.Link);
                                    await File.WriteAllBytesAsync(manifestFilePath, data);
                                }
                            }

                            var psi = new ProcessStartInfo
                            {
                                FileName = Path.Combine("cslol-tools", "ManifestDownloader.exe"),
                                Arguments = $"\"{manifestFilePath}\" -f {Fixer.Settings.Character}.wad -o \"{manifestFolder}\"",
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };

                            using (var process = Process.Start(psi))
                            {
                                // Wait for exit asynchronously
                                await process.WaitForExitAsync();
                            }
                        manifestTimer.Stop();
                        LowerLog($"[INFO] Manifest downloaded in {manifestTimer.Elapsed.TotalSeconds:F2}s", "#2dc55e");
                    }

                        downloadedFiles.AddRange(
                            Directory.EnumerateFiles(manifestFolder, "*.wad.client", SearchOption.AllDirectories)
                        );

                        Fixer.Settings.OldLookUp = downloadedFiles;
                        LowerLog("[INFO] Manifest Ready", "#2dc55e");
                    }


                // Finally, run the Fixer logic
                // Since this runs on the background thread now, FixiniYoursSkini MUST use 
                // the thread-safe LowerLog/UpperLog/UpdateProgress methods we updated in Step 1.
                var fixerTimer = System.Diagnostics.Stopwatch.StartNew();
                Fixer.FixiniYoursSkini(this);
                fixerTimer.Stop();
                LowerLog($"[INFO] Finished fixing mod in {fixerTimer.Elapsed.TotalSeconds:F2}s", "#2dc55e");

            });
            close_txt.IsEnabled = true;
            close_txt.Content = "Fixing done, Close Fixer";
            CallerModListEntry.set_fixer(false);
            CallerModListEntry.end_fixer();
        }
        public void UpperLog(string text, string hexColor = "#FFFFFF")
        {
            // Pass the specific RichTextBox control for the Upper Log
            AppendToLog(txtLogUpper, text, hexColor);
        }

        public void LowerLog(string text, string hexColor = "#FFFFFF")
        {
            // Pass the specific RichTextBox control for the Lower Log
            AppendToLog(txtLogLower, text, hexColor);
        }

        // ---------------------------------------------------------
        // 2. THE HELPER (Handles the messy RichTextBox logic)
        // ---------------------------------------------------------

        private void AppendToLog(RichTextBox box, string text, string hexColor)
        {
            // Ensure UI updates happen on the main thread
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // 1. Convert the Hex string (e.g., "#FF0000") to a Brush
                    var converter = new System.Windows.Media.BrushConverter();
                    var brush = (SolidColorBrush)converter.ConvertFromString(hexColor);

                    // 2. Create the text "Run" with the specific color
                    Run run = new Run(text) { Foreground = brush };

                    // 3. Get the existing paragraph or create a new one
                    // (RichTextBoxes need a Paragraph to hold text)
                    Paragraph p = box.Document.Blocks.FirstBlock as Paragraph;
                    if (p == null)
                    {
                        p = new Paragraph();
                        // Remove default margin/spacing if you want tight logs
                        p.Margin = new Thickness(0);
                        box.Document.Blocks.Add(p);
                    }

                    // 4. Add the text and a new line
                    p.Inlines.Add(run);
                    p.Inlines.Add(new Run(Environment.NewLine));

                    // 5. Scroll to bottom
                    box.ScrollToEnd();
                }
                catch (Exception ex)
                {
                    // Failsafe in case a bad hex code is passed
                    System.Diagnostics.Debug.WriteLine($"Log Error: {ex.Message}");
                }
            });
        }
        private void UpdateProgress(int percent)
        {
            Dispatcher.Invoke(() =>
            {
                progressBar.Value = percent;
                txtProgress.Text = $"{percent}%";
            });
        }
    }
}