using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ModManager
{
    public partial class HoverDetailsPopup : UserControl
    {
        public HoverDetailsPopup()
        {
            InitializeComponent();
        }

        public void SetContent(string content)
        {
            ContentPanel.Children.Clear();

            if (string.IsNullOrWhiteSpace(content))
            {
                TitleText.Text = "No Details";
                return;
            }

            // Create a text block to display the content
            var contentText = new TextBlock
            {
                Text = content,
                Foreground = new SolidColorBrush(Colors.White),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 280,
                LineHeight = 18
            };

            ContentPanel.Children.Add(contentText);
        }
    }
}