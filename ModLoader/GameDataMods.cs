#nullable enable
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace ModPkgLibSpace;

/// <summary>Persists authoring inputs separately from the WADs consumed by mod-tools.</summary>
internal static class GameDataMods
{
    private const string Sidecar = "game-data.json";
    private static readonly string[] Manifests = ["game_data.yaml", "game_data.yml", "game_data.json", "game_data.toml"];

    internal static string LayerFolder(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name is "." or "..")
            throw new InvalidDataException($"Invalid mod layer name: {name}");
        return name.Equals("base", StringComparison.OrdinalIgnoreCase) ? "WAD" : $"WAD_{name}";
    }

    internal static void RequireInside(string root, string path)
    {
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Package path leaves the mod directory.");
    }

    internal static string ResourcePath(string mod, string layer, string path)
    {
        var root = Path.Combine(mod, "META", "game-data", LayerFolder(layer));
        var result = Path.Combine(root, path);
        RequireInside(root, result);
        return result;
    }

    internal static void SaveDeclarations(string mod, List<ModpkgLayerMetadata> layers)
    {
        foreach (var layer in layers) LayerFolder(layer.Name);
        var file = Path.Combine(mod, "META", Sidecar);
        var resources = Path.Combine(mod, "META", "game-data");
        RequireInside(mod, resources);
        if (Directory.Exists(resources)) Directory.Delete(resources, recursive: true);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, JsonSerializer.Serialize(layers));
    }

    internal static List<ModpkgLayerMetadata> ReadLayers(string mod)
    {
        var file = Path.Combine(mod, "META", Sidecar);
        return File.Exists(file)
            ? JsonSerializer.Deserialize<List<ModpkgLayerMetadata>>(File.ReadAllText(file))
                ?? throw new InvalidDataException("Invalid game-data layer table.")
            : [];
    }

    internal static byte[]? ReadDeclaration(string mod, string layer) =>
        ReadLayers(mod).FirstOrDefault(l => l.Name.Equals(layer, StringComparison.OrdinalIgnoreCase))?.GameData;

    internal static IEnumerable<(string Path, string File)> Resources(string mod, string layer)
    {
        var root = Path.Combine(mod, "META", "game-data", LayerFolder(layer));
        if (!Directory.Exists(root)) yield break;
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            yield return (System.IO.Path.GetRelativePath(root, file).Replace('\\', '/'), file);
    }

    internal static bool HasDeclarations(string mod)
    {
        var declarations = ReadLayers(mod);
        if (declarations.Count == 0) return false;
        HashSet<string>? active = null;
        string details = Path.Combine(mod, "META", "details.json");
        if (File.Exists(details))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(details));
            active = new(StringComparer.OrdinalIgnoreCase) { "base" };
            if (doc.RootElement.TryGetProperty("Layers", out var layers))
                foreach (var layer in layers.EnumerateArray())
                    if (layer.TryGetProperty("is_active", out var enabled) && enabled.ValueKind == JsonValueKind.True)
                        active.Add(layer.GetProperty("Name").GetString()!);
        }
        return declarations.Any(l => (active == null || active.Contains(l.Name)) &&
            (l.GameData != null || Manifests.Any(m => File.Exists(ResourcePath(mod, l.Name, m)))));
    }

    internal static async Task BuildAsync(string game, string profile, IEnumerable<string> mods,
        bool noTft, Action<string> progress, CancellationToken token)
    {
        string helper = Path.Combine(AppContext.BaseDirectory, "cslol-tools", "game-data-overlay.exe");
        if (!File.Exists(helper))
            throw new FileNotFoundException("This mod contains BIN deltas. Install a build that includes game-data-overlay.exe.", helper);

        // Separate state from mod-tools: never reuse an overlay built by the other engine.
        string state = Path.Combine(profile, "game-data");
        Directory.CreateDirectory(state);
        string request = Path.Combine(state, "request.json");
        await File.WriteAllTextAsync(request, JsonSerializer.Serialize(new
        {
            game = Path.GetFullPath(game),
            output = Path.GetFullPath(Path.Combine(profile, "overlay")),
            state = Path.GetFullPath(state),
            mods = mods.Select(Path.GetFullPath).ToArray(),
            no_tft = noTft
        }), token);
        var start = new ProcessStartInfo(helper)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(request);
        using var process = Process.Start(start) ?? throw new IOException("Could not start the BIN delta builder.");
        using var cancellation = token.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        });
        var errors = process.StandardError.ReadToEndAsync();
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
        if (process.ExitCode != 0) throw new InvalidOperationException($"BIN delta build failed:\n{error}");
    }
}
