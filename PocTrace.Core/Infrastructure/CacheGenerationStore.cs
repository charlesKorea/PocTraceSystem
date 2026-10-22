using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Serilog;

namespace PocTrace.Core.Infrastructure
{
    /// <summary>
    /// Shared-cache generation folders: write a complete snapshot, then switch
    /// current.txt with a single replace so readers never see a half-written set.
    /// Readers open files with FileShare.Read|Delete so Admin can drop old gens.
    /// </summary>
    public static class CacheGenerationStore
    {
        public const string PointerFileName = "current.txt";
        public const string GenerationPrefix = "g";
        public const int DefaultKeepGenerations = 2;

        public static readonly FileShare ReaderShare = FileShare.Read | FileShare.Delete;

        public static FileStream OpenRead(string path, int bufferSize = 64 * 1024)
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, ReaderShare, bufferSize);
        }

        public static string NewGenerationName()
        {
            return GenerationPrefix
                   + DateTime.UtcNow.ToString("yyyyMMddHHmmss")
                   + "_"
                   + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        public static string CreateGenerationDirectory(string cacheRoot)
        {
            if (string.IsNullOrWhiteSpace(cacheRoot))
                throw new ArgumentException("cacheRoot is required", nameof(cacheRoot));
            Directory.CreateDirectory(cacheRoot);
            var dir = Path.Combine(cacheRoot, NewGenerationName());
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static void PublishGeneration(
            string cacheRoot,
            string generationDir,
            int keepGenerations = DefaultKeepGenerations,
            IEnumerable<string> legacyFlatFiles = null)
        {
            if (string.IsNullOrWhiteSpace(generationDir) || !Directory.Exists(generationDir))
                throw new DirectoryNotFoundException(generationDir ?? "(null)");

            var name = Path.GetFileName(
                generationDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            WritePointerAtomic(cacheRoot, name);
            if (legacyFlatFiles != null)
                TryDeleteLegacyFlatFiles(cacheRoot, legacyFlatFiles);
            PruneOldGenerations(cacheRoot, name, keepGenerations);
        }

        public static bool TryResolveLiveDirectory(string cacheRoot, out string liveDir)
        {
            liveDir = null;
            if (string.IsNullOrWhiteSpace(cacheRoot) || !Directory.Exists(cacheRoot))
                return false;

            var pointerPath = Path.Combine(cacheRoot, PointerFileName);
            if (File.Exists(pointerPath))
            {
                try
                {
                    var name = ReadPointer(pointerPath);
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        var pointed = Path.Combine(cacheRoot, name);
                        if (IsLiveCacheDir(pointed))
                        {
                            liveDir = pointed;
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[CacheGen] Failed reading pointer {Path}", pointerPath);
                }
            }

            var newest = ListGenerationDirectories(cacheRoot)
                .Where(IsLiveCacheDir)
                .OrderByDescending(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (newest != null)
            {
                liveDir = newest;
                return true;
            }

            if (IsLiveCacheDir(cacheRoot))
            {
                liveDir = cacheRoot;
                return true;
            }

            return false;
        }

        public static string ResolveLiveDirectoryOrRoot(string cacheRoot)
        {
            string live;
            return TryResolveLiveDirectory(cacheRoot, out live) ? live : cacheRoot;
        }

        public static void ReplaceFile(string sourcePath, string destinationPath)
        {
            if (!File.Exists(destinationPath))
            {
                File.Move(sourcePath, destinationPath);
                return;
            }

            try
            {
                File.Replace(sourcePath, destinationPath, null);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[CacheGen] File.Replace failed, falling back to copy: {Dest}", destinationPath);
                File.Copy(sourcePath, destinationPath, overwrite: true);
                try { File.Delete(sourcePath); }
                catch { /* ignore leftover tmp */ }
            }
        }

        public static void PruneOldGenerations(string cacheRoot, string keepName, int keepGenerations)
        {
            if (keepGenerations < 1)
                keepGenerations = 1;

            var gens = ListGenerationDirectories(cacheRoot)
                .OrderByDescending(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase)
                .ToList();

            int kept = 0;
            foreach (var dir in gens)
            {
                var name = Path.GetFileName(dir);
                if (kept < keepGenerations
                    || string.Equals(name, keepName, StringComparison.OrdinalIgnoreCase))
                {
                    kept++;
                    continue;
                }

                TryDeleteDirectory(dir);
            }
        }

        public static void TryDeleteLegacyFlatFiles(string cacheRoot, IEnumerable<string> fileNames)
        {
            if (string.IsNullOrWhiteSpace(cacheRoot) || fileNames == null)
                return;

            foreach (var name in fileNames)
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                var path = Path.Combine(cacheRoot, name);
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                        Log.Information("[CacheGen] Removed legacy flat file {Path}", path);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[CacheGen] Could not delete legacy flat file {Path}", path);
                }
            }
        }

        private static bool IsLiveCacheDir(string dir)
        {
            return Directory.Exists(dir)
                   && File.Exists(Path.Combine(dir, CacheConstants.ManifestFileName));
        }

        private static string ReadPointer(string path)
        {
            using (var fs = new FileStream(
                       path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 256))
            using (var r = new StreamReader(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                return (r.ReadLine() ?? string.Empty).Trim();
            }
        }

        private static void WritePointerAtomic(string cacheRoot, string generationName)
        {
            Directory.CreateDirectory(cacheRoot);
            var finalPath = Path.Combine(cacheRoot, PointerFileName);
            var tempPath = finalPath + ".tmp";
            File.WriteAllText(tempPath, generationName + Environment.NewLine, new UTF8Encoding(false));
            ReplaceFile(tempPath, finalPath);
            Log.Information("[CacheGen] Pointer switched to {Gen} at {Root}", generationName, cacheRoot);
        }

        private static IEnumerable<string> ListGenerationDirectories(string cacheRoot)
        {
            if (!Directory.Exists(cacheRoot))
                return Array.Empty<string>();

            return Directory.GetDirectories(cacheRoot)
                .Where(d =>
                {
                    var name = Path.GetFileName(d);
                    return name != null
                           && name.StartsWith(GenerationPrefix, StringComparison.OrdinalIgnoreCase)
                           && name.Length > GenerationPrefix.Length;
                });
        }

        private static void TryDeleteDirectory(string dir)
        {
            try
            {
                Directory.Delete(dir, recursive: true);
                Log.Information("[CacheGen] Pruned generation {Dir}", dir);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[CacheGen] Could not prune generation (readers may still hold it): {Dir}", dir);
            }
        }
    }
}
