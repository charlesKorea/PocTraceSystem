using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PocTrace.Core.Enums;
using PocTrace.Core.Infrastructure.Diagnostics;
using PocTrace.Core.Models;
using Serilog;

namespace PocTrace.Core.Infrastructure.Repositories
{
    /// <summary>
    /// v5: per-DB folder with section bins (avoids single 500~600MB file OOM).
    /// v4 legacy monolithic file still readable once for migration.
    /// Writes go to a new generation folder; current.txt is the only in-place switch.
    /// Incremental (section-level): use <see cref="SaveSection"/> to rewrite one group.
    /// </summary>
    public class BinaryCacheRepository
    {
        public class CacheLoadResult
        {
            public DateTime SavedAt { get; set; }
            public bool IsStale { get; set; }
            public int Version { get; set; }
            public IEnumerable<NodeInfo> Nodes { get; set; }
            public IEnumerable<PocData> Pocs { get; set; }
            public IEnumerable<PipeData> Pipes { get; set; }
            public IEnumerable<TakeoffData> Takeoffs { get; set; }
            public IEnumerable<DynamicPropertiesData> HookupNumbers { get; set; }
        }

        private sealed class ManifestInfo
        {
            public int Version { get; set; }
            public DateTime SavedAt { get; set; }
            public Dictionary<CacheSectionKind, int> Counts { get; set; }
                = new Dictionary<CacheSectionKind, int>();
            public Dictionary<CacheSectionKind, int> SectionVersions { get; set; }
                = new Dictionary<CacheSectionKind, int>();

            public int GetSectionVersion(CacheSectionKind kind)
            {
                int version;
                return SectionVersions != null && SectionVersions.TryGetValue(kind, out version)
                    ? version
                    : Version;
            }

            public bool HasStaleSectionLayout()
            {
                if (Version < CacheConstants.CurrentVersion)
                    return true;
                if (SectionVersions == null || SectionVersions.Count == 0)
                    return Version < CacheConstants.CurrentVersion;
                return SectionVersions.Values.Any(v => v < CacheConstants.CurrentVersion);
            }
        }

        private static readonly string[] GraphCacheFlatFiles =
        {
            CacheConstants.ManifestFileName,
            CacheConstants.SectionNodes,
            CacheConstants.SectionPocs,
            CacheConstants.SectionPipes,
            CacheConstants.SectionTakeoffs,
            CacheConstants.SectionDynProps
        };

        public async Task<CacheLoadResult> LoadFromCacheAsync(string dbName, IProgress<string> progress = null)
        {
            return await Task.Run(() => LoadFromCache(dbName, progress)).ConfigureAwait(false);
        }

        /// <summary>manifest.bin만 읽어 버전·저장 시각 확인 (전체 로드 없음).</summary>
        public bool TryPeekManifest(string dbName, out int version, out DateTime savedAt)
        {
            version = 0;
            savedAt = default(DateTime);
            var path = GlobalConfig.GetManifestPath(dbName);
            if (!File.Exists(path))
                return false;

            var manifest = ReadManifest(path);
            version = manifest.Version;
            savedAt = manifest.SavedAt;
            return true;
        }

        public CacheLoadResult LoadFromCache(string dbName, IProgress<string> progress = null)
        {
            var liveDir = GlobalConfig.GetLiveCacheDirectory(dbName);
            var manifestPath = Path.Combine(liveDir, CacheConstants.ManifestFileName);
            if (File.Exists(manifestPath))
                return LoadSplitV5(dbName, liveDir, manifestPath, progress);

            var legacyPath = GlobalConfig.GetLegacyCacheFilePath(dbName);
            if (File.Exists(legacyPath))
            {
                Log.Warning("[Cache] Loading legacy v4 monolithic file for {Db}. Re-save via Admin to migrate to v5 split.", dbName);
                progress?.Report("레거시 v4 캐시 로드…(正在加载旧版 v4 缓存…)");
                return LoadLegacyV4(dbName, legacyPath);
            }

            throw new FileNotFoundException(
                $"Cache not found for '{dbName}'. Expected manifest: {manifestPath} or legacy: {legacyPath}");
        }

