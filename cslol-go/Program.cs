using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;

internal static class Program
{
    static async Task<int> Main(string[] args)
    {
        bool debug = args.Any(a => a.Equals("-debug", StringComparison.OrdinalIgnoreCase)) || args.Any(a => a.Equals("-d", StringComparison.OrdinalIgnoreCase));
        bool wait = args.Any(a => a.Equals("-w", StringComparison.OrdinalIgnoreCase));
        if (wait)
        {
            Thread.Sleep(200); // waits for 100 milliseconds
        }

        if (debug)
        {
            ConsoleHelper.AllocConsole();
            Console.WriteLine("Checking for updates");
        }

        try
        {
            await RunUpdater(debug);
        }
        catch (Exception ex)
        {
            Log("FATAL ERROR:", debug);
            Log(ex.ToString(), debug);

            if (debug)
            {
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }
        try
        {
            Log("Launching main app...", debug);
            LaunchMainApp();

            return 0;
        }
        catch (Exception ex)
        {
            Log("FATAL ERROR:", debug);
            Log(ex.ToString(), debug);

            if (debug)
            {
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }

            return 1;
        }
    }
    static async Task RunUpdater(bool debug)
    {
        string baseDir = AppContext.BaseDirectory;

        string versionFile = Path.Combine(baseDir, "version.txt");
        string localVersion = File.Exists(versionFile)
            ? File.ReadAllText(versionFile).Trim()
            : "0.0.0";

        Log($"Local version: {localVersion}", debug);

        const string OWNER = "Aurecueil";
        const string REPO = "Cs-lol-go";

        using HttpClient http = new();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("cslol-go-auto-update");

        Log("Checking latest GitHub release...", debug);

        GitHubRelease release = await GetLatestRelease(http, OWNER, REPO);

        string remoteVersion = NormalizeVersion(release.tag_name);
        Log($"Remote version: {remoteVersion}", debug);

        if (!IsNewer(remoteVersion, localVersion))
        {
            Log("No update needed", debug);
            return;
        }

        Log("Update available!", debug);

        GitHubAsset asset = release.assets
            .FirstOrDefault(a => a.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            ?? throw new Exception("No .zip asset found in latest release");

        string tempDir = Path.Combine(baseDir, "_update");
        string zipPath = Path.Combine(tempDir, asset.name);

        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, true);

        Directory.CreateDirectory(tempDir);

        Log($"Downloading {asset.name}...", debug);
        await DownloadFileAsync(http, asset.browser_download_url, zipPath);

        Log("Extracting update...", debug);
        ZipFile.ExtractToDirectory(zipPath, tempDir, overwriteFiles: true);

        Log("Applying update...", debug);
        ApplyUpdate(tempDir, baseDir, debug);

        File.WriteAllText(versionFile, remoteVersion);

        Log("Update complete", debug);
    }
    static async Task<GitHubRelease> GetLatestRelease(HttpClient http, string owner, string repo)
    {
        string url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
        string json = await http.GetStringAsync(url);

        return JsonSerializer.Deserialize<GitHubRelease>(json)!;
    }
    static string NormalizeVersion(string tag)
    {
        return tag.StartsWith("v", StringComparison.OrdinalIgnoreCase)
            ? tag[1..]
            : tag;
    }
    static async Task DownloadFileAsync(HttpClient http, string url, string path)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        await response.Content.CopyToAsync(fs);
    }
    record GitHubRelease(
    string tag_name,
    bool prerelease,
    List<GitHubAsset> assets
);

    record GitHubAsset(
        string name,
        string browser_download_url
    );

    static void ApplyUpdate(string sourceDir, string targetDir, bool debug)
    {
        foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceDir, file);
            string dest = Path.Combine(targetDir, relative);

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }
    }

    static bool IsNewer(string remote, string local)
    {
        if (Version.TryParse(remote, out var r) &&
            Version.TryParse(local, out var l))
        {
            return r > l;
        }

        return false;
    }

    static void LaunchMainApp()
    {
        string exePath = Path.Combine(AppContext.BaseDirectory, "ModLoader.exe");

        Process.Start(new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = true
        });
    }

    static void Log(string msg, bool debug)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        File.AppendAllText("updater.log", line + Environment.NewLine);

        if (debug)
            Console.WriteLine(line);
    }
}
static class ConsoleHelper
{
    [DllImport("kernel32.dll")]
    public static extern bool AllocConsole();
}
