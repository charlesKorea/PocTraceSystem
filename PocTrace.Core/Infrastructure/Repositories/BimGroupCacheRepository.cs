using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Serilog;

namespace PocTrace.Core.Infrastructure.Repositories
{
  /// <summary>
  /// ConfigurationGroupLog sidecar (Name + ParentID 경로).
  /// GraphCache와 폴더·버전·실패 도메인이 완전 분리된다.
  /// v1: names.bin / v2: groups.bin
  /// </summary>
  public sealed class BimGroupCacheRepository
  {
    public sealed class LoadResult
    {
      public DateTime SavedAt { get; set; }
      public bool IsStale { get; set; }
      public int Version { get; set; }
      public IReadOnlyList<string> Names { get; set; }
      public IReadOnlyList<BimGroupRecord> Records { get; set; }
    }

    /// <summary>구버전 호환: Name만 저장(v1). 신규 Admin은 Save(records) 사용.</summary>
    public void Save(string dbName, IEnumerable<string> names)
    {
      var records = (names ?? Enumerable.Empty<string>())
        .Where(n => !string.IsNullOrWhiteSpace(n))
        .Select(n => new BimGroupRecord
        {
          GroupId = Guid.Empty,
          Name = n.Trim(),
          ParentId = Guid.Empty
        });
      Save(dbName, records);
    }

