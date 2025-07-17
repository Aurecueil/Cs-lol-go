using Microsoft.Win32;
using ModManager;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;

namespace ModManager
{

    public partial class ModListEntry : UserControl
    {
        private MainWindow Main => (MainWindow)Application.Current.MainWindow;


        // Properties to hold the data
        public HierarchyElement FolderElement { get; private set; }
        public Mod ModElement { get; private set; }
        public bool IsMod { get; private set; }
        public bool IsParentFolder { get; private set; }
        public int ParentId { get; private set; }

        public string identifier = string.Empty;

        // Selection and dragging properties
        public bool IsSelected { get; set; }
        private bool isDragging = false;
        private Point dragStartPoint;
        private static List<ModListEntry> selectedEntries = new List<ModListEntry>();

        // Event for when folder is double-clicked
        public event Action<int> FolderDoubleClicked;

        // Events for drag operations
        public static event Action<List<ModListEntry>, ModListEntry> ItemsDropped;

        // Reference to MainWindow for drag handler
        public static MainWindow MainWindowInstance { get; set; }

        public ModListEntry(string new_id)
        {
            identifier = new_id;
            InitializeComponent();

            // Add event handlers
            this.MouseDoubleClick += OnDoubleClick;
            this.MouseLeftButtonDown += OnMouseLeftButtonDown;
            this.MouseMove += OnMouseMove;
            this.MouseLeftButtonUp += OnMouseLeftButtonUp;

            // Add event handlers for icons
            AddIconEventHandlers();
            // Enable drag and drop
            this.AllowDrop = true;
            this.Drop += OnDrop;
            this.DragEnter += OnDragEnter;
            this.DragOver += OnDragOver;
        }

        // Initialize with folder (HierarchyElement)
        public void InitializeWithFolder(HierarchyElement folderElement)
        {
            FolderElement = folderElement;
            ModElement = null;
            IsMod = false;

            UpdateUIForFolder();
        }

        // Initialize as parent folder entry (..)
        public void InitializeAsParentFolder(int parentId)
        {
            FolderElement = null;
            ModElement = null;
            IsMod = false;

            UpdateUIForParentFolder(parentId);
        }

        // Initialize with mod
        public void InitializeWithMod(Mod modElement)
        {
            ModElement = modElement;
            FolderElement = null;
            IsMod = true;

            UpdateUIForMod();
        }

        private void UpdateUIForParentFolder(int parentId)
        {
            IsParentFolder = true;
            ParentId = parentId;

            EntryName.Visibility = Visibility.Collapsed;
            // Set name to ".."
            DetailsText.Text = ". .";
            DetailsText.FontWeight = FontWeights.Bold;

            // Set details (collapsed for parent folder)
            DetailsText.Visibility = Visibility.Visible;

            // No checkbox for parent folder
            ActiveCheckbox.Visibility = Visibility.Collapsed;

            // Hide all action icons for parent folder
            FixingIcon.Visibility = Visibility.Collapsed;
            EditModIcon.Visibility = Visibility.Collapsed;
            ExportIcon.Visibility = Visibility.Collapsed;
            ModHandlingIcon.Visibility = Visibility.Collapsed;
            DeleteIcon.Visibility = Visibility.Collapsed;

            UpdateSelectionVisual();
        }
        private void ActiveCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            if (IsSelected)
            {
                foreach (var entry in selectedEntries)
                {
                    if (entry.IsMod && entry.ModElement != null)
                    {
                        entry.ModElement.isActive = true;
                        MainWindow.ProfileEntries[entry.ModElement.ModFolder] = entry.ModElement.Details.Priority;
                    }
                    else if (entry.FolderElement != null)
                    {
                        entry.FolderElement.isActive = true;
                        MainWindow.ProfileEntries[entry.FolderElement.ID.ToString()] = entry.FolderElement.Priority;
                    }
                }
            }
            else
            {
                if (IsMod && ModElement != null)
                {
                    ModElement.isActive = true;
                    MainWindow.ProfileEntries[ModElement.ModFolder] = ModElement.Details.Priority;
                }
                else if (FolderElement != null)
                {
                    FolderElement.isActive = true;
                    MainWindow.ProfileEntries[FolderElement.ID.ToString()] = FolderElement.Priority;
                }
            }
            MainWindow.SaveProfileEntriesToFile();
        }

