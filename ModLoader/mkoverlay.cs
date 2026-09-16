// ============================================================================
// OverlayBuilder.cs - High-Performance League of Legends WAD Overlay Builder
// Target Framework: .NET 8.0+
// ============================================================================

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace ModLoader
{
    public sealed class WadOverlayBuilder
    {
        private const int IoBufferSize = 2 * 1024 * 1024; // 2 MB streaming buffer
        private const int HeaderSize = 272;
        private const int TocEntrySize = 32;

        public async Task BuildOverlayAsync(
            string gameDirectory,
            string outputDirectory,
            IReadOnlyList<string> modDirectories,
            CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(gameDirectory))
                throw new DirectoryNotFoundException($"Game directory not found: {gameDirectory}");

            ZstdNative.Initialize(gameDirectory);

            string overlayRoot = Path.Combine(outputDirectory, "overlay");
            Directory.CreateDirectory(overlayRoot);

            // =========================================================================
            // Phase 1: Fast TOC-Only Indexing
            // =========================================================================
            Console.WriteLine("[*] Phase 1: Indexing Game WAD TOCs...");
            var wadFiles = Directory.EnumerateFiles(gameDirectory, "*.wad.client", SearchOption.AllDirectories).ToList();
            if (wadFiles.Count == 0)
            {
                Console.WriteLine("[!] No .wad.client files found in game directory.");
                return;
            }

            var allWads = new List<WadContext>(wadFiles.Count);
            var wadsByName = new Dictionary<string, WadContext>(StringComparer.OrdinalIgnoreCase);
            var hashToWadsMap = new Dictionary<ulong, List<WadContext>>();

            foreach (var wadPath in wadFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var context = ReadWadToc(wadPath, gameDirectory);
                if (context == null) continue;

                allWads.Add(context);
                wadsByName[context.FileName] = context;

                foreach (var entry in context.TocEntries)
                {
                    if (!hashToWadsMap.TryGetValue(entry.PathHash, out var list))
                    {
                        list = new List<WadContext>();
                        hashToWadsMap[entry.PathHash] = list;
                    }
                    list.Add(context);
                }
            }

            Console.WriteLine($"[*] Indexed {allWads.Count} WAD archives containing {hashToWadsMap.Count} unique file hashes.");

            // =========================================================================
            // Phase 2: Mod Scanning & Targeted Change Verification
            // =========================================================================
            Console.WriteLine("[*] Scanning and mapping mod files...");
            foreach (var modDir in modDirectories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!Directory.Exists(modDir)) continue;

                // Detect if the mod directory itself is named after a WAD (e.g., "gragas.wad.client")
                string folderName = Path.GetFileName(modDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                WadContext? defaultTargetWad = null;
                if (folderName.EndsWith(".wad.client", StringComparison.OrdinalIgnoreCase))
                {
                    wadsByName.TryGetValue(folderName, out defaultTargetWad);
                }

                foreach (var filePath in Directory.EnumerateFiles(modDir, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string relPath = Path.GetRelativePath(modDir, filePath);
                    string normalized = NormalizePath(relPath);

                    WadContext? explicitWad = defaultTargetWad;

                    // Support subfolders inside modDir containing ".wad.client" (e.g., "DATA/Final/Champions/Gragas.wad.client/assets/...")
                    int wadIdx = normalized.IndexOf(".wad.client", StringComparison.OrdinalIgnoreCase);
                    if (wadIdx >= 0)
                    {
                        string prefix = normalized.Substring(0, wadIdx + 11);
                        string wadName = Path.GetFileName(prefix);
                        if (wadsByName.TryGetValue(wadName, out var foundWad))
                        {
                            explicitWad = foundWad;
                            // Strip everything up to and including the WAD name
                            normalized = normalized.Substring(wadIdx + 11).TrimStart('/');
                        }
                    }

                    // Hash only the internal relative path (e.g., "assets/characters/gragas/skins/base_tx_tm.tex")
                    ulong pathHash = HashMaster.HashPath(normalized.AsSpan());

                    var fileInfo = new FileInfo(filePath);
                    long modFileSize = fileInfo.Length;

                    byte[]? rawBytesCache = null;
                    byte[] GetRawBytes() => rawBytesCache ??= File.ReadAllBytes(filePath);

                    var modItem = new PendingModFile(pathHash, filePath, normalized);

                    // Resolve the single target WAD
                    WadContext? targetWad = explicitWad;
                    if (targetWad == null && hashToWadsMap.TryGetValue(pathHash, out var candidateWads))
                    {
                        targetWad = candidateWads[0];
                    }

                    if (targetWad != null)
                    {
                        // Check if file already exists in this WAD and has identical content
                        if (targetWad.ChunkByHash.TryGetValue(pathHash, out var existingEntry))
                        {
                            if (IsContentIdentical(targetWad, existingEntry, modFileSize, GetRawBytes))
                            {
                                continue; // Skip identical file
                            }
                        }

                        // Register modification/addition strictly to this one target WAD
                        targetWad.PendingModifications[pathHash] = modItem;
                    }
                    else
                    {
                        // File cannot be mapped to any known WAD
                        Console.WriteLine($"[!] Skipping unmapped loose file: {normalized}");
                    }
                }
            }

            var wadsToProcess = allWads.Where(w => w.PendingModifications.Count > 0).ToList();
            Console.WriteLine($"[*] WADs requiring rebuild/patch: {wadsToProcess.Count} of {allWads.Count}");

            if (wadsToProcess.Count == 0)
            {
                Console.WriteLine("[*] No edited or added files detected. All targets skipped.");
                return;
            }

            // =========================================================================
            // Phase 3: Dual-Mode Patching & Thread Throttling
            // =========================================================================
            Console.WriteLine("[*] Phase 3: Processing target WADs (Block Cloning / Streaming)...");
            int maxConcurrency = Math.Clamp(Environment.ProcessorCount / 2, 2, 4);
            using var throttle = new SemaphoreSlim(maxConcurrency, maxConcurrency);

            var tasks = wadsToProcess.Select(async wad =>
            {
                await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await ProcessWadArchiveAsync(wad, overlayRoot, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    throttle.Release();
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private static bool IsContentIdentical(WadContext wad, WadChunkEntry entry, long modFileSize, Func<byte[]> getRawBytes)
        {
            // 1. Uncompressed length mismatch means modified
            if (entry.UncompressedSize != (uint)modFileSize)
                return false;

            byte[] rawBytes = getRawBytes();

            // 2. Official Riot format: First 8 bytes of uncompressed SHA-256
            Span<byte> sha256 = stackalloc byte[32];
            SHA256.HashData(rawBytes, sha256);
            ulong shaChecksum = BinaryPrimitives.ReadUInt64LittleEndian(sha256.Slice(0, 8));
            if (entry.Checksum == shaChecksum)
                return true;

            // 3. Custom/Community loaders: XxHash64
            ulong xxChecksum = XxHash64.HashToUInt64(rawBytes);
            if (entry.Checksum == xxChecksum)
                return true;

            // 4. Byte-for-byte fallback comparison
            return IsSourceChunkEqual(wad.SourcePath, entry, rawBytes);
        }

        private static bool IsSourceChunkEqual(string sourceWadPath, WadChunkEntry entry, byte[] rawBytes)
        {
            try
            {
                using var fs = new FileStream(sourceWadPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (entry.DataOffset + entry.CompressedSize > fs.Length)
                    return false;

                fs.Seek(entry.DataOffset, SeekOrigin.Begin);
                byte[] chunkBuffer = new byte[entry.CompressedSize];
                fs.ReadExactly(chunkBuffer);

                if (entry.CompressionType == 0)
                {
                    return rawBytes.AsSpan().SequenceEqual(chunkBuffer);
                }

                if (entry.CompressionType == 3 && ZstdNative.IsAvailable)
                {
                    byte[] decompressed = new byte[entry.UncompressedSize];
                    if (ZstdNative.Decompress(chunkBuffer, decompressed, out int decompSize) && decompSize == rawBytes.Length)
                    {
                        return rawBytes.AsSpan().SequenceEqual(decompressed);
                    }
                }
            }
            catch
            {
                // On read failure, treat as modified to be safe
            }

            return false;
        }

        private static async Task ProcessWadArchiveAsync(WadContext wad, string overlayRoot, CancellationToken ct)
        {
            string destPath = Path.Combine(overlayRoot, wad.RelativePath);
            string? destDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(destDir))
                Directory.CreateDirectory(destDir);

            // Fast Path: CoW Block Cloning (Windows ReFS / Dev Drive)
            if (OperatingSystem.IsWindows() && Win32Extents.TryDuplicateFileExtents(wad.SourcePath, destPath))
            {
                try
                {
                    bool patchedInPlace = await TryPatchBlockClonedWadAsync(wad, destPath, ct).ConfigureAwait(false);
                    if (patchedInPlace)
                    {
                        Console.WriteLine($"[+] [Cloned]   {wad.RelativePath} (+{wad.PendingModifications.Count} mods)");
                        return;
                    }
                }
                catch
                {
                    // Fallback to streaming
                }

                try { File.Delete(destPath); } catch { }
            }

            // Fallback Path: Zero-Decompression Streaming
            await StreamPatchWadAsync(wad, destPath, ct).ConfigureAwait(false);
            Console.WriteLine($"[+] [Streamed] {wad.RelativePath} (+{wad.PendingModifications.Count} mods)");
        }

        private static async Task<bool> TryPatchBlockClonedWadAsync(WadContext wad, string destPath, CancellationToken ct)
        {
            int additionsCount = wad.PendingModifications.Count(m => !wad.ChunkByHash.ContainsKey(m.Key));
            int newTotalCount = wad.TocEntries.Count + additionsCount;
            long neededTocBytes = HeaderSize + ((long)newTotalCount * TocEntrySize);

            long minPayloadOffset = wad.TocEntries.Count > 0
                ? wad.TocEntries.Where(c => c.CompressedSize > 0).Select(c => (long)c.DataOffset).DefaultIfEmpty(wad.SourceFileSize).Min()
                : wad.SourceFileSize;

            if (neededTocBytes > minPayloadOffset)
                return false; // Header/TOC expansion would overwrite existing payload data

            await using var stream = new FileStream(
                destPath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None,
                IoBufferSize,
                FileOptions.Asynchronous);

            stream.Seek(0, SeekOrigin.End);

            var activeEntries = new List<WadChunkEntry>(wad.TocEntries.Count + additionsCount);
            foreach (var e in wad.TocEntries) activeEntries.Add(e.Clone());
            var activeMap = activeEntries.ToDictionary(e => e.PathHash, e => e);

            foreach (var mod in wad.PendingModifications.Values)
            {
                ct.ThrowIfCancellationRequested();
                byte[] rawBytes = await File.ReadAllBytesAsync(mod.DiskPath, ct).ConfigureAwait(false);
                PreparePayload(rawBytes, ShouldStoreRaw(mod.NormalizedPath), out byte compType, out byte[] payload);

                uint writeOffset = (uint)stream.Position;
                await stream.WriteAsync(payload.AsMemory(), ct).ConfigureAwait(false);

                Span<byte> sha256 = stackalloc byte[32];
                SHA256.HashData(rawBytes, sha256);
                ulong chunkChecksum = BinaryPrimitives.ReadUInt64LittleEndian(sha256.Slice(0, 8));

                if (activeMap.TryGetValue(mod.Hash, out var existing))
                {
                    existing.DataOffset = writeOffset;
                    existing.CompressedSize = (uint)payload.Length;
                    existing.UncompressedSize = (uint)rawBytes.Length;
                    existing.CompressionType = compType;
                    existing.Checksum = chunkChecksum;
                }
                else
                {
                    var newEntry = new WadChunkEntry
                    {
                        PathHash = mod.Hash,
                        DataOffset = writeOffset,
                        CompressedSize = (uint)payload.Length,
                        UncompressedSize = (uint)rawBytes.Length,
                        CompressionType = compType,
                        Checksum = chunkChecksum
                    };
                    activeEntries.Add(newEntry);
                    activeMap[mod.Hash] = newEntry;
                }
            }

            // Rewrite TOC
            byte[] tocBuffer = new byte[activeEntries.Count * TocEntrySize];
            for (int i = 0; i < activeEntries.Count; i++)
            {
                activeEntries[i].Write(tocBuffer.AsSpan(i * TocEntrySize, TocEntrySize), wad.Header.VersionMinor);
            }

            stream.Seek(HeaderSize, SeekOrigin.Begin);
            await stream.WriteAsync(tocBuffer.AsMemory(), ct).ConfigureAwait(false);

            // Update Header
            ulong tocChecksum = XxHash64.HashToUInt64(tocBuffer);
            byte[] headerPatch = new byte[12];
            BinaryPrimitives.WriteUInt64LittleEndian(headerPatch.AsSpan(0, 8), tocChecksum);
            BinaryPrimitives.WriteUInt32LittleEndian(headerPatch.AsSpan(8, 4), (uint)activeEntries.Count);

            stream.Seek(260, SeekOrigin.Begin);
            await stream.WriteAsync(headerPatch.AsMemory(), ct).ConfigureAwait(false);

            await stream.FlushAsync(ct).ConfigureAwait(false);
            return true;
        }

        private static async Task StreamPatchWadAsync(WadContext wad, string destPath, CancellationToken ct)
        {
            await using var srcStream = new FileStream(
                wad.SourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                IoBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            await using var dstStream = new FileStream(
                destPath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                IoBufferSize,
                FileOptions.Asynchronous);

            var untouched = wad.TocEntries.Where(e => !wad.PendingModifications.ContainsKey(e.PathHash)).Select(e => e.Clone()).ToList();
            int totalEntries = untouched.Count + wad.PendingModifications.Count;
            long payloadStart = HeaderSize + ((long)totalEntries * TocEntrySize);

            dstStream.Seek(payloadStart, SeekOrigin.Begin);
            dstStream.SetLength(payloadStart);

            byte[] copyBuffer = ArrayPool<byte>.Shared.Rent(IoBufferSize);
            var finalEntries = new List<WadChunkEntry>(totalEntries);

            try
            {
                // 1. Copy untouched chunks directly
                foreach (var chunk in untouched)
                {
                    ct.ThrowIfCancellationRequested();
                    if (chunk.CompressedSize > 0)
                    {
                        long targetOffset = dstStream.Position;
                        srcStream.Seek(chunk.DataOffset, SeekOrigin.Begin);
                        await CopySliceAsync(srcStream, dstStream, chunk.CompressedSize, copyBuffer, ct).ConfigureAwait(false);
                        chunk.DataOffset = (uint)targetOffset;
                    }
                    else
                    {
                        chunk.DataOffset = 0;
                    }
                    finalEntries.Add(chunk);
                }

                // 2. Append mod chunks
                foreach (var mod in wad.PendingModifications.Values)
                {
                    ct.ThrowIfCancellationRequested();
                    byte[] rawBytes = await File.ReadAllBytesAsync(mod.DiskPath, ct).ConfigureAwait(false);
                    PreparePayload(rawBytes, ShouldStoreRaw(mod.NormalizedPath), out byte compType, out byte[] payload);

                    long targetOffset = dstStream.Position;
                    await dstStream.WriteAsync(payload.AsMemory(), ct).ConfigureAwait(false);

                    Span<byte> sha256 = stackalloc byte[32];
                    SHA256.HashData(rawBytes, sha256);
                    ulong chunkChecksum = BinaryPrimitives.ReadUInt64LittleEndian(sha256.Slice(0, 8));

                    finalEntries.Add(new WadChunkEntry
                    {
                        PathHash = mod.Hash,
                        DataOffset = (uint)targetOffset,
                        CompressedSize = (uint)payload.Length,
                        UncompressedSize = (uint)rawBytes.Length,
                        CompressionType = compType,
                        Checksum = chunkChecksum
                    });
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(copyBuffer);
            }

            // 3. Write TOC
            byte[] tocBuffer = new byte[finalEntries.Count * TocEntrySize];
            for (int i = 0; i < finalEntries.Count; i++)
            {
                finalEntries[i].Write(tocBuffer.AsSpan(i * TocEntrySize, TocEntrySize), wad.Header.VersionMinor);
            }

            dstStream.Seek(HeaderSize, SeekOrigin.Begin);
            await dstStream.WriteAsync(tocBuffer.AsMemory(), ct).ConfigureAwait(false);

            // 4. Write Header
            Span<byte> header = stackalloc byte[HeaderSize];
            header[0] = 0x52; // 'R'
            header[1] = 0x57; // 'W'
            header[2] = 3;
            header[3] = wad.Header.VersionMinor;
            wad.Header.EcdsaSignature.CopyTo(header.Slice(4, 256));

            ulong tocChecksum = XxHash64.HashToUInt64(tocBuffer);
            BinaryPrimitives.WriteUInt64LittleEndian(header.Slice(260, 8), tocChecksum);
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(268, 4), (uint)finalEntries.Count);

            dstStream.Seek(0, SeekOrigin.Begin);
            await dstStream.WriteAsync(header.ToArray(), ct).ConfigureAwait(false);
            await dstStream.FlushAsync(ct).ConfigureAwait(false);
        }

        private static async Task CopySliceAsync(Stream src, Stream dst, uint bytesToCopy, byte[] buffer, CancellationToken ct)
        {
            uint remaining = bytesToCopy;
            while (remaining > 0)
            {
                int readSize = (int)Math.Min(remaining, (uint)buffer.Length);
                int bytesRead = await src.ReadAsync(buffer.AsMemory(0, readSize), ct).ConfigureAwait(false);
                if (bytesRead == 0)
                    throw new EndOfStreamException("Premature EOF encountered while streaming WAD chunk payload.");

                await dst.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
                remaining -= (uint)bytesRead;
            }
        }

        private static WadContext? ReadWadToc(string fullPath, string rootGameDir)
        {
            try
            {
                using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024);
                if (stream.Length < HeaderSize) return null;

                Span<byte> headerBytes = stackalloc byte[HeaderSize];
                stream.ReadExactly(headerBytes);

                if (headerBytes[0] != 0x52 || headerBytes[1] != 0x57) return null; // "RW"
                byte major = headerBytes[2];
                byte minor = headerBytes[3];
                if (major != 3) return null;

                byte[] signature = headerBytes.Slice(4, 256).ToArray();
                ulong dataChecksum = BinaryPrimitives.ReadUInt64LittleEndian(headerBytes.Slice(260, 8));
                uint entryCount = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes.Slice(268, 4));

                long tocSize = (long)entryCount * TocEntrySize;
                if (stream.Position + tocSize > stream.Length) return null;

                byte[] rawToc = new byte[tocSize];
                stream.ReadExactly(rawToc);

                var entries = new List<WadChunkEntry>((int)entryCount);
                var entryMap = new Dictionary<ulong, WadChunkEntry>((int)entryCount);

                for (int i = 0; i < entryCount; i++)
                {
                    int offset = i * TocEntrySize;
                    var slice = rawToc.AsSpan(offset, TocEntrySize);
                    var entry = WadChunkEntry.Read(slice, minor);

                    entries.Add(entry);
                    entryMap[entry.PathHash] = entry;
                }

                var header = new WadHeader(major, minor, signature, dataChecksum, entryCount);
                return new WadContext
                {
                    SourcePath = fullPath,
                    RelativePath = Path.GetRelativePath(rootGameDir, fullPath),
                    FileName = Path.GetFileName(fullPath),
                    SourceFileSize = stream.Length,
                    Header = header,
                    TocEntries = entries,
                    ChunkByHash = entryMap
                };
            }
            catch
            {
                return null;
            }
        }

        private static void PreparePayload(byte[] rawBytes, bool isRawOnly, out byte compType, out byte[] payload)
        {
            if (isRawOnly)
            {
                compType = 0;
                payload = rawBytes;
                return;
            }

            if (ZstdNative.Compress(rawBytes, 3, out var compressed))
            {
                compType = 3;
                payload = compressed;
            }
            else
            {
                compType = 0;
                payload = rawBytes;
            }
        }

        private static bool ShouldStoreRaw(string normalizedPath) =>
            normalizedPath.EndsWith(".bnk", StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.EndsWith(".wpk", StringComparison.OrdinalIgnoreCase);

        public static string NormalizePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return string.Empty;
            return relativePath.Trim().Replace('\\', '/').Trim('/').ToLowerInvariant();
        }
    }

    public sealed record WadHeader(byte VersionMajor, byte VersionMinor, byte[] EcdsaSignature, ulong DataChecksum, uint TocEntryCount);

    public sealed class WadChunkEntry
    {
        public ulong PathHash { get; set; }
        public uint DataOffset { get; set; }
        public uint CompressedSize { get; set; }
        public uint UncompressedSize { get; set; }
        public byte CompressionType { get; set; }
        public byte IsDuplicated { get; set; }
        public ushort SubchunkStart { get; set; }
        public byte SubchunkCount { get; set; }
        public ulong Checksum { get; set; }

        public WadChunkEntry Clone() => new()
        {
            PathHash = PathHash,
            DataOffset = DataOffset,
            CompressedSize = CompressedSize,
            UncompressedSize = UncompressedSize,
            CompressionType = CompressionType,
            IsDuplicated = IsDuplicated,
            SubchunkStart = SubchunkStart,
            SubchunkCount = SubchunkCount,
            Checksum = Checksum
        };

        public static WadChunkEntry Read(ReadOnlySpan<byte> span, byte minor)
        {
            var entry = new WadChunkEntry
            {
                PathHash = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(0, 8)),
                DataOffset = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(8, 4)),
                CompressedSize = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(12, 4)),
                UncompressedSize = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(16, 4)),
                Checksum = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(24, 8))
            };

            if (minor >= 4)
            {
                uint packed = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(20, 4));
                entry.CompressionType = (byte)(packed & 0x0F);
                entry.SubchunkCount = (byte)((packed >> 4) & 0xFF);
                entry.SubchunkStart = (ushort)((packed >> 12) & 0xFFFF);
            }
            else
            {
                entry.CompressionType = span[20];
                entry.IsDuplicated = span[21];
                entry.SubchunkStart = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(22, 2));
            }

            return entry;
        }

        public void Write(Span<byte> span, byte minor)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(0, 8), PathHash);
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(8, 4), DataOffset);
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(12, 4), CompressedSize);
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(16, 4), UncompressedSize);

            if (minor >= 4)
            {
                uint packed = ((uint)CompressionType & 0x0F)
                            | (((uint)SubchunkCount & 0xFF) << 4)
                            | (((uint)SubchunkStart & 0xFFFF) << 12);
                BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(20, 4), packed);
            }
            else
            {
                span[20] = CompressionType;
                span[21] = IsDuplicated;
                BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(22, 2), SubchunkStart);
            }

            BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(24, 8), Checksum);
        }
    }

    public sealed class WadContext
    {
        public required string SourcePath { get; init; }
        public required string RelativePath { get; init; }
        public required string FileName { get; init; }
        public required long SourceFileSize { get; init; }
        public required WadHeader Header { get; set; }
        public required List<WadChunkEntry> TocEntries { get; init; }
        public required Dictionary<ulong, WadChunkEntry> ChunkByHash { get; init; }
        public ConcurrentDictionary<ulong, PendingModFile> PendingModifications { get; } = new();
    }

    public readonly record struct PendingModFile(ulong Hash, string DiskPath, string NormalizedPath);

    internal static class Win32Extents
    {
        private const uint FSCTL_DUPLICATE_EXTENTS_TO_FILE = 0x00098344;

        [StructLayout(LayoutKind.Sequential)]
        private struct DUPLICATE_EXTENTS_DATA
        {
            public IntPtr FileHandle;
            public long SourceFileOffset;
            public long TargetFileOffset;
            public long ByteCount;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeviceIoControl(
            SafeFileHandle hDevice,
            uint dwIoControlCode,
            ref DUPLICATE_EXTENTS_DATA lpInBuffer,
            uint nInBufferSize,
            IntPtr lpOutBuffer,
            uint nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped);

        public static bool TryDuplicateFileExtents(string sourceFile, string destFile)
        {
            try
            {
                using var srcStream = new FileStream(
                    sourceFile,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    4096);

                long sourceLength = srcStream.Length;
                if (sourceLength == 0)
                {
                    using (File.Create(destFile)) { }
                    return true;
                }

                using var dstStream = new FileStream(
                    destFile,
                    FileMode.Create,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    4096);

                const long clusterAlignment = 65536;
                long alignedLength = ((sourceLength + clusterAlignment - 1) / clusterAlignment) * clusterAlignment;
                dstStream.SetLength(alignedLength);

                var extents = new DUPLICATE_EXTENTS_DATA
                {
                    FileHandle = srcStream.SafeFileHandle.DangerousGetHandle(),
                    SourceFileOffset = 0,
                    TargetFileOffset = 0,
                    ByteCount = alignedLength
                };

                bool success = DeviceIoControl(
                    dstStream.SafeFileHandle,
                    FSCTL_DUPLICATE_EXTENTS_TO_FILE,
                    ref extents,
                    (uint)Marshal.SizeOf<DUPLICATE_EXTENTS_DATA>(),
                    IntPtr.Zero,
                    0,
                    out _,
                    IntPtr.Zero);

                if (success)
                {
                    dstStream.SetLength(sourceLength);
                    dstStream.Flush();
                    return true;
                }

                dstStream.Dispose();
                try { File.Delete(destFile); } catch { }
                return false;
            }
            catch
            {
                try { if (File.Exists(destFile)) File.Delete(destFile); } catch { }
                return false;
            }
        }
    }

    internal static class ZstdNative
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate nuint ZstdCompressDelegate(
            ref byte dst,
            nuint dstCapacity,
            in byte src,
            nuint srcSize,
            int compressionLevel);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate nuint ZstdDecompressDelegate(
            ref byte dst,
            nuint dstCapacity,
            in byte src,
            nuint srcSize);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate nuint ZstdCompressBoundDelegate(nuint srcSize);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate uint ZstdIsErrorDelegate(nuint code);

        private static bool _initialized;
        private static bool _isAvailable;
        public static bool IsAvailable => _isAvailable;

        private static ZstdCompressDelegate? _compress;
        private static ZstdDecompressDelegate? _decompress;
        private static ZstdCompressBoundDelegate? _compressBound;
        private static ZstdIsErrorDelegate? _isError;
        private static readonly object InitLock = new();

        public static void Initialize(string gameDirectory)
        {
            if (_initialized) return;

            lock (InitLock)
            {
                if (_initialized) return;
                _initialized = true;

                var searchPaths = new List<string>
                {
                    Path.Combine(gameDirectory, "zstd.dll"),
                    Path.Combine(gameDirectory, "libzstd.dll"),
                    Path.Combine(AppContext.BaseDirectory, "zstd.dll"),
                    Path.Combine(AppContext.BaseDirectory, "libzstd.dll"),
                    "zstd.dll",
                    "libzstd.dll"
                };

                IntPtr handle = IntPtr.Zero;
                foreach (var path in searchPaths)
                {
                    try
                    {
                        if (File.Exists(path) && NativeLibrary.TryLoad(path, out handle))
                            break;
                        if (NativeLibrary.TryLoad(path, out handle))
                            break;
                    }
                    catch { }
                }

                if (handle != IntPtr.Zero)
                {
                    try
                    {
                        if (NativeLibrary.TryGetExport(handle, "ZSTD_compress", out var compPtr) &&
                            NativeLibrary.TryGetExport(handle, "ZSTD_decompress", out var decompPtr) &&
                            NativeLibrary.TryGetExport(handle, "ZSTD_compressBound", out var boundPtr) &&
                            NativeLibrary.TryGetExport(handle, "ZSTD_isError", out var errPtr))
                        {
                            _compress = Marshal.GetDelegateForFunctionPointer<ZstdCompressDelegate>(compPtr);
                            _decompress = Marshal.GetDelegateForFunctionPointer<ZstdDecompressDelegate>(decompPtr);
                            _compressBound = Marshal.GetDelegateForFunctionPointer<ZstdCompressBoundDelegate>(boundPtr);
                            _isError = Marshal.GetDelegateForFunctionPointer<ZstdIsErrorDelegate>(errPtr);
                            _isAvailable = true;
                        }
                    }
                    catch { }
                }

                if (!_isAvailable)
                {
                    Console.WriteLine("[!] Warning: zstd.dll was not found. Mod chunks will fallback to uncompressed bytes (type 0).");
                }
            }
        }

        public static bool Compress(ReadOnlySpan<byte> src, int level, out byte[] compressedData)
        {
            if (!_isAvailable || _compress == null || _compressBound == null || _isError == null || src.Length == 0)
            {
                compressedData = Array.Empty<byte>();
                return false;
            }

            nuint bound = _compressBound((nuint)src.Length);
            byte[] dst = new byte[(int)bound];

            nuint res = _compress(
                ref MemoryMarshal.GetArrayDataReference(dst),
                bound,
                in MemoryMarshal.GetReference(src),
                (nuint)src.Length,
                level);

            if (_isError(res) != 0)
            {
                compressedData = Array.Empty<byte>();
                return false;
            }

            compressedData = new byte[(int)res];
            Buffer.BlockCopy(dst, 0, compressedData, 0, (int)res);
            return true;
        }

        public static bool Decompress(ReadOnlySpan<byte> src, Span<byte> dst, out int decompressedSize)
        {
            if (!_isAvailable || _decompress == null || _isError == null || src.Length == 0)
            {
                decompressedSize = 0;
                return false;
            }

            nuint res = _decompress(
                ref MemoryMarshal.GetReference(dst),
                (nuint)dst.Length,
                in MemoryMarshal.GetReference(src),
                (nuint)src.Length);

            if (_isError(res) != 0)
            {
                decompressedSize = 0;
                return false;
            }

            decompressedSize = (int)res;
            return true;
        }
    }

    internal static class HashMaster
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong HashPath(ReadOnlySpan<char> path)
        {
            Span<byte> utf8Bytes = stackalloc byte[path.Length * 3];
            int written = System.Text.Encoding.UTF8.GetBytes(path, utf8Bytes);
            return XxHash64.HashToUInt64(utf8Bytes.Slice(0, written));
        }
    }
}