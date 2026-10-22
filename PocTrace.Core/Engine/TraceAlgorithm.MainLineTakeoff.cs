using PocTrace.Core.Models;
using PocTrace.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PocTrace.Core.Engine.Logic
{
  public partial class TraceAlgorithm
  {
    // [VOC-1] 대상 유틸리티: 시작 POC의 UtilityName이 아래 중 하나를 "포함"(대소문자 무시)하면 대상.
    // 'fore line'은 공백/하이픈/붙임 표기를 모두 허용.
    private static readonly string[] _mainLineUtilities =
        { "vent", "vac", "fore line", "fore-line", "foreline" };

    /// <summary>
    /// [VOC-1] MAIN 설비의 vent/vac/fore line POC가 Takeoff에 도달한 경우 TOF로 종료하지 않고
    /// Water Takeoff와 동일하게 하류 진입점을 큐에 등록해 기존 BFS로 NOZ/CAP/EQP까지 계속 추적한다.
    ///
    /// 이전 구현은 자체 while-루프에서 "하류 Takeoff = 분기 → ING"로 처리해
    /// 메인라인에 연속 Takeoff가 있는 경우 무조건 ING로 귀결되는 버그가 있었다.
    /// Water와 동일한 continuation + BFS 재사용으로 정렬한다.
    /// 대상이 아니면 false → 호출부가 Water/일반 TOF 로직을 수행.
    /// </summary>
    private bool TryContinueFromMainLineTakeoff(
        TakeoffData takeoff,
        TraceState state,
        Queue<TraceState> queue,
        PocData startPoc,
        string startEquipTag,
        List<TraceResult> results,
        ref TraceStep lastProcessedStep,
        int currentLen)
    {
      if (!IsMainLineUtilityStart(startPoc))
        return false;

      string startUt = startPoc?.UtilityName ?? "";
      string startSub1 = _cache.GetSubdivision1(startPoc.OwnerId) ?? "";
      LogDetailed(
        "  🛤️ [MainLine] Takeoff 감지 — TOF 종료 대신 하류 BFS 계속. Tag={Tag}, TakeoffId={Id}, S.Tag={STag}, S.UT={Ut}, S.Sub1={Sub1}, hops={Hops}, len={Len}",
        takeoff.Tag, takeoff.TakeoffId, startEquipTag, startUt, startSub1, state.Steps?.Count ?? 0, currentLen);

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

      // Water와 동일: 재방문 Takeoff는 역류로 소멸(ING 미기록)
      if (state.VisitedNodes.Contains(takeoff.TakeoffId))
      {
        LogDetailed("  🛤️ [MainLine] Takeoff({Tag}/{Id}) 재방문 — 역류 분기 소멸 (결과 미기록).", takeoff.Tag, takeoff.TakeoffId);
        return true;
      }

      // Water와 동일한 continuation 해석기 재사용 (PipeId → parent pipe outlets)
      if (!TryResolveTakeoffContinuationPoint(takeoff, state.VisitedNodes, out var continuationOutlet))
      {
        LogDetailed("  🛤️ [MainLine] Takeoff({Tag}/{Id}) continuation outlet 없음 — ING 1건만 기록.", takeoff.Tag, takeoff.TakeoffId);
        results.Add(CreateResult(startEquipTag, startPoc, EndPointType.Broken, ValidationStatus.Error,
            steps, currentLen, $"MainLine Takeoff[{takeoff.Tag}] - No connected pipe", takeoff.TakeoffId));
        return true;
      }

      var visited = new HashSet<Guid>(state.VisitedNodes) { takeoff.TakeoffId };

      // TOF까지 경로는 하나 — 큐에 쌓여 있던 형제 분기는 버리고 continuation만 진행
      int dropped = queue.Count;
      if (dropped > 0)
      {
        queue.Clear();
        LogDetailed("  🛤️ [MainLine] TOF 도달 후 형제 큐 {Dropped}건 폐기 (TOF→하류 단일 경로).", dropped);
      }

      queue.Enqueue(new TraceState
      {
        CurrentOutlet = continuationOutlet,
        Steps = steps,
        VisitedNodes = visited,
        AccumulatedLength = currentLen
      });

      LogDetailed(
        "  🛤️ [MainLine] continuation Outlet={Outlet} 큐 등록 (남은큐={Q}). EQP 도달 시 비-EQP 결과는 PreferMainLineEqpResult에서 폐기.",
        continuationOutlet, queue.Count);
      return true;
    }

    /// <summary>
    /// MainLine(TOF 경유) 탐색에서 EQP에 도달했으면 그 외 ING/CAP/NOZ/TOF 결과는 버리고
    /// 최단 EQP 1건만 남긴다. TOF까지 경로가 단일인 VOC-1 전제.
    /// </summary>
    private List<TraceResult> PreferMainLineEqpResult(PocData startPoc, List<TraceResult> results)
    {
      if (results == null || results.Count == 0 || !IsMainLineUtilityStart(startPoc))
        return results;

      var eqp = results.Where(r => r.EndType == EndPointType.Equipment).ToList();
      if (eqp.Count == 0)
      {
        LogDetailed("  🛤️ [MainLine] EQP 미도달 — 하류 결과 {Count}건 유지. statuses=[{Statuses}]",
          results.Count,
          string.Join(",", results.Select(r => r.STATUS)));
        return results;
      }

      var best = eqp.OrderBy(r => r.TotalLength).ThenBy(r => r.Steps?.Count ?? int.MaxValue).First();
      if (results.Count > 1 || eqp.Count > 1)
      {
        LogDetailed(
          "  🛤️ [MainLine] EQP 도달 — 비-EQP/중복 EQP {Dropped}건 폐기, 최단 EQP 1건만 유효 (EndPoc={Poc}, len={Len}, hops={Hops}).",
          results.Count - 1,
          best.EndPocId,
          best.TotalLength,
          best.Steps?.Count ?? 0);
      }
      else
      {
        LogDetailed("  🛤️ [MainLine] EQP 단일 결과 확정 (EndPoc={Poc}, len={Len}).", best.EndPocId, best.TotalLength);
      }

      return new List<TraceResult> { best };
    }

    /// <summary>시작 설비 Sub1에 'MAIN' 포함 && 시작 POC UtilityName이 vent/vac/fore line 포함.</summary>
    private bool IsMainLineUtilityStart(PocData startPoc)
    {
      if (startPoc == null) return false;

      string sub1 = _cache.GetSubdivision1(startPoc.OwnerId) ?? string.Empty;
      if (sub1.IndexOf("MAIN", StringComparison.OrdinalIgnoreCase) < 0)
        return false;

      string ut = startPoc.UtilityName ?? string.Empty;
      return _mainLineUtilities.Any(u => ut.IndexOf(u, StringComparison.OrdinalIgnoreCase) >= 0);
    }
  }
}