        private void ActiveCheckbox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (IsSelected)
            {
                foreach (var entry in selectedEntries)
                {
                    if (entry.IsMod && entry.ModElement != null)
                    {
                        entry.ModElement.isActive = false;
                        MainWindow.ProfileEntries.Remove(entry.ModElement.ModFolder);
                    }
                    else if (entry.FolderElement != null)
                    {
                        entry.FolderElement.isActive = false;
                        MainWindow.ProfileEntries.Remove(entry.FolderElement.ID.ToString());
                    }
                }
            }
            else {
                if (IsMod && ModElement != null)
                {
                    ModElement.isActive = false;
                    MainWindow.ProfileEntries.Remove(ModElement.ModFolder);
                }
                else if (FolderElement != null)
                {
                    FolderElement.isActive = false;
                    MainWindow.ProfileEntries.Remove(FolderElement.ID.ToString());
                }
            }
            

            MainWindow.SaveProfileEntriesToFile(); // Save immediately
        }

        private void Delete_click_clicked(object sender, RoutedEventArgs e)
        {
            if (IsSelected)
            {
                List < (string,int, bool) > meow = new List<(string,int, bool)>();
                int has_folder = 0;
                foreach (var entry in selectedEntries)
                {
                    if (entry.IsMod)
                    {
                        meow.Add((entry.ModElement.ModFolder, 0, true));
                    }
                    else
                    {
                        has_folder = 1;
                        meow.Add(("", entry.FolderElement.ID, false));
                    }
                }
                if (has_folder == 1)
                {
                    var result = CustomMessageBox.Show(
    $"Delete folder All selected Folders?",
    new[] { "Delete with content", "Only Folders", "Cancel" },
    "Delete Folder");

                    switch (result)
                    {
                        case "Delete with content":
                            has_folder = 2;
                            break;
                        case "Only Folders":
                            has_folder = 3;
                            break;
                        case "Cancel":
                        case "": // Empty string is returned when dialog is closed without selecting a button
                            has_folder = 4;
                            break;
                    }
                }
                foreach (var element in meow)
                {
                    if (element.Item3)
                    {
                        Main.DeleteMod(element.Item1);
                    }
                    else
                    {
                        switch (has_folder)
                        {
                            case 2:
                                Main.DeleteFolderElement(element.Item2, true);
                                break;
                            case 3:
                                Main.DeleteFolderElement(element.Item2, false);
                                break;
                            case 4:
                                // do nothing
                                break;
                        }
                    }
                }
            }
            else
            {
                if (IsMod)
                {
                    Main.DeleteMod(ModElement.ModFolder);
                }
                else
                {
                    var result = CustomMessageBox.Show(
    $"Delete folder \"{FolderElement.Name}\"?", new[] { "Delete with content", "Only Folders", "Cancel" },
    "Delete Folder");

                    switch (result)
                    {
                        case "Delete with content":
                            Main.DeleteFolderElement(FolderElement.ID, true);
                            break;
                        case "Only Folders":
                            Main.DeleteFolderElement(FolderElement.ID, false);
                            break;
                        case "Cancel":
                        case "": // Empty string is returned when dialog is closed without selecting a button
                                 // do nothing
                            break;
                    }
                }
            }
            
            
        }

        private void Export_Item(object sender, RoutedEventArgs e)
        {
            if (IsMod)
            {
                string export_dir = Path.Combine("installed", ModElement.ModFolder);

                var saveDialog = new SaveFileDialog
                {
                    Title = "Export to .fantome",
                    Filter = "Fantome Files (*.fantome)|*.fantome",
                    FileName = $"{ModElement.Info.Name} by {ModElement.Info.Author}.fantome",
                    DefaultExt = ".fantome"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    string targetPath = saveDialog.FileName;

                    try
                    {
                        // Temp zip path
                        string tempZip = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".zip");

                        ZipFile.CreateFromDirectory(export_dir, tempZip);

                        // Ensure previous .fantome file doesn't exist
                        if (File.Exists(targetPath))
                            File.Delete(targetPath);

                        // Rename .zip to .fantome by moving
                        File.Move(tempZip, targetPath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        private void Open_details_page(object sender, RoutedEventArgs e)
        {
            if (IsMod)
            {
                Main.ElementsSettings(ModElement.ModFolder, true);
            }
            else
            {
                Main.ElementsSettings(FolderElement.ID.ToString(), false);
            }
        }
        private void Open_Info_page(object sender, RoutedEventArgs e)
        {
            if (IsMod)
            {
                Main.ModInfoEdit(ModElement.ModFolder);
            }
        }
        private void Fixer_settings_panel_open(object sender, RoutedEventArgs e)
        {
            string url = "https://www.youtube.com/watch?v=BbeeuzU5Qc8&autoplay=1"; // Replace with your actual URL
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true // Required for default browser
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open link: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // Your delete method example
        private void DeleteElement(HierarchyElement element, bool withContents)
        {
            // delete logic here
        }


        private void UpdateUIForFolder()
        {
            IsParentFolder = false;

            EntryName.Text = FolderElement.Name;

            // Set details (collapsed for folders)
            DetailsText.Visibility = Visibility.Collapsed;

            // Set checkbox based on isActive property
            ActiveCheckbox.IsChecked = FolderElement.isActive;
            ActiveCheckbox.Visibility = Visibility.Visible;

            // Hide mod-specific icons
            FixingIcon.Visibility = Visibility.Collapsed;
            EditModIcon.Visibility = Visibility.Collapsed;
            ExportIcon.Visibility = Visibility.Collapsed;

            // Show common icons
            ModHandlingIcon.Visibility = Visibility.Visible;
            DeleteIcon.Visibility = Visibility.Visible;

            // Update tooltips for folder context
            ModHandlingIcon.ToolTip = "Folder Settings";
            DeleteIcon.ToolTip = "Delete Folder";

            UpdateSelectionVisual();
        }

        private void UpdateUIForMod()
        {
            // Set name
            EntryName.Text = ModElement.Info.Name;

            if (Main.settings.details_displ)
            {
                string details = $"{ModElement.Info.Version} by {ModElement.Info.Author}";
                if (!string.IsNullOrWhiteSpace(ModElement.Info.Description))
                {
                    details += $"\n{ModElement.Info.Description}";
                }

                DetailsText.Text = details;
                DetailsText.Visibility = Visibility.Visible;
            }
            else
            {
                det_grid.ColumnDefinitions[1].Width = new GridLength(0);
            }
            

            // Set checkbox based on isActive property
            ActiveCheckbox.IsChecked = ModElement.isActive;

            // Show all mod-specific icons
            FixingIcon.Visibility = Visibility.Visible;
            EditModIcon.Visibility = Visibility.Visible;
            ExportIcon.Visibility = Visibility.Visible;
            ModHandlingIcon.Visibility = Visibility.Visible;
            DeleteIcon.Visibility = Visibility.Visible;

            // Update tooltips for mod context
            ModHandlingIcon.ToolTip = "Mod Settings";
            DeleteIcon.ToolTip = "Delete Mod";

            UpdateSelectionVisual();
        }

        private void UpdateSelectionVisual()
        {
            var border = this.FindName("EntryBorder") as Border;
            if (border == null)
            {
                // If border doesn't have a name, find it by type
                border = this.GetVisualChild<Border>();
            }

            if (border != null)
            {
                if (IsSelected)
                {
                    border.BorderBrush = new SolidColorBrush(Colors.LightBlue);
                    border.BorderThickness = new Thickness(2);
                    border.Background = new SolidColorBrush(Color.FromArgb(50, 173, 216, 230)); // Light blue with transparency
                }
                else
                {
                    border.BorderBrush = null;
                    border.BorderThickness = new Thickness(0);
                    border.Background = new SolidColorBrush(Color.FromArgb(255, 42, 42, 42)); // Original background
                }
            }
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Don't handle selection for parent folders
            if (IsParentFolder) return;
            // Check if Ctrl or Shift is pressed
            bool ctrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            bool shiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            if (ctrlPressed)
            {
                ToggleSelection();
            }
            else if (shiftPressed)
            {
                HandleRangeSelection();
            }
            else
            {
                // Single selection
                if (!IsSelected)
                {
                    ClearAllSelections();
                    SetSelected(true);
                }
            }

            Main.UpdateDetailsPanel(identifier);

            // Store drag start point
            dragStartPoint = e.GetPosition(this);
            this.CaptureMouse();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && this.IsMouseCaptured && !isDragging)
            {
                Point currentPosition = e.GetPosition(this);
                Vector diff = dragStartPoint - currentPosition;

                // Check if we should start dragging
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    StartDrag();
                }
            }
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (this.IsMouseCaptured)
            {
                this.ReleaseMouseCapture();
            }
            isDragging = false;
        }

        private void StartDrag()
        {
            if (IsParentFolder) return;

            isDragging = true;

            // If this item isn't selected, select it and clear others
            if (!IsSelected)
            {
                ClearAllSelections();
                SetSelected(true);
            }

            // Get all selected entries
            var selectedItems = selectedEntries.ToList();

            // Create drag data
            DataObject dragData = new DataObject("ModListEntries", selectedItems);

            // Start drag operation
            DragDropEffects result = DragDrop.DoDragDrop(this, dragData, DragDropEffects.Move);

            // Reset drag state after drag operation completes
            isDragging = false;

            // Release mouse capture if still captured
            if (this.IsMouseCaptured)
            {
                this.ReleaseMouseCapture();
            }
        }

        private void OnDrop(object sender, DragEventArgs e)
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
                    HandleItemsDrop(draggedItems, this);
                }
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
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

        private void OnDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(ModListEntry)))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true; // Prevent ScrollViewer from handling
            }
            OnDragEnter(sender, e);
        }

        public List<ModListEntry> ReadSelect()
        {
            return selectedEntries;
        }

        private void ToggleSelection()
        {
            SetSelected(!IsSelected);
        }

        public void SetSelection(bool sel)
        {
            SetSelected(sel);
        }

        private void SetSelected(bool selected)
        {
            if (IsSelected != selected)
            {
                IsSelected = selected;

                if (selected)
                {
                    if (!selectedEntries.Contains(this))
                        selectedEntries.Add(this);
                }
                else
                {
                    selectedEntries.Remove(this);
                }
                Main.CatchSelectedEntries(selectedEntries);
                UpdateSelectionVisual();
            }
        }

        private void HandleRangeSelection()
        {
            // Get parent panel to find all entries
            var parent = this.Parent as Panel;
            if (parent == null) return;

            var allEntries = parent.Children.OfType<ModListEntry>().ToList();

            // Find the last selected item
            var lastSelected = selectedEntries.LastOrDefault();
            if (lastSelected == null)
            {
                SetSelected(true);
                return;
            }

            int lastIndex = allEntries.IndexOf(lastSelected);
            int currentIndex = allEntries.IndexOf(this);

            if (lastIndex >= 0 && currentIndex >= 0)
            {
                int startIndex = Math.Min(lastIndex, currentIndex);
                int endIndex = Math.Max(lastIndex, currentIndex);

                // Select all items in range
                for (int i = startIndex; i <= endIndex; i++)
                {
                    var entry = allEntries[i];
                    if (!entry.IsParentFolder)
                    {
                        entry.SetSelected(true);
                    }
                }
            }
        }

        private static void ClearAllSelections()
        {
            foreach (var entry in selectedEntries.ToList())
            {
                entry.SetSelected(false);
            }
        }

        // Empty function for handling drop operations - implement your logic here
        // Updated HandleItemsDrop method to support parent folder as drop target
        private void HandleItemsDrop(List<ModListEntry> draggedItems, ModListEntry dropTarget)
        {
            // Call MainWindow's DragHandler with the proper format
            if (MainWindowInstance != null)
            {
                // Convert dragged items to the required format
                var draggedElements = new List<(string, bool)>();
                foreach (var item in draggedItems)
                {
                    if (item.IsMod && item.ModElement != null)
                    {
                        draggedElements.Add((item.ModElement.ModFolder, true));
                    }
                    else if (!item.IsMod && !item.IsParentFolder && item.FolderElement != null)
                    {
                        draggedElements.Add((item.FolderElement.ID.ToString(), false));
                    }
                }

                // Convert drop target to the required format
                (string, bool)? dropTargetElement = null;
                if (dropTarget.IsMod && dropTarget.ModElement != null)
                {
                    dropTargetElement = (dropTarget.ModElement.ModFolder, true);
                }
                else if (!dropTarget.IsMod && !dropTarget.IsParentFolder && dropTarget.FolderElement != null)
                {
                    dropTargetElement = (dropTarget.FolderElement.ID.ToString(), false);
                }
                else if (dropTarget.IsParentFolder)
                {
                    // Handle parent folder as drop target
                    dropTargetElement = (dropTarget.ParentId.ToString(), false);
                }

                // Call the DragHandler if we have valid data
                if (draggedElements.Count > 0 && dropTargetElement.HasValue)
                {
                    MainWindowInstance.DragHandler(draggedElements, dropTargetElement.Value);
                }
            }

            // Keep selections after drop - don't clear them

            // Also trigger the static event for backward compatibility
            ItemsDropped?.Invoke(draggedItems, dropTarget);
        }

        private void OnDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (IsParentFolder)
            {
                // Handle parent folder double-click
                FolderDoubleClicked?.Invoke(ParentId);
            }
            else if (!IsMod && FolderElement != null)
            {
                // Handle regular folder double-click
                FolderDoubleClicked?.Invoke(FolderElement.ID);
            }
        }

        private void AddIconEventHandlers()
        {
            // Add click handlers for all icons
            FixingIcon.MouseLeftButtonUp += OnFixingClicked;
            EditModIcon.MouseLeftButtonUp += OnEditModClicked;
            ExportIcon.MouseLeftButtonUp += OnExportClicked;
            ModHandlingIcon.MouseLeftButtonUp += OnModHandlingClicked;
            DeleteIcon.MouseLeftButtonUp += OnDeleteClicked;

            // Add checkbox handler
            ActiveCheckbox.Checked += OnCheckboxChanged;
            ActiveCheckbox.Unchecked += OnCheckboxChanged;
        }

        private void OnFixingClicked(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // Prevent drag operation
            if (IsMod && ModElement != null)
            {
                // Handle fixing action for mod
                MessageBox.Show($"Fixing mod: {ModElement.Info.Name}");
            }
        }

        private void OnEditModClicked(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // Prevent drag operation
            if (IsMod && ModElement != null)
            {
                // Handle edit mod action
                MessageBox.Show($"Editing mod: {ModElement.Info.Name}");
            }
        }

        private void OnExportClicked(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // Prevent drag operation
            if (IsMod && ModElement != null)
            {
                // Handle export action
                MessageBox.Show($"Exporting mod: {ModElement.Info.Name}");
            }
        }

        private void OnModHandlingClicked(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // Prevent drag operation
            if (IsMod && ModElement != null)
            {
                // Handle mod settings
                MessageBox.Show($"Mod settings for: {ModElement.Info.Name}");
            }
            else if (!IsMod && FolderElement != null)
            {
                // Handle folder settings
                MessageBox.Show($"Folder settings for: {FolderElement.Name}");
            }
        }

        private void OnDeleteClicked(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // Prevent drag operation
            string itemName = IsMod ? ModElement.Info.Name : FolderElement.Name;
            string itemType = IsMod ? "mod" : "folder";

            var result = MessageBox.Show($"Are you sure you want to delete this {itemType}: {itemName}?",
                                       "Confirm Delete",
                                       MessageBoxButton.YesNo,
                                       MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // Handle delete action
                MessageBox.Show($"Deleted {itemType}: {itemName}");
            }
        }

        private void OnCheckboxChanged(object sender, RoutedEventArgs e)
        {
            // Don't handle checkbox changes for parent folders
            if (IsParentFolder) return;

            bool isChecked = ActiveCheckbox.IsChecked ?? false;

            if (IsSelected)
            {
                foreach (var entry in selectedEntries)
                {
                    if (entry.IsMod && entry.ModElement != null)
                    {
                        // Update mod's isActive property based on checkbox
                        entry.ModElement.isActive = isChecked;
                        entry.RefreshDisplay();
                    }
                    else if (!entry.IsMod && entry.FolderElement != null)
                    {
                        // Update folder's isActive property based on checkbox
                        entry.FolderElement.isActive = isChecked;
                        entry.RefreshDisplay();
                    }
                }
            }
            else
            {
                if (IsMod && ModElement != null)
                {
                    // Update mod's isActive property based on checkbox
                    ModElement.isActive = isChecked;
                }
                else if (!IsMod && FolderElement != null)
                {
                    // Update folder's isActive property based on checkbox
                    FolderElement.isActive = isChecked;
                }
            }
        }

        public void RefreshDisplay()
        {
            if (IsParentFolder)
            {
                UpdateUIForParentFolder(ParentId);
            }
            else if (IsMod && ModElement != null)
            {
                UpdateUIForMod();
            }
            else if (!IsMod && FolderElement != null)
            {
                UpdateUIForFolder();
            }
        }

        // Helper method to clear all selections (can be called from outside)
        public static void ClearAllSelectionsStatic()
        {
            ClearAllSelections();
        }

        // Helper method to get all selected entries
        public static List<ModListEntry> GetSelectedEntries()
        {
            return selectedEntries.ToList();
        }
    }

    // Extension method to help find visual children
    public static class VisualTreeHelperExtensions
    {
        public static T GetVisualChild<T>(this DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;

                var childOfChild = GetVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }
    }
}