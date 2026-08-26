using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

public static class HashMaster
{
    // Configure default paths here or set via HashMaster.GameFilePath = "..."
    public static string GameFilePath { get; set; } = "hashes.game.txt";
    public static string CustomFilePath { get; set; } = "hashes.custom.txt";

    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(5);
    private static readonly SemaphoreSlim Lock = new(1, 1);
    private static readonly Timer ExpiryTimer = new(OnTimerElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

    private static Dictionary<ulong, string>? _hashToStr;
    private static HashSet<string>? _customEntriesToPersist;
    private static bool _isLoaded;

    // --- HASHING ALGORITHM ---
    public static ulong HashPath(string path, bool not_x16 = false)
    {
        string norm = path.Replace('\\', '/').ToLowerInvariant();
        byte[] data = Encoding.UTF8.GetBytes(norm);
        return XxHash64.HashToUInt64(data, seed: 0);
    }

    // --- BATCH UNHASH ---
    public static async Task<Dictionary<ulong, string?>> UnhashBatchAsync(
        IEnumerable<ulong> hashes,
        CancellationToken ct = default)
    {   
        await Lock.WaitAsync(ct);
        try
        {
            await EnsureLoadedAsync();
            ResetTimer();

            var results = new Dictionary<ulong, string?>();
            foreach (ulong h in hashes)
            {
                results[h] = _hashToStr!.TryGetValue(h, out var s) ? s : null;
            }
            return results;
        }
        finally
        {
            Lock.Release();
        }
    }

    // --- BATCH FIND SIMILAR ---
    public static async Task<Dictionary<string, List<string>>> FindSimilarBatchAsync(
        IEnumerable<string> targets,
        int maxResultsPerTarget = 50,
        CancellationToken ct = default)
    {
        await Lock.WaitAsync(ct);
        try
        {
            await EnsureLoadedAsync();
            ResetTimer();

            var results = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var targetList = new List<string>(targets);

            foreach (var t in targetList)
            {
                results[t] = new List<string>();
            }

            foreach (var str in _hashToStr!.Values)
            {
                foreach (var t in targetList)
                {
                    if (results[t].Count < maxResultsPerTarget &&
                        str.Contains(t, StringComparison.OrdinalIgnoreCase))
                    {
                        results[t].Add(str);
                    }
                }
            }

            return results;
        }
        finally
        {
            Lock.Release();
        }
    }

    // --- ADD CUSTOM HASHES (PERSISTED ON SAVE/UNLOAD) ---
    public static async Task AddCustomHashesAsync(IEnumerable<string> paths, CancellationToken ct = default)
    {
        await Lock.WaitAsync(ct);
        try
        {
            await EnsureLoadedAsync();
            ResetTimer();

            foreach (var path in paths)
            {
                ulong h = HashPath(path);
                _hashToStr![h] = path;
                _customEntriesToPersist!.Add($"{h:x16} {path}");
            }
        }
        finally
        {
            Lock.Release();
        }
    }

    // --- ADD TEMPORARY HASHES (RAM ONLY) ---
    public static async Task AddTemporaryHashesAsync(IEnumerable<string> paths, CancellationToken ct = default)
    {
        await Lock.WaitAsync(ct);
        try
        {
            await EnsureLoadedAsync();
            ResetTimer();

            foreach (var path in paths)
            {
                ulong h = HashPath(path);
                _hashToStr![h] = path;
            }
        }
        finally
        {
            Lock.Release();
        }
    }

    // --- MANUAL SAVE ---
    public static async Task SaveCustomHashesAsync(CancellationToken ct = default)
    {
        await Lock.WaitAsync(ct);
        try
        {
            if (_customEntriesToPersist == null || _customEntriesToPersist.Count == 0)
                return;

            await File.AppendAllLinesAsync(CustomFilePath, _customEntriesToPersist, ct);
            _customEntriesToPersist.Clear();
            ResetTimer();
        }
        finally
        {
            Lock.Release();
        }
    }

    // --- MEMORY LIFECYCLE & LOADING ---
    private static async Task EnsureLoadedAsync()
    {
        if (_isLoaded) return;

        _hashToStr = new Dictionary<ulong, string>(4_000_000);
        _customEntriesToPersist = new HashSet<string>();

        if (File.Exists(GameFilePath))
        {
            await LoadFileIntoDictAsync(GameFilePath);
        }

        if (File.Exists(CustomFilePath))
        {
            await LoadFileIntoDictAsync(CustomFilePath);
        }

        _isLoaded = true;
    }

    private static async Task LoadFileIntoDictAsync(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 65536, useAsync: true);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (TryParseLine(line, out ulong hash, out string value))
            {
                _hashToStr![hash] = value;
            }
        }
    }

    private static bool TryParseLine(string line, out ulong hash, out string value)
    {
        hash = 0;
        value = string.Empty;

        if (string.IsNullOrWhiteSpace(line)) return false;

        int firstSpace = line.IndexOf(' ');
        if (firstSpace <= 0 || firstSpace >= line.Length - 1) return false;

        ReadOnlySpan<char> hexSpan = line.AsSpan(0, firstSpace);
        if (ulong.TryParse(hexSpan, System.Globalization.NumberStyles.HexNumber, null, out hash))
        {
            value = line.Substring(firstSpace + 1);
            return true;
        }

        return false;
    }

    private static void ResetTimer()
    {
        ExpiryTimer.Change(IdleTimeout, Timeout.InfiniteTimeSpan);
    }

    private static void OnTimerElapsed(object? state)
    {
        if (!Lock.Wait(0)) return;

        try
        {
            if (!_isLoaded) return;

            if (_customEntriesToPersist != null && _customEntriesToPersist.Count > 0)
            {
                try
                {
                    File.AppendAllLines(CustomFilePath, _customEntriesToPersist);
                }
                catch { /* Handle/log error if necessary */ }
            }

            _hashToStr = null;
            _customEntriesToPersist = null;
            _isLoaded = false;

            GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
        }
        finally
        {
            Lock.Release();
        }
    }
}