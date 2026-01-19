using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;

namespace ModManager
{
    public partial class ProfileNameDialog : UserControl
    {
        public event Action<string> OnProfileCreated;
        public event Action OnCanceled;

        public ProfileNameDialog()
        {
            InitializeComponent();
            this.Loaded += (sender, e) =>
            {
                ProfileNameBox.Focus();
                Keyboard.Focus(ProfileNameBox); // Ensures keyboard focus specifically
            };
        }

        public void SetPlaceholderText(string text)
        {
            ProfileNameBox.ApplyTemplate();

            if (ProfileNameBox.Template != null)
            {
                var placeholder = (TextBlock)ProfileNameBox.Template.FindName("placeholderText", ProfileNameBox);
                if (placeholder != null)
                {
                    placeholder.Text = text;
                }
            }
        }


        private void ProfileNameBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Create_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            var name = ProfileNameBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                // Replace non-standard PBE suffix with standard one
                if (name.EndsWith("‗PBE‗profile", StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(0, name.Length - "‗PBE‗profile".Length) + "_PBE_profile";
                }

                OnProfileCreated?.Invoke(name);
            }
        }


        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            OnCanceled?.Invoke();
        }
    }

}
