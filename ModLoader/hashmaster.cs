using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public static class HashMaster
{
    public static string GameFilePath { get; set; } = "cslol-tools/hashes.game.bin";
    public static string CustomFilePath { get; set; } = "cslol-tools/hashes.custom.bin";

    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(5);
    private static readonly SemaphoreSlim Lock = new(1, 1);
    private static readonly Timer ExpiryTimer = new(OnTimerElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

    // Memory-mapped handles
    private static MemoryMappedFile? _mmf;
    private static MemoryMappedViewStream? _viewStream;
    private static HashIndexRecord[]? _index;
    private static byte[]? _stringArena;

    // In-memory custom entries
    private static Dictionary<ulong, string>? _customEntries;
    private static List<(ulong Hash, string Path)>? _customEntriesToPersist;
    private static bool _isLoaded;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct HashIndexRecord
    {
        public readonly ulong Hash;          // 8 bytes
        public readonly int StringOffset;    // 4 bytes
        public readonly ushort StringLength; // 2 bytes
        private readonly ushort _padding;    // 2 bytes (Enforces exact 16-byte alignment)

        public HashIndexRecord(ulong hash, int stringOffset, ushort stringLength)
        {
            Hash = hash;
            StringOffset = stringOffset;
            StringLength = stringLength;
            _padding = 0;
        }
    }

    // --- ZERO-ALLOCATION HASHING ---

    // --- ASYNC ENTRY POINT (No spans or ref structs here) ---
    public static async Task<List<string>> FilterNonGamePathsAsync(
        IEnumerable<string> paths,
        CancellationToken ct = default)
    {
        await Lock.WaitAsync(ct);
        try
        {
            EnsureLoaded();
            ResetTimer();

            return FilterNonGamePathsInternal(paths);
        }
        finally
        {
            Lock.Release();
        }
    }

    // --- SYNCHRONOUS WORKER (Spans and ref structs are fully safe here) ---
    private static List<string> FilterNonGamePathsInternal(IEnumerable<string> paths)
    {
        var nonGamePaths = new List<string>();

        if (_index == null || _index.Length == 0)
        {
            nonGamePaths.AddRange(paths);
            return nonGamePaths;
        }

        ReadOnlySpan<HashIndexRecord> index = _index.AsSpan();

        foreach (var path in paths)
        {
            ulong hash = HashPath(path);

            if (!ContainsGameHash(hash, index))
            {
                nonGamePaths.Add(path);
            }
        }

        return nonGamePaths;
    }

    // Fast binary search
    private static bool ContainsGameHash(ulong targetHash, ReadOnlySpan<HashIndexRecord> index)
    {
        int low = 0;
        int high = index.Length - 1;

        while (low <= high)
        {
            int mid = low + ((high - low) >> 1);
            ref readonly var record = ref index[mid];

            if (record.Hash < targetHash)
            {
                low = mid + 1;
            }
            else if (record.Hash > targetHash)
            {
                high = mid - 1;
            }
            else
            {
                return true;
            }
        }

        return false;
    }
    public static ulong HashPath(ReadOnlySpan<char> path)
    {
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(path.Length);
        byte[]? rented = null;
        Span<byte> utf8Buf = maxByteCount <= 512 ? stackalloc byte[512] : (rented = ArrayPool<byte>.Shared.Rent(maxByteCount));

        try
        {
            int byteCount = 0;
            for (int i = 0; i < path.Length; i++)
            {
                char c = path[i];
                if (c == '\\') c = '/';
                else if (c is >= 'A' and <= 'Z') c = (char)(c + 32);

                if (c <= 0x7F)
                {
                    utf8Buf[byteCount++] = (byte)c;
                }
                else
                {
                    Span<char> singleChar = stackalloc char[] { c };
                    byteCount += Encoding.UTF8.GetBytes(singleChar, utf8Buf.Slice(byteCount));
                }
            }

            return XxHash64.HashToUInt64(utf8Buf.Slice(0, byteCount), seed: 0);
        }
        finally
        {
            if (rented != null) ArrayPool<byte>.Shared.Return(rented);
        }
    }

    public static ulong HashPath(string path, bool not_x16 = false) => HashPath(path.AsSpan());

    // --- BATCH UNHASH (ASYNC WRAPPER) ---
    public static async Task<Dictionary<ulong, string?>> UnhashBatchAsync(IEnumerable<ulong> hashes, CancellationToken ct = default)
    {
        await Lock.WaitAsync(ct);
        try
        {
            EnsureLoaded();
            ResetTimer();
            return UnhashBatchInternal(hashes);
        }
        finally
        {
            Lock.Release();
        }
    }

    private static Dictionary<ulong, string?> UnhashBatchInternal(IEnumerable<ulong> hashes)
    {
        var results = new Dictionary<ulong, string?>();

        foreach (ulong h in hashes)
        {
            if (_customEntries != null && _customEntries.TryGetValue(h, out var customVal))
            {
                results[h] = customVal;
                continue;
            }

            results[h] = LookupGameHash(h);
        }

        return results;
    }

    // Binary search over the loaded index array
    private static string? LookupGameHash(ulong targetHash)
    {
        if (_index == null || _stringArena == null || _index.Length == 0) return null;

        ReadOnlySpan<HashIndexRecord> index = _index.AsSpan();
        int low = 0;
        int high = index.Length - 1;

        while (low <= high)
        {
            int mid = low + ((high - low) >> 1);
            ref readonly var record = ref index[mid];

            if (record.Hash < targetHash)
            {
                low = mid + 1;
            }
            else if (record.Hash > targetHash)
            {
                high = mid - 1;
            }
            else
            {
                ReadOnlySpan<byte> strBytes = _stringArena.AsSpan(record.StringOffset, record.StringLength);
                return Encoding.UTF8.GetString(strBytes);
            }
        }

        return null;
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
            EnsureLoaded();
            ResetTimer();
            return FindSimilarBatchInternal(targets, maxResultsPerTarget);
        }
        finally
        {
            Lock.Release();
        }
    }

    private static Dictionary<string, List<string>> FindSimilarBatchInternal(IEnumerable<string> targets, int maxResultsPerTarget)
    {
        var results = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var targetList = new List<string>(targets);

        foreach (var t in targetList)
            results[t] = new List<string>();

        if (_index != null && _stringArena != null)
        {
            ReadOnlySpan<HashIndexRecord> index = _index.AsSpan();
            for (int i = 0; i < index.Length; i++)
            {
                ref readonly var record = ref index[i];
                ReadOnlySpan<byte> strBytes = _stringArena.AsSpan(record.StringOffset, record.StringLength);
                string str = Encoding.UTF8.GetString(strBytes);

                for (int j = 0; j < targetList.Count; j++)
                {
                    var t = targetList[j];
                    var list = results[t];
                    if (list.Count < maxResultsPerTarget && str.Contains(t, StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(str);
                    }
                }
            }
        }

        if (_customEntries != null)
        {
            foreach (var str in _customEntries.Values)
            {
                for (int j = 0; j < targetList.Count; j++)
                {
                    var t = targetList[j];
                    var list = results[t];
                    if (list.Count < maxResultsPerTarget && str.Contains(t, StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(str);
                    }
                }
            }
        }

        return results;
    }

    // --- ADD CUSTOM HASHES ---
    public static async Task AddCustomHashesAsync(IEnumerable<string> paths, CancellationToken ct = default)
    {
        await Lock.WaitAsync(ct);
        try
        {
            EnsureLoaded();
            ResetTimer();

            foreach (var path in paths)
            {
                ulong h = HashPath(path);
                _customEntries![h] = path;
                _customEntriesToPersist!.Add((h, path));
            }
        }
        finally
        {
            Lock.Release();
        }
    }

    public static async Task AddTemporaryHashesAsync(IEnumerable<string> paths, CancellationToken ct = default)
    {
        await Lock.WaitAsync(ct);
        try
        {
            EnsureLoaded();
            ResetTimer();

            foreach (var path in paths)
            {
                _customEntries![HashPath(path)] = path;
            }
        }
        finally
        {
            Lock.Release();
        }
    }

    public static async Task SaveCustomHashesAsync(CancellationToken ct = default)
    {
        await Lock.WaitAsync(ct);
        try
        {
            PersistCustomEntriesInternal();
            ResetTimer();
        }
        finally
        {
            Lock.Release();
        }
    }

    // --- SAFE INSTANT LOAD (STREAM-BASED BLOCK READ) ---
    private static void EnsureLoaded()
    {
        if (_isLoaded) return;

        _customEntries = new Dictionary<ulong, string>();
        _customEntriesToPersist = new List<(ulong, string)>();

        if (File.Exists(GameFilePath))
        {
            _mmf = MemoryMappedFile.CreateFromFile(GameFilePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            _viewStream = _mmf.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);

            // 1. Read entry count (4 bytes)
            Span<byte> countBytes = stackalloc byte[sizeof(int)];
            _viewStream.ReadExactly(countBytes);
            int count = MemoryMarshal.Read<int>(countBytes);

            // 2. Read index table in one bulk block (16 bytes per item)
            _index = new HashIndexRecord[count];
            Span<byte> indexRawBytes = MemoryMarshal.AsBytes(_index.AsSpan());
            _viewStream.ReadExactly(indexRawBytes);

            // 3. Read string arena in one block
            long remainingBytes = _viewStream.Length - _viewStream.Position;
            _stringArena = new byte[remainingBytes];
            _viewStream.ReadExactly(_stringArena);
        }

        if (File.Exists(CustomFilePath) && new FileInfo(CustomFilePath).Length > 0)
        {
            using var fs = new FileStream(CustomFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs, Encoding.UTF8);
            while (fs.Position < fs.Length)
            {
                ulong hash = br.ReadUInt64();
                string val = br.ReadString();
                _customEntries[hash] = val;
            }
        }

        _isLoaded = true;
    }

    private static void PersistCustomEntriesInternal()
    {
        if (_customEntriesToPersist == null || _customEntriesToPersist.Count == 0) return;

        using (var fs = new FileStream(CustomFilePath, FileMode.Append, FileAccess.Write, FileShare.None))
        using (var bw = new BinaryWriter(fs, Encoding.UTF8))
        {
            foreach (var (hash, path) in _customEntriesToPersist)
            {
                bw.Write(hash);
                bw.Write(path);
            }
        }

        _customEntriesToPersist.Clear();
    }

    private static void ResetTimer() => ExpiryTimer.Change(IdleTimeout, Timeout.InfiniteTimeSpan);

    private static void OnTimerElapsed(object? state)
    {
        if (!Lock.Wait(0)) return;

        try
        {
            if (!_isLoaded) return;

            PersistCustomEntriesInternal();

            _viewStream?.Dispose();
            _viewStream = null;

            _mmf?.Dispose();
            _mmf = null;

            _index = null;
            _stringArena = null;
            _customEntries = null;
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