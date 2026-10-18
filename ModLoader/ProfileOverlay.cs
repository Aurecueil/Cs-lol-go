#nullable enable
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace ModPkgLibSpace;

internal static class ProfileOverlay
{
    internal static string ProfilePath(string root, string name, bool tft, string gameExe) =>
        Path.Combine(root, "profiles", name) + (tft ? "‗TFT" : "") +
        (gameExe.EndsWith(@"(PBE)\Game\League of Legends.exe", StringComparison.OrdinalIgnoreCase) ? "‗PBE‗profile" : "");

    internal static string[] SelectedPaths(string installed, IEnumerable<string> selected) =>
        selected.Distinct(StringComparer.OrdinalIgnoreCase).Select(name =>
        {
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(['/', '\\']) >= 0)
                throw new InvalidDataException("Invalid selected mod folder.");
            string path = Path.GetFullPath(Path.Combine(installed, name));
            GameDataMods.RequireInside(installed, path);
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException($"Selected mod is missing: {name}");
            return path;
        }).ToArray();

    internal static bool HasRawAssets(string mod) =>
        Directory.Exists(Path.Combine(mod, "RAW")) && Directory.EnumerateFiles(Path.Combine(mod, "RAW"), "*", SearchOption.AllDirectories).Any();

    internal static async Task BuildNativeAsync(string helper, string game, string profile,
        string installed, IReadOnlyList<string> mods, bool noTft, Action<string> progress, CancellationToken token)
    {
        // Snapshot only the caller's selected mods. Never discover enabled mods by
        // scanning installed/, and never publish partial output after a failed build.
        var paths = SelectedPaths(installed, mods);
        string state = Path.Combine(profile, "mod-tools");
        string staging = Path.Combine(state, "staging");
        string output = Path.Combine(profile, "overlay");
        Directory.CreateDirectory(state);
        GameDataMods.RequireInside(state, staging);
        if (Directory.Exists(staging)) Directory.Delete(staging, true);
        Directory.CreateDirectory(staging);
        await File.WriteAllTextAsync(Path.Combine(state, "request.json"), JsonSerializer.Serialize(new
        {
            game = Path.GetFullPath(game), output = Path.GetFullPath(output), mods = paths, no_tft = noTft
        }), token);

        var start = new ProcessStartInfo(helper)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add("mkoverlay");
        start.ArgumentList.Add(Path.GetFullPath(installed));
        start.ArgumentList.Add(Path.GetFullPath(staging));
        start.ArgumentList.Add("--game:" + Path.GetFullPath(game));
        start.ArgumentList.Add("--mods:" + string.Join('/', mods));
        if (noTft) start.ArgumentList.Add("--noTFT");
        start.ArgumentList.Add("--ignoreConflict");
        progress($"Building ordinary mods: {string.Join(", ", mods)}");
        using var process = Process.Start(start) ?? throw new IOException("Could not start mod-tools.");
        using var cancellation = token.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        });
        Task<string> errors = process.StandardError.ReadToEndAsync();
        try
        {
            while (await process.StandardOutput.ReadLineAsync(token) is { } line) progress(line);
            await process.WaitForExitAsync(token);
        }
        catch (OperationCanceledException)
        {
            await process.WaitForExitAsync(CancellationToken.None);
            await errors;
            throw;
        }
        string error = await errors;
        await File.WriteAllTextAsync(Path.Combine(state, "stderr.txt"), error, CancellationToken.None);
        token.ThrowIfCancellationRequested();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"mod-tools failed (exit {process.ExitCode}):\n{error}");
        if (!string.IsNullOrWhiteSpace(error)) progress(error);
        Publish(profile, state, staging);
    }

    internal static void Publish(string profile, string state, string staging)
    {
        string output = Path.Combine(profile, "overlay");
        string backup = Path.Combine(state, "previous-overlay");
        GameDataMods.RequireInside(profile, state);
        GameDataMods.RequireInside(state, staging);
        GameDataMods.RequireInside(state, backup);
        if (!Directory.Exists(staging)) throw new DirectoryNotFoundException("Build produced no staging directory.");
        if (Directory.Exists(backup)) Directory.Delete(backup, true);
        if (Directory.Exists(output)) Directory.Move(output, backup);
        try { Directory.Move(staging, output); }
        catch
        {
            if (Directory.Exists(backup)) Directory.Move(backup, output);
            throw;
        }
        if (Directory.Exists(backup)) Directory.Delete(backup, true);
    }
}