        private CacheLoadResult LoadSplitV5(
            string dbName,
            string liveDir,
            string manifestPath,
            IProgress<string> progress = null)
        {
            var manifest = ReadManifest(manifestPath);
            if (manifest.Version < CacheConstants.MinimumSupportedVersion)
            {
                Log.Warning("[Cache] Unsupported split version {Version} (minimum {Min}).",
                    manifest.Version, CacheConstants.MinimumSupportedVersion);
            }

            Log.Information("[Cache] Loading split cache for {Db}: version={V}, savedAt={At}, dir={Dir}",
                dbName, manifest.Version, manifest.SavedAt, liveDir);
            MemoryLoadLog.Stage(
                "Cache.Manifest",
                "db=" + dbName
                + " version=" + manifest.Version
                + " nodes=" + ManifestCount(manifest, CacheSectionKind.Nodes)
                + " pocs=" + ManifestCount(manifest, CacheSectionKind.Pocs)
                + " pipes=" + ManifestCount(manifest, CacheSectionKind.Pipes)
                + " takeoffs=" + ManifestCount(manifest, CacheSectionKind.Takeoffs)
                + " dyn=" + ManifestCount(manifest, CacheSectionKind.DynProps)
                + " dir=" + liveDir);
            if (manifest.HasStaleSectionLayout())
            {
                Log.Warning(
                    "[Cache] {Db} is version {V} (current {Cur}). v9 pipes stay 3 endpoints until Admin re-saves v10.",
                    dbName, manifest.Version, CacheConstants.CurrentVersion);
            }

            progress?.Report("캐시: nodes 로드…(缓存: 正在加载 nodes…)");
            var nodeVersion = manifest.GetSectionVersion(CacheSectionKind.Nodes);
            var nodes = ReadSectionFile(
                GlobalConfig.GetSectionFilePathIn(liveDir, CacheSectionKind.Nodes),
                r => ReadNodes(r, nodeVersion), CacheSectionKind.Nodes, manifest);
            BimIdDiagnostics.AnalyzeAndLog("CacheLoad", dbName, nodeVersion, nodes);
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized);

            progress?.Report("캐시: pocs 로드…(缓存: 正在加载 pocs…)");
            var pocs = ReadSectionFile(
                GlobalConfig.GetSectionFilePathIn(liveDir, CacheSectionKind.Pocs),
                r => ReadPocs(r), CacheSectionKind.Pocs, manifest);
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized);

