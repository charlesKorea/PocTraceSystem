using System;
using System.Configuration;
using System.IO;
using System.Linq;

namespace PocTrace.Core.Infrastructure
{
  public static class GlobalConfig
  {
    public static string SharedCachePath =>
      GetAppSetting("SharedCachePath", @"X:\jHOOK\PocTraceData");

    public static string DailyReportPath =>
      GetAppSetting("DailyReportPath", @"X:\jHOOK\DailyReport");

    public static bool AllowDatabaseFallback =>
      GetBoolAppSetting("AllowDatabaseFallback", false);

    public static bool EnableDetailedDebug =>
      GetBoolAppSetting("EnableDetailedDebug", false);

    public static double CacheStaleHours =>
      GetDoubleAppSetting("CacheStaleHours", CacheConstants.DefaultStaleHours);

    /// <summary>Viewer startup hard gate (hours). Default 48.</summary>
    public static double CacheMaxAgeHours =>
      GetDoubleAppSetting("CacheMaxAgeHours", CacheConstants.DefaultMaxAgeHours);

    /// <summary>Live + previous generation folders to keep after a cache publish.</summary>
    public static int CacheKeepGenerations =>
      Math.Max(1, GetIntAppSetting("CacheKeepGenerations", CacheGenerationStore.DefaultKeepGenerations));

    public static int MaxParallelTrace =>
      GetIntAppSetting("MaxParallelTrace", 4);

    /// <summary>Admin Full_List 등 대용량 Excel — 이 행 수 이상이면 LargeXlsx 스트리밍 사용.</summary>
    public static int ExcelLargeExportRowThreshold =>
      GetIntAppSetting("ExcelLargeExportRowThreshold", 50000);

    /// <summary>Auto 모드에서 LargeXlsx 전환 허용 (Admin Full_List 기본 true).</summary>
    public static bool ExcelUseLargeXlsxForFullList =>
      GetBoolAppSetting("ExcelUseLargeXlsxForFullList", true);

    /// <summary>Admin Initialize 시 ReverseGraphIndex 구축 (NozzleTrace용). 기본 false — 메모리 절약.</summary>
    public static bool AdminBuildReverseGraphIndex =>
      GetBoolAppSetting("AdminBuildReverseGraphIndex", false);

    /// <summary>Admin DB 라인 처리 후 GC + 캐시 해제. x1→x2 OOM 완화.</summary>
    public static bool AdminGcBetweenDatabases =>
      GetBoolAppSetting("AdminGcBetweenDatabases", true);

    /// <summary>한 DB 실패 시 다음 TargetDatabase 계속 처리.</summary>
    public static bool AdminContinueOnDbFailure =>
      GetBoolAppSetting("AdminContinueOnDbFailure", true);

    // --- Design Progress ---
    public static bool DesignProgressEnabled =>
      GetBoolAppSetting("DesignProgressEnabled", true);

    public static int DesignProgressInProgressDays =>
      GetIntAppSetting("DesignProgressInProgressDays", 2);

    public static int DesignProgressIdleStableDays =>
      GetIntAppSetting("DesignProgressIdleStableDays", 14);

    public static int DesignProgressMinCohortSize =>
      GetIntAppSetting("DesignProgressMinCohortSize", 3);

    public static double DesignProgressLengthWeight =>
      GetDoubleAppSetting("DesignProgressLengthWeight", 0.6);

    /// <summary>Empty = Graph-only progress (no FRM).</summary>
    public static string FrmConnectionString =>
      GetAppSetting("FrmConnectionString", string.Empty);

    public static string FrmEquipmentView =>
      GetAppSetting("FrmEquipmentView", "dbo.V_FRM_EquipmentProgress");

    /// <summary>
    /// S5d | TbStar | WebDdw. 값이 없으면 S5d. 알 수 없는 문자열은 예외이며 다른 프로파일로 넘어가지 않는다.
    /// </summary>
    public static SchemaKind ActiveSchema
    {
      get
      {
        var raw = ConfigurationManager.AppSettings["SchemaKind"];
        if (string.IsNullOrWhiteSpace(raw))
          return SchemaKind.S5d;
        return SchemaKindParser.Parse(raw);
      }
    }

    public static string[] TargetDatabases =>
      GetAppSetting("TargetDatabases", "x1")
        .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(d => d.Trim())
        .Where(d => d.Length > 0)
        .ToArray();

    /// <summary>Per-DB folder for v5 split cache. Adding x2_m = new folder only.</summary>
    public static string GetCacheDirectory(string dbName) =>
      Path.Combine(SharedCachePath, $"GraphCache_{SanitizeDbName(dbName)}");

    /// <summary>Live generation folder (or legacy flat root if no current.txt).</summary>
    public static string GetLiveCacheDirectory(string dbName) =>
      CacheGenerationStore.ResolveLiveDirectoryOrRoot(GetCacheDirectory(dbName));

    /// <summary>BIM Group Name sidecar cache (independent of GraphCache).</summary>
    public static string GetBimGroupCacheDirectory(string dbName) =>
      Path.Combine(SharedCachePath, $"BimGroupCache_{SanitizeDbName(dbName)}");

    public static string GetLiveBimGroupCacheDirectory(string dbName) =>
      CacheGenerationStore.ResolveLiveDirectoryOrRoot(GetBimGroupCacheDirectory(dbName));

    public static string GetBimGroupManifestPath(string dbName) =>
      Path.Combine(GetLiveBimGroupCacheDirectory(dbName), BimGroupCacheConstants.ManifestFileName);

    public static string GetBimGroupNamesPath(string dbName) =>
      Path.Combine(GetLiveBimGroupCacheDirectory(dbName), BimGroupCacheConstants.NamesFileName);

    public static string GetBimGroupGroupsPath(string dbName) =>
      Path.Combine(GetLiveBimGroupCacheDirectory(dbName), BimGroupCacheConstants.GroupsFileName);

    /// <summary>철거 전용 3-% (template) Equipment sidecar.</summary>
    public static string GetTemplateEquipmentCacheDirectory(string dbName) =>
      Path.Combine(SharedCachePath, $"TemplateEqpCache_{SanitizeDbName(dbName)}");

    public static string GetLiveTemplateEquipmentCacheDirectory(string dbName) =>
      CacheGenerationStore.ResolveLiveDirectoryOrRoot(GetTemplateEquipmentCacheDirectory(dbName));

    public static string GetTemplateEquipmentManifestPath(string dbName) =>
      Path.Combine(GetLiveTemplateEquipmentCacheDirectory(dbName), TemplateEquipmentCacheConstants.ManifestFileName);

    public static string GetTemplateEquipmentNodesPath(string dbName) =>
      Path.Combine(GetLiveTemplateEquipmentCacheDirectory(dbName), TemplateEquipmentCacheConstants.NodesFileName);

    public static string GetManifestPath(string dbName) =>
      Path.Combine(GetLiveCacheDirectory(dbName), CacheConstants.ManifestFileName);

    public static string GetSectionFileName(CacheSectionKind section)
    {
      switch (section)
      {
        case CacheSectionKind.Nodes: return CacheConstants.SectionNodes;
        case CacheSectionKind.Pocs: return CacheConstants.SectionPocs;
        case CacheSectionKind.Pipes: return CacheConstants.SectionPipes;
        case CacheSectionKind.Takeoffs: return CacheConstants.SectionTakeoffs;
        case CacheSectionKind.DynProps: return CacheConstants.SectionDynProps;
        default: throw new ArgumentOutOfRangeException(nameof(section));
      }
    }

    public static string GetSectionFilePath(string dbName, CacheSectionKind section) =>
      Path.Combine(GetLiveCacheDirectory(dbName), GetSectionFileName(section));

    public static string GetSectionFilePathIn(string directory, CacheSectionKind section) =>
      Path.Combine(directory, GetSectionFileName(section));

    /// <summary>Legacy v4 monolithic file (read-only migration).</summary>
    public static string GetLegacyCacheFileName(string dbName) =>
      $"GraphCache_{SanitizeDbName(dbName)}.bin";

    public static string GetLegacyCacheFilePath(string dbName) =>
      Path.Combine(SharedCachePath, GetLegacyCacheFileName(dbName));

    [Obsolete("Use GetLegacyCacheFileName / GetCacheDirectory. Kept for callers expecting old name.")]
    public static string GetCacheFileName(string dbName) => GetLegacyCacheFileName(dbName);

    [Obsolete("Use GetManifestPath or GetCacheDirectory.")]
    public static string GetCacheFilePath(string dbName) => GetLegacyCacheFilePath(dbName);

    private static string SanitizeDbName(string dbName)
    {
      if (string.IsNullOrWhiteSpace(dbName))
        throw new ArgumentException("dbName is required", nameof(dbName));
      foreach (var c in Path.GetInvalidFileNameChars())
        dbName = dbName.Replace(c, '_');
      return dbName.Trim();
    }
    private static string GetAppSetting(string key, string defaultValue)
    {
      var value = ConfigurationManager.AppSettings[key];
      return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
    }

    private static bool GetBoolAppSetting(string key, bool defaultValue)
    {
      var value = ConfigurationManager.AppSettings[key];
      if (string.IsNullOrWhiteSpace(value)) return defaultValue;
      return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static int GetIntAppSetting(string key, int defaultValue)
    {
      var value = ConfigurationManager.AppSettings[key];
      if (string.IsNullOrWhiteSpace(value)) return defaultValue;
      return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static double GetDoubleAppSetting(string key, double defaultValue)
    {
      var value = ConfigurationManager.AppSettings[key];
      if (string.IsNullOrWhiteSpace(value)) return defaultValue;
      return double.TryParse(value, out var parsed) ? parsed : defaultValue;
    }
  }
}
