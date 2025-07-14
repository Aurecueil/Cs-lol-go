using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ModManager;

namespace ModManager
{
    public partial class ModDetailsEditor : UserControl
    {
        public Mod Mod { get; set; }

        public ModDetailsEditor(Mod mod)
        {
            InitializeComponent();
            Mod = mod;
            DataContext = this;
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            MainWindow main = (MainWindow)Application.Current.MainWindow;
            main.SaveModInfo(Mod); // Changed from SaveModInfo to 
            ((Panel)this.Parent).Children.Remove(this);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            ((Panel)this.Parent).Children.Remove(this);
        }

        private void SelectImage_Click(object sender, RoutedEventArgs e)
        {
        }
    }
}