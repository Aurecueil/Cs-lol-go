using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;

namespace ModLoader
{
    public partial class ProfileNameDialog : UserControl
    {
        public event Action<string> OnProfileCreated;
        public event Action OnCanceled;

        public ProfileNameDialog()
        {
            InitializeComponent();
        }

        public void SetPlaceholderText(string text)
        {
            // Find the placeholderText TextBlock inside the TextBox's template
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
                OnProfileCreated?.Invoke(name);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            OnCanceled?.Invoke();
        }
    }

}