            progress?.Report("캐시: pipes 로드…(缓存: 正在加载 pipes…)");
            var pipeVersion = manifest.GetSectionVersion(CacheSectionKind.Pipes);
            var pipes = ReadSectionFile(
                GlobalConfig.GetSectionFilePathIn(liveDir, CacheSectionKind.Pipes),
                r => ReadPipes(r, pipeVersion), CacheSectionKind.Pipes, manifest);
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized);

            progress?.Report("캐시: takeoffs 로드…(缓存: 正在加载 takeoffs…)");
            var takeoffs = ReadSectionFile(
                GlobalConfig.GetSectionFilePathIn(liveDir, CacheSectionKind.Takeoffs),
                r => ReadTakeoffs(r), CacheSectionKind.Takeoffs, manifest);

            progress?.Report("캐시: dynprops 로드…(缓存: 正在加载 dynprops…)");
            var dynVersion = manifest.GetSectionVersion(CacheSectionKind.DynProps);
            var dyn = ReadSectionFile(
                GlobalConfig.GetSectionFilePathIn(liveDir, CacheSectionKind.DynProps),
                r => ReadDynamicProperties(r, dynVersion), CacheSectionKind.DynProps, manifest);

            progress?.Report("캐시 로드 완료(缓存加载完成)");

            bool isStale = (DateTime.Now - manifest.SavedAt).TotalHours > GlobalConfig.CacheStaleHours
                           || manifest.Version < CacheConstants.MinimumSupportedVersion
                           || manifest.HasStaleSectionLayout();

            return new CacheLoadResult
            {
                SavedAt = manifest.SavedAt,
                IsStale = isStale,
                Version = manifest.Version,
                Nodes = nodes,
                Pocs = pocs,
                Pipes = pipes,
                Takeoffs = takeoffs,
                HookupNumbers = dyn
            };
        }

        private CacheLoadResult LoadLegacyV4(string dbName, string fullPath)
        {
            using (var fs = CacheGenerationStore.OpenRead(fullPath, 64 * 1024))
            using (var reader = new BinaryReader(fs, Encoding.UTF8))
            {
                int cacheVersion = reader.ReadInt32();
                DateTime savedAt = DateTime.FromBinary(reader.ReadInt64());

                bool isStale = (DateTime.Now - savedAt).TotalHours > GlobalConfig.CacheStaleHours;
                // v4 is below MinimumSupportedVersion (5) → soft stale so Admin re-save is encouraged
                if (cacheVersion < CacheConstants.MinimumSupportedVersion)
                    isStale = true;

                var nodes = ReadNodes(reader, cacheVersion);
                BimIdDiagnostics.AnalyzeAndLog("CacheLoadLegacy", dbName, cacheVersion, nodes);
                var pocs = ReadPocs(reader);
                var pipes = ReadPipes(reader, cacheVersion);
                var takeoffs = ReadTakeoffs(reader);
                var dynamicProperties = ReadDynamicProperties(reader, cacheVersion);

                return new CacheLoadResult
                {
                    SavedAt = savedAt,
                    IsStale = isStale,
                    Version = cacheVersion,
                    Nodes = nodes,
                    Pocs = pocs,
                    Pipes = pipes,
                    Takeoffs = takeoffs,
                    HookupNumbers = dynamicProperties
                };
            }
        }

        /// <summary>
        /// Full save: write every section + manifest into a new generation folder,
        /// then switch current.txt. Readers keep using the previous generation.
        /// </summary>
        public void SaveToCache(
            string dbName,
            IEnumerable<NodeInfo> nodes,
            IEnumerable<PocData> pocs,
            IEnumerable<PipeData> pipes,
            IEnumerable<TakeoffData> takeoffs,
            IEnumerable<DynamicPropertiesData> dynamicProperties)
        {
            var nodeList = nodes as List<NodeInfo> ?? nodes?.ToList() ?? new List<NodeInfo>();
            var pocList = pocs as List<PocData> ?? pocs?.ToList() ?? new List<PocData>();
            var pipeList = pipes as List<PipeData> ?? pipes?.ToList() ?? new List<PipeData>();
            var takeoffList = takeoffs as List<TakeoffData> ?? takeoffs?.ToList() ?? new List<TakeoffData>();
            var dynList = dynamicProperties as List<DynamicPropertiesData> ?? dynamicProperties?.ToList()
                          ?? new List<DynamicPropertiesData>();

            var root = GlobalConfig.GetCacheDirectory(dbName);
            var genDir = CacheGenerationStore.CreateGenerationDirectory(root);

            var savedAt = DateTime.Now;
            var counts = new Dictionary<CacheSectionKind, int>
            {
                [CacheSectionKind.Nodes] = nodeList.Count,
                [CacheSectionKind.Pocs] = pocList.Count,
                [CacheSectionKind.Pipes] = pipeList.Count,
                [CacheSectionKind.Takeoffs] = takeoffList.Count,
                [CacheSectionKind.DynProps] = dynList.Count
            };
            var sectionVersions = CurrentSectionVersions();

            Log.Information(
                "[Cache] Saving v{Ver} generation for {Db}: Nodes={N}, Pocs={P}, Pipes={I}, Takeoffs={T}, Dyn={D}",
                CacheConstants.CurrentVersion,
                dbName, nodeList.Count, pocList.Count, pipeList.Count, takeoffList.Count, dynList.Count);

            WriteSectionAtomic(genDir, CacheSectionKind.Nodes, w => WriteNodes(w, nodeList));
            WriteSectionAtomic(genDir, CacheSectionKind.Pocs, w => WritePocs(w, pocList));
            WriteSectionAtomic(genDir, CacheSectionKind.Pipes, w => WritePipes(w, pipeList));
            WriteSectionAtomic(genDir, CacheSectionKind.Takeoffs, w => WriteTakeoffs(w, takeoffList));
            WriteSectionAtomic(genDir, CacheSectionKind.DynProps, w => WriteHookupNumbers(w, dynList));

            WriteManifestAtomic(genDir, savedAt, counts, sectionVersions);

            CacheGenerationStore.PublishGeneration(
                root, genDir, GlobalConfig.CacheKeepGenerations, GraphCacheFlatFiles);

            TryDeleteLegacyMonolith(dbName);

            Log.Information("💾 Cache Saved Successfully (v{Ver} generation): {Dir}", CacheConstants.CurrentVersion, genDir);
        }

        /// <summary>
        /// Incremental section save: copy the live generation, rewrite one group, switch pointer.
        /// Refuses if any section is older than <see cref="CacheConstants.CurrentVersion"/> —
        /// mixed layouts (manifest v9 + nodes.bin v7) break stream alignment.
        /// </summary>
        public void SaveSection(string dbName, CacheSectionKind section, object data)
        {
            var root = GlobalConfig.GetCacheDirectory(dbName);
            string liveDir;
            if (!CacheGenerationStore.TryResolveLiveDirectory(root, out liveDir)
                || !File.Exists(Path.Combine(liveDir, CacheConstants.ManifestFileName)))
            {
                throw new InvalidOperationException(
                    $"Cannot SaveSection for '{dbName}': v5 manifest missing. Call SaveToCache first.");
            }

            var manifest = ReadManifest(Path.Combine(liveDir, CacheConstants.ManifestFileName));
            if (manifest.HasStaleSectionLayout())
            {
                throw new InvalidOperationException(
                    $"Cannot SaveSection for '{dbName}': cache is v{manifest.Version} "
                    + $"(need v{CacheConstants.CurrentVersion} on every section). Run a full Admin SaveToCache.");
            }

            int count;
            Action<BinaryWriter> write;
            switch (section)
            {
                case CacheSectionKind.Nodes:
                    var nodes = data as IEnumerable<NodeInfo>
                                ?? throw new ArgumentException("Expected IEnumerable<NodeInfo>");
                    var nodeList = nodes as List<NodeInfo> ?? nodes.ToList();
                    count = nodeList.Count;
                    write = w => WriteNodes(w, nodeList);
                    break;
                case CacheSectionKind.Pocs:
                    var pocs = data as IEnumerable<PocData>
                               ?? throw new ArgumentException("Expected IEnumerable<PocData>");
                    var pocList = pocs as List<PocData> ?? pocs.ToList();
                    count = pocList.Count;
                    write = w => WritePocs(w, pocList);
                    break;
                case CacheSectionKind.Pipes:
                    var pipes = data as IEnumerable<PipeData>
                                ?? throw new ArgumentException("Expected IEnumerable<PipeData>");
                    var pipeList = pipes as List<PipeData> ?? pipes.ToList();
                    count = pipeList.Count;
                    write = w => WritePipes(w, pipeList);
                    break;
                case CacheSectionKind.Takeoffs:
                    var takeoffs = data as IEnumerable<TakeoffData>
                                   ?? throw new ArgumentException("Expected IEnumerable<TakeoffData>");
                    var takeoffList = takeoffs as List<TakeoffData> ?? takeoffs.ToList();
                    count = takeoffList.Count;
                    write = w => WriteTakeoffs(w, takeoffList);
                    break;
                case CacheSectionKind.DynProps:
                    var dyn = data as IEnumerable<DynamicPropertiesData>
                              ?? throw new ArgumentException("Expected IEnumerable<DynamicPropertiesData>");
                    var dynList = dyn as List<DynamicPropertiesData> ?? dyn.ToList();
                    count = dynList.Count;
                    write = w => WriteHookupNumbers(w, dynList);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(section));
            }

            var genDir = CacheGenerationStore.CreateGenerationDirectory(root);
            foreach (CacheSectionKind kind in Enum.GetValues(typeof(CacheSectionKind)))
            {
                if (kind == section)
                    continue;
                var source = GlobalConfig.GetSectionFilePathIn(liveDir, kind);
                var dest = GlobalConfig.GetSectionFilePathIn(genDir, kind);
                if (!File.Exists(source))
                    throw new FileNotFoundException($"Missing cache section {kind}: {source}");
                File.Copy(source, dest, overwrite: true);
            }

            WriteSectionAtomic(genDir, section, write);
            manifest.Counts[section] = count;
            manifest.SectionVersions[section] = CacheConstants.CurrentVersion;
            WriteManifestAtomic(genDir, DateTime.Now, manifest.Counts, manifest.SectionVersions);
            CacheGenerationStore.PublishGeneration(
                root, genDir, GlobalConfig.CacheKeepGenerations, GraphCacheFlatFiles);

            Log.Information("[Cache] Incremental section saved: {Db}/{Section} ({Count} rows) -> {Dir}",
                dbName, section, count, genDir);
        }

        private static Dictionary<CacheSectionKind, int> CurrentSectionVersions()
        {
            return new Dictionary<CacheSectionKind, int>
            {
                [CacheSectionKind.Nodes] = CacheConstants.CurrentVersion,
                [CacheSectionKind.Pocs] = CacheConstants.CurrentVersion,
                [CacheSectionKind.Pipes] = CacheConstants.CurrentVersion,
                [CacheSectionKind.Takeoffs] = CacheConstants.CurrentVersion,
                [CacheSectionKind.DynProps] = CacheConstants.CurrentVersion
            };
        }

        private static void WriteSectionAtomic(string directory, CacheSectionKind section, Action<BinaryWriter> write)
        {
            var finalPath = GlobalConfig.GetSectionFilePathIn(directory, section);
            var tempPath = finalPath + ".tmp";
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024))
            using (var w = new BinaryWriter(fs, Encoding.UTF8))
            {
                write(w);
                w.Flush();
                fs.Flush(true);
            }

            CacheGenerationStore.ReplaceFile(tempPath, finalPath);
        }

        private static void WriteManifestAtomic(
            string directory,
            DateTime savedAt,
            IDictionary<CacheSectionKind, int> counts,
            IDictionary<CacheSectionKind, int> sectionVersions)
        {
            var finalPath = Path.Combine(directory, CacheConstants.ManifestFileName);
            var tempPath = finalPath + ".tmp";

            using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4 * 1024))
            using (var w = new BinaryWriter(fs, Encoding.UTF8))
            {
                w.Write(CacheConstants.ManifestMagic);
                w.Write(CacheConstants.CurrentVersion);
                w.Write(savedAt.ToBinary());
                w.Write(counts.Count);
                foreach (var kv in counts.OrderBy(x => (int)x.Key))
                {
                    w.Write((int)kv.Key);
                    w.Write(kv.Value);
                }

                var versions = sectionVersions ?? CurrentSectionVersions();
                w.Write(versions.Count);
                foreach (var kv in versions.OrderBy(x => (int)x.Key))
                {
                    w.Write((int)kv.Key);
                    w.Write(kv.Value);
                }

                w.Flush();
                fs.Flush(true);
            }

            CacheGenerationStore.ReplaceFile(tempPath, finalPath);
        }

        private static ManifestInfo ReadManifest(string path)
        {
            using (var fs = CacheGenerationStore.OpenRead(path, 4 * 1024))
            using (var r = new BinaryReader(fs, Encoding.UTF8))
            {
                int magic = r.ReadInt32();
                if (magic != CacheConstants.ManifestMagic)
                    throw new InvalidDataException($"Invalid cache manifest magic at {path}");

                var info = new ManifestInfo
                {
                    Version = r.ReadInt32(),
                    SavedAt = DateTime.FromBinary(r.ReadInt64())
                };

                int sectionCount = r.ReadInt32();
                for (int i = 0; i < sectionCount; i++)
                {
                    var kind = (CacheSectionKind)r.ReadInt32();
                    int count = r.ReadInt32();
                    info.Counts[kind] = count;
                }

                if (fs.Position < fs.Length)
                {
                    int versionCount = r.ReadInt32();
                    for (int i = 0; i < versionCount && fs.Position < fs.Length; i++)
                    {
                        var kind = (CacheSectionKind)r.ReadInt32();
                        int version = r.ReadInt32();
                        info.SectionVersions[kind] = version;
                    }
                }

                if (info.SectionVersions.Count == 0)
                {
                    foreach (var kind in info.Counts.Keys)
                        info.SectionVersions[kind] = info.Version;
                }

                return info;
            }
        }

        private static int ManifestCount(ManifestInfo manifest, CacheSectionKind kind)
        {
            int count;
            return manifest != null && manifest.Counts != null && manifest.Counts.TryGetValue(kind, out count)
                ? count
                : -1;
        }

        private static List<T> ReadSectionFile<T>(
            string path,
            Func<BinaryReader, List<T>> read,
            CacheSectionKind kind,
            ManifestInfo manifest)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Missing cache section {kind}: {path}");

            long fileBytes = new FileInfo(path).Length;
            MemoryLoadLog.Stage("Cache.ReadSection", kind + " bytes=" + fileBytes + " path=" + path);

            try
            {
                using (var fs = CacheGenerationStore.OpenRead(path, 64 * 1024))
                using (var r = new BinaryReader(fs, Encoding.UTF8))
                {
                    var list = read(r);
                    if (manifest.Counts.TryGetValue(kind, out int expected) && expected != list.Count)
                    {
                        Log.Warning(
                            "[Cache] Section count mismatch {Kind}: manifest={Expected}, file={Actual}",
                            kind, expected, list.Count);
                    }

                    MemoryLoadLog.Stage("Cache.ReadSection.Done", kind + " count=" + list.Count);
                    return list;
                }
            }
            catch (Exception ex)
            {
                MemoryLoadLog.Fail("Cache.ReadSection." + kind, ex);
                throw;
            }
        }

        private static void TryDeleteLegacyMonolith(string dbName)
        {
            try
            {
                var legacy = GlobalConfig.GetLegacyCacheFilePath(dbName);
                if (File.Exists(legacy))
                {
                    File.Delete(legacy);
                    Log.Information("[Cache] Removed legacy monolith after v5 save: {Path}", legacy);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Cache] Could not delete legacy monolith for {Db}", dbName);
            }
        }

        // ---------------------------------------------------------------------
        // Record serializers (shared by v4/v5)
        // ---------------------------------------------------------------------

        private List<NodeInfo> ReadNodes(BinaryReader r, int cacheVersion)
        {
            int count = r.ReadInt32();
            MemoryLoadLog.EnsureRecordCount("Cache.Nodes", count, r.BaseStream.Length);
            var list = new List<NodeInfo>(count);
            for (int i = 0; i < count; i++)
            {
                var node = new NodeInfo
                {
                    InstanceId = new Guid(r.ReadBytes(16)),
                    Tag = r.ReadString(),
                    Name = r.ReadString(),
                    Type = (NodeType)r.ReadInt32(),
                    Division = r.ReadInt32(),
                    Sub1Name = r.ReadString(),
                    Sub2Name = r.ReadString(),
                    QCode = r.ReadString(),
                    PZ = r.ReadInt64()
                };
                if (cacheVersion >= 6)
                {
                    node.InstanceNumericId = r.ReadInt64();
                    node.ConstructionCode = r.ReadString();
                }
                else
                {
                    node.InstanceNumericId = 0;
                    node.ConstructionCode = null;
                }
                if (cacheVersion >= 7)
                    node.BimId = r.ReadString();
                else
                    node.BimId = null;
                if (cacheVersion >= 8)
                    node.BimGroupName = r.ReadString();
                else
                    node.BimGroupName = null;
                list.Add(node);
            }
            return list;
        }

        private List<PocData> ReadPocs(BinaryReader r)
        {
            int count = r.ReadInt32();
            MemoryLoadLog.EnsureRecordCount("Cache.Pocs", count, r.BaseStream.Length);
            var list = new List<PocData>(count);
            for (int i = 0; i < count; i++)
            {
                var poc = new PocData
                {
                    InstanceId = new Guid(r.ReadBytes(16)),
                    OwnerId = new Guid(r.ReadBytes(16))
                };
                bool hasOutlet = r.ReadBoolean();
                poc.Outlet = hasOutlet ? new Guid(r.ReadBytes(16)) : (Guid?)null;
                poc.ReferenceTag = r.ReadString();
                poc.PointID = new Guid(r.ReadBytes(16));
                poc.Tag = r.ReadString();
                poc.PocName = r.ReadString();
                poc.Information = r.ReadString();
                poc.Size = r.ReadDouble();
                poc.UtilityName = r.ReadString();
                poc.UtilityGroupName = r.ReadString();
                poc.ModuleName = r.ReadString();
                list.Add(poc);
            }
            return list;
        }

        private List<PipeData> ReadPipes(BinaryReader r, int cacheVersion)
        {
            return PipeSectionFormat.Read(r, cacheVersion);
        }

        private List<TakeoffData> ReadTakeoffs(BinaryReader r)
        {
            int count = r.ReadInt32();
            MemoryLoadLog.EnsureRecordCount("Cache.Takeoffs", count, r.BaseStream.Length);
            var list = new List<TakeoffData>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(new TakeoffData
                {
                    TakeoffId = new Guid(r.ReadBytes(16)),
                    Tag = r.ReadString(),
                    PipeId = new Guid(r.ReadBytes(16))
                });
            }
            return list;
        }

        private List<DynamicPropertiesData> ReadDynamicProperties(BinaryReader r, int cacheVersion)
        {
            int count = r.ReadInt32();
            MemoryLoadLog.EnsureRecordCount("Cache.DynProps", count, r.BaseStream.Length);
            var list = new List<DynamicPropertiesData>(count);
            for (int i = 0; i < count; i++)
            {
                var dp = new DynamicPropertiesData
                {
                    PointID = new Guid(r.ReadBytes(16)),
                    HookupNumber = r.ReadString(),
                    PocNo = r.ReadString()
                };
                if (cacheVersion >= 9)
                    dp.ConnectorUse = r.ReadString();
                list.Add(dp);
            }
            return list;
        }

        private void WriteNodes(BinaryWriter w, List<NodeInfo> data)
        {
            w.Write(data.Count);
            foreach (var item in data)
            {
                w.Write(item.InstanceId.ToByteArray());
                w.Write(item.Tag ?? "");
                w.Write(item.Name ?? "");
                w.Write((int)item.Type);
                w.Write(item.Division);
                w.Write(item.Sub1Name ?? "");
                w.Write(item.Sub2Name ?? "");
                w.Write(item.QCode ?? "");
                w.Write((long)item.PZ);
                // v6+: InstanceNumericId + ConstructionCode
                w.Write(item.InstanceNumericId);
                w.Write(item.ConstructionCode ?? "");
                // v7+: BimId
                w.Write(item.BimId ?? "");
                // v8+: BimGroupName
                w.Write(item.BimGroupName ?? "");
            }
        }

        private void WritePocs(BinaryWriter w, List<PocData> data)
        {
            w.Write(data.Count);
            foreach (var item in data)
            {
                w.Write(item.InstanceId.ToByteArray());
                w.Write(item.OwnerId.ToByteArray());
                w.Write(item.Outlet.HasValue);
                if (item.Outlet.HasValue) w.Write(item.Outlet.Value.ToByteArray());
                w.Write(item.ReferenceTag ?? "");
                w.Write(item.PointID.ToByteArray());
                w.Write(item.Tag ?? "");
                w.Write(item.PocName ?? "");
                w.Write(item.Information ?? "");
                w.Write(item.Size);
                w.Write(item.UtilityName ?? "");
                w.Write(item.UtilityGroupName ?? "");
                w.Write(item.ModuleName ?? "");
            }
        }

        private void WritePipes(BinaryWriter w, List<PipeData> data)
        {
            PipeSectionFormat.Write(w, data);
        }

        private void WriteTakeoffs(BinaryWriter w, List<TakeoffData> data)
        {
            w.Write(data.Count);
            foreach (var item in data)
            {
                w.Write(item.TakeoffId.ToByteArray());
                w.Write(item.Tag ?? "");
                w.Write(item.PipeId.ToByteArray());
            }
        }

        private void WriteHookupNumbers(BinaryWriter w, List<DynamicPropertiesData> data)
        {
            w.Write(data.Count);
            foreach (var item in data)
            {
                w.Write(item.PointID.ToByteArray());
                w.Write(item.HookupNumber ?? "");
                w.Write(item.PocNo ?? "");
                w.Write(item.ConnectorUse ?? "");
            }
        }
    }
}
