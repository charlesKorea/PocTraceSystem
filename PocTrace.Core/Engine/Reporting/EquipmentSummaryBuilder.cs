using System;
using System.Collections.Generic;
using System.Linq;
using PocTrace.Core.Enums;
using PocTrace.Core.Models;

namespace PocTrace.Core.Engine.Reporting
{
    /// <summary>
    /// TraceResult 목록을 설비(Tag) 단위 <see cref="EquipmentTraceSummary"/>로 집계하는 공통 빌더.
    ///
    /// [Refactor] 기존에 아래 3곳에 거의 동일하게 중복되어 있던 집계 로직을 단일화한 것이다.
    ///   - PocTrace.UI  : MainViewModel.GenerateSummaries / MainViewModel.GetGroupedSummaries
    ///   - PocTrace.Admin: DailyTraceJob.GenerateSummariesForExcel
    /// 상태별 카운트(CAP/EMP/EQP/ING/NOZ/TOF)와 작업자 정보(가장 많이 작업한 사람 기준)를 항상 채운다.
    /// (호출부가 사용하지 않는 필드까지 채워도 부작용이 없으며, 동작을 보존한다.)
    /// </summary>
    public static class EquipmentSummaryBuilder
    {
        /// <summary>기존 코드에서 mm → m 변환에 사용하던 계수.</summary>
        public const double MillimetersPerMeter = 1000.0;

        /// <summary>
        /// 설비 태그(<see cref="TraceResult.StartEquipmentTag"/>) 기준으로 그룹핑하여 요약을 생성한다.
        /// </summary>
        /// <param name="results">추적 결과 목록</param>
        /// <param name="lengthDivisor">
        /// <see cref="EquipmentTraceSummary.TotalLength"/>에 적용할 나눗셈 계수.
        /// 1.0 = 원본(mm) 합계, 1000.0(기본) = m 단위 변환. (0 이하이면 나누지 않음)
        /// </param>
        public static List<EquipmentTraceSummary> Build(
            IEnumerable<TraceResult> results,
            double lengthDivisor = MillimetersPerMeter)
        {
            var list = new List<EquipmentTraceSummary>();
            if (results == null)
                return list;

            var grouped = results
                .Where(r => !string.IsNullOrEmpty(r.StartEquipmentTag))
                .GroupBy(r => r.StartEquipmentTag);

            foreach (var group in grouped)
            {
                var first = group.First();

                // 설비 수량 (Tag 내 Unique GUID 수)
                int equipCount = group.Select(r => r.StartNodeGUID).Distinct().Count();

                // Issue Count 합산 (CheckResult(PASS/FAIL)는 CountERR로 자동 계산됨)
                long totalFailCount = group.Sum(r => r.FailCount);

                // 상태(EndType)별 카운트
                var statusCounts = group.GroupBy(r => r.EndType)
                                        .ToDictionary(g => g.Key, g => g.Count());

                var worker = ExtractWorkerInfo(group);

                double totalLength = group.Sum(r => r.TotalLength);
                if (lengthDivisor > 0)
                    totalLength = totalLength / lengthDivisor;

                list.Add(new EquipmentTraceSummary
                {
                    EquipmentTag = group.Key,
                    EquipmentName = first.StartNodeName ?? "Unknown",
                    EquipmentGuid = first.StartNodeGUID.ToString(), // 대표 GUID

                    EquipmentCount = equipCount,
                    CountERR = totalFailCount,

                    DerivedBranchCount = group.Count(),
                    TotalPocCount = group.Select(r => r.StartPocId).Distinct().Count(),

                    TotalLength = totalLength,
                    TotalHopCount = group.Sum(r => r.HopCountDisplay),

                    CountCAP = statusCounts.TryGetValue(EndPointType.Cap, out var cap) ? cap : 0,
                    CountEMP = statusCounts.TryGetValue(EndPointType.Empty, out var emp) ? emp : 0,
                    CountEQP = statusCounts.TryGetValue(EndPointType.Equipment, out var eqp) ? eqp : 0,
                    CountING = statusCounts.TryGetValue(EndPointType.Broken, out var ing) ? ing : 0,
                    CountNOZ = statusCounts.TryGetValue(EndPointType.Nozzle, out var noz) ? noz : 0,
                    CountTOF = statusCounts.TryGetValue(EndPointType.Takeoff, out var tof) ? tof : 0,

                    LatestOpTime = worker.OpTime,
                    LatestUserName = worker.UserName,
                    LatestCompanyName = worker.CompanyName
                });
            }

            return list;
        }

        /// <summary>
        /// 그룹 내 모든 Steps에서 배관(Pipe) 작업자 정보를 모아, 가장 많이 작업한 사람 기준으로 대표값을 추출한다.
        /// </summary>
        private static (string OpTime, string UserName, string CompanyName) ExtractWorkerInfo(
            IEnumerable<TraceResult> group)
        {
            var pipeSteps = group
                .Where(r => r.Steps != null)
                .SelectMany(r => r.Steps)
                .Where(s => s.NodeType != null
                            && s.NodeType.Contains("Pipe")
                            && s.OpTime.HasValue
                            && !string.IsNullOrEmpty(s.UserName))
                .ToList();

            if (!pipeSteps.Any())
                return (string.Empty, string.Empty, string.Empty);

            var top = pipeSteps
                .GroupBy(s => s.UserName)
                .Select(g => new
                {
                    UserName = g.Key,
                    CompanyName = g.First().CompanyName,
                    WorkCount = g.Count(),
                    LastOpTime = g.Max(s => s.OpTime)
                })
                .OrderByDescending(x => x.WorkCount)
                .FirstOrDefault();

            if (top == null)
                return (string.Empty, string.Empty, string.Empty);

            return (
                top.LastOpTime?.ToString("yyyy-MM-dd") ?? string.Empty,
                top.UserName ?? string.Empty,
                top.CompanyName ?? string.Empty);
        }
    }
}
