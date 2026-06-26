using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ModManager
{
    public static class IsRunningCheck
    {
        public static bool IsRunning()
        {
            if (CSLolHostManager.IsRunning || CSLolManager.IsRunning)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
    public static class CSLolHostManager
    {
        private static Process _hostProcess;
        private static CancellationTokenSource _cts;
        private const string HOST_EXE_PATH = "cslol-tools/cslol-host.exe";

        private static readonly object _lock = new object();
        private static DateTime? _collectDeadline = null;
        private const int WAD_FAILURE_COLLECT_WINDOW_MS = 750;
        private static bool _enableSkinhackDetection = false;

        public static bool IsRunning => _hostProcess != null && !_hostProcess.HasExited;

        public static void Initialize(
            string overlayPrefixPath,
            bool elevate,
            CancellationToken token,
            Action<string> onLog,
            Action onStopped,
            Action onGameStatusChanged = null,
            Action<string, string> onWadScanFailed = null,
            Action<string> onError = null,
            bool enableSkinhackDetection = true)
        {
            if (!File.Exists(HOST_EXE_PATH))
            {
                onError?.Invoke($"cslol-host.exe missing, try restarting cslol-go");
                return;
            }

            Stop();

            _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _enableSkinhackDetection = enableSkinhackDetection;

            try
            {
                // 1. Force the path to be absolute / global
                overlayPrefixPath = Path.GetFullPath(overlayPrefixPath);

                // 2. CRITICAL: Physically ensure the folder layout exists on disk 
                // so the host's directory-verification checks pass successfully.
                if (!Directory.Exists(overlayPrefixPath))
                {
                    Directory.CreateDirectory(overlayPrefixPath);
                }

                // 3. Convert path separators to forward slashes for the Rust line protocol driver
                overlayPrefixPath = overlayPrefixPath.Replace('\\', '/');
                if (!overlayPrefixPath.EndsWith("/"))
                {
                    overlayPrefixPath += "/";
                }
            }
            catch (Exception ex)
            {
                onError?.Invoke($"Failed to resolve profile path: {ex.Message}");
                return;
            }
            Task.Run(async () =>
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = Path.GetFullPath(HOST_EXE_PATH),
                        CreateNoWindow = true
                    };

                    if (elevate)
                    {
                        startInfo.UseShellExecute = true;
                        startInfo.Verb = "runas";

                        startInfo.Arguments = $"--elevate --config-loglevel 16 --config-flags 0 --config-prefix \"{overlayPrefixPath}\" --start-scan";

                        onLog("Launching elevated host. Please accept the UAC prompt if it appears...");
                    }
                    else
                    {
                        startInfo.UseShellExecute = false;
                        startInfo.Arguments = "";
                        startInfo.RedirectStandardInput = true;
                        startInfo.RedirectStandardOutput = true;
                        startInfo.RedirectStandardError = true;
                    }

                    _hostProcess = new Process { StartInfo = startInfo };
                    _hostProcess.Start();
                    var localProcess = _hostProcess;
                    // FIX: Only touch streams if we are NOT elevated
                    if (!elevate)
                    {
                        onLog("Configuring via streams...");

                        using (StreamWriter writer = _hostProcess.StandardInput)
                        {
                            writer.AutoFlush = true;

                            await writer.WriteLineAsync("config loglevel 16");
                            await writer.WriteLineAsync("config flags 0");
                            await writer.WriteLineAsync($"config prefix {overlayPrefixPath}");
                            await writer.WriteLineAsync("start scan");

                            // ✅ Safely moved inside the non-elevated block
                            _ = ConsumeStreamAsync(localProcess.StandardOutput, onLog, onWadScanFailed);
                            _ = ConsumeStreamAsync(localProcess.StandardError, err => onLog($"[host-stderr] {err}"), null);

                            while (!_cts.Token.IsCancellationRequested && !_hostProcess.HasExited)
                            {
                                lock (_lock)
                                {
                                    if (_enableSkinhackDetection && _collectDeadline.HasValue && DateTime.UtcNow >= _collectDeadline.Value)
                                    {
                                        onLog("Skinhack Detected, Modding Rejected");
                                        _cts.Cancel();
                                    }
                                }
                                await Task.Delay(100);
                            }

                            if (!_hostProcess.HasExited)
                            {
                                await writer.WriteLineAsync("stop");
                            }
                        }
                    }
                    else
                    {
                        onLog("Host running with elevated privileges.");

                        // Elevated tracking loop
                        while (!_cts.Token.IsCancellationRequested)
                        {
                            // Check if cslol-host is still alive in Windows by its name
                            var runningHosts = Process.GetProcessesByName("cslol-host");

                            if (runningHosts.Length == 0)
                            {
                                // The process actually closed on its own (manually closed or crashed)
                                onLog("Elevated host process was closed.");
                                break;
                            }

                            // Clean up the temporary process handles we fetched
                            foreach (var p in runningHosts) p.Dispose();

                            await Task.Delay(250); // Chill for 250ms before checking again
                        }
                    }
                }
                catch (Exception ex)
                {
                    onError?.Invoke($"Host Patcher Exception: {ex.Message}");
                }
                finally
                {
                    CleanUp();
                    onStopped?.Invoke();
                }
            });

        }

        private static async Task ConsumeStreamAsync(StreamReader reader, Action<string> onLog, Action<string, string> onWadScanFailed)
        {
            try
            {
                // 1. Guard check: make sure the stream reader isn't null to begin with
                if (reader == null) return;
                if (_hostProcess == null) return;

                while (_cts != null && !_cts.Token.IsCancellationRequested && !reader.EndOfStream)
                {
                    string line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    ParseProtocolLine(line, onLog, onWadScanFailed);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException || ex is NullReferenceException)
            {
                // 2. Safe Exit: The process was killed by Stop(), closing the pipe.
                // We catch this quietly because it's an intentional shutdown.
                onLog("[Host] Stream connection closed safely.");
            }
            catch (Exception ex)
            {
                // Catch any actual unexpected errors
                onLog($"[Host] Unexpected stream error: {ex.Message}");
            }
        }

        private static void ParseProtocolLine(string line, Action<string> onLog, Action<string, string> onWadScanFailed)
        {
            string[] parts = line.Split(new[] { ' ' }, 2);
            if (parts.Length == 0) return;

            string keyword = parts[0];
            string rest = parts.Length > 1 ? parts[1] : "";

            switch (keyword)
            {
                case "ok":
                    onLog($"[Host] {GetMessageContent(rest)}");
                    break;
                case "error":
                    onLog($"[Host Protocol Error] {GetMessageContent(rest)}");
                    break;
                case "status":
                    handleStatusTransition(rest, onLog);
                    break;
                case "dll":
                    HandleDllTelemetry(rest, onLog, onWadScanFailed);
                    break;
            }
        }

        private static void handleStatusTransition(string rest, Action<string> onLog)
        {
            string[] tokens = rest.Split(new[] { ' ' }, 3);
            if (tokens.Length < 2) return;

            string state = tokens[1];
            string message = tokens.Length > 2 ? tokens[2] : "";

            switch (state)
            {
                case "injecting":
                    onLog("Waiting for game to start...");
                    break;
                case "injected":
                    onLog("GAME FOUND!");
                    break;
                case "waiting":
                    onLog("Waiting for game to exit...");
                    break;
                case "exited":
                    onLog("Waiting for game to start...");
                    break;
                case "failed":
                    onLog($"ERROR: {message}");
                    break;
            }
        }

        private static void HandleDllTelemetry(string rest, Action<string> onLog, Action<string, string> onWadScanFailed)
        {
            string[] tokens = rest.Split(new[] { ' ' }, 4);
            if (tokens.Length < 4) return;

            string msgContent = tokens[3];
            // onLog($"[Game Thread DLL] {msgContent}");

            if (msgContent.Contains("WAD scan failed"))
            {
                string status = "unknown";
                string wad = "unknown";

                if (msgContent.Contains("status with "))
                {
                    var statusPart = msgContent.Split(new[] { "status with " }, StringSplitOptions.None)[1];
                    status = statusPart.Split(' ')[0];
                }
                if (msgContent.Contains(" for "))
                {
                    wad = msgContent.Split(new[] { " for " }, StringSplitOptions.None)[1].Trim();
                }

                onWadScanFailed?.Invoke(wad, status);

                if (_enableSkinhackDetection)
                {
                    lock (_lock)
                    {
                        if (!_collectDeadline.HasValue)
                        {
                            _collectDeadline = DateTime.UtcNow.AddMilliseconds(WAD_FAILURE_COLLECT_WINDOW_MS);
                        }
                    }
                }
            }
        }

        private static string GetMessageContent(string rest)
        {
            string[] tokens = rest.Split(new[] { ' ' }, 2);
            return tokens.Length > 1 ? tokens[1] : rest;
        }

        private static void CleanUp()
        {
            try
            {
                // 1. Try standard kill first
                if (_hostProcess is null)
                {

                }
                else
                {
                    if (!_hostProcess.HasExited) { _hostProcess.Kill(); }
                }
            }
            catch { }

            try
            {
                // 2. Elevated fallback: Kill by process name if standard handle lost association
                var runningHosts = Process.GetProcessesByName("cslol-host");
                foreach (var p in runningHosts)
                {
                    p.Kill();
                    p.Dispose();
                }
            }
            catch { }

            _hostProcess?.Dispose();
            _hostProcess = null;
            _cts?.Dispose();
            _cts = null;
            lock (_lock) _collectDeadline = null;
        }

        public static void Stop()
        {
            _cts?.Cancel();
            CleanUp();
        }
    }
}