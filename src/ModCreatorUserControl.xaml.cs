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
    public partial class ModCreatorUserControl : UserControl
    {
        private string installedPath = Path.Combine(Directory.GetCurrentDirectory(), "installed");
        private MainWindow Main => (MainWindow)Application.Current.MainWindow;

        public ModCreatorUserControl()
        {
            InitializeComponent();
            ValidateInput();
            lstDroppedFiles.ItemsSource = droppedFiles;
            if (Main.settings.default_author != "Unknown")
                txtAuthor.Text = Main.settings.default_author;
            txtHeart.Text = Main.settings.default_Hearth;
            txtHome.Text = Main.settings.default_home;

        }
        private List<DroppedFile> droppedFiles = new();
        private void ValidateInput()
        {
            bool isValid = false;
            if (txtModFolder.Text == "")
            {
                if(PlaceholderService.GetPlaceholder(txtModFolder) != "Mod Folder" && txtName.Text != "")
                {
                    true_folder_name = PlaceholderService.GetPlaceholder(txtModFolder);
                    isValid = true;
                }
                else
                {
                    PlaceholderService.SetBorderBrush(txtModFolder, Brushes.Red);
                    PlaceholderService.SetFocusedBorderBrush(txtModFolder, Brushes.Red);
                }
            }
            else
            {
                true_folder_name = txtModFolder.Text;
                if (txtName.Text != "")
                {
                    isValid = !Directory.Exists(Path.Combine(installedPath, txtModFolder.Text));
                }
                if (Directory.Exists(Path.Combine(installedPath, txtModFolder.Text)))
                {
                    PlaceholderService.SetBorderBrush(txtModFolder, Brushes.Red);
                    PlaceholderService.SetFocusedBorderBrush(txtModFolder, Brushes.Red);
                }
                    
            }
            if (isValid)
            {
                PlaceholderService.SetBorderBrush(txtModFolder, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666")));
                PlaceholderService.SetFocusedBorderBrush(txtModFolder, (Brush)Application.Current.Resources["AccentBrush"]);
            }
            if (txtName.Text == "")
            {
                PlaceholderService.SetBorderBrush(txtName, Brushes.Red);
                PlaceholderService.SetFocusedBorderBrush(txtName, Brushes.Red);
            }
            else
            {
                PlaceholderService.SetBorderBrush(txtName, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666")));
                PlaceholderService.SetFocusedBorderBrush(txtName, (Brush)Application.Current.Resources["AccentBrush"]);
            }
            btnCreate.IsEnabled = isValid;
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

            sanitized = sanitized.Trim().TrimEnd('.', ' ');

            return sanitized;
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
        }

        private void AddFiles_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "Select Files",
                Filter = "WAD and Client Files (*.wad;*.client)|*.wad;*.client|All Files|*.*",
                Multiselect = true,
                CheckFileExists = true
            };

            List<string> selectedFiles = new List<string>();

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (string file in openFileDialog.FileNames)
                {
                    string fileName = Path.GetFileName(file).ToLower();
                    string ext = Path.GetExtension(file).ToLower();

                    if (fileName.EndsWith(".wad.client") ||
                        ext == ".wad" ||
                        ext == ".client" ||
                        string.IsNullOrEmpty(ext))
                    {
                        selectedFiles.Add(file);
                    }
                }

                OnFilesDropped(selectedFiles);
            }
        }
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
            var existingPaths = new HashSet<string>(droppedFiles.Select(f => f.FullPath), StringComparer.OrdinalIgnoreCase);

            // Dictionary to track the newest file/directory for each trimmed name (case insensitive)
            var newestByTrimmedName = new Dictionary<string, (string path, DateTime lastWrite)>(StringComparer.OrdinalIgnoreCase);

            // Initialize with existing files
            foreach (var existingFile in droppedFiles)
            {
                var trimmedName = File.Exists(existingFile.FullPath) ?
                    Main.TrimWadName(Path.GetFileNameWithoutExtension(existingFile.Name)) :
                    Main.TrimWadName(existingFile.Name); // For directories, use the full name
                var lastWrite = File.Exists(existingFile.FullPath) ?
                    new FileInfo(existingFile.FullPath).LastWriteTime :
                    new DirectoryInfo(existingFile.FullPath).LastWriteTime;

                newestByTrimmedName[trimmedName] = (existingFile.FullPath, lastWrite);
            }

            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    var extension = Path.GetExtension(path)?.ToLowerInvariant();
                    if ((extension == ".wad" || extension == ".client") && !existingPaths.Contains(path))
                    {
                        var info = new FileInfo(path);
                        var trimmedName = Main.TrimWadName(Path.GetFileNameWithoutExtension(info.Name));

                        // Check if we need to replace an existing file with the same trimmed name
                        if (newestByTrimmedName.TryGetValue(trimmedName, out var existing))
                        {
                            if (info.LastWriteTime >= existing.lastWrite)
                            {
                                // Find the index of the existing file to replace it at the same position
                                var existingIndex = droppedFiles.FindIndex(f => f.FullPath == existing.path);

                                // Remove the older entry
                                RemoveExistingFile(existing.path, existingPaths);

                                // Add the newer file at the same position
                                var newFile = new DroppedFile
                                {
                                    Name = info.Name,
                                    Size = $"{info.Length / 1024.0:F1} KB",
                                    FullPath = path
                                };

                                if (existingIndex >= 0 && existingIndex < droppedFiles.Count)
                                {
                                    droppedFiles.Insert(existingIndex, newFile);
                                }
                                else
                                {
                                    droppedFiles.Add(newFile);
                                }

                                existingPaths.Add(path);
                                newestByTrimmedName[trimmedName] = (path, info.LastWriteTime);
                                AnimateFileAdded(info.Name);
                            }
                            // If current is older, skip it
                        }
                        else
                        {
                            // First occurrence of this trimmed name
                            AddFileToDroppedFiles(info, existingPaths);
                            newestByTrimmedName[trimmedName] = (path, info.LastWriteTime);
                            AnimateFileAdded(info.Name);
                        }
                    }
                }
                else if (Directory.Exists(path))
                {
                    ProcessDirectory(path, existingPaths, newestByTrimmedName);
                }
            }

            UpdateDropVisuals();
        }

        private void ProcessDirectory(string dirPath, HashSet<string> existingPaths, Dictionary<string, (string path, DateTime lastWrite)> newestByTrimmedName)
        {
            var queue = new Queue<string>();
            queue.Enqueue(dirPath);

            while (queue.Count > 0)
            {
                var currentPath = queue.Dequeue();
                var dirInfo = new DirectoryInfo(currentPath);
                var trimmedName = Main.TrimWadName(dirInfo.Name);

                if (Main.EMPTY_WADS.ContainsKey(trimmedName))
                {
                    if (!existingPaths.Contains(currentPath))
                    {
                        var lastWriteTime = dirInfo.LastWriteTime;

                        // Check if this is the newest for this trimmed name
                        if (newestByTrimmedName.TryGetValue(trimmedName, out var existing))
                        {
                            if (lastWriteTime >= existing.lastWrite)
                            {
                                // Find the index of the existing file to replace it at the same position
                                var existingIndex = droppedFiles.FindIndex(f => f.FullPath == existing.path);

                                // Remove the older entry
                                RemoveExistingFile(existing.path, existingPaths);

                                // Add newer directory at the same position
                                var dirSizeBytes = GetDirectorySize(dirInfo);
                                var dirSizeKB = dirSizeBytes / 1024.0;
                                var newDir = new DroppedFile
                                {
                                    Name = dirInfo.Name,
                                    Size = $"{dirSizeKB:F1} KB",
                                    FullPath = currentPath
                                };

                                if (existingIndex >= 0 && existingIndex < droppedFiles.Count)
                                {
                                    droppedFiles.Insert(existingIndex, newDir);
                                }
                                else
                                {
                                    droppedFiles.Add(newDir);
                                }

                                existingPaths.Add(currentPath);
                                newestByTrimmedName[trimmedName] = (currentPath, lastWriteTime);
                                AnimateFileAdded(dirInfo.Name);
                            }
                            // If current is older, skip it
                        }
                        else
                        {
                            // First occurrence of this trimmed name
                            newestByTrimmedName[trimmedName] = (currentPath, lastWriteTime);
                            AddDirectoryToDroppedFiles(dirInfo, currentPath, existingPaths);
                            AnimateFileAdded(dirInfo.Name);
                        }
                    }
                    continue; // Skip subfolders
                }

                // Process files in current directory
                foreach (var file in Directory.GetFiles(currentPath))
                {
                    var extension = Path.GetExtension(file)?.ToLowerInvariant();
                    if ((extension == ".wad" || extension == ".client") && !existingPaths.Contains(file))
                    {
                        var info = new FileInfo(file);
                        var fileTrimmedName = Main.TrimWadName(Path.GetFileNameWithoutExtension(info.Name));

                        // Check if we need to replace an existing file with the same trimmed name
                        if (newestByTrimmedName.TryGetValue(fileTrimmedName, out var existing))
                        {
                            if (info.LastWriteTime >= existing.lastWrite)
                            {
                                // Find the index of the existing file to replace it at the same position
                                var existingIndex = droppedFiles.FindIndex(f => f.FullPath == existing.path);

                                // Remove the older entry
                                RemoveExistingFile(existing.path, existingPaths);

                                // Add newer file at the same position
                                var newFile = new DroppedFile
                                {
                                    Name = info.Name,
                                    Size = $"{info.Length / 1024.0:F1} KB",
                                    FullPath = file
                                };

                                if (existingIndex >= 0 && existingIndex < droppedFiles.Count)
                                {
                                    droppedFiles.Insert(existingIndex, newFile);
                                }
                                else
                                {
                                    droppedFiles.Add(newFile);
                                }

                                existingPaths.Add(file);
                                newestByTrimmedName[fileTrimmedName] = (file, info.LastWriteTime);
                                AnimateFileAdded(info.Name);
                            }
                            // If current is older, skip it
                        }
                        else
                        {
                            // First occurrence of this trimmed name
                            AddFileToDroppedFiles(info, existingPaths);
                            newestByTrimmedName[fileTrimmedName] = (file, info.LastWriteTime);
                            AnimateFileAdded(info.Name);
                        }
                    }
                }

                // Add subdirectories to queue
                foreach (var subDir in Directory.GetDirectories(currentPath))
                {
                    queue.Enqueue(subDir);
                }
            }
        }

        private void AddDirectoryToDroppedFiles(DirectoryInfo dirInfo, string currentPath, HashSet<string> existingPaths)
        {
            var dirSizeBytes = GetDirectorySize(dirInfo);
            var dirSizeKB = dirSizeBytes / 1024.0;
            droppedFiles.Add(new DroppedFile
            {
                Name = dirInfo.Name,
                Size = $"{dirSizeKB:F1} KB",
                FullPath = currentPath
            });
            existingPaths.Add(currentPath);
        }

        private void AddFileToDroppedFiles(FileInfo info, HashSet<string> existingPaths)
        {
            droppedFiles.Add(new DroppedFile
            {
                Name = info.Name,
                Size = $"{info.Length / 1024.0:F1} KB",
                FullPath = info.FullName
            });
            existingPaths.Add(info.FullName);
        }

        private void RemoveExistingFile(string pathToRemove, HashSet<string> existingPaths)
        {
            var oldFile = droppedFiles.FirstOrDefault(f => f.FullPath == pathToRemove);
            if (oldFile != null)
            {
                droppedFiles.Remove(oldFile);
                existingPaths.Remove(pathToRemove);
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
            timer.Start();
        }

        private void UpdateDropVisuals()
        {
            bool hasFiles = droppedFiles.Count > 0;
            dropPlaceholder.Visibility = hasFiles ? Visibility.Collapsed : Visibility.Visible;
            lstDroppedFiles.Visibility = hasFiles ? Visibility.Visible : Visibility.Collapsed;
            lstDroppedFiles.ItemsSource = null;
            lstDroppedFiles.ItemsSource = droppedFiles;
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


        private void RemoveFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is DroppedFile file)
            {
                droppedFiles.Remove(file);
                UpdateDropVisuals();
            }
        }

        private void ClearFiles_Click(object sender, RoutedEventArgs e)
        {
            droppedFiles.Clear();
            UpdateDropVisuals();
        }


        private void CreateMod_Click(object sender, RoutedEventArgs e)
        {
            Block.Visibility = Visibility.Visible;
            ModInfo modInfo = new ModInfo();
            ModDetails modDetails = new ModDetails();

            modInfo.Author = txtAuthor.Text;
            modInfo.Description = txtDescription.Text;
            modInfo.Version = txtVersion.Text;
            modInfo.Name = txtName.Text;
            modInfo.Heart = txtHeart.Text;
            modInfo.Home = txtHome.Text;

            modDetails.Random = false;
            if (txtPriority.Text == "")
            {
                modDetails.Priority = int.Parse(PlaceholderService.GetPlaceholder(txtPriority));
            }
            else
            {
                modDetails.Priority = int.Parse(txtPriority.Text);
            }
            modDetails.override_ = Override.IsChecked == true;
            modDetails.InnerPath = "0";

            string Pathh = Path.Combine(installedPath, true_folder_name, "META");
            Directory.CreateDirectory(Pathh);
            Pathh = Path.Combine(installedPath, true_folder_name, "WAD");
            Directory.CreateDirectory(Pathh);

            string defaultInfoJson = JsonSerializer.Serialize(modInfo, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(installedPath, true_folder_name, "META", "info.json"), defaultInfoJson);
            string defaultDetailsJson = JsonSerializer.Serialize(modDetails, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(installedPath, true_folder_name, "META", "details.json"), defaultDetailsJson);

            foreach (var file in droppedFiles)
            {
                string sourcePath = file.FullPath;
                string targetPath = Path.Combine(Pathh, file.Name);

                try
                {
                    if (File.Exists(sourcePath))
                    {
                        File.Copy(sourcePath, targetPath, overwrite: true);
                    }
                    else if (Directory.Exists(sourcePath))
                    {
                        CopyDirectory(sourcePath, targetPath);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to copy '{file.Name}': {ex.Message}");
                }
            }
            Main.CreateModFromFolder(Path.Combine(installedPath, true_folder_name), true);
            Main.RefreshModListPanel(Main.Current_location_folder);
            Main.OverlayHost.Children.Clear();
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


        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
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
        }

        private void txtPriority_TextChanged(object sender, TextChangedEventArgs e)
        {
            SanitizeTextBoxToInt(txtPriority);
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