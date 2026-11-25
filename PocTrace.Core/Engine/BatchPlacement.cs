using System;
using System.Collections.Generic;
using PocTrace.Core.Models;

namespace PocTrace.Core.Engine
{
    /// <summary>배치 업로드에서 태그가 없거나 배관이 없을 때의 결과 문구.</summary>
    public static class BatchPlacement
    {
        public const string Unplaced = "미배치설비";
        public const string Undesigned = "미설계 배치 설비";
        public const string IntegrityIssue = "설비정합오류";
        public const string IntegrityOwner = "ft 기술";

        /// <summary>미배치설비와 미설계 배치 설비는 개별 엑셀을 만들지 않는다.</summary>
        public static bool KeepsIndividualExcel(IList<TraceResult> results)
        {
            if (results == null || results.Count == 0)
                return false;
            for (int i = 0; i < results.Count; i++)
            {
                if (!IsSkipped(results[i]))
                    return true;
            }
            return false;
        }

        /// <summary>배치 엑셀과 Drawing에 넣지 않는 행.</summary>
        public static bool IsSkipped(TraceResult row)
        {
            if (row == null)
                return true;
            return string.Equals(row.ResultLabel, Unplaced, StringComparison.Ordinal)
                || string.Equals(row.ResultLabel, Undesigned, StringComparison.Ordinal);
        }

        public static List<TraceResult> ReportRows(IList<TraceResult> results)
        {
            var list = new List<TraceResult>();
            if (results == null)
                return list;
            for (int i = 0; i < results.Count; i++)
            {
                if (!IsSkipped(results[i]))
                    list.Add(results[i]);
            }
            return list;
        }
    }
}
