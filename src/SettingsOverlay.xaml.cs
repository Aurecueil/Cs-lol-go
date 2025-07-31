using Microsoft.Win32;
using ModManager;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.IO;

namespace ModLoader
{
    public partial class SettingsOverlay : UserControl
    {
        private MainWindow mainWindow;
        private Color _originalColor;
        public SettingsOverlay()
        {
            InitializeComponent();

            this.mainWindow = Application.Current.MainWindow as MainWindow;
            DataContext = mainWindow.colorManager;
            _originalColor = mainWindow.settings.theme_color;
            LoadSettingsFromMainWindow();
        }
        private void Restore_color(object sender, RoutedEventArgs e)
        {
            mainWindow.colorManager.theme_color = _originalColor;
            mainWindow.save_settings();
        }
        private void Save_color(object sender, RoutedEventArgs e)
        {
            mainWindow.save_settings();
        }

        public void LoadSettingsFromMainWindow()
        {
            if (mainWindow?.settings == null)
            {
                MessageBox.Show("Settings object is null!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            // Load values from MainWindow.settings into UI controls
            AutoDetectPathToggle.IsChecked = mainWindow.settings.autodetect_game_path;
            DetailsDeisplay.IsChecked = mainWindow.settings.details_displ;
            ThumbDisp.IsChecked = mainWindow.settings.show_thumbs;
            path_reload.IsChecked = mainWindow.settings.update_path_on_active;
            Details_columns_state.IsChecked = mainWindow.settings.detials_column_active;
            GamePathTextBox.Text = mainWindow.settings.gamepath ?? "";
            HashesUpdatesToggle.IsChecked = mainWindow.settings.auto_update_hashes;
            // CatchUpdatesToggle.IsChecked = mainWindow.settings.catch_updated;
            mainWindow.settings.catch_updated = false;
            ImportOverrideComboBox.SelectedIndex = mainWindow.settings.import_override;
            reinitialized.IsChecked = mainWindow.settings.reinitialize;
            TilesHeights.Text = mainWindow.settings.Tile_height.ToString();
            TilesWidths.Text = mainWindow.settings.Tile_width.ToString();

            Startup_Choice.SelectedIndex = mainWindow.settings.startup_index;
            Startup_Choice.IsEnabled = mainWindow.settings.startup_start;

            StartOnStartupToggle.IsChecked = mainWindow.settings.startup_start;

            Start_Normal.SelectedIndex = mainWindow.settings.start_mode;

            no_tft.IsChecked = mainWindow.settings.not_tft;
            supress_install.IsChecked = mainWindow.settings.supress_install_confilcts;

            defhearth.Text = mainWindow.settings.default_Hearth;
            defhome.Text = mainWindow.settings.default_home;
            defAuthor.Text = mainWindow.settings.default_author;
        }

        #region Event Handlers
        private void supress_install_Changed(object sender, RoutedEventArgs e)
        {
            if (mainWindow?.settings == null) return;

            mainWindow.settings.supress_install_confilcts = supress_install.IsChecked ?? false;
            mainWindow.save_settings();
        }
        private void no_tft_Changed(object sender, RoutedEventArgs e)
        {
            if (mainWindow?.settings == null) return;

            mainWindow.settings.not_tft = no_tft.IsChecked ?? false;
            mainWindow.save_settings();
        }

        private void ThumbDisp_Changed(object sender, RoutedEventArgs e)
        {
            if (mainWindow?.settings == null) return;

            mainWindow.settings.show_thumbs = ThumbDisp.IsChecked ?? false;
            mainWindow.save_settings();
            mainWindow.RefreshAllCachedElementsDisplay();
        }
        private void HashUpdates_Changed(object sender, RoutedEventArgs e)
        {
            if (mainWindow?.settings == null) return;

            mainWindow.settings.auto_update_hashes = HashesUpdatesToggle.IsChecked ?? false;
            mainWindow.save_settings();
            mainWindow.RefreshAllCachedElementsDisplay();
        }
        private void DetailsDeisplay_Changed(object sender, RoutedEventArgs e)
        {
            if (mainWindow?.settings == null) return;

            mainWindow.settings.details_displ = DetailsDeisplay.IsChecked ?? false;
            mainWindow.save_settings();
            mainWindow.RefreshAllCachedElementsDisplay();
        }
        private void Tileswidth(object sender, TextChangedEventArgs e)
        {
            int caretIndex = TilesWidths.CaretIndex;

            string cleaned = new string(TilesWidths.Text.Where(char.IsDigit).ToArray());

            if (TilesWidths.Text != cleaned)
            {
                TilesWidths.Text = cleaned;
                TilesWidths.CaretIndex = Math.Min(caretIndex, cleaned.Length);
            }
        }

        private void TilesWidth_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TilesWidths.Text, out int value))
            {
                if (value < 150)
                    value = 150;
                else if (value > 1200)
                    value = 1200;

                TilesWidths.Text = value.ToString();
                mainWindow.settings.Tile_width = value;
            }
            else
            {
                TilesWidths.Text = "620";
                mainWindow.settings.Tile_width = 620;
            }
            mainWindow.update_tile_contrains();
            mainWindow.save_settings();
        }


        private void TilesHeight(object sender, TextChangedEventArgs e)
        {
            int caretIndex = TilesHeights.CaretIndex;

            string cleaned = new string(TilesHeights.Text.Where(char.IsDigit).ToArray());

            if (TilesHeights.Text != cleaned)
            {
                TilesHeights.Text = cleaned;
                TilesHeights.CaretIndex = Math.Min(caretIndex, cleaned.Length);
            }
        }

