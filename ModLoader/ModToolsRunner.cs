using System.Diagnostics;
using System.IO;

namespace ModLoader
{
    public class ModToolsRunner
    {
        private Process process;

        public void KillProcess()
        {
            if (process != null && !process.HasExited)
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(1000); // Wait up to 1 second
                }
                catch (InvalidOperationException){}
                catch (System.ComponentModel.Win32Exception ex)
                {
                    // Access denied (e.g. running as Admin vs User). This should be logged.
                    Console.WriteLine($"Failed to kill process: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected error killing process: {ex}");
                }
                finally
                {
                    process?.Close();
                    process?.Dispose();
                    process = null;
                }
            }
        }

        private readonly string modToolsPath;

        public ModToolsRunner(string modToolsExePath)
        {
            if (!File.Exists(modToolsExePath))
                throw new FileNotFoundException("mod-tools.exe not found", modToolsExePath);

            modToolsPath = modToolsExePath;
        }

        public async Task<int> RunAsync(string args, Action<string> onOutput = null, Action<string> onError = null)
        {
            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = modToolsPath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                },
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (sender, e) => {
                if (!string.IsNullOrEmpty(e.Data))
                    onOutput?.Invoke(e.Data);
            };

            process.ErrorDataReceived += (sender, e) => {
                if (!string.IsNullOrEmpty(e.Data))
                    onError?.Invoke(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();
            return process.ExitCode;
        }
    }
}
