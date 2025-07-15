using Microsoft.Win32;
using ModManager;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ModLoader
{
    public partial class SettingsOverlay : UserControl
    {
        private MainWindow mainWindow;

        public SettingsOverlay()
        {
            InitializeComponent();

            // Find the MainWindow instance
            this.mainWindow = Application.Current.MainWindow as MainWindow;
            LoadSettingsFromMainWindow();
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
            path_reload.IsChecked = mainWindow.settings.update_path_on_active;
            Details_columns_state.IsChecked = mainWindow.settings.detials_column_active;
            GamePathTextBox.Text = mainWindow.settings.gamepath ?? "";
            // CatchUpdatesToggle.IsChecked = mainWindow.settings.catch_updated;
            mainWindow.settings.catch_updated = false;
            ImportOverrideComboBox.SelectedIndex = mainWindow.settings.import_override;
            reinitialized.IsChecked = mainWindow.settings.reinitialize;
            TilesHeights.Text = mainWindow.settings.Tile_height.ToString();
            TilesWidths.Text = mainWindow.settings.Tile_width.ToString();

            StartOnStartupToggle.IsChecked = mainWindow.settings.startup_start;

            Startup_Choice.IsEnabled = mainWindow.settings.startup_start;
            Startup_Choice.SelectedIndex = mainWindow.settings.startup_index;

            Start_Normal.SelectedIndex = mainWindow.settings.start_mode;
        }

        #region Event Handlers

        private void DetailsDeisplay_Changed(object sender, RoutedEventArgs e)
        {
            if (mainWindow?.settings == null) return;

            mainWindow.settings.details_displ = DetailsDeisplay.IsChecked ?? false;
            mainWindow.save_settings();
            mainWindow.RefreshModListPanel(mainWindow.Current_location_folder);
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
                mainWindow.CloseSettingsOverlay();
            }
        }

        private void Close_Click(object sender, MouseButtonEventArgs e)
        {
            if (mainWindow != null)
            {
                mainWindow.CloseSettingsOverlay();
            }
        }
        public static void SetStartup(bool enable, string appName, string exePath, int index)
        {
            using RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);

            if (enable) { 
                switch (index)
                {
                    case 0:
                        key.SetValue(appName, $"\"{exePath}\"");
                        break;
                    case 1:
                        key.SetValue(appName, $"\"{exePath}\" --minimized");
                        break;
                    case 2:
                        key.SetValue(appName, $"\"{exePath}\" --startup");
                        break;
                    case 3:
                        key.SetValue(appName, $"\"{exePath}\" --startup --minimized");
                        break;
                }
            }

            else
                key.DeleteValue(appName, false);
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