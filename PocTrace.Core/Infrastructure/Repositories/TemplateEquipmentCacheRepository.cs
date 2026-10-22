using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PocTrace.Core.Enums;
using PocTrace.Core.Models;
using Serilog;

namespace PocTrace.Core.Infrastructure.Repositories
{
  /// <summary>
  /// Subdivision1 LIKE '3-%' Equipment 전용 캐시 (철거 설비정보 sheet용).
  /// GraphCache와 분리 — POC Trace는 로드하지 않음.
  /// </summary>
  public sealed class TemplateEquipmentCacheRepository
  {
    public sealed class LoadResult
    {
      public DateTime SavedAt { get; set; }
      public bool IsStale { get; set; }
      public int Version { get; set; }
      public IReadOnlyList<NodeInfo> Nodes { get; set; }
    }

    public void Save(string dbName, IEnumerable<NodeInfo> nodes)
    {
      if (string.IsNullOrWhiteSpace(dbName))
        throw new ArgumentException("dbName is required", nameof(dbName));

      var list = (nodes ?? Enumerable.Empty<NodeInfo>())
        .Where(n => n != null && n.InstanceId != Guid.Empty)
        .GroupBy(n => n.InstanceId)
        .Select(g => g.First())
        .ToList();

      string root = GlobalConfig.GetTemplateEquipmentCacheDirectory(dbName);
      string genDir = CacheGenerationStore.CreateGenerationDirectory(root);
      string nodesPath = Path.Combine(genDir, TemplateEquipmentCacheConstants.NodesFileName);
      string manifestPath = Path.Combine(genDir, TemplateEquipmentCacheConstants.ManifestFileName);

      try
      {
        using (var fs = new FileStream(nodesPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var w = new BinaryWriter(fs, Encoding.UTF8))
        {
          w.Write(list.Count);
          foreach (var n in list)
          {
            w.Write(n.InstanceId.ToByteArray());
            w.Write(n.InstanceNumericId);
            w.Write(n.Tag ?? string.Empty);
            w.Write(n.Name ?? string.Empty);
            w.Write(n.Division);
            w.Write(n.Sub1Name ?? string.Empty);
            w.Write(n.Sub2Name ?? string.Empty);
            w.Write(n.ConstructionCode ?? string.Empty);
            w.Write(n.BimId ?? string.Empty);
            w.Write(n.QCode ?? string.Empty);
          }
        }

        var savedAt = DateTime.Now;
        using (var fs = new FileStream(manifestPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var w = new BinaryWriter(fs, Encoding.UTF8))
        {
          w.Write(TemplateEquipmentCacheConstants.ManifestMagic);
          w.Write(TemplateEquipmentCacheConstants.CurrentVersion);
          w.Write(savedAt.ToBinary());
          w.Write(list.Count);
        }

        CacheGenerationStore.PublishGeneration(
          root,
          genDir,
          GlobalConfig.CacheKeepGenerations,
          new[]
          {
            TemplateEquipmentCacheConstants.NodesFileName,
            TemplateEquipmentCacheConstants.ManifestFileName
          });

        Log.Information(
          "[TemplateEqpCache] Saved {Db}: nodes={Count}, path={Dir}",
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
      string manifestPath = GlobalConfig.GetTemplateEquipmentManifestPath(dbName);
      string nodesPath = GlobalConfig.GetTemplateEquipmentNodesPath(dbName);

      if (!File.Exists(manifestPath) || !File.Exists(nodesPath))
      {
        throw new FileNotFoundException(
          "TemplateEqpCache not found for '" + dbName + "'. Expected: " + manifestPath);
      }

      int version;
      DateTime savedAt;
      int expectedCount;
      using (var fs = CacheGenerationStore.OpenRead(manifestPath, 4 * 1024))
      using (var r = new BinaryReader(fs, Encoding.UTF8))
      {
        int magic = r.ReadInt32();
        if (magic != TemplateEquipmentCacheConstants.ManifestMagic)
          throw new InvalidDataException("Invalid TemplateEqpCache magic for " + dbName);
        version = r.ReadInt32();
        savedAt = DateTime.FromBinary(r.ReadInt64());
        expectedCount = r.ReadInt32();
      }

      List<NodeInfo> nodes;
      using (var fs = CacheGenerationStore.OpenRead(nodesPath, 64 * 1024))
      using (var r = new BinaryReader(fs, Encoding.UTF8))
      {
        int count = r.ReadInt32();
        nodes = new List<NodeInfo>(count);
        for (int i = 0; i < count; i++)
        {
          nodes.Add(new NodeInfo
          {
            InstanceId = new Guid(r.ReadBytes(16)),
            InstanceNumericId = r.ReadInt64(),
            Tag = r.ReadString(),
            Name = r.ReadString(),
            Type = NodeType.Equipment,
            Division = r.ReadInt32(),
            Sub1Name = r.ReadString(),
            Sub2Name = r.ReadString(),
            ConstructionCode = r.ReadString(),
            BimId = r.ReadString(),
            QCode = r.ReadString()
          });
        }
      }

      if (expectedCount != nodes.Count)
      {
        Log.Warning(
          "[TemplateEqpCache] {Db} count mismatch: manifest={M}, nodes={N}",
          dbName, expectedCount, nodes.Count);
      }

      bool isStale = (DateTime.Now - savedAt).TotalHours > GlobalConfig.CacheStaleHours
                     || version < TemplateEquipmentCacheConstants.CurrentVersion;

      Log.Information(
        "[TemplateEqpCache] Loaded {Db}: version={V}, savedAt={At}, nodes={Count}, stale={Stale}",
        dbName, version, savedAt, nodes.Count, isStale);

      return new LoadResult
      {
        Version = version,
        SavedAt = savedAt,
        IsStale = isStale,
        Nodes = nodes
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
        Log.Warning(ex, "[TemplateEqpCache] TryLoad failed for {Db}", dbName);
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
        // ignore
      }
    }
  }
}
