using System;
using System.Collections.Generic;
using System.Linq;
using PocTrace.Core.Models;
using Serilog;

namespace PocTrace.Core.Infrastructure.Diagnostics
{
    public sealed class BimIdDiagnosticsResult
    {
        public int TotalNodes { get; set; }
        public int BimIdFilled { get; set; }
        public double FillPercent => TotalNodes > 0 ? 100.0 * BimIdFilled / TotalNodes : 0;
        public int CacheVersion { get; set; }
        public bool CacheVersionSupportsBimId => CacheVersion <= 0 || CacheVersion >= 7;
        public List<string> SampleBimIds { get; set; } = new List<string>();
        public List<string> UserWarnings { get; set; } = new List<string>();
    }

    /// <summary>
    /// ObjectInstanceLog.BIMID → SQL GetSelectNodes → nodes.bin(v7+) → NodeInfo.BimId → Excel S:BIM ID 경로 진단.
    /// </summary>
    public static class BimIdDiagnostics
    {
        public static BimIdDiagnosticsResult Analyze(string dbName, int cacheVersion, IEnumerable<NodeInfo> nodes)
        {
            var list = nodes as IList<NodeInfo> ?? nodes?.ToList() ?? new List<NodeInfo>();
            var result = new BimIdDiagnosticsResult
            {
                TotalNodes = list.Count,
                BimIdFilled = list.Count(n => !string.IsNullOrWhiteSpace(n.BimId)),
                CacheVersion = cacheVersion
            };

            result.SampleBimIds = list
                .Where(n => !string.IsNullOrWhiteSpace(n.BimId))
                .Take(3)
                .Select(n => n.BimId.Trim())
                .ToList();

            if (cacheVersion > 0 && cacheVersion < 7)
            {
                result.UserWarnings.Add(string.Format(
                    "캐시 v{0} — BimId 필드 없음. Admin CacheOnly로 v7+ 캐시 재생성 필요 (Excel S:BIM ID 전부 비어 보임).",
                    cacheVersion));
            }
            else if (list.Count > 0 && result.BimIdFilled == 0)
            {
                result.UserWarnings.Add(
                    "BimId가 모두 비어 있습니다. DB ObjectInstanceLog.BIMID 컬럼·데이터를 확인하세요.");
            }

            return result;
        }

        public static BimIdDiagnosticsResult AnalyzeAndLog(string context, string dbName, int cacheVersion, IEnumerable<NodeInfo> nodes)
        {
            var result = Analyze(dbName, cacheVersion, nodes);
            Log.Information(
                "[{Context}][{Db}] BimId — nodes={Total:N0}, non-empty={Bim:N0} ({Pct:0.1}%), cache=v{Ver}",
                context, dbName, result.TotalNodes, result.BimIdFilled, result.FillPercent, cacheVersion);

            if (result.SampleBimIds.Count > 0)
            {
                Log.Information("[{Context}][{Db}] BimId sample: {Samples}",
                    context, dbName, string.Join(", ", result.SampleBimIds));
            }

            foreach (var warning in result.UserWarnings)
                Log.Warning("[{Context}][{Db}] {Warning}", context, dbName, warning);

            return result;
        }

        public static string FormatUserSummary(BimIdDiagnosticsResult result, DateTime? savedAt = null)
        {
            string line = string.Format("노드 {0:N0}건 · BimId {1:0.0}%", result.TotalNodes, result.FillPercent);
            if (result.CacheVersion > 0)
                line += string.Format(" · 캐시 v{0}", result.CacheVersion);
            if (savedAt.HasValue)
                line += string.Format(" · {0:yyyy-MM-dd HH:mm}", savedAt.Value);
            if (result.UserWarnings.Count > 0)
                line += Environment.NewLine + "⚠ " + result.UserWarnings[0];
            return line;
        }
    }
}
