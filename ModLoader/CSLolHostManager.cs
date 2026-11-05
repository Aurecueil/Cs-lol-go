using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ModManager
{
    public static class IsRunningCheck
    {
        public static bool IsRunning()
        {
            return CSLolHostManager.IsRunning; // || CSLolManager.IsRunning;
        }
    }

    public static class CSLolHostManager
    {
        private static Process _hostProcess;
        private static CancellationTokenSource _cts;

        private static readonly object _lock = new object();
        private static readonly object _fileLogLock = new object();
        private static DateTime? _collectDeadline = null;
        private const int WAD_FAILURE_COLLECT_WINDOW_MS = 750;
        private static bool _poof = true;
        private static string _patcherLogFilePath = null;
        private static string _lastGameStatus = null;
        private static Action _onEndOfLife = null;

        public static bool IsRunning => _hostProcess != null && !_hostProcess.HasExited;

        public static void Initialize(
            string overlayPrefixPath,
            string hostPath,
            CancellationToken token,
            Action<string> onLog,
            Action onStopped,
            Action<string, string> onGameStatusChanged = null,
            Action<string, string> onWadScanFailed = null,
            Action<string> onError = null,
            Action onEndOfLife = null,
            bool poof = false)
        {
            if (string.IsNullOrWhiteSpace(hostPath) || !File.Exists(hostPath))
            {
                onError?.Invoke($"Patcher host missing at '{hostPath}'");
                return;
            }

            hostPath = Path.GetFullPath(hostPath);
            string dllPath = Path.Combine(Path.GetDirectoryName(hostPath) ?? "", "ltk_patcher_dll.dll");

            if (!File.Exists(dllPath))
            {
                onError?.Invoke($"Patcher DLL missing at '{dllPath}'");
                return;
            }

            Stop();

            _lastGameStatus = null;
            _onEndOfLife = onEndOfLife;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _poof = poof;

            try
            {
                overlayPrefixPath = Path.GetFullPath(overlayPrefixPath);

                if (!Directory.Exists(overlayPrefixPath))
                {
                    Directory.CreateDirectory(overlayPrefixPath);
                }

                // Inside CSLolHostManager.Initialize, replace the _patcherLogFilePath setup with:
                _patcherLogFilePath = PatcherConfig.LogFilePath;
                string logDir = Path.GetDirectoryName(_patcherLogFilePath);
                if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                if (!overlayPrefixPath.EndsWith("\\"))
                {
                    overlayPrefixPath += "\\";
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
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        Arguments = "",
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    _hostProcess = new Process { StartInfo = startInfo };
                    _hostProcess.Start();
                    var localProcess = _hostProcess;

                    onLog?.Invoke("Sending configuration to patcher host...");

                    using (var writer = new StreamWriter(_hostProcess.StandardInput.BaseStream, new UTF8Encoding(false)))
                    {
                        writer.AutoFlush = true;

                        await writer.WriteLineAsync("config loglevel 16");
                        await writer.WriteLineAsync("config flags 0");
                        await writer.WriteLineAsync($"config prefix {overlayPrefixPath}");
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
            Action<string, string> onGameStatusChanged,
            Action<string, string> onWadScanFailed)
        {
            try
            {
                if (reader == null || _hostProcess == null) return;

                while (_cts != null && !_cts.Token.IsCancellationRequested && !reader.EndOfStream)
                {
                    string line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    AppendToPatcherLog(line);

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

        private static void AppendToPatcherLog(string line)
        {
            if (string.IsNullOrEmpty(_patcherLogFilePath)) return;

            try
            {
                lock (_fileLogLock)
                {
                    File.AppendAllText(_patcherLogFilePath, line + Environment.NewLine);
                }
            }
            catch { }
        }

        private static void ParseProtocolLine(
            string line,
            Action<string> onLog,
            Action<string, string> onGameStatusChanged,
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

        private static void HandleStatusTransition(
            string rest,
            Action<string> onLog,
            Action<string, string> onGameStatusChanged)
        {
            string[] tokens = rest.Split(new[] { ' ' }, 3);
            if (tokens.Length < 2) return;

            string state = tokens[1];
            string message = tokens.Length > 2 ? tokens[2] : "";
            string previousState = _lastGameStatus;
            _lastGameStatus = state;

            switch (state)
            {
                case "injecting":
                    onLog?.Invoke("Waiting for game to start...");
                    break;
                case "injected":
                case "hooked":
                case "attached":
                    onLog?.Invoke("Game Found!");
                    break;
                case "waiting":
                    onLog?.Invoke("Waiting for game to exit...");
                    break;
                case "exited":
                    onLog?.Invoke("Waiting for game to start...");
                    break;
                case "failed":
                    onLog?.Invoke($"Patcher Error: {message}");
                    break;
            }

            onGameStatusChanged?.Invoke(previousState, state);
        }

        private static void HandleDllTelemetry(string rest, Action<string> onLog, Action<string, string> onWadScanFailed)
        {
            string[] tokens = rest.Split(new[] { ' ' }, 4);
            if (tokens.Length < 4) return;

            string msgContent = tokens[3];

            if (msgContent.Contains("end of life reached"))
            {
                _onEndOfLife?.Invoke();
            }

            if (msgContent.Contains("host verification failed, refusing overlay"))
            {
                string explanation = msgContent.Contains("VRF00000008")
                    ? "ModLoader.exe must be started directly from Windows Explorer (desktop shortcut, Start menu, or double-click in Explorer) — not from a terminal, Steam, or another launcher."
                    : "Executable signature or process chain verification failed.";

                onLog?.Invoke($"Patcher refused to run: {msgContent} ({explanation})");
            }
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