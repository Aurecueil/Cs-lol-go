using System.IO;

namespace ModLoader
{
    public static class OverlayTool
    {
        public static void MkOverlay(
            string srcDir,
            string dstDir,
            string gameDir,
            HashSet<string> modNames,
            Action<string> onStatusUpdate = null,
            Action<double> onProgressUpdate = null)
        {
            void Report(string status, double progress)
            {
                onStatusUpdate?.Invoke(status);
                onProgressUpdate?.Invoke(progress);
            }

            Report("Initializing...", 0.0);

            // Dictionary to map WAD Names -> Archive Objects
            var activeWads = new Dictionary<string, WadArchive>(StringComparer.OrdinalIgnoreCase);

            // Global Map: FileHash -> List of WADs that contain this file.
            // Used to propagate mod updates to ALL wads containing the same file.
            var globalHashMap = new Dictionary<ulong, List<WadArchive>>();

            // Keep track of valid output paths for cleanup
            var validRelativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var gameFiles = Directory.GetFiles(gameDir, "*.wad.client", SearchOption.AllDirectories);
            int totalGameFiles = gameFiles.Length;

            // 1. FAST INDEXING (Game WADs)
            // We only read headers to map out where files live.
            for (int i = 0; i < totalGameFiles; i++)
            {
                var file = gameFiles[i];
                var wadName = Path.GetFileName(file);
                string relativePath = GetRelativePath(gameDir, file);

                if (!activeWads.TryGetValue(wadName, out var archive))
                {
                    archive = new WadArchive(wadName, relativePath, file);
                    activeWads[wadName] = archive;
                    validRelativePaths.Add(relativePath);
                }

                // Reads the TOC into memory, populates globalHashMap
                archive.LoadGameWadFast(file, globalHashMap);
            }

            // 2. PROCESS MODS
            double modStep = 0.30 / (modNames.Count > 0 ? modNames.Count : 1);
            int j = 0;
            foreach (string modName in modNames)
            {
                var modPath = Path.Combine(srcDir, modName);

                Report($"Processing Mod: {modName}", 0.30 + (j * modStep));

                if (Directory.Exists(modPath))
                {
                    ProcessMod(modPath, activeWads, globalHashMap);
                }
                j++;
            }

            // 3. WRITE OUTPUT
            // Filter to only WADs that have been modified ("Dirty")
            var dirtyWads = activeWads.Values.Where(w => w.IsDirty).ToList();
            double writeStep = 0.40 / (dirtyWads.Count > 0 ? dirtyWads.Count : 1);
            int count = 0;

            foreach (var wad in dirtyWads)
            {
                Report($"Writing: {wad.Name}", 0.60 + (count * writeStep));

                var dstPath = Path.Combine(dstDir, wad.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(dstPath));

                // Smart Write: Copies Original & Patches if possible, else Rebuilds
                wad.SmartWrite(dstPath);
                count++;
            }

            // 4. CLEANUP
            Report("Cleaning up...", 0.95);
            Cleanup(dstDir, validRelativePaths);

            Report("Done!", 1.0);
        }

        private static void ProcessMod(
            string modPath,
            Dictionary<string, WadArchive> activeWads,
            Dictionary<ulong, List<WadArchive>> globalHashMap)
        {
            var wadFiles = Directory.GetFiles(modPath, "*.wad.client", SearchOption.AllDirectories);

            foreach (var wadFile in wadFiles)
            {
                var wadName = Path.GetFileName(wadFile);

                // Get target WAD (or create new if mod adds a completely new WAD)
                if (!activeWads.TryGetValue(wadName, out var targetWad))
                {
                    string relativePath = GetRelativePath(modPath, wadFile);
                    // OriginalPath is null because this is a new custom WAD
                    targetWad = new WadArchive(wadName, relativePath, null);
                    activeWads[wadName] = targetWad;
                }

                // Merge mod files into the target WAD (and propagate via globalHashMap)
                targetWad.MergeModWad(wadFile, globalHashMap);
            }
        }

        private static void Cleanup(string dstDir, HashSet<string> validRelativePaths)
        {
            if (!Directory.Exists(dstDir)) return;
            var existingFiles = Directory.GetFiles(dstDir, "*.wad.client", SearchOption.AllDirectories);
            foreach (var file in existingFiles)
            {
                string relPath = GetRelativePath(dstDir, file);
                if (!validRelativePaths.Contains(relPath))
                {
                    File.Delete(file);
                }
            }
            DeleteEmptyDirs(dstDir);
        }

