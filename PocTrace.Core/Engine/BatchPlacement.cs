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

        /// <summary>미배치설비만 있으면 개별 엑셀을 만들지 않는다. 요약에는 남긴다.</summary>
        public static bool KeepsIndividualExcel(IList<TraceResult> results)
        {
            if (results == null || results.Count == 0)
                return false;
            for (int i = 0; i < results.Count; i++)
            {
                var row = results[i];
                if (row == null)
                    continue;
                if (!string.Equals(row.ResultLabel, Unplaced, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }
}
