using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace ModManager
{
    public static class CSLolManager
    {
        private static Thread _workerThread;
        private static CancellationTokenSource _internalCts;
        private const string DLL_PATH = "cslol-tools/cslol-dll.dll";

        public static bool IsRunning => _workerThread != null && _workerThread.IsAlive;

        public static void Initialize(string profilePath, CancellationToken token, Action<string> onLog, Action onStopped, Action onGameStatusChanged = null, Action<string> onError = null)
        {
            if (!File.Exists(DLL_PATH))
            {
                onError?.Invoke("cslol-dll.dll is missing.");
                return;
            }

            if (IsDllLockedByExternalProcess())
            {
                onError?.Invoke("cslol-dll.dll is locked down by another process (likely Vanguard). Try restarting your PC");
                return;
            }
            Stop();

            _internalCts = new CancellationTokenSource();
            var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(token, _internalCts.Token).Token;

            _workerThread = new Thread(() =>
            {
                try
                {
                    CSLolInterop.cslol_set_flags(0);
                    var err = CSLolInterop.cslol_init();
                    if (err != IntPtr.Zero) throw new Exception(Marshal.PtrToStringAnsi(err));

                    err = CSLolInterop.cslol_set_config(profilePath);
                    if (err != IntPtr.Zero) throw new Exception(Marshal.PtrToStringAnsi(err));

                    CSLolInterop.cslol_set_log_level(0x10);

                    while (!combinedToken.IsCancellationRequested)
                    {
                        onLog("Waiting for game to start...");
                        uint tid = 0;

                        while ((tid = CSLolInterop.cslol_find()) == 0)
                        {
                            if (combinedToken.IsCancellationRequested) return;
                            Thread.Sleep(50);
                        }

                        onLog($"Game found!");

                        err = CSLolInterop.cslol_hook(tid, 300000, 100);

                        if (err != IntPtr.Zero)
                        {
                            onLog("Failed to hook: " + Marshal.PtrToStringAnsi(err));
                        }
                        else
                        {
                            onLog("Waiting for game to exit...");
                        }

                        while (CSLolInterop.cslol_find() == tid && !combinedToken.IsCancellationRequested)
                        {
                            IntPtr msgPtr;
                            while ((msgPtr = CSLolInterop.cslol_log_pull()) != IntPtr.Zero)
                            {
                                // onLog("[cslol] " + Marshal.PtrToStringAnsi(msgPtr));
                            }
                            Thread.Sleep(1000);
                        }

                        if (!combinedToken.IsCancellationRequested)
                        {
                            onLog("Waiting for game to start...");
                            onGameStatusChanged?.Invoke();
                        }
                    }
                }
                catch (Exception ex) { onError?.Invoke("Patcher Error: " + ex.Message + "\nTry restarting PC if nothing else works"); }
                finally { onStopped?.Invoke(); }
            })
            {
                IsBackground = true,
                // CRITICAL: Prevent the OS from "pausing" the hook thread while the game loads
                Priority = ThreadPriority.Highest
            };

            _workerThread.Start();
        }
        private static bool IsDllLockedByExternalProcess()
        {
            try
            {
                using (var fs = File.Open(DLL_PATH, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    return false;
                }
            }
            catch (IOException)
            {
                return true;
            }
        }
        public static void Stop()
        {
            _internalCts?.Cancel();
            if (_workerThread != null && _workerThread.IsAlive) _workerThread.Join(2000);
            _workerThread = null;
            _internalCts?.Dispose();
            _internalCts = null;
        }
    }
}