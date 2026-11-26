using PocTrace.Core.Models;
using PocTrace.Core.Enums;
using System;
using System.Collections.Generic;

namespace PocTrace.Core.Engine.Logic
{
  public partial class TraceAlgorithm
  {
    /// <summary>
    /// 시작 POC의 Utility Group에 "water"가 포함되고 아직 방문하지 않은 하류가 있으면
    /// Takeoff.PipeId(또는 분기 배관)를 통해 기존 BFS를 계속한다.
    /// Water가 아니거나 미방문 하류가 없으면 false를 반환해 호출부가 TOF로 끝낸다.
    /// </summary>
    private bool TryContinueFromWaterTakeoff(
      TakeoffData takeoff,
      TraceState state,
      Queue<TraceState> queue,
      PocData startPoc,
      string startEquipTag,
      List<TraceResult> results,
      ref TraceStep lastProcessedStep,
      int currentLen)
    {
      if (!IsWaterUtilityStart(startPoc))
        return false;

      LogDetailed("  💧 [Water UT] Takeoff({Tag}) 감지 — TOF 종료 대신 하류 탐색을 계속합니다.", takeoff.Tag);

      var steps = CloneSteps(state.Steps);
      var takeoffStep = new TraceStep
      {
        StepIndex = steps.Count + 1,
        NodeId = takeoff.TakeoffId,
        NodeName = $"Damper[{takeoff.Tag}]",
        NodeType = "Takeoff"
      };
      steps.Add(takeoffStep);
      lastProcessedStep = takeoffStep;

      if (state.VisitedNodes.Contains(takeoff.TakeoffId))
      {
        LogDetailed("  💧 [Water UT] Takeoff({Tag}) 재방문 — 역류 분기 소멸 (ING 기록 없음).", takeoff.Tag);
        return true;
      }

      if (!TryResolveTakeoffContinuationPoint(takeoff, state.VisitedNodes, out var continuationOutlet))
      {
        // S5D TakeoffLog.PipeID는 방금 걸어온 배관인 경우가 많다. 그 배관을 하류로 다시 넣으면 Open Pipe ING가 된다.
        // 미방문 하류가 없으면 Takeoff 자체가 끝이다. 호출부가 TOF를 기록한다.
        LogDetailed("  💧 [Water UT] Takeoff({Tag})에 미방문 하류 배관이 없습니다 — TOF로 종료합니다.", takeoff.Tag);
        return false;
      }

      var visited = new HashSet<Guid>(state.VisitedNodes) { takeoff.TakeoffId };

      queue.Enqueue(new TraceState
      {
        CurrentOutlet = continuationOutlet,
        Steps = steps,
        VisitedNodes = visited,
        AccumulatedLength = currentLen
      });

      LogDetailed("  💧 [Water UT] Outlet({Outlet}) 진입점으로 큐 등록 — NOZ/CAP까지 기존 탐색 로직 재사용.", continuationOutlet);
      return true;
    }

    private static bool IsWaterUtilityStart(PocData startPoc)
    {
      var utilityGroup = startPoc?.UtilityGroupName ?? string.Empty;
      return utilityGroup.IndexOf("water", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// 1) Takeoff.PipeId (DB/캐시에 저장된 하류 배관)
    /// 2) TakeoffId가 Outlet인 상위 배관의 다른 Outlet → 하류 Pipe/POC
    /// </summary>
    private bool TryResolveTakeoffContinuationPoint(
      TakeoffData takeoff,
      HashSet<Guid> visitedNodes,
      out Guid nextOutlet)
    {
      nextOutlet = Guid.Empty;

      if (takeoff.PipeId != Guid.Empty
          && (visitedNodes == null || !visitedNodes.Contains(takeoff.PipeId))
          && _cache.TryGetPipe(takeoff.PipeId, out _))
      {
        nextOutlet = takeoff.PipeId;
        return true;
      }

      if (!_cache.TryFindPipeIdByOutlet(takeoff.TakeoffId, out var parentPipeId)
          || !_cache.TryGetPipe(parentPipeId, out var parentPipe))
      {
        return false;
      }

      foreach (var outlet in EnumeratePipeOutlets(parentPipe))
      {
        if (!outlet.HasValue || outlet.Value == takeoff.TakeoffId)
          continue;

        if (visitedNodes.Contains(outlet.Value))
          continue;

        if (_cache.TryGetTakeoff(outlet.Value, out _))
        {
          nextOutlet = outlet.Value;
          return true;
        }

        if (_cache.TryGetPipe(outlet.Value, out var branchPipe))
        {
          nextOutlet = branchPipe.PipeId;
          return true;
        }

        if (_cache.TryGetPoc(outlet.Value, out _))
        {
          nextOutlet = outlet.Value;
          return true;
        }
      }

      return false;
    }

    private static IEnumerable<Guid?> EnumeratePipeOutlets(PipeData pipe)
    {
      foreach (var target in PipeEndpointTraversal.ConnectedTargets(pipe))
        yield return target;
    }
  }
}
