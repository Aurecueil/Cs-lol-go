using ModManager;
using System.Windows.Controls;
using System.Windows;

namespace ModManager
{
public partial class HierarchyElementEditor : UserControl
{
    private HierarchyElement _folder;
    private Mod _mod;
    private bool _isMod;

    public HierarchyElementEditor(HierarchyElement folder, bool box)
    {
        InitializeComponent();
        _folder = folder;
        _isMod = false;

        OverrideBox.Text = folder.Override.ToString();
        PriorityBox.Text = folder.Priority.ToString();
            RandomCheck.IsChecked = box;
        }

    public HierarchyElementEditor(Mod mod, bool box)
    {
        InitializeComponent();
        _mod = mod;
        _isMod = true;

        OverrideBox.Text = mod.Details.Override.ToString();
        PriorityBox.Text = mod.Details.Priority.ToString();
            RandomCheck.IsChecked = box;
        }

    private void ValueChanged()
    {
        if (_isMod)
        {
            if (int.TryParse(OverrideBox.Text, out var ovr)) _mod.Details.Override = ovr;
            if (int.TryParse(PriorityBox.Text, out var pri)) _mod.Details.Priority = pri;
            if (RandomCheck.IsChecked == true) { _mod.Details.Random = true; }else { _mod.Details.Random = false; }

                Main.SaveModDetails(_mod);
        }
        else
        {
            if (int.TryParse(OverrideBox.Text, out var ovr)) _folder.Override = ovr;
            if (int.TryParse(PriorityBox.Text, out var pri)) _folder.Priority = pri;
            if (RandomCheck.IsChecked == true) { _folder.Random = true; }else { _folder.Random = false; }

            Main.SaveFolder(_folder);
        }
    }
    private void Close_Click(object sender, RoutedEventArgs e)
    {
            if (_isMod)
            {
                if (int.TryParse(OverrideBox.Text, out var ovr)) _mod.Details.Override = ovr;
                if (int.TryParse(PriorityBox.Text, out var pri)) _mod.Details.Priority = pri;
                if (RandomCheck.IsChecked == true) { _mod.Details.Random = true; } else { _mod.Details.Random = false; }

                Main.SaveModDetails(_mod);
            }
            else
            {
                if (int.TryParse(OverrideBox.Text, out var ovr)) _folder.Override = ovr;
                if (int.TryParse(PriorityBox.Text, out var pri)) _folder.Priority = pri;
                if (RandomCheck.IsChecked == true) { _folder.Random = true; } else { _folder.Random = false; }

                Main.SaveFolder(_folder);
            }
        ((Panel)this.Parent).Children.Remove(this);
    }
    private void OverrideBox_TextChanged(object sender, TextChangedEventArgs e) => ValueChanged();
    private void PriorityBox_TextChanged(object sender, TextChangedEventArgs e) => ValueChanged();
    private void RandomCheck_Checked(object sender, RoutedEventArgs e) => ValueChanged();

    private MainWindow Main => (MainWindow)Application.Current.MainWindow;
}
}