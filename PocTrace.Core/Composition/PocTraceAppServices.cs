using System;
using PocTrace.Core.Engine;
using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Engine.Logic;
using PocTrace.Core.Engine.ReverseTrace;
using PocTrace.Core.Engine.Reporting;
using PocTrace.Core.Infrastructure;
using PocTrace.Core.Infrastructure.Data;
using PocTrace.Core.Infrastructure.Repositories;
using Serilog;

namespace PocTrace.Core.Composition
{
  /// <summary>
  /// [Refactor / Goal 3] ?üç(?????????) ???????? ???.
  /// ???????? ??? ???????? ??? IoC ??????? ??????? ???? ???,
  /// ??? ?????? ????/????(?????)?? ?????? ?? ?? ?????? ???????.
  /// </summary>
  public sealed class PocTraceAppServices
  {
    // ????? ????
    public GraphCacheManager CacheManager { get; }
    public IGraphLoader GraphLoader { get; }
    public ILogger Logger { get; }
    public TraceExcelReportService ExcelReport { get; }

    // ???? ???? ???? (???: ?¡¾??/?????? ?? ?????? ???)
    public Func<ITraceEngine> EngineFactory { get; }

    private PocTraceAppServices(GraphCacheManager cacheManager, IGraphLoader graphLoader, ILogger logger)
    {
      CacheManager = cacheManager;
      GraphLoader = graphLoader;
      Logger = logger;
      ExcelReport = new TraceExcelReportService(logger);
      EngineFactory = () => CreateTraceEngine();
    }

    public static PocTraceAppServices Create(ILogger logger, bool allowDatabaseFallback)
    {
      var cacheManager = new GraphCacheManager();
      IDbConnectionFactory dbFactory = allowDatabaseFallback ? new DbConnectionFactory() : null;
      var graphLoader = new GraphLoaderRepository(dbFactory);
      return new PocTraceAppServices(cacheManager, graphLoader, logger);
    }

    public ITraceEngine CreateTraceEngine(IGraphCache cache = null)
    {
      var cacheImpl = cache ?? (IGraphCache)CacheManager;
      var algorithm = new TraceAlgorithm(cacheImpl, Logger)
      {
        EnableDetailedDebug = GlobalConfig.EnableDetailedDebug
      };
      return new TraceEngine(cacheImpl, algorithm, Logger);
    }

    public NozzleTraceService CreateNozzleTraceService(IGraphCache cache = null)
    {
      var cacheImpl = cache ?? (IGraphCache)CacheManager;
      var manager = cacheImpl as GraphCacheManager;
      var reverseIndex = manager?.GetReverseGraphIndex() ?? ReverseGraphIndex.Build(cacheImpl);
      var origin = new NozzleReverseTraceOrigin();
      return new NozzleTraceService(cacheImpl, reverseIndex, origin, Logger);
    }
  }
}
