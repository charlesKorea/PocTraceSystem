using PocTrace.Core.Models;
using PocTrace.Core.Engine.Validation;
using System.Collections.Generic;

namespace PocTrace.Core.Engine.Logic
{
    public class TraceExecutionReport
    {
        // 1. 물리적 탐색 결과
        public List<TraceResult> Paths { get; set; } = new List<TraceResult>();

        // 2. 🌟 정상(OK)과 에러를 모두 포함하는 논리적 검증 결과
        public List<HookupValidationResult> HookupValidations { get; set; } = new List<HookupValidationResult>();

        // 종합 요약 정보 (방어 코드용)
        public bool HasHookupValidations => HookupValidations != null && HookupValidations.Count > 0;
    }
}