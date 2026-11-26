using System;
using System.Collections.Generic;
using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Enums;
using PocTrace.Core.Infrastructure.Data;
using PocTrace.Core.Models;

namespace PocTrace.Core.Engine.Reporting
{
    /// <summary>x2-m 배치 업로드 행을 요약 행에 붙이고, 건설코드가 캐시와 다르면 비고를 적는다.</summary>
    public static class BatchUploadBinder
    {
        public static void Apply(
            IList<EquipmentTraceSummary> summaries,
            IList<EqCodeSheet.BatchUploadRow> rows,
            IGraphCache cache,
            string line)
        {
            if (summaries == null || rows == null || rows.Count == 0)
                return;
            string db = cache != null && !string.IsNullOrWhiteSpace(cache.DbName) ? cache.DbName : line;
            if (!DdwLine.IsCacheKey(db))
                return;

            var map = new Dictionary<string, EqCodeSheet.BatchUploadRow>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                if (row == null || string.IsNullOrWhiteSpace(row.EqCode))
                    continue;
                string key = row.EqCode.Trim();
                if (!map.ContainsKey(key))
                    map[key] = row;
            }
            if (map.Count == 0)
                return;

            foreach (var summary in summaries)
            {
                if (summary == null)
                    continue;
                summary.DdwColumns = true;
                EqCodeSheet.BatchUploadRow hit = Find(map, summary.EquipmentTag);
                if (hit == null)
                    continue;
                summary.MajorClass = hit.Major ?? "";
                summary.MinorClass = hit.Minor ?? "";
                summary.UploadConstructionCode = hit.ConstructionCode ?? "";
                summary.DesignCompany = hit.Designer ?? "";
                summary.MoveInDate = hit.MoveInDate ?? "";
                summary.DrawingNeedDate = hit.DrawingNeedDate ?? "";
                if (hit.ConstructionPresent)
                    summary.Remark = ConstructionRemark(cache, summary.EquipmentTag, hit.ConstructionCode);
            }
        }

        private static EqCodeSheet.BatchUploadRow Find(
            Dictionary<string, EqCodeSheet.BatchUploadRow> map,
            string tag)
        {
            tag = (tag ?? "").Trim();
            if (tag.Length == 0)
                return null;
            EqCodeSheet.BatchUploadRow hit;
            if (map.TryGetValue(tag, out hit))
                return hit;
            string[] parts = tag.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (part.Length > 0 && map.TryGetValue(part, out hit))
                    return hit;
            }
            return null;
        }

        private static string ConstructionRemark(IGraphCache cache, string tag, string uploadedCode)
        {
            var codes = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (cache != null)
                Collect(cache, tag, seen, codes);

            string uploaded = (uploadedCode ?? "").Trim();
            bool mismatch = codes.Count == 0
                ? uploaded.Length > 0
                : Differs(codes, uploaded);
            if (!mismatch)
                return "";

            string cacheText = codes.Count == 0 ? "-" : string.Join(", ", codes.ToArray());
            string uploadText = uploaded.Length == 0 ? "-" : uploaded;
            return "건설코드 불일치 (캐시: " + cacheText + ", 업로드: " + uploadText + ")";
        }

        private static bool Differs(List<string> codes, string uploaded)
        {
            for (int i = 0; i < codes.Count; i++)
            {
                if (!string.Equals(codes[i], uploaded, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static void Collect(IGraphCache cache, string tag, HashSet<string> seen, List<string> codes)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return;
            Add(cache.FindNodesByTag(tag, false), seen, codes);
            string[] parts = tag.Split(',');
            if (parts.Length < 2)
                return;
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (part.Length > 0)
                    Add(cache.FindNodesByTag(part, false), seen, codes);
            }
        }

        private static void Add(IReadOnlyList<NodeInfo> nodes, HashSet<string> seen, List<string> codes)
        {
            if (nodes == null)
                return;
            for (int i = 0; i < nodes.Count; i++)
            {
                NodeInfo node = nodes[i];
                if (node == null || node.Type != NodeType.Equipment)
                    continue;
                string code = (node.ConstructionCode ?? "").Trim();
                if (code.Length == 0 || !seen.Add(code))
                    continue;
                codes.Add(code);
            }
        }
    }
}