    public void Save(string dbName, IEnumerable<BimGroupRecord> records)
    {
      if (string.IsNullOrWhiteSpace(dbName))
        throw new ArgumentException("dbName is required", nameof(dbName));

      var list = (records ?? Enumerable.Empty<BimGroupRecord>())
        .Where(r => r != null && !string.IsNullOrWhiteSpace(r.Name))
        .Select(r => new BimGroupRecord
        {
          GroupId = r.GroupId,
          Name = r.Name.Trim(),
          ParentId = r.ParentId
        })
        .GroupBy(r => r.GroupId == Guid.Empty ? (object)r.Name : r.GroupId)
        .Select(g => g.First())
        .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

      string root = GlobalConfig.GetBimGroupCacheDirectory(dbName);
      string genDir = CacheGenerationStore.CreateGenerationDirectory(root);
      string groupsPath = Path.Combine(genDir, BimGroupCacheConstants.GroupsFileName);
      string namesPath = Path.Combine(genDir, BimGroupCacheConstants.NamesFileName);
      string manifestPath = Path.Combine(genDir, BimGroupCacheConstants.ManifestFileName);

      try
      {
        using (var fs = new FileStream(groupsPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var w = new BinaryWriter(fs, Encoding.UTF8))
        {
          w.Write(list.Count);
          foreach (var r in list)
          {
            w.Write(r.GroupId.ToByteArray());
            w.Write(r.Name ?? string.Empty);
            w.Write(r.ParentId.ToByteArray());
          }
        }

        // v1 호환 names.bin도 함께 기록
        using (var fs = new FileStream(namesPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var w = new BinaryWriter(fs, Encoding.UTF8))
        {
          w.Write(list.Count);
          foreach (var r in list)
            w.Write(r.Name ?? string.Empty);
        }

        var savedAt = DateTime.Now;
        using (var fs = new FileStream(manifestPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var w = new BinaryWriter(fs, Encoding.UTF8))
        {
          w.Write(BimGroupCacheConstants.ManifestMagic);
          w.Write(BimGroupCacheConstants.CurrentVersion);
          w.Write(savedAt.ToBinary());
          w.Write(list.Count);
        }

        CacheGenerationStore.PublishGeneration(
          root,
          genDir,
          GlobalConfig.CacheKeepGenerations,
          new[]
          {
            BimGroupCacheConstants.GroupsFileName,
            BimGroupCacheConstants.NamesFileName,
            BimGroupCacheConstants.ManifestFileName
          });

        Log.Information(
          "[BimGroupCache] Saved {Db}: groups={Count}, path={Dir}",
          dbName, list.Count, genDir);
      }
      catch
      {
        TryDeleteDirectory(genDir);
        throw;
      }
    }

    public LoadResult Load(string dbName)
    {
      string manifestPath = GlobalConfig.GetBimGroupManifestPath(dbName);
      string namesPath = GlobalConfig.GetBimGroupNamesPath(dbName);
      string groupsPath = GlobalConfig.GetBimGroupGroupsPath(dbName);

      if (!File.Exists(manifestPath))
      {
        throw new FileNotFoundException(
          "BimGroupCache not found for '" + dbName + "'. Expected: " + manifestPath);
      }

      int version;
      DateTime savedAt;
      int expectedCount;
      using (var fs = CacheGenerationStore.OpenRead(manifestPath, 4 * 1024))
      using (var r = new BinaryReader(fs, Encoding.UTF8))
      {
        int magic = r.ReadInt32();
        if (magic != BimGroupCacheConstants.ManifestMagic)
          throw new InvalidDataException("Invalid BimGroupCache magic for " + dbName);
        version = r.ReadInt32();
        savedAt = DateTime.FromBinary(r.ReadInt64());
        expectedCount = r.ReadInt32();
      }

      List<BimGroupRecord> records;
      if (version >= 2 && File.Exists(groupsPath))
      {
        using (var fs = CacheGenerationStore.OpenRead(groupsPath, 64 * 1024))
        using (var r = new BinaryReader(fs, Encoding.UTF8))
        {
          int count = r.ReadInt32();
          records = new List<BimGroupRecord>(count);
          for (int i = 0; i < count; i++)
          {
            var gid = new Guid(r.ReadBytes(16));
            string name = r.ReadString() ?? string.Empty;
            var pid = new Guid(r.ReadBytes(16));
            records.Add(new BimGroupRecord { GroupId = gid, Name = name, ParentId = pid });
          }
        }
      }
      else if (File.Exists(namesPath))
      {
        using (var fs = CacheGenerationStore.OpenRead(namesPath, 64 * 1024))
        using (var r = new BinaryReader(fs, Encoding.UTF8))
        {
          int count = r.ReadInt32();
          records = new List<BimGroupRecord>(count);
          for (int i = 0; i < count; i++)
          {
            records.Add(new BimGroupRecord
            {
              GroupId = Guid.Empty,
              Name = r.ReadString() ?? string.Empty,
              ParentId = Guid.Empty
            });
          }
        }
      }
      else
      {
        throw new FileNotFoundException(
          "BimGroupCache data missing for '" + dbName + "'.");
      }

      if (expectedCount != records.Count)
      {
        Log.Warning(
          "[BimGroupCache] {Db} count mismatch: manifest={M}, records={N}",
          dbName, expectedCount, records.Count);
      }

      bool isStale = (DateTime.Now - savedAt).TotalHours > GlobalConfig.CacheStaleHours
                     || version < BimGroupCacheConstants.CurrentVersion;

      var names = records
        .Where(x => !string.IsNullOrWhiteSpace(x.Name))
        .Select(x => x.Name.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

      Log.Information(
        "[BimGroupCache] Loaded {Db}: version={V}, savedAt={At}, groups={Count}, stale={Stale}",
        dbName, version, savedAt, records.Count, isStale);

      return new LoadResult
      {
        Version = version,
        SavedAt = savedAt,
        IsStale = isStale,
        Names = names,
        Records = records
      };
    }

    public LoadResult TryLoad(string dbName)
    {
      try
      {
        return Load(dbName);
      }
      catch (Exception ex)
      {
        Log.Warning(ex, "[BimGroupCache] TryLoad failed for {Db}", dbName);
        return null;
      }
    }

    private static void TryDeleteDirectory(string dir)
    {
      try
      {
        if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
          Directory.Delete(dir, recursive: true);
      }
      catch
      {
        // ignore cleanup
      }
    }
  }
}
