using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Linq;
using Application = System.Windows.Application;
using DataFormats = System.Windows.DataFormats;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Path = System.IO.Path;

namespace ModManager
{
    public partial class MetaEdior : UserControl
    {
        public ModListEntry CallerModListEntry { get; set; }

        private string installedPath = Path.Combine(Directory.GetCurrentDirectory(), "installed");
        private MainWindow Main => (MainWindow)Application.Current.MainWindow;
        public HierarchyElement FolderElement { get; private set; }
        public Mod ModElement { get; private set; }
        public bool IsMod { get; private set; }
        private bool valid_Name = true;
        private bool valid_Folder = true;
        private string start_name = string.Empty;


        public MetaEdior()
        {
            InitializeComponent();
        }
        private ObservableCollection<DroppedFile> droppedFiles = new();
        private class DroppedFile : INotifyPropertyChanged
        {
            public string Name { get; set; }
            public string Size { get; set; }
            public string FullPath { get; set; }

            private bool _isGlowing;
            public bool IsGlowing
            {
                get => _isGlowing;
                set { _isGlowing = value; OnPropertyChanged(nameof(IsGlowing)); }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string name)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }


        public void InitializeWithMod(Mod modElement)
        {
            ModElement = modElement;
            FolderElement = null;
            IsMod = true;
            titler.Text = "Edit Mod";
            start_name = modElement.ModFolder;
            txtName.Text = modElement.Info.Name;
            txtModFolder.Text = modElement.ModFolder;
            txtAuthor.Text = modElement.Info.Author;
            txtVersion.Text = modElement.Info.Version;
            txtDescription.Text = modElement.Info.Description;
            txtHeart.Text = modElement.Info.Heart;
            txtHome.Text = modElement.Info.Home;
            txtPriority.Text = modElement.Details.Priority.ToString();
            Override.IsChecked = modElement.Details.override_;
            Random.IsChecked = modElement.Details.Random;
            dropPanelDisp.Visibility = Visibility.Visible;
            InitializeDropFiles();
        }
        public void InitializeWithFolder(HierarchyElement folderElement)
        {
            MainVorder.Height = 200;
            nameGridd.Margin = new Thickness(0, 0, 0, 15);
            MYgrid.RowDefinitions[2].Height = new GridLength(0);
            FolderElement = folderElement;
            ModElement = null;
            IsMod = false;
            titler.Text = "Edit Folder";
            FolderGrid.Visibility = Visibility.Collapsed;
            AuthorGrid.Visibility = Visibility.Collapsed;
            DescriptionGrid.Visibility = Visibility.Collapsed;
            VersionGrid.Visibility = Visibility.Collapsed;
            HHGrid.Visibility = Visibility.Collapsed;

            Override.IsChecked = folderElement.override_;
            Random.IsChecked = folderElement.Random;

            txtName.Text = folderElement.Name;
            txtPriority.Text = folderElement.Priority.ToString();
        }
        private void Override_Checked(object sender, RoutedEventArgs e)
        {
            if (Override.IsChecked == null)
                return;

            if (IsMod)
            {
                ModElement.Details.override_ = Override.IsChecked == true;
            }
            else
            {
                FolderElement.override_ = Override.IsChecked == true;
            }
        }

        private void Random_Checked(object sender, RoutedEventArgs e)
        {
            if (IsMod)
            {
                ModElement.Details.Random = Random.IsChecked == true;
            }
            else
            {
                FolderElement.Random = Random.IsChecked == true;
            }
        }

