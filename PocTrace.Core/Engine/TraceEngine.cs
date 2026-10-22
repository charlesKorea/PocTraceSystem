using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Engine.Logic;
using PocTrace.Core.Engine.Validation;
using PocTrace.Core.Enums;
using PocTrace.Core.Models;
using Serilog;

namespace PocTrace.Core.Engine
{
  public class TraceEngine : ITraceEngine
  {
    private readonly IGraphCache _cache;
    private readonly TraceAlgorithm _algorithm;
    private readonly ILogger _logger;
    private readonly TraceValidator _validator;

    public TraceEngine(IGraphCache cache, TraceAlgorithm algorithm, ILogger logger)
    {
      _cache = cache;
      _algorithm = algorithm;
      _logger = logger;
      _validator = new TraceValidator(logger);
    }

    public Task<List<TraceResult>> RunTraceAsync(string targetTag, int limitCount, bool detailedLog = false,
      bool includePartialTagMatch = false)
    {
      return RunTraceAsync(targetTag, limitCount, detailedLog, CancellationToken.None, includePartialTagMatch);
    }

    public Task<List<TraceResult>> RunTraceAsync(
      string targetTag,
      int limitCount,
      bool detailedLog,
      CancellationToken cancellationToken,
      bool includePartialTagMatch = false)
    {
      return Task.Run(
        () => RunTrace(targetTag, limitCount, detailedLog, cancellationToken, includePartialTagMatch),
        cancellationToken);
    }

    public List<TraceResult> RunTrace(
      string targetTag,
      int limitCount,
      bool detailedLog,
      CancellationToken cancellationToken,
      bool includePartialTagMatch = false)
    {
      _logger.Information("🚀 [TraceEngine] Searching for Equipment Line:{Line} Tag: {Tag}", _cache.DbName, targetTag);

      var startPocs = _cache.FindStartPocs(targetTag, includePartialTagMatch);
      if (startPocs == null || !startPocs.Any())
      {
        _logger.Warning("⚠️ No POCs found for tag: {Tag}", targetTag);
        return new List<TraceResult>();
      }

      var equipGroups = startPocs.GroupBy(p => p.OwnerId);
      var currentDbMap = ResolveUtilityMap();
      var allResults = new List<TraceResult>();

        foreach (var group in equipGroups)
        {
          cancellationToken.ThrowIfCancellationRequested();

          var equipId = group.Key;
          var ownerNode = _cache.GetNode(equipId);
          if (!IsRealEquipment(ownerNode))
            continue;

          var equipName = _cache.GetNodeName(equipId);
          // 검색어("a")가 아닌 DB 정규 Tag("a,b")를 StartEquipmentTag로 사용 — Scrubber 검수 오탐 방지
          var canonicalEquipTag = !string.IsNullOrWhiteSpace(ownerNode?.Tag) ? ownerNode.Tag : targetTag;
          var targets = limitCount > 0 ? group.Take(limitCount) : group;

          foreach (var poc in targets)
          {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
              _algorithm.EnableDetailedDebug = detailedLog;
              var pathResults = _algorithm.TracePath(poc, canonicalEquipTag);

              foreach (var res in pathResults)
              {
                res.StartNodeName = equipName;
                res.StartNodeGUID = equipId;
                res.Line = _cache.DbName;

                var startNode = _cache.GetNode(equipId);
                NodeInfo endNode = null;
                PocData endPoc = null;

                if (res.EndPocId.HasValue)
                {
                  if (res.EndType == EndPointType.Takeoff)
                  {
                    res.TargetNodeGUID = res.EndPocId.Value;
                  }
                  else
                  {
                    endPoc = _cache.GetPoc(res.EndPocId.Value);
                    if (endPoc != null)
                      endNode = _cache.GetNode(endPoc.OwnerId);
                  }
                }
                else
                {
                  var lastStep = res.Steps.LastOrDefault();
                  if (lastStep != null)
                    endNode = _cache.GetNode(lastStep.NodeId);
                }

                if (endNode != null)
                  res.TargetNodeGUID = endNode.InstanceId;

                _validator.Validate(_cache, res, poc, startNode, endNode, endPoc, pathResults, currentDbMap);
              }

              lock (allResults)
              {
                allResults.AddRange(pathResults);
              }
            }
            catch (Exception ex)
            {
              _logger.Error(ex, "🔥 Error tracing POC {Id}", poc.InstanceId);
            }
          }
        }

        return allResults;
    }

    private Dictionary<string, Dictionary<string, string>> ResolveUtilityMap()
    {
      var currentDbMap = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
      if (_cache.UtilityMaps != null &&
          _cache.UtilityMaps.TryGetValue(_cache.DbName, out var map))
      {
        currentDbMap = map;
      }

      return currentDbMap;
    }

    private static bool IsRealEquipment(NodeInfo node)
    {
      if (node == null || string.IsNullOrEmpty(node.Sub1Name))
        return false;

      return node.Sub1Name.StartsWith("1-") ||
             node.Sub1Name.StartsWith("2-") ||
             node.Sub1Name.StartsWith("4-") ||
             node.Sub1Name.StartsWith("6-");
    }
  }
}
