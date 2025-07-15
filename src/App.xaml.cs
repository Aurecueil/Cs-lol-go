using ModManager;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using SystemColors = System.Windows.SystemColors;

namespace ModManager
{
    public static class Globals
    {
        public static bool StartWithLoaded = false;
        public static bool StartMinimized = false;
    }
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (e.Args.Contains("--startup"))
            {
                Globals.StartWithLoaded = true;
            }
            if (e.Args.Contains("--minimized"))
            {
                Globals.StartMinimized = true;
            }
        }

    }
    public class ContextMenuWindow : Window
    {
        public event Action OnShowClicked;
        public event Action OnExitClicked;

        public ContextMenuWindow()
        {
            InitializeWindow();
            CreateMenu();
        }

        private void InitializeWindow()
        {
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = System.Windows.Media.Brushes.Transparent;
            this.ShowInTaskbar = false;
            this.Topmost = true;
            this.Width = 120;
            this.Height = 80;
            this.Deactivated += (s, e) => this.Hide();
        }

        private void CreateMenu()
        {
            System.Windows.Media.Brush backgroundBrush = (System.Windows.Media.Brush)Application.Current.FindResource("BackgroundBrush");
            System.Windows.Media.Brush borderBrush = (System.Windows.Media.Brush)Application.Current.FindResource("AccentBrush");
            System.Windows.Media.Brush textBrush = System.Windows.Media.Brushes.White;

            var border = new Border
            {
                Background = backgroundBrush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2)
            };

            var stackPanel = new StackPanel();

            var showButton = CreateMenuItem("Show", textBrush);
            showButton.Click += (s, e) => { OnShowClicked?.Invoke(); this.Hide(); };

            var separator = new Separator { Margin = new Thickness(2, 0, 2, 0) };

            var exitButton = CreateMenuItem("Exit", textBrush);
            exitButton.Click += (s, e) => { OnExitClicked?.Invoke(); this.Hide(); };

            stackPanel.Children.Add(showButton);
            stackPanel.Children.Add(separator);
            stackPanel.Children.Add(exitButton);

            border.Child = stackPanel;
            this.Content = border;
        }


        private Button CreateMenuItem(string text, System.Windows.Media.Brush foreground)
        {
            return new Button
            {
                Content = text,
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = foreground,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Cursor = Cursors.Hand
            };
        }


        public void ShowAt(int x, int y)
        {
            this.Left = x;
            this.Top = y;
            this.Show();
            this.Activate();
        }
    }

    public class TrayIcon : IDisposable
    {
        private const int WM_LBUTTONDBLCLK = 0x203;
        private const int WM_RBUTTONDOWN = 0x204;
        private const int WM_USER = 0x400;
        private const int WM_TRAYICON = WM_USER + 1;

        [StructLayout(LayoutKind.Sequential)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public int uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public int dwInfoFlags;
        }

        [DllImport("shell32.dll")]
        private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA pnid);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        private const uint IMAGE_ICON = 1;
        private const uint LR_LOADFROMFILE = 0x00000010;
        private const uint LR_DEFAULTSIZE = 0x00000040;

        private const int NIM_ADD = 0x00000000;
        private const int NIM_MODIFY = 0x00000001;
        private const int NIM_DELETE = 0x00000002;
        private const int NIF_MESSAGE = 0x00000001;
        private const int NIF_ICON = 0x00000002;
        private const int NIF_TIP = 0x00000004;

        private NOTIFYICONDATA _notifyIconData;
        private HwndSource _hwndSource;
        private Action _onDoubleClick;
        private Action _onRightClick;
        private IntPtr _customIconHandle = IntPtr.Zero;

        private IntPtr LoadCustomIcon(string iconPath)
        {
            if (!string.IsNullOrEmpty(iconPath))
            {
                // Try to load from full path first
                if (System.IO.File.Exists(iconPath))
                {
                    _customIconHandle = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE);
                    if (_customIconHandle != IntPtr.Zero)
                        return _customIconHandle;
                }

                // Try to load from exe directory
                var exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                var fullPath = System.IO.Path.Combine(exeDir, iconPath);
                if (System.IO.File.Exists(fullPath))
                {
                    _customIconHandle = LoadImage(IntPtr.Zero, fullPath, IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE);
                    if (_customIconHandle != IntPtr.Zero)
                        return _customIconHandle;
                }
            }

            return SystemIcons.Application.Handle;
        }

        public void ShowTrayIcon(string tooltip, Action onDoubleClick, Action onRightClick, string iconPath = null)
        {
            _onDoubleClick = onDoubleClick;
            _onRightClick = onRightClick;

            // Create a hidden window to receive messages
            var hwndSourceParameters = new HwndSourceParameters()
            {
                Width = 0,
                Height = 0,
                WindowStyle = 0,
                ParentWindow = IntPtr.Zero,
                WindowName = "TrayIconWindow"
            };

            _hwndSource = new HwndSource(hwndSourceParameters);
            _hwndSource.AddHook(WndProc);

            // Load custom icon or use default
            IntPtr iconHandle = LoadCustomIcon(iconPath);

            // Create the tray icon
            _notifyIconData = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwndSource.Handle,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = iconHandle,
                szTip = tooltip
            };

            Shell_NotifyIcon(NIM_ADD, ref _notifyIconData);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_TRAYICON)
            {
                switch (lParam.ToInt32())
                {
                    case WM_LBUTTONDBLCLK:
                        _onDoubleClick?.Invoke();
                        handled = true;
                        break;
                    case WM_RBUTTONDOWN:
                        _onRightClick?.Invoke();
                        handled = true;
                        break;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_notifyIconData.hWnd != IntPtr.Zero)
            {
                Shell_NotifyIcon(NIM_DELETE, ref _notifyIconData);
            }

            // Clean up custom icon
            if (_customIconHandle != IntPtr.Zero)
            {
                DestroyIcon(_customIconHandle);
                _customIconHandle = IntPtr.Zero;
            }

            _hwndSource?.Dispose();
        }
    }
}