        private void ValidateInput()
        {
            if (txtModFolder.Text == start_name)
            {
                PlaceholderService.SetBorderBrush(txtModFolder, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666")));
                PlaceholderService.SetFocusedBorderBrush(txtModFolder, (Brush)Application.Current.Resources["AccentBrush"]);
                Update_folder_name.IsEnabled = false;
                valid_Folder = false;
            }
            else if (txtModFolder.Text == "")
            {
                PlaceholderService.SetBorderBrush(txtModFolder, Brushes.Red);
                PlaceholderService.SetFocusedBorderBrush(txtModFolder, Brushes.Red);
                Update_folder_name.IsEnabled = false;
            }
            else
            {
                true_folder_name = txtModFolder.Text;
                if (Directory.Exists(Path.Combine(installedPath, txtModFolder.Text)))
                {
                    PlaceholderService.SetBorderBrush(txtModFolder, Brushes.Red);
                    PlaceholderService.SetFocusedBorderBrush(txtModFolder, Brushes.Red);
                    Update_folder_name.IsEnabled = false;
                    valid_Folder = false;
                }
                else
                {
                    PlaceholderService.SetBorderBrush(txtModFolder, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666")));
                    PlaceholderService.SetFocusedBorderBrush(txtModFolder, (Brush)Application.Current.Resources["AccentBrush"]);
                    Update_folder_name.IsEnabled = true;
                    valid_Folder = true;
                }

            }
            if (txtName.Text == "")
            {
                PlaceholderService.SetBorderBrush(txtName, Brushes.Red);
                PlaceholderService.SetFocusedBorderBrush(txtName, Brushes.Red);
                valid_Folder = false;
            }
            else
            {
                PlaceholderService.SetBorderBrush(txtName, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666")));
                PlaceholderService.SetFocusedBorderBrush(txtName, (Brush)Application.Current.Resources["AccentBrush"]);
                valid_Folder = true;
            }
        }
        private string true_folder_name = "";

        private void ValidateModFolder(object sender = null, EventArgs e = null)
        {
            txtModFolder.Text = SanitizeFolderName(txtModFolder.Text);
            ValidateInput();
        }

        public static string SanitizeFolderName(string name)
        {
            string invalidChars = new string(Path.GetInvalidFileNameChars());
            string escaped = Regex.Escape(invalidChars);
            string sanitized = Regex.Replace(name, $"[{escaped}]", "");

            return sanitized;
        }
        private void UpdateFolder_Click(object sender, EventArgs e)
        {
            if (IsMod && valid_Folder)
            {
                true_folder_name = true_folder_name.Trim().TrimEnd('.', ' ');
                string source = Path.Combine("installed", ModElement.ModFolder);
                string destination = Path.Combine("installed", true_folder_name);
                Directory.Move(source, destination);
                Main.DeleteMod(ModElement.ModFolder);
                if (ModElement.isActive)
                {
                    MainWindow.ProfileEntries[ModElement.ModFolder] = ModElement.Details.Priority;
                }
                ModElement = Main.CreateModFromFolder(true_folder_name);
                start_name = true_folder_name;
                Main.RefreshModListPanel(Main.Current_location_folder);
            }
        }
        private void Author_Changed(object sender, EventArgs e)
        {
            if (IsMod)
            {
                ModElement.Info.Author = txtAuthor.Text;
                CallerModListEntry?.RefreshDisplay();
            }
        }
        private void Version_Changed(object sender, EventArgs e)
        {
            if (IsMod)
            {
                ModElement.Info.Version = txtVersion.Text;
                CallerModListEntry?.RefreshDisplay();
            }
        }
        private void Home_changed(object sender, EventArgs e)
        {
            if (IsMod)
            {
                ModElement.Info.Home = txtHome.Text;
            }
        }
        private void Hearth_changed(object sender, EventArgs e)
        {
            if (IsMod)
            {
                ModElement.Info.Heart = txtHeart.Text;
            }
        }

        private void Name_input_changed(object sender, RoutedEventArgs e)
        {
            string new_folder = SanitizeFolderName(txtName.Text);
            if (txtModFolder.Text == "" && new_folder != "")
            {
                int counter = 2;
                string baseFolder = new_folder;
                while (Directory.Exists(Path.Combine(installedPath, new_folder)))
                {
                    new_folder = $"{baseFolder}_{counter}";
                    counter++;
                }

                PlaceholderService.SetPlaceholder(txtModFolder, new_folder);
            }
            else
            {
                PlaceholderService.SetPlaceholder(txtModFolder, "Mod Folder");
            }
            ValidateInput();
            if (txtName.Text != "")
            {
                if (IsMod)
                {
                    ModElement.Info.Name = txtName.Text;
                }
                else
                {
                    FolderElement.Name = txtName.Text;
                }
                CallerModListEntry?.RefreshDisplay();

            }
        }
        private void InitializeDropFiles()
        {
            if (!IsMod || ModElement == null) return;

            droppedFiles.Clear();
            string wadPath = Path.Combine(installedPath, ModElement.ModFolder, "WAD");

            if (!Directory.Exists(wadPath)) return;

            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string filePath in Directory.GetFiles(wadPath))
            {
                string trimmed = TrimWadName(Path.GetFileName(filePath));
                if (unique.Add(trimmed))
                {
                    var info = new FileInfo(filePath);
                    droppedFiles.Add(new DroppedFile
                    {
                        Name = $"{info.Name}",
                        Size = $"{info.Length / 1024.0:F1} KB",
                        FullPath = filePath
                    });
                }
            }

            foreach (string dirPath in Directory.GetDirectories(wadPath))
            {
                string trimmed = Path.GetFileName(dirPath);
                if (unique.Add(trimmed))
                {
                    long dirSizeBytes = GetDirectorySize(new DirectoryInfo(dirPath));
                    droppedFiles.Add(new DroppedFile
                    {
                        Name = $"{Path.GetFileName(dirPath)} (DIR)",
                        Size = $"{dirSizeBytes / 1024.0:F1} KB",
                        FullPath = dirPath
                    });
                }
            }

            UpdateDropVisuals();
        }
        private void dropBorder_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;

            e.Handled = true;
        }
        private void dropBorder_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] droppedPaths = (string[])e.Data.GetData(DataFormats.FileDrop);
                OnFilesDropped(droppedPaths);
            }
        }
        private void OnFilesDropped(IEnumerable<string> paths)
        {
            if (!IsMod || ModElement == null) return;

            string targetDir = Path.Combine(installedPath, ModElement.ModFolder, "WAD");
            Directory.CreateDirectory(targetDir);

            // Store the files that will be animated
            var filesToAnimate = new List<string>();

            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    string trimmed = TrimWadName(Path.GetFileName(path));
                    DeleteConflictingFiles(targetDir, trimmed);
                    string destPath = Path.Combine(targetDir, Path.GetFileName(path));
                    File.Copy(path, destPath, overwrite: true);
                    filesToAnimate.Add(Path.GetFileName(path)); // Store just the filename
                }
                else if (Directory.Exists(path))
                {
                    string trimmed = new DirectoryInfo(path).Name;
                    DeleteConflictingFiles(targetDir, TrimWadName(trimmed));
                    string destPath = Path.Combine(targetDir, trimmed);
                    Directory.CreateDirectory(destPath);
                    CopyDirectory(path, destPath);
                    filesToAnimate.Add($"{trimmed} (DIR)"); // Store the directory name
                }
            }

            // Refresh the list first
            InitializeDropFiles();

            // Then animate the files that were just added
            foreach (var fileName in filesToAnimate)
            {
                AnimateFileAdded(fileName);
            }
        }

        private void DeleteConflictingFiles(string targetDir, string trimmedName)
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(targetDir))
            {
                string name = Path.GetFileName(entry);
                string trimmed = TrimWadName(name);
                if (string.Equals(trimmed, trimmedName, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        if (Directory.Exists(entry))
                            Directory.Delete(entry, true);
                        else
                            File.Delete(entry);
                    }
                    catch { /* handle/log as needed */ }
                }
            }
        }
        private void AnimateFileAdded(string fileName)
        {
            var file = droppedFiles.FirstOrDefault(f => f.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase));
            if (file == null) return;

            file.IsGlowing = true;

            // Use a timer instead of Task.Delay to avoid timing issues
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(1100); // Slightly longer than animation
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                file.IsGlowing = false;
            };
        }


        private void UpdateDropVisuals()
        {
            bool hasFiles = droppedFiles.Count > 0;
            dropPlaceholder.Visibility = hasFiles ? Visibility.Collapsed : Visibility.Visible;
            lstDroppedFiles.Visibility = hasFiles ? Visibility.Visible : Visibility.Collapsed;
            lstDroppedFiles.ItemsSource = null;
            lstDroppedFiles.ItemsSource = droppedFiles;
        }



        private void RemoveFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is DroppedFile file)
            {
                try
                {
                    if (File.Exists(file.FullPath))
                        File.Delete(file.FullPath);
                    else if (Directory.Exists(file.FullPath))
                        Directory.Delete(file.FullPath, true);
                }
                catch { /* log or handle error */ }

                droppedFiles.Remove(file);
                UpdateDropVisuals();
            }
        }

        long GetDirectorySize(DirectoryInfo dir)
        {
            long size = 0;
            // Add file sizes.
            FileInfo[] files = dir.GetFiles("*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                size += file.Length;
            }
            return size;
        }
        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);

            foreach (string filePath in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourceDir, filePath);
                string destPath = Path.Combine(destinationDir, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                File.Copy(filePath, destPath, overwrite: true);
            }
        }
        private string TrimWadName(string fileName)
        {
            string trimmed = fileName;
            if (trimmed.EndsWith(".client", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(0, trimmed.Length - 7);
            if (trimmed.EndsWith(".wad", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(0, trimmed.Length - 4);
            return trimmed;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (IsMod)
            {
                Main.SaveModDetails(ModElement);
                Main.SaveModInfo(ModElement);
            }
            else
            {
                Main.SaveFolder(FolderElement);
            }
            Main.OverlayHost.Children.Clear();
        }
        private void txtDescription_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.Height = Double.NaN; // Reset to auto
                textBox.UpdateLayout();

                int lineCount = textBox.LineCount;
                if (lineCount == 0)
                    lineCount = textBox.Text.Split('\n').Length; // Fallback if LineCount not yet updated

                double lineHeight = textBox.FontSize * 1.4; // Approximate line height
                double newHeight = lineCount * lineHeight + textBox.Padding.Top + textBox.Padding.Bottom;

                textBox.Height = Math.Max(newHeight, textBox.MinHeight);
            }
            if (IsMod)
            {
                ModElement.Info.Description = txtDescription.Text;
                CallerModListEntry?.RefreshDisplay();
            }
        }


        private void txtPriority_TextChanged(object sender, TextChangedEventArgs e)
        {
            SanitizeTextBoxToInt(txtPriority);

            if (string.IsNullOrWhiteSpace(txtPriority.Text)) return;

            if (IsMod)
            {
                if (ModElement?.Details == null) return;

                ModElement.Details.Priority = int.Parse(txtPriority.Text);
            }
            else
            {
                if (FolderElement == null) return;

                int prio = int.Parse(txtPriority.Text);
                FolderElement.Priority = prio;
            }
        }


        private void SanitizeTextBoxToInt(TextBox textBox)
        {
            if (textBox == null) return;

            string text = textBox.Text;
            if (int.TryParse(text, out int result))
            {
                // Optionally: Clamp value to a certain range
                // result = Math.Max(min, Math.Min(max, result));
                // Optional: Update text to clean format
                if (text != result.ToString())
                    textBox.Text = result.ToString();
            }
            else
            {
                // Remove non-digit characters or reset to 0
                string digitsOnly = new string(text.Where(char.IsDigit).ToArray());
                if (string.IsNullOrEmpty(digitsOnly)) digitsOnly = "0";
                textBox.Text = digitsOnly;

                // Move cursor to end
                textBox.CaretIndex = textBox.Text.Length;
            }
        }
    }
}