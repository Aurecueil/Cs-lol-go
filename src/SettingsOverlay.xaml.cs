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
            GamePathTextBox.Text = mainWindow.settings.gamepath ?? "";
            StartOnStartupToggle.IsChecked = mainWindow.settings.startup_start;
            LoadModsOnStartToggle.IsChecked = mainWindow.settings.load_start;
            CatchUpdatesToggle.IsChecked = mainWindow.settings.catch_updated;
            ImportOverrideComboBox.SelectedIndex = mainWindow.settings.import_override;
        }

        #region Event Handlers

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
        public static void SetStartup(bool enable, string appName, string exePath)
        {
            using RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);

            if (enable)
                // key.SetValue(appName, $"\"{exePath}\"");
                //key.SetValue(appName, $"\"{exePath}\" --startup");
                key.SetValue(appName, $"\"{exePath}\" --startup --minimized");

            else
                key.DeleteValue(appName, false);
        }

        private void AutoDetectPath_Changed(object sender, RoutedEventArgs e)
        {
            if (mainWindow?.settings == null) return;

            mainWindow.settings.autodetect_game_path = AutoDetectPathToggle.IsChecked ?? false;
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

            SetStartup(enableStartup, "MyApp", Process.GetCurrentProcess().MainModule.FileName);

            mainWindow.settings.startup_start = enableStartup;
            mainWindow.save_settings();
        }


        private void LoadModsOnStart_Changed(object sender, RoutedEventArgs e)
        {
            if (mainWindow?.settings == null) return;

            mainWindow.settings.load_start = LoadModsOnStartToggle.IsChecked ?? false;
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