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
            return CSLolHostManager.IsRunning || CSLolManager.IsRunning;
        }
    }

    public static class CSLolHostManager
    {
        private static Process _hostProcess;
        private static CancellationTokenSource _cts;

        private const string HOST_EXE_PATH = "cslol-tools/ltk_patcher_host.exe";

        private static readonly object _lock = new object();
        private static DateTime? _collectDeadline = null;
        private const int WAD_FAILURE_COLLECT_WINDOW_MS = 750;
        private static bool _poof = true;

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
            bool poof = false)
        {
            if (!File.Exists(HOST_EXE_PATH))
            {
                onError?.Invoke($"Patcher host missing at '{HOST_EXE_PATH}'");
                return;
            }

            Stop();

            _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _poof = poof;

            // Protocol flag bitmask: 0 if enabled, 4 (OPT_OUT_AH_V1) if disabled
            int configFlags = _poof ? 0 : 4;

            try
            {
                // 1. Force the path to be absolute
                overlayPrefixPath = Path.GetFullPath(overlayPrefixPath);

                // 2. Ensure folder layout exists on disk for host directory checks
                if (!Directory.Exists(overlayPrefixPath))
                {
                    Directory.CreateDirectory(overlayPrefixPath);
                }

                // 3. Convert path separators to forward slashes for the host protocol driver
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
                        startInfo.Arguments = $"--elevate --config-loglevel 16 --config-flags {configFlags} --config-prefix \"{overlayPrefixPath}\" --start-scan";

                        onLog("Launching elevated patcher host. Please accept the UAC prompt if it appears...");
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

                    if (!elevate)
                    {
                        onLog("Configuring patcher host via streams...");

                        using (StreamWriter writer = _hostProcess.StandardInput)
                        {
                            writer.AutoFlush = true;

                            await writer.WriteLineAsync("config loglevel 16");
                            await writer.WriteLineAsync($"config flags {configFlags}");
                            await writer.WriteLineAsync($"config prefix {overlayPrefixPath}");
                            await writer.WriteLineAsync("start scan");

                            _ = ConsumeStreamAsync(localProcess.StandardOutput, onLog, onGameStatusChanged, onWadScanFailed);
                            _ = ConsumeStreamAsync(localProcess.StandardError, err => onLog($"[host-stderr] {err}"), null, null);

                            while (!_cts.Token.IsCancellationRequested && !_hostProcess.HasExited)
                            {
                                lock (_lock)
                                {
                                    if (_poof && _collectDeadline.HasValue && DateTime.UtcNow >= _collectDeadline.Value)
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
                        onLog("Patcher host running with elevated privileges.");

                        // Elevated tracking loop
                        while (!_cts.Token.IsCancellationRequested)
                        {
                            var runningHosts = Process.GetProcessesByName("ltk_patcher_host");

                            if (runningHosts.Length == 0)
                            {
                                onLog("Elevated patcher host process was closed.");
                                break;
                            }

                            foreach (var p in runningHosts) p.Dispose();

                            await Task.Delay(250);
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

        private static async Task ConsumeStreamAsync(
            StreamReader reader,
            Action<string> onLog,
            Action onGameStatusChanged,
            Action<string, string> onWadScanFailed)
        {
            try
            {
                if (reader == null || _hostProcess == null) return;

                while (_cts != null && !_cts.Token.IsCancellationRequested && !reader.EndOfStream)
                {
                    string line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    ParseProtocolLine(line, onLog, onGameStatusChanged, onWadScanFailed);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException || ex is NullReferenceException)
            {
                onLog("[Host] Stream connection closed safely.");
            }
            catch (Exception ex)
            {
                onLog($"[Host] Unexpected stream error: {ex.Message}");
            }
        }

        private static void ParseProtocolLine(
            string line,
            Action<string> onLog,
            Action onGameStatusChanged,
            Action<string, string> onWadScanFailed)
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
                    HandleStatusTransition(rest, onLog, onGameStatusChanged);
                    break;
                case "dll":
                    HandleDllTelemetry(rest, onLog, onWadScanFailed);
                    break;
            }
        }

        private static void HandleStatusTransition(string rest, Action<string> onLog, Action onGameStatusChanged)
        {
            string[] tokens = rest.Split(new[] { ' ' }, 3);
            if (tokens.Length < 2) return;

            string state = tokens[1];
            string message = tokens.Length > 2 ? tokens[2] : "";

            switch (state)
            {
                case "injecting":
                    onLog("Waiting for game to start...");
                    onGameStatusChanged?.Invoke();
                    break;
                case "injected":
                case "hooked":
                case "attached":
                    onLog("GAME FOUND!");
                    onGameStatusChanged?.Invoke();
                    break;
                case "waiting":
                    onLog("Waiting for game to exit...");
                    onGameStatusChanged?.Invoke();
                    break;
                case "exited":
                    onLog("Waiting for game to start...");
                    onGameStatusChanged?.Invoke();
                    break;
                case "failed":
                    onLog($"ERROR: {message}");
                    onGameStatusChanged?.Invoke();
                    break;
            }
        }

        private static void HandleDllTelemetry(string rest, Action<string> onLog, Action<string, string> onWadScanFailed)
        {
            string[] tokens = rest.Split(new[] { ' ' }, 4);
            if (tokens.Length < 4) return;

            string msgContent = tokens[3];

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

                if (_poof)
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
                if (_hostProcess != null && !_hostProcess.HasExited)
                {
                    _hostProcess.Kill();
                }
            }
            catch { }

            try
            {
                var runningHosts = Process.GetProcessesByName("ltk_patcher_host");

                foreach (var p in runningHosts)
                {
                    try
                    {
                        p.Kill();
                        p.Dispose();
                    }
                    catch { }
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