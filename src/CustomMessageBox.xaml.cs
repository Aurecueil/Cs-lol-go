using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;

namespace ModManager
{
    public partial class CustomMessageBox : Window
    {
        private TaskCompletionSource<string> _tcs = new();

        public CustomMessageBox(string message, IEnumerable<string> options, string? title = null)
        {
            InitializeComponent();
            MessageText.Text = message;

            // Set window title if provided, otherwise use default
            if (!string.IsNullOrEmpty(title))
            {
                this.Title = title;
            }

            var buttons = new List<string>(options);
            ButtonPanel.Columns = buttons.Count;
            foreach (var option in buttons)
            {
                var button = new Button
                {
                    Style = (Style)Application.Current.Resources["diagwindow_highlight"],
                    Content = option,
                    Background = System.Windows.Media.Brushes.Transparent,
                    Foreground = System.Windows.SystemColors.ControlTextBrush,
                    BorderBrush = (System.Windows.Media.Brush)Application.Current.Resources["AccentBrush"],
                    BorderThickness = new Thickness(2),
                    Padding = new Thickness(8, 4, 8, 4),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Margin = new Thickness(4),
                    MinWidth = 60,
                };
                // Apply rounded corners using a CornerRadius-capable style template
                button.Resources.Add(typeof(Border), new Style(typeof(Border))
                {
                    Setters =
                {
                    new Setter(Border.CornerRadiusProperty, new CornerRadius(6))
                }
                });
                button.Click += (_, _) =>
                {
                    _tcs.TrySetResult(option);
                    Close();
                };
                ButtonPanel.Children.Add(button);
            }
            this.Closed += (s, e) =>
            {
                if (!_tcs.Task.IsCompleted)
                {
                    _tcs.TrySetResult("");
                }
            };
        }

        public static string Show(string message, IEnumerable<string>? options = null, string? title = null)
        {
            var box = new CustomMessageBox(message, options ?? new[] { "OK" }, title)
            {
                Owner = Application.Current.MainWindow
            };
            box.ShowDialog();
            return box._tcs.Task.Result;
        }
    }
}