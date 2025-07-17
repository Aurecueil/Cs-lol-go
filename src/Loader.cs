using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media.Media3D;

namespace ModManager
{
    public static class CSLolManager
    {
        private static Thread _workerThread;
        private static CancellationTokenSource _internalCts;

        public static void Initialize(string profilePath, CancellationToken token, Action<string> onLog, Action onStopped, Action onGameStatusChanged = null, Action<string> onError = null)
        {
            if (string.IsNullOrWhiteSpace(profilePath))
            {
                onError?.Invoke("No profile path provided!");
                return;
            }

            // Stop any existing worker thread
            Stop();

            var error = CSLolInterop.cslol_init();
            if (error != IntPtr.Zero)
            {
                onError?.Invoke("Failed to init: " + Marshal.PtrToStringAnsi(error));
                return;
            }

            error = CSLolInterop.cslol_set_config(profilePath);
            if (error != IntPtr.Zero)
            {
                onError?.Invoke("Failed to set prefix: " + Marshal.PtrToStringAnsi(error));
                return;
            }

            _internalCts = new CancellationTokenSource();
            var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(token, _internalCts.Token).Token;

            _workerThread = new Thread(() =>
            {
                try
                {
                    while (!combinedToken.IsCancellationRequested)
                    {
                        onLog("Waiting for game to start...");
                        uint tid = 0;

                        // Wait for game to start
                        while ((tid = CSLolInterop.cslol_find()) == 0)
                        {
                            if (combinedToken.IsCancellationRequested)
                                break;
                            Thread.Sleep(16);
                        }

                        if (combinedToken.IsCancellationRequested)
                            break;

                        onLog("Game found!");

                        // Hook into the game
                        error = CSLolInterop.cslol_hook(tid, 30000, 100);
                        if (error != IntPtr.Zero)
                        {
                            onError?.Invoke("Failed to hook: " + Marshal.PtrToStringAnsi(error));
                            break;
                        }

                        onLog("Waiting for game to exit...");

                        // Monitor game while it's running
                        while (CSLolInterop.cslol_find() == tid)
                        {
                            if (combinedToken.IsCancellationRequested)
                                break;

                            IntPtr msgPtr;
                            while ((msgPtr = CSLolInterop.cslol_log_pull()) != IntPtr.Zero)
                            {
                                string msg = Marshal.PtrToStringAnsi(msgPtr);
                                onLog(msg);
                            }
                            Thread.Sleep(1000);
                        }

                        // Game has exited - notify for reinitialization
                        if (!combinedToken.IsCancellationRequested)
                        {
                            onLog("Game exited. Preparing to reinitialize...");
                            onGameStatusChanged?.Invoke();
                        }
                    }
                }
                catch (Exception ex)
                {
                    onError?.Invoke("CSLol worker thread error: " + ex.Message);
                }
                finally
                {
                    onStopped?.Invoke();
                }
            })
            {
                IsBackground = true
            };

            _workerThread.Start();
        }

        public static bool IsRunning()
        {
            return _workerThread != null && _workerThread.IsAlive && !(_internalCts?.IsCancellationRequested ?? true);
        }

        public static void Stop()
        {
            _internalCts?.Cancel();

            if (_workerThread != null && _workerThread.IsAlive)
            {
                if (!_workerThread.Join(2000)) // Wait up to 5 seconds
                {
                    try
                    {
                        _workerThread.Abort(); // Force kill if it doesn't stop gracefully
                    }
                    catch (Exception) { }
                }
            }

            _workerThread = null;
            _internalCts?.Dispose();
            _internalCts = null;
        }
    }
}