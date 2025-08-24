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
using System.Windows.Media.Imaging;
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

        private string image_path = "";
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
        private string TrimWadName(string fileName)
        {
            string trimmed = fileName;
            if (trimmed.EndsWith(".client", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(0, trimmed.Length - 7);
            if (trimmed.EndsWith(".wad", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(0, trimmed.Length - 4);
            return trimmed;
        }
        private static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif", ".ico"
};

        private bool IsImageFile(string filePath)
        {
            string extension = Path.GetExtension(filePath);
            return ImageExtensions.Contains(extension);
        }

        private bool IsValidImage(string imagePath)
        {
            try
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.UriSource = new Uri(imagePath, UriKind.Absolute);
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void UpdateBackgroundImage()
        {
            // Direct access to the named Image control
            if (!string.IsNullOrEmpty(image_path) && File.Exists(image_path))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(image_path, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bg_img_border.Source = bitmap;
                }
                catch
                {
                    // If loading fails, clear the image
                    bg_img_border.Source = null;
                    image_path = "";
                }
            }
            else
            {
                bg_img_border.Source = null;
            }
        }
        private void OnFilesDropped(IEnumerable<string> paths)
        {
            foreach (var path in paths.Where(p => File.Exists(p) && IsImageFile(p)))
            {
                if (IsValidImage(path))
                {
                    image_path = path;
                    UpdateBackgroundImage();
                    break; // Use only the first valid image found
                }
                else
                {
                    image_path = "";
                    UpdateBackgroundImage();
                }
            }
            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    string trimmed = TrimWadName(Path.GetFileName(path));
                    if (!Main.EMPTY_WADS.ContainsKey(trimmed)) { continue; }
                    bool skip = false;
                    var info = new FileInfo(path);
                    for (int i = 0; i < droppedFiles.Count; i++)
                    {
                        if (TrimWadName(droppedFiles[i].Name) == trimmed)
                        {
                            skip = true;
                            droppedFiles[i] = new DroppedFile
                            {
                                Name = $"{trimmed}.wad.client",
                                Size = $"{info.Length / 1024.0:F1} KB",
                                FullPath = path
                            };
                            AnimateFileAdded($"{trimmed}.wad.client");
                            break; 
                        }
                    }
                    if (skip) { continue; }
                    droppedFiles.Add(new DroppedFile
                    {
                        Name = $"{trimmed}.wad.client",
                        Size = $"{info.Length / 1024.0:F1} KB",
                        FullPath = path
                    });
                    AnimateFileAdded($"{trimmed}.wad.client");

                }
                else if (Directory.Exists(path))
                {
                    var info = new DirectoryInfo(path);
                    string trimmed = TrimWadName(info.Name);
                    if (!Main.EMPTY_WADS.ContainsKey(trimmed))
                    {
                        OnFilesDropped(Directory.EnumerateFileSystemEntries(path, "*", SearchOption.TopDirectoryOnly)); continue; }
                    bool skip = false;
                    for (int i = 0; i < droppedFiles.Count; i++)
                    {
                        if (TrimWadName(droppedFiles[i].Name) == trimmed)
                        {
                            skip = true;
                            droppedFiles[i] = new DroppedFile
                            {
                                Name = $"{trimmed}.wad.client",
                                Size = $"{GetDirectorySize(info) / 1024.0:F1} KB",
                                FullPath = path
                            };
                            AnimateFileAdded($"{trimmed}.wad.client");
                            break;
                        }
                    }
                    if (skip) { continue; }
                    droppedFiles.Add(new DroppedFile
                    {
                        Name = $"{trimmed}.wad.client",
                        Size = $"{GetDirectorySize(info) / 1024.0:F1} KB",
                        FullPath = path
                    });
                    AnimateFileAdded($"{trimmed}.wad.client");
                }
            }

            UpdateDropVisuals();
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
            true_folder_name = true_folder_name.Trim().TrimEnd('.', ' ');
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
            if (image_path != "")
            {
                using (var img = System.Drawing.Image.FromFile(image_path)) img.Save(Path.Combine(Pathh, "image.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
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
            Main.SaveModDetails(Main.CreateModFromFolder(Path.Combine(installedPath, true_folder_name), true));
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