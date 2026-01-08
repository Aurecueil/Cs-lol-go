using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

            UpdateUIForFolder(true, true, true);
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

            UpdateUIForMod(true, true, true, true);
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
            ExportIcon.Visibility = Visibility.Collapsed;
            ModHandlingIcon.Visibility = Visibility.Collapsed;
            DeleteIcon.Visibility = Visibility.Collapsed;

            ApplyAlignmentSettings(); // Apply alignment settings for parent folder
            UpdateSelectionVisual();
        }

        private void ActiveCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            if (fixerRunning) return;
            if (IsSelected)
            {
                foreach (var entry in selectedEntries)
                {
                    if (entry.IsMod && entry.ModElement != null)
                    {
                        if (entry.fixerRunning) continue;
                        entry.ModElement.isActive = true;
                        entry.RefreshDisplay(false, true);
                        MainWindow.ProfileEntries[entry.ModElement.ModFolder] = entry.ModElement.Details.Priority;
                    }
                    else if (entry.FolderElement != null)
                    {
                        entry.FolderElement.isActive = true;
                        entry.RefreshDisplay(false, true);
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
            if (fixerRunning) return;
            if (IsSelected)
            {
                foreach (var entry in selectedEntries)
                {
                    if (entry.fixerRunning) continue;
                    if (entry.IsMod && entry.ModElement != null)
                    {
                        entry.ModElement.isActive = false;
                        entry.RefreshDisplay(false, true);
                        MainWindow.ProfileEntries.Remove(entry.ModElement.ModFolder);
                    }
                    else if (entry.FolderElement != null)
                    {
                        entry.FolderElement.isActive = false;
                        entry.RefreshDisplay(false, true);
                        MainWindow.ProfileEntries.Remove(entry.FolderElement.ID.ToString());
                    }
                }
            }
            else
            {
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
                List<(string, int, bool)> meow = new List<(string, int, bool)>();
                int has_folder = 0;
                foreach (var entry in selectedEntries)
                {
                    if (entry.fixerRunning) continue;
                    if (entry.exporter) continue;

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

        private async void Export_Item(object sender, RoutedEventArgs e)
        {
            if (IsSelected)
            {
                List<ModListEntry> meow = new List<ModListEntry>();
                foreach (var entry in selectedEntries)
                {
                    if (entry.ModElement == ModElement) continue;
                    if (entry.fixerRunning) continue;
                    if (entry.exporter) continue;

                    if (entry.IsMod)
                    {
                        entry.block.Text = "Exporting Mod";
                        meow.Add(entry);
                        entry.set_export(true);
                    }
                }
                block.Text = "Exporting Mod";
                set_export(true);
                await Main.Export_Mod(this, meow);
            }
            else
            {
                if (IsMod)
                {
                    block.Text = "Exporting Mod";
                    set_export(true);
                    await Main.Export_Mod(this);
                }
            }
        }

        private void Open_details_page(object _, RoutedEventArgs e)
        {
            MetaEdior metaEdior = new MetaEdior
            {
                CallerModListEntry = this // ← pass reference to self
            };

            if (IsMod)
                metaEdior.InitializeWithMod(ModElement);
            else
                metaEdior.InitializeWithFolder(FolderElement);

            Main.OverlayHost.Children.Add(metaEdior);
        }

        Repatheruwu rep = null;
        FixerUI Fixer = null;
        public bool fixerRunning = false;
        bool exporter = false;
        bool was_enabled;

        private void Fixer_settings_panel_open(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!File.Exists(Main.settings.gamepath))
                {
                    CustomMessageBox.Show("Set Gamepath in settings before using Topaz Fixer");
                    return;
                }
                if (Fixer == null || !fixerRunning) // Only create it if it doesn’t exist
                {
                    rep = new Repatheruwu();

                    Fixer = new FixerUI(Main, ModElement, rep)
                    {
                        CallerModListEntry = this,
                    };
                    Main.OverlayHost.Children.Add(Fixer);
                }
                else
                {
                    if (!Main.OverlayHost.Children.Contains(Fixer))
                        Main.OverlayHost.Children.Add(Fixer);
                }
            }
            catch (Exception ex) { 
                MessageBox.Show(ex.ToString());
                Logger.LogError("Failed to handle Runeforge protocol", ex); }
        }
        public void null_fixer()
        {
            Fixer = null;
        }

        public void end_fixer()
        {
            if (!Main.OverlayHost.Children.Contains(Fixer))
            {
                Fixer = null;
            }
        }
        public void set_export(bool running)
        {
            exporter = running;
            if (running)
            {
                FixerOverlay.Visibility = Visibility.Visible;
            }
            else
            {
                FixerOverlay.Visibility = Visibility.Collapsed;
            }
        }
        public void set_fixer(bool running)
        {
            fixerRunning = running;
            if (running)
            {
                whenrun.Visibility = Visibility.Visible;
                block.Text = "Fixer is Running";
                FixingIcon.Visibility = Visibility.Collapsed;
                FixerOverlay.Visibility = Visibility.Visible;
                was_enabled = ModElement.isActive;
                if (was_enabled) MainWindow.ProfileEntries.Remove(ModElement.ModFolder);
                ModElement.isActive = false;
                ActiveCheckbox.IsChecked = ModElement.isActive;
            }
            else
            {
                whenrun.Visibility = Visibility.Collapsed;
                FixingIcon.Visibility = Visibility.Visible;
                FixerOverlay.Visibility = Visibility.Collapsed;
                if (was_enabled) MainWindow.ProfileEntries[ModElement.ModFolder] = ModElement.Details.Priority;
                ModElement.isActive = was_enabled;
                ActiveCheckbox.IsChecked = ModElement.isActive;
            }
        }

        private void UpdateUIForFolder(bool info, bool basee, bool first)
        {
            if (basee)
            {
                ActiveCheckbox.IsChecked = FolderElement.isActive;
            }
            if (info)
            {
                EntryName.Text = $"➡️  {FolderElement.Name}";

                // Set details (collapsed for folders)
                DetailsText.Visibility = Visibility.Collapsed;
            }
            if (first)
            {

                IsParentFolder = false;
                // Hide mod-specific icons
                FixingIcon.Visibility = Visibility.Collapsed;
                ExportIcon.Visibility = Visibility.Collapsed;

                // Show common icons
                ModHandlingIcon.Visibility = Visibility.Visible;
                DeleteIcon.Visibility = Visibility.Visible;
                BGBorder.Background = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FF3C322A"));
            }


            // Update tooltips for folder context
            ApplyAlignmentSettings(); // Apply alignment settings for regular folder
            UpdateSelectionVisual();
        }

        public void SetStatus(int status)
        {
            switch (status)
            {
                case 0:
                    StatusBorder.Background = Brushes.Transparent;
                    StatusBorderBorder.BorderBrush = Brushes.Transparent;
                    break;
                case 1:
                    StatusBorder.SetResourceReference(Border.BackgroundProperty, "AccentBrush");
                    StatusBorderBorder.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");
                    break;
            }
        }
        private bool _isInitializingCombo = true;

        public void UpdateLayerComboBox()
        {
            ImportOverrideComboBox.Items.Clear();

            // Always include default options
            ImportOverrideComboBox.Items.Add(new ComboBoxItem { Content = "None" });
            ImportOverrideComboBox.Items.Add(new ComboBoxItem { Content = "Random" });

            // Add layers except "base"
            var extraLayers = ModElement.Details.Layers
                .Where(l => !string.Equals(l.Name, "base", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var layer in extraLayers)
            {
                ImportOverrideComboBox.Items.Add(new ComboBoxItem { Content = layer.Name });
            }

            // Show or hide combo box
            ImportOverrideComboBox.Visibility = extraLayers.Any() ? Visibility.Visible : Visibility.Collapsed;

            // Try to restore previous selection if it exists
            string savedSelection = ModElement.Details.layerss;
            if (!string.IsNullOrEmpty(savedSelection))
            {
                foreach (ComboBoxItem item in ImportOverrideComboBox.Items)
                {
                    if (string.Equals(item.Content.ToString(), savedSelection, StringComparison.OrdinalIgnoreCase))
                    {
                        ImportOverrideComboBox.SelectedItem = item;
                        _isInitializingCombo = false;
                        return;
                    }
                }
            }
            Main.SaveModDetails(ModElement);
            ImportOverrideComboBox.SelectedIndex = 0;
            _isInitializingCombo = false;
        }

        public void ShowSelectedLayerSetting(object sender, RoutedEventArgs e)
        {
            if (_isInitializingCombo)
                return; // skip updates during initialization
            if (ImportOverrideComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string selectedText = selectedItem.Content.ToString();
                ModElement.Details.layerss = selectedText;
                Main.SaveModDetails(ModElement);
            }
        }

        // NEW HELPER METHOD FOR ALIGNMENT
        private void ApplyAlignmentSettings()
        {
            switch (Main.settings.Ailgment)
            {
                case 0: // Top
                    elements1.VerticalAlignment = VerticalAlignment.Top;
                    elements2.VerticalAlignment = VerticalAlignment.Top;
                    elements2.Margin = new Thickness(6);
                    elements3.VerticalAlignment = VerticalAlignment.Top;
                    elements3.Margin = new Thickness(0,18,0,0);
                    break;
                case 1: // Center
                    elements1.VerticalAlignment = VerticalAlignment.Center;
                    elements2.VerticalAlignment = VerticalAlignment.Center;
                    elements2.Margin = new Thickness(8, 0, 0, 0);
                    DetailsText.VerticalAlignment = VerticalAlignment.Center;
                    DetailsText.Margin = new Thickness(8, 0, 8, 0);
                    break;
                case 2: // Bottom
                    elements1.VerticalAlignment = VerticalAlignment.Bottom;
                    elements2.VerticalAlignment = VerticalAlignment.Bottom;
                    elements2.Margin = new Thickness(6);
                    elements3.VerticalAlignment = VerticalAlignment.Bottom;
                    elements3.Margin = new Thickness(0, 0, 0, 18);
                    break;
                default:
                    break;
            }
        }
        

        private async void UpdateBackgroundUI()
        {
            if (has_thumb != null && Main.settings.show_thumbs == true)
            {
                BackgroundBorder.Background = new ImageBrush(has_thumb)
                {
                    Stretch = Stretch.UniformToFill,
                    Opacity = Main.settings.thumb_opacity
                };
            }
            else
            {
                BackgroundBorder.Background = new SolidColorBrush(Colors.Transparent);
            }
        }

        private async Task loadin_thumb()
        {
            has_thumb = await Task.Run(() => ImageLoader.GetModImage(ModElement.ModFolder));
            UpdateBackgroundUI();
        }

        private async void UpdateUIForMod(bool info, bool basee, bool image3, bool first)
        {
            if (first)
            {
                ExportIcon.Visibility = Visibility.Visible;
                ModHandlingIcon.Visibility = Visibility.Visible;
                DeleteIcon.Visibility = Visibility.Visible;
                FixingIcon.Visibility = Visibility.Visible;

                // Update tooltips for mod context
                ModHandlingIcon.ToolTip = "Mod Settings";
                DeleteIcon.ToolTip = "Delete Mod";
                loadin_thumb();
            }
            if (info)
            {
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
                    // det_grid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
                    // det_grid.ColumnDefinitions[0].MaxWidth = 250;
                    // det_grid.ColumnDefinitions[2].MaxWidth = 250;
                }
                else
                {
                    DetailsText.Visibility = Visibility.Collapsed;
                    // det_grid.ColumnDefinitions[1].Width = new GridLength(0, GridUnitType.Pixel);
                    // det_grid.ColumnDefinitions[0].MaxWidth = 1500;
                    // det_grid.ColumnDefinitions[2].MaxWidth = 1500;
                }
            }
            if (image3)
            {
                if (!first) { UpdateBackgroundUI(); }
            }


            if (basee)
            {
                ActiveCheckbox.IsChecked = ModElement.isActive;
            }

            // Calls the new helper method instead of the local switch statement
            ApplyAlignmentSettings();

            UpdateSelectionVisual();
        }
        private BitmapSource has_thumb;
        private void UpdateSelectionVisual()
        {
            var border = this.FindName("EntryBorder") as Border;
            if (border == null)
            {
                border = this.GetVisualChild<Border>();
            }

            if (border != null)
            {
                if (IsSelected)
                {
                    border.BorderBrush = new SolidColorBrush(Colors.LightBlue);
                    border.BorderThickness = new Thickness(2);
                    BGBorder.Background = new SolidColorBrush(Color.FromArgb(50, 173, 216, 230)); // Light blue with transparency
                }
                else
                {
                    border.BorderBrush = null;
                    border.BorderThickness = new Thickness(0);
                    BGBorder.Background = new SolidColorBrush(Color.FromArgb(255, 42, 42, 42)); // Original background
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
            if (selectedEntries.Count <= 1)
            {
                Main.UpdateDetailsPanel(identifier);
            }
            else
            {
                Main.UpdateDetailsPanel(identifier, false);
            }


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
                        item.SetSelection(false);
                    }
                    else if (!item.IsMod && !item.IsParentFolder && item.FolderElement != null)
                    {
                        draggedElements.Add((item.FolderElement.ID.ToString(), false));
                        item.SetSelection(false);
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


        public void RefreshDisplay(bool info = false, bool basee = false, bool image = false, bool first = false)
        {
            // check status
            // name details image
            // base
            if (IsParentFolder)
            {
                UpdateUIForParentFolder(ParentId);
            }
            else if (IsMod && ModElement != null)
            {
                UpdateUIForMod(info, basee, image, first);
            }
            else if (!IsMod && FolderElement != null)
            {
                UpdateUIForFolder(info, basee, first);
            }
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