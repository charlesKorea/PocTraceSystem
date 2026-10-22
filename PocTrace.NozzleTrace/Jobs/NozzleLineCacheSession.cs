using System;
using PocTrace.Core.Composition;
using PocTrace.Core.Infrastructure.Diagnostics;
using PocTrace.Core.Engine.Cache;

namespace PocTrace.NozzleTrace.Jobs
{
  public sealed class NozzleLineCacheLoadResult
  {
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }
    public NozzleLineCacheSession Session { get; set; }
  }

  /// <summary>한 라인(DB) 캐시 로드·Initialize 완료 상태. Viewer LoadGraph와 동일 패턴.</summary>
  public sealed class NozzleLineCacheSession : IDisposable
  {
    public PocTraceAppServices Services { get; }
    public string DbName { get; }
    public DateTime SavedAt { get; }
    public int CacheVersion { get; }
    public bool IsStale { get; }
    public BimIdDiagnosticsResult BimIdStats { get; }

    internal NozzleLineCacheSession(
      PocTraceAppServices services,
      string dbName,
      DateTime savedAt,
      int cacheVersion,
      bool isStale,
      BimIdDiagnosticsResult bimIdStats)
    {
      Services = services;
      DbName = dbName;
      SavedAt = savedAt;
      CacheVersion = cacheVersion;
      IsStale = isStale;
      BimIdStats = bimIdStats;
    }

    public void Dispose()
    {
      try
      {
        Services?.CacheManager?.ReleaseMemory();
      }
      catch { }
    }
  }
}