        private void TilesHeight_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TilesHeights.Text, out int value))
            {
                if (value < 30)
                    value = 30;
                else if (value > 99)
                    value = 99;

                TilesHeights.Text = value.ToString();
                mainWindow.settings.Tile_height = (double)value;
            }
            else
            {
                TilesHeights.Text = "60";
                mainWindow.settings.Tile_height = 60.0;
            }
            mainWindow.update_tile_contrains();
            mainWindow.save_settings();
        }


        private void Details_columns_state_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }


        private void reinitialized_Changed(object sender, RoutedEventArgs e)
        {
            if (mainWindow?.settings == null) return;

            mainWindow.settings.reinitialize = reinitialized.IsChecked ?? false;
            mainWindow.save_settings();
        }
        private void Details_columns_state_Changed(object sender, RoutedEventArgs e)
        {
            if (mainWindow?.settings == null) return;

            mainWindow.settings.detials_column_active = Details_columns_state.IsChecked ?? false;
            mainWindow.details_colums_change(mainWindow.settings.detials_column_active);
            mainWindow.save_settings();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (mainWindow != null)
            {
                mainWindow.save_settings();
                mainWindow.CloseSettingsOverlay();
            }
        }
        private void Author_Changed(object sender, RoutedEventArgs e)
        {
            if (defAuthor.Text != "" && defAuthor.Text != null)
            {
                mainWindow.settings.default_author = defAuthor.Text;
            }
            else
            {
                mainWindow.settings.default_author = "Unknown";
            }

        }
        private void hearth_Changed(object sender, RoutedEventArgs e)
        {
            mainWindow.settings.default_Hearth = defhearth.Text;
        }
        private void home_Changed(object sender, RoutedEventArgs e)
        {
            mainWindow.settings.default_home = defhome.Text;
        }

        public void SetStartup(bool enable, string appName, string exePath, int index)
        {
            string args = index switch
            {
                0 => "--isstartup",
                1 => "--isstartup --minimized",
                2 => "--isstartup --startup",
                3 => "--isstartup --startup --minimized",
                _ => "--isstartup"
            };

            string startupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string shortcutPath = Path.Combine(startupPath, $"{appName}.lnk");

            if (StartOnStartupToggle.IsChecked == true)
            {
                string psScript = $@"
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut('{shortcutPath}')
$Shortcut.TargetPath = '{exePath}'
$Shortcut.Arguments = '{args}'
$Shortcut.WorkingDirectory = '{Path.GetDirectoryName(exePath)}'
$Shortcut.WindowStyle = 1
$Shortcut.Description = '{appName} startup shortcut'
$Shortcut.Save()
";

                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{psScript}\"",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                process?.WaitForExit();
            }
            else
            {
                if (File.Exists(shortcutPath))
                    File.Delete(shortcutPath);
            }
        }



        private void AutoDetectPath_Changed(object sender, RoutedEventArgs e)
        {
            if (mainWindow?.settings == null) return;

            mainWindow.settings.autodetect_game_path = AutoDetectPathToggle.IsChecked ?? false;
            mainWindow.save_settings();
        }

        private void path_reload_Changed(object sender, RoutedEventArgs e)
        {
            if (mainWindow?.settings == null) return;

            mainWindow.settings.update_path_on_active = path_reload.IsChecked ?? false;
            mainWindow.save_settings();
        }

        private void BrowseGamePath_Click(object sender, RoutedEventArgs e)
        {
            if (mainWindow?.settings == null) return;

            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select League of Legends.exe",
                Filter = "League of Legends|League of Legends.exe|Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedPath = openFileDialog.FileName;

                if (System.IO.Path.GetFileName(selectedPath).Equals("League of Legends.exe", StringComparison.OrdinalIgnoreCase))
                {
                    GamePathTextBox.Text = selectedPath;
                    mainWindow.settings.gamepath = selectedPath;
                    mainWindow.save_settings();
                    mainWindow.LoadWadFiles();
                }
                else
                {
                    MessageBox.Show("Please select the League of Legends.exe file.", "Invalid Selection",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void StartOnStartup_Changed(object sender, RoutedEventArgs e)
        {
            if (mainWindow?.settings == null) return;

            bool enableStartup = StartOnStartupToggle.IsChecked ?? false;

            Startup_Choice.IsEnabled = enableStartup;

            SetStartup(enableStartup, "League Moddo Louda", Process.GetCurrentProcess().MainModule.FileName, Startup_Choice.SelectedIndex);

            mainWindow.settings.startup_index = Startup_Choice.SelectedIndex;
            mainWindow.settings.startup_start = enableStartup;
            mainWindow.save_settings();
        }
        private void Startup_Choice_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (mainWindow?.settings == null) return;

            SetStartup(true, "League Moddo Louda", Process.GetCurrentProcess().MainModule.FileName, Startup_Choice.SelectedIndex);

            mainWindow.settings.startup_index = Startup_Choice.SelectedIndex;
            mainWindow.save_settings();
        }

        private void Start_Normal_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (mainWindow?.settings == null) return;

            mainWindow.settings.start_mode = Start_Normal.SelectedIndex;
            mainWindow.save_settings();
        }

        private void CatchUpdates_Changed(object sender, RoutedEventArgs e)
        {
            if (mainWindow?.settings == null) return;

            mainWindow.settings.catch_updated = CatchUpdatesToggle.IsChecked ?? false;
            mainWindow.save_settings();
        }

        private void ImportOverride_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (mainWindow?.settings == null) return;

            mainWindow.settings.import_override = ImportOverrideComboBox.SelectedIndex;
            mainWindow.save_settings();
        }

        #endregion
    }
}