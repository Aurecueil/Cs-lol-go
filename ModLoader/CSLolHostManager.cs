using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        private static readonly string HOST_REL_PATH = Path.Combine("cslol-tools", "ltk_patcher_host.exe");
        private static readonly string DLL_REL_PATH = Path.Combine("cslol-tools", "ltk_patcher_dll.dll");

        private static readonly object _lock = new object();
        private static DateTime? _collectDeadline = null;
        private const int WAD_FAILURE_COLLECT_WINDOW_MS = 750;
        private static bool _poof = true;

        public static bool IsRunning => _hostProcess != null && !_hostProcess.HasExited;

        private static string GetAbsoluteHostPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, HOST_REL_PATH);
        }

        private static string GetAbsoluteDllPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DLL_REL_PATH);
        }

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
            string hostPath = GetAbsoluteHostPath();
            string dllPath = GetAbsoluteDllPath();

            if (!File.Exists(hostPath))
            {
                onError?.Invoke($"Patcher host missing at '{hostPath}'");
                return;
            }

            if (!File.Exists(dllPath))
            {
                onError?.Invoke($"Patcher DLL missing at '{dllPath}'");
                return;
            }

            Stop();

            _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _poof = poof;

            int configFlags = _poof ? 0 : 4;

            try
            {
                // Format overlay prefix path strictly for LTK Host driver
                overlayPrefixPath = Path.GetFullPath(overlayPrefixPath);

                if (!Directory.Exists(overlayPrefixPath))
                {
                    Directory.CreateDirectory(overlayPrefixPath);
                }

                // Convert to forward slashes and ensure trailing slash
                overlayPrefixPath = overlayPrefixPath.Replace('\\', '/');
                if (!overlayPrefixPath.EndsWith("/"))
                {
                    overlayPrefixPath += "/";
                }
            }
            catch (Exception ex)
            {
                onError?.Invoke($"Failed to resolve overlay path: {ex.Message}");
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = hostPath,
                        WorkingDirectory = Path.GetDirectoryName(hostPath),
                        CreateNoWindow = true
                    };

                    if (elevate)
                    {
                        startInfo.UseShellExecute = true;
                        startInfo.Verb = "runas";
                        startInfo.Arguments = $"--elevate --config-loglevel 16 --config-flags {configFlags} --config-prefix \"{overlayPrefixPath}\" --start-scan";

                        onLog?.Invoke("Launching elevated patcher host...");
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
                        onLog?.Invoke("Sending configuration to patcher host...");

                        using (var writer = new StreamWriter(_hostProcess.StandardInput.BaseStream, new System.Text.Encoding.UTF8Encoding(false)))
                        {
                            writer.AutoFlush = true;

                            // Commands sent sequentially to LTK Host IPC
                            await writer.WriteLineAsync("config loglevel 16");
                            await writer.WriteLineAsync($"config flags {configFlags}");
                            await writer.WriteLineAsync($"config prefix \"{overlayPrefixPath}\"");
                            await writer.WriteLineAsync("start scan");

                            _ = ConsumeStreamAsync(localProcess.StandardOutput, onLog, onGameStatusChanged, onWadScanFailed);
                            _ = ConsumeStreamAsync(localProcess.StandardError, err => onLog?.Invoke($"[host-stderr] {err}"), null, null);

                            while (!_cts.Token.IsCancellationRequested && !_hostProcess.HasExited)
                            {
                                lock (_lock)
                                {
                                    if (_poof && _collectDeadline.HasValue && DateTime.UtcNow >= _collectDeadline.Value)
                                    {
                                        onLog?.Invoke("Skinhack Detected, Modding Rejected");
                                        _cts.Cancel();
                                    }
                                }
                                await Task.Delay(100);
                            }

                            if (!_hostProcess.HasExited)
                            {
                                try { await writer.WriteLineAsync("stop"); } catch { }
                            }
                        }
                    }
                    else
                    {
                        onLog?.Invoke("Patcher host running elevated...");

                        while (!_cts.Token.IsCancellationRequested)
                        {
                            var runningHosts = Process.GetProcessesByName("ltk_patcher_host");

                            if (runningHosts.Length == 0)
                            {
                                onLog?.Invoke("Elevated patcher host closed.");
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
                onLog?.Invoke("[Host] Stream closed.");
            }
            catch (Exception ex)
            {
                onLog?.Invoke($"[Host] Stream error: {ex.Message}");
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
                    onLog?.Invoke($"[Host] {GetMessageContent(rest)}");
                    break;
                case "error":
                    onLog?.Invoke($"[Host Error] {GetMessageContent(rest)}");
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
                    onLog?.Invoke("Waiting for game to start...");
                    onGameStatusChanged?.Invoke();
                    break;
                case "injected":
                case "hooked":
                case "attached":
                    onLog?.Invoke("Game Found!");
                    onGameStatusChanged?.Invoke();
                    break;
                case "waiting":
                    onLog?.Invoke("Waiting for game to exit...");
                    onGameStatusChanged?.Invoke();
                    break;
                case "exited":
                    onLog?.Invoke("Waiting for game to start...");
                    onGameStatusChanged?.Invoke();
                    break;
                case "failed":
                    onLog?.Invoke($"Patcher Error: {message}");
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
                    try { p.Kill(); p.Dispose(); } catch { }
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