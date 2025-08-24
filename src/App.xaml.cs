using ModManager;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.IO.Pipes;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using SystemColors = System.Windows.SystemColors;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Application = System.Windows.Application;
using Cursors = System.Windows.Input.Cursors;
using System.Windows.Media.Imaging;
using System.Drawing.Imaging;
using Image = System.Drawing.Image;
using System.Net;
using System.Text.Json;
using System.Text;

namespace ModManager
{
    public static class Logger
    {
        private static readonly string logFilePath = "Simple_logs.txt";
        public static void Log(string message)
        {
            try
            {
                // Split by newlines so each line gets a timestamp
                var lines = message.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                using (StreamWriter sw = new StreamWriter(logFilePath, true))
                {
                    foreach (var line in lines)
                    {
                        string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {line}";
                        sw.WriteLine(logEntry);
                    }
                }
            }
            catch
            {
                // silently ignore logging errors to prevent crashes
            }
        }
        public static void LogError(string message, Exception ex)
        {
            Log($"ERROR: {message}\nException: {ex}");
        }
    }

    public static class Globals
    {
        public static bool StartWithLoaded = false;
        public static bool StartMinimized = false;
        public static bool is_startup = false;

    }

    public partial class App : Application
    {
        const string PipeName = "ModdoLoudaDA_paipu";
        private static Mutex _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            bool isNewInstance;
            _mutex = new Mutex(true, @"Global\ModdoLoudaDA", out isNewInstance);

            if (!isNewInstance)
            {
                SendArgsToExistingInstance(e.Args);
                Shutdown();
                return;
            }

            if (e.Args.Contains("--startup"))
            {
                Globals.StartWithLoaded = true;
            }
            if (e.Args.Contains("--minimized"))
            {
                Globals.StartMinimized = true;
            }
            if (e.Args.Contains("--isstartup"))
            {
                Globals.is_startup = true;
            }

            base.OnStartup(e);

            StartHttpServer();

            Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
                        using var reader = new StreamReader(server);
                        server.WaitForConnection();
                        string arguments = reader.ReadToEnd();
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            HandleArguments(arguments);
                            if (!arguments.Contains("--dbu"))
                            {
                                BringToFront();
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Pipe error: {ex.Message}");
                    }
                }
            });
        }

        private HttpListener _listener;

        private void StartHttpServer()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://localhost:7345/");
            _listener.Start();

            Task.Run(async () =>
            {
                while (_listener.IsListening)
                {
                    try
                    {
                        var context = await _listener.GetContextAsync();
                        _ = Task.Run(() => HandleRequest(context));
                    }
                    catch (Exception ex)
                    {
                        File.AppendAllText("http_server_error.log", ex.ToString());
                    }
                }
            });
        }

        private async void HandleRequest(HttpListenerContext context)
        {
            context.Response.AddHeader("Access-Control-Allow-Origin", "*");
            context.Response.AddHeader("Access-Control-Allow-Methods", "POST, OPTIONS");
            context.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type");

            if (context.Request.HttpMethod == "OPTIONS")
            {
                context.Response.StatusCode = 200;
                context.Response.Close();
                return;
            }

            if (context.Request.HttpMethod == "GET" && context.Request.Url.AbsolutePath == "/fetch-mods")
            {
                var savedMods = new List<ModStatus>();

                // Path to your /installed folder (relative or absolute)
                string installedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "installed");

                if (Directory.Exists(installedPath))
                {
                    var modFolders = Directory.GetDirectories(installedPath)
                        .Where(dir => Path.GetFileName(dir).StartsWith(".rf---"));

                    foreach (var folder in modFolders)
                    {
                        string folderName = Path.GetFileName(folder);
                        string modId = folderName.Substring(".rf---".Length); // Remove prefix

                        string releaseFile = Path.Combine(folder, "meta", "release.txt");
                        string release = "";

                        if (File.Exists(releaseFile))
                        {
                            try
                            {
                                release = File.ReadAllText(releaseFile).Trim();
                            }
                            catch
                            {
                                release = "";
                            }
                        }

                        savedMods.Add(new ModStatus
                        {
                            Id = modId,
                            Release = release,
                            Status = ""
                        });
                    }
                }

                var jsonResponse = JsonSerializer.Serialize(new { Mods = savedMods });

                var buffer = Encoding.UTF8.GetBytes(jsonResponse);
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer);
                context.Response.Close();
                return;
            }

            else if (context.Request.Url.AbsolutePath == "/install-mod")
                {
                    using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                    var body = await reader.ReadToEndAsync();

                    var installRequest = JsonSerializer.Deserialize<InstallModRequest>(body);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var mainWindow = (MainWindow)Application.Current.MainWindow;

                    
                    mainWindow.handle_rf_install(
                        installRequest.Id,
                        installRequest.Release,
                        installRequest.Extra
                    );
                });

                // No need to wait or send detailed response
                context.Response.StatusCode = 200;
                    context.Response.Close();
                    return;
                }

            context.Response.StatusCode = 404;
            context.Response.Close();
        }

        public class InstallModRequest
        {
            public string Id { get; set; }
            public string Release { get; set; }
            public string Extra { get; set; }
        }

        
        public class ModStatus
        {
            public string Id { get; set; }
            public string Release { get; set; }
            public string Status { get; set; }
        }


        void HandleArguments(string args)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Application.Current.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.ProcessArguments(args);
                }
            });
        }



        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_RESTORE = 9;

        void BringToFront()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow == null) return;

                // If your app uses "Hide to Tray", it might be Visibility.Hidden
                if (mainWindow.Visibility != Visibility.Visible)
                {
                    mainWindow.Show(); // Bring it back
                }

                // Also handle minimize
                if (mainWindow.WindowState == WindowState.Minimized)
                {
                    mainWindow.WindowState = WindowState.Normal;
                }
                mainWindow.ShowInTaskbar = true;
                // Bring it to foreground
                mainWindow.Activate(); // Better than SetForegroundWindow in WPF
                mainWindow.Topmost = true;  // Force z-order
                mainWindow.Topmost = false; // Reset
                mainWindow.Focus();
            });
        }


        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            base.OnExit(e);
        }
        void SendArgsToExistingInstance(string[] args)
        {
            try
            {
                using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                {
                    client.Connect(500);
                    using (var writer = new StreamWriter(client))
                    {
                        writer.WriteLine(string.Join(" ", args));
                        writer.Flush(); // Ensure data is sent
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Pipe error: {ex.Message}"); // Debug the actual error
            }
        }
    }




    public class ContextMenuWindow : Window
    {
        public event Action OnShowClicked;
        public event Action OnExitClicked;
        public event Action OnLoaderClicked;

        private Button _loaderButton;
        private bool _isLoaderRunning = false;
        private bool _isLoaderDisabled = false;

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
            this.Height = 76; // Increased height to accommodate third button
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
                BorderThickness = new Thickness(2), // Increased from 1 to 3 (2px thicker)
                CornerRadius = new CornerRadius(3)
            };

            var stackPanel = new StackPanel();

            // Create loader button (positioned at top)
            _loaderButton = CreateMenuItem("Load Mods", textBrush);
            _loaderButton.Click += (s, e) => { OnLoaderClicked?.Invoke(); this.Hide(); };

            var showButton = CreateMenuItem("Show", textBrush);
            showButton.Click += (s, e) => { OnShowClicked?.Invoke(); this.Hide(); };

            var exitButton = CreateMenuItem("Exit", textBrush);
            exitButton.Click += (s, e) => { OnExitClicked?.Invoke(); this.Hide(); };

            // Add buttons in order: Loader, Show, Exit
            stackPanel.Children.Add(_loaderButton);
            stackPanel.Children.Add(showButton);
            stackPanel.Children.Add(exitButton);

            border.Child = stackPanel;
            this.Content = border;
        }

        private Button CreateMenuItem(string text, System.Windows.Media.Brush foreground)
        {
            return new Button
            {
                Style = (Style)Application.Current.Resources["tray_highlight"],
                Content = text,
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = foreground,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Cursor = Cursors.Hand
            };
        }

        public void UpdateLoaderState(bool isRunning, bool isDisabled = false)
        {
            _isLoaderRunning = isRunning;
            _isLoaderDisabled = isDisabled;

            if (_loaderButton != null)
            {
                _loaderButton.Content = isRunning ? "Stop Mods" : "Load Mods";
                _loaderButton.IsEnabled = !isDisabled;

                // Optional: Change appearance when disabled
                if (isDisabled)
                {
                    _loaderButton.Foreground = System.Windows.Media.Brushes.Gray;
                    _loaderButton.Cursor = Cursors.Arrow;
                }
                else
                {
                    _loaderButton.Foreground = System.Windows.Media.Brushes.White;
                    _loaderButton.Cursor = Cursors.Hand;
                }
            }
        }

        public void ShowAt(int x, int y)
        {
            this.Left = x;
            this.Top = y - this.Height;
            this.Show();
            this.Activate();
        }
    }
    public static class ImageLoader
    {
        public static BitmapImage GetModImage(string modFolder)
        {
            string metaFolder = Path.Combine(Directory.GetCurrentDirectory(), "installed", modFolder, "META");

            if (!Directory.Exists(metaFolder))
                return null;

            // Supported extensions
            string[] extensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };

            foreach (var ext in extensions)
            {
                string imagePath = Path.Combine(metaFolder, "image" + ext);
                if (File.Exists(imagePath))
                {
                    var image = LoadBitmapImage(imagePath);
                    if (image != null)
                        return image;
                }
            }

            return null; // No valid image found
        }

        private static BitmapImage LoadBitmapImage(string filePath)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // Load image into memory immediately
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache; // Force refresh, ignore cache
                bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze(); // Makes it safe for multi-threaded use
                return bitmap;
            }
            catch
            {
                return null; // In case of an invalid image or error
            }
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