        private static string GetRelativePath(string rootPath, string fullPath)
        {
            rootPath = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
                return fullPath.Substring(rootPath.Length);
            return Path.GetFileName(fullPath);
        }

        private static void DeleteEmptyDirs(string startDir)
        {
            try
            {
                foreach (var d in Directory.EnumerateDirectories(startDir)) DeleteEmptyDirs(d);
                if (!Directory.EnumerateFileSystemEntries(startDir).Any()) Directory.Delete(startDir);
            }
            catch { }
        }
    }

    public class WadArchive
    {
        public string Name { get; }
        public string RelativePath { get; }
        public string OriginalGamePath { get; }

        public Dictionary<ulong, WadEntry> Files { get; } = new Dictionary<ulong, WadEntry>();

        public bool IsDirty { get; private set; } = false;
        private bool _hasNewFiles = false; // If true, we MUST rebuild TOC (cannot patch)

        public WadArchive(string name, string relativePath, string originalPath)
        {
            Name = name;
            RelativePath = relativePath;
            OriginalGamePath = originalPath;
        }

        public void LoadGameWadFast(string path, Dictionary<ulong, List<WadArchive>> globalHashMap)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var br = new BinaryReader(fs))
            {
                // Header (268) + FileCount (4) = 272 bytes minimum
                if (fs.Length < 272) return;

                fs.Seek(268, SeekOrigin.Begin);
                uint fileCount = br.ReadUInt32();

                // Read entire TOC block
                int tocSize = (int)(fileCount * 32);
                byte[] tocBuffer = br.ReadBytes(tocSize);

                for (int i = 0; i < fileCount; i++)
                {
                    int offset = i * 32;
                    ulong pathHash = BitConverter.ToUInt64(tocBuffer, offset);

                    var entry = new WadEntry
                    {
                        Hash = pathHash,
                        TocIndex = i,
                        SourceOffset = BitConverter.ToUInt32(tocBuffer, offset + 8),
                        SourceSize = BitConverter.ToUInt32(tocBuffer, offset + 12),
                        UncompressedSize = BitConverter.ToUInt32(tocBuffer, offset + 16),
                        Type = (WadCompressionType)tocBuffer[offset + 20],
                        SourcePath = path // Pointing to ORIGINAL GAME WAD
                    };

                    Files[pathHash] = entry;

                    // Add to Global Map for propagation
                    if (!globalHashMap.TryGetValue(pathHash, out var list))
                    {
                        list = new List<WadArchive>();
                        globalHashMap[pathHash] = list;
                    }
                    list.Add(this);
                }
            }
        }

        public void MergeModWad(string path, Dictionary<ulong, List<WadArchive>> globalHashMap)
        {
            using (var fs = File.OpenRead(path))
            using (var br = new BinaryReader(fs))
            {
                if (fs.Length < 272) return;

                fs.Seek(268, SeekOrigin.Begin);
                uint fileCount = br.ReadUInt32();

                int tocSize = (int)(fileCount * 32);
                byte[] tocBuffer = br.ReadBytes(tocSize);

                for (int i = 0; i < fileCount; i++)
                {
                    int offset = i * 32;
                    ulong pathHash = BitConverter.ToUInt64(tocBuffer, offset);

                    var entry = new WadEntry
                    {
                        Hash = pathHash,
                        SourceOffset = BitConverter.ToUInt32(tocBuffer, offset + 8),
                        SourceSize = BitConverter.ToUInt32(tocBuffer, offset + 12),
                        UncompressedSize = BitConverter.ToUInt32(tocBuffer, offset + 16),
                        Type = (WadCompressionType)tocBuffer[offset + 20],
                        SourcePath = path // Pointing to MOD WAD
                    };

                    // Apply to Self
                    AddOrUpdate(entry);

                    // Propagate to linked archives
                    if (globalHashMap.TryGetValue(pathHash, out var linked))
                    {
                        foreach (var other in linked)
                        {
                            if (other != this) other.AddOrUpdate(entry);
                        }
                    }
                }
            }
        }

        public void AddOrUpdate(WadEntry entry)
        {
            if (Files.TryGetValue(entry.Hash, out var existing))
            {
                // REPLACING existing file.
                // Keep the original TOC Index so we can patch it in place if needed.
                entry.TocIndex = existing.TocIndex;
            }
            else
            {
                // NEW file.
                _hasNewFiles = true;
                entry.TocIndex = Files.Count; // Assign new virtual index
            }

            Files[entry.Hash] = entry;
            IsDirty = true;
        }

        public void SmartWrite(string dstPath)
        {
            // STRATEGY A: PATCHING
            // Conditions: 
            // 1. We have an Original File (not a new custom WAD)
            // 2. No NEW files were added (only replacements) -> TOC size matches
            if (OriginalGamePath != null && !_hasNewFiles)
            {
                // Copy the original file (Fastest operation)
                File.Copy(OriginalGamePath, dstPath, true);

                using (var fs = new FileStream(dstPath, FileMode.Open, FileAccess.ReadWrite))
                using (var bw = new BinaryWriter(fs))
                {
                    // Find modified entries
                    var modifiedEntries = Files.Values.Where(e => e.SourcePath != OriginalGamePath).ToList();

                    foreach (var entry in modifiedEntries)
                    {
                        // 1. Append Mod Data to End of File
                        fs.Seek(0, SeekOrigin.End);
                        uint newOffset = (uint)fs.Position;
                        AppendData(bw, entry);

                        // 2. Patch TOC Entry
                        // Offset Calc: Header(268) + FileCount(4) + (Index * 32)
                        long tocOffset = 272 + (entry.TocIndex * 32);

                        fs.Seek(tocOffset + 8, SeekOrigin.Begin); // Skip Hash (8 bytes)

                        bw.Write(newOffset);         // Offset (4)
                        bw.Write(entry.SourceSize);  // CompressedSize (4)
                        bw.Write(entry.UncompressedSize); // UncompressedSize (4)
                        bw.Write((byte)entry.Type);  // Type (1)
                        // Duplicate(1), Pad(2), Sha(8) are left untouched
                    }
                }
            }
            // STRATEGY B: FULL REBUILD
            // Required if we added new files or if it's a brand new WAD
            else
            {
                WriteFullRebuild(dstPath);
            }
        }

        private void WriteFullRebuild(string dstPath)
        {
            var sortedEntries = Files.Values.OrderBy(x => x.Hash).ToList();

            using (var fs = File.Create(dstPath))
            using (var bw = new BinaryWriter(fs))
            {
                // Header (RW31)
                bw.Write(new char[] { 'R', 'W' });
                bw.Write((byte)3); bw.Write((byte)1);
                bw.Write(new byte[256]); // Signature (Empty)
                bw.Write((ulong)0);      // Checksum (Empty)

                // File Count
                bw.Write((uint)sortedEntries.Count);

                // Data Start: 268 + 4 + (Count * 32)
                uint currentOffset = 272 + (uint)(sortedEntries.Count * 32);

                // Write TOC
                foreach (var entry in sortedEntries)
                {
                    bw.Write(entry.Hash);
                    bw.Write(currentOffset);
                    bw.Write(entry.SourceSize);
                    bw.Write(entry.UncompressedSize);
                    bw.Write((byte)entry.Type);
                    bw.Write((byte)0);
                    bw.Write((ushort)0);
                    bw.Write((ulong)0);

                    currentOffset += entry.SourceSize;
                }

                // Write Data
                foreach (var entry in sortedEntries)
                {
                    AppendData(bw, entry);
                }
            }
        }

        private void AppendData(BinaryWriter bw, WadEntry entry)
        {
            using (var src = File.OpenRead(entry.SourcePath))
            {
                src.Seek(entry.SourceOffset, SeekOrigin.Begin);
                byte[] buffer = new byte[81920];
                long left = entry.SourceSize;
                while (left > 0)
                {
                    int read = src.Read(buffer, 0, (int)Math.Min(buffer.Length, left));
                    bw.Write(buffer, 0, read);
                    left -= read;
                }
            }
        }
    }

    public class WadEntry
    {
        public ulong Hash;
        public int TocIndex;
        public string SourcePath;
        public uint SourceOffset;
        public uint SourceSize;
        public uint UncompressedSize;
        public WadCompressionType Type;
    }

    public enum WadCompressionType : byte { Uncompressed = 0, Gzip = 1, Zstd = 3 }
}