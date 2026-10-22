using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Models;
using PocTrace.Core.Utils;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PocTrace.Core.Engine.Validation
{
                        public class HookupValidationResult
    {
        public Guid ObjectId { get; set; }
        public string ObjectName { get; set; }
        public string CurrentHookup { get; set; }
        public string ExpectedHookup { get; set; }
        public string Status { get; set; }
        // [신규] 26.05.01
        public string UtilityName { get; set; }
        public string ModuleName { get; set; }
        // [신규] Connector Tag (수동 입력값)
        public string ConnectorTag { get; set; }
        // [신규] PocNo (가공된 값)
        public string PocNo { get; set; }
        // [신규] Equipment Tag (통계용)
        public string EquipmentTag { get; set; }
        // [신규] 작업자 정보 (Steps에서 추출)
        public string WorkOpTime { get; set; }      // 작업날짜 (yyyy-MM-dd)
        public string WorkUserName { get; set; }    // 작업자 이름
        public string WorkCompanyName { get; set; } // 작업자 귀속 회사
    }

    public class HookupValidationService
    {
        private readonly Util util = new Util();
        private readonly ILogger _logger;
        private readonly string[] _targetUtilityGroups = { "exhaust", "vac" };

        // 🌟 [로깅] 생성자에서 Logger 주입
        public HookupValidationService(ILogger logger)
        {
            _logger = logger;
        }

        public List<HookupValidationResult> Validate(List<TraceResult> traceResults, IGraphCache cache)
        {
            var validationResults = new List<HookupValidationResult>();
            var processedGuids = new HashSet<Guid>();

            //_logger.Information("=================================================");
            //_logger.Information("🕵️‍♂️ [Hookup 검증 시작] 총 {Count}개의 탐색 결과(TraceResult)에 대한 속성 검증을 시작합니다.", traceResults.Count);
            //_logger.Information("=================================================");

            foreach (var result in traceResults)
            {
                if (!cache.TryGetPoc(result.StartPocId, out var startPoc)) continue;

                string utGroup = startPoc.UtilityGroupName ?? "";
                bool isTargetUt = _targetUtilityGroups.Any(g => utGroup.IndexOf(g, StringComparison.OrdinalIgnoreCase) >= 0);

                if (!isTargetUt)
                {
                    //_logger.Information("  ⏭️ [검증 스킵] 시작 설비({Tag})의 UT Group({UT})은 검증 대상(Exhaust/Vac)이 아닙니다.", result.StartEquipmentTag, utGroup);
                    continue;
                }

                string manualTag = startPoc.Tag ?? ""; // 수동으로 입력된 Connector Tag
                string expectedHookup = "";
                bool hasConnectorTagError = false;

                // [수정] 표준 규칙 우선 적용
                string moduleName = startPoc.ModuleName ?? "";
                // [수정] moduleName이 빈 값인 경우 "MAIN"으로 간주
                if (string.IsNullOrWhiteSpace(moduleName))
                {
                    moduleName = "MAIN";
                }

                string pocNo = result.StartPocNo ?? "";
                string utilityName = result.StartPocUtilityName ?? "";
                string subDivision1 = result.StartNodeSubDivision1 ?? "";

                //26.05.26
                //Main Tool, Sub Tool에 대해서만 훅업넘버 검사한다.
                if (!util.IsStringContains(subDivision1, "MAIN") && !util.IsStringContains(subDivision1, "SUB"))
                {
                    continue; //Main, Sub 외 훅업넘버 체크 불필요
                }

                // [수정] pocNo에서 '_' 다음의 2자리 숫자만 추출 (예: value_01 → "01", value_02 → "02")
                string processedPocNo = "";
                if (!string.IsNullOrWhiteSpace(pocNo))
                {
                    int underscoreIndex = pocNo.LastIndexOf('_');
                    if (underscoreIndex >= 0 && underscoreIndex < pocNo.Length - 1)
                    {
                        string afterUnderscore = pocNo.Substring(underscoreIndex + 1);
                        // 뒤에서부터 2자리 숫자 추출 (0 포함)
                        if (afterUnderscore.Length >= 2)
                        {
                            processedPocNo = afterUnderscore.Substring(0, 2);
                        }
                        else
                        {
                            processedPocNo = afterUnderscore.PadLeft(2, '0');
                        }
                    }
                    else
                    {
                        // '_'가 없으면 전체 사용
                        processedPocNo = pocNo;
                    }
                }

                // 1. 표준 규칙으로 expectedHookup 계산
                bool isValidExpectedHookup = false;
                //X1
                if (!string.IsNullOrWhiteSpace(moduleName) && 
                    (utGroup.IndexOf("vac", StringComparison.OrdinalIgnoreCase) >= 0) &&
                         !string.IsNullOrWhiteSpace(processedPocNo))
                {
                    // [vac 규칙] EQPID_FORE LINE_ModuleName
                    expectedHookup = $"{result.StartEquipmentTag}_FORE LINE_{moduleName}({processedPocNo})";
                    isValidExpectedHookup = true;
                }
                else if (utGroup.IndexOf("exhaust", StringComparison.OrdinalIgnoreCase) >= 0 &&
                         !string.IsNullOrWhiteSpace(utilityName) &&
                         !string.IsNullOrWhiteSpace(subDivision1) &&
                         !string.IsNullOrWhiteSpace(moduleName) &&
                         !string.IsNullOrWhiteSpace(processedPocNo))
                {
                    // [exhaust 규칙] EQPID_EXHAUST_UT_SubDivision1_ModuleName(PocNo)
                    // processedPocNo는 '_' 다음 2자리 숫자
                    expectedHookup = $"{result.StartEquipmentTag}_EXHAUST_{utilityName}_{subDivision1}_{moduleName}({processedPocNo})";
                    isValidExpectedHookup = true;
                }
                else
                {
                    // 표준 규칙 적용 불가 시 N/A
                    expectedHookup = "N/A(No ConnectorTag)";
                    isValidExpectedHookup = false;      
                }

                // 2. 수동으로 입력된 Connector Tag(startPoc.Tag)가 표준 규칙과 일치하는지 확인
                if (!string.IsNullOrWhiteSpace(manualTag))
                {
                    // 표준 규칙과 비교 (대소문자 무시, 시작 부분만 비교)
                    bool isMatch = manualTag.Equals(expectedHookup, StringComparison.OrdinalIgnoreCase) ||
                                   manualTag.StartsWith(expectedHookup, StringComparison.OrdinalIgnoreCase);

                    if (!isMatch)
                    {
                        // 표준 규칙과 일치하지 않으면 오류 플래그 설정
                        hasConnectorTagError = true;
                        // 표준 규칙을 우선 사용하므로 expectedHookup은 표준 규칙 유지
                    }
                    else
                    {
                        // 일치하면 수동 태그 사용 (표준 규칙이므로)
                        expectedHookup = manualTag;
                    }
                }

                //_logger.Information("  🎯 [검증 타겟 발견] 설비: {Tag}, UT: {UT} | 💡 기준 정답지(Expected): '{Expected}'",
                //    result.StartEquipmentTag, utGroup, expectedHookup);

                int okCount = 0, errCount = 0;

                // TraceResult당 1회만 계산 (step 루프마다 GroupBy 반복 시 OOM/CPU 부담)
                string workOpTime = "";
                string workUserName = "";
                string workCompanyName = "";
                var pipeStepsWithWork = result.Steps
                    .Where(s => s.NodeType.IndexOf("Pipe", StringComparison.OrdinalIgnoreCase) >= 0
                             && s.OpTime.HasValue
                             && !string.IsNullOrEmpty(s.UserName))
                    .ToList();
                if (pipeStepsWithWork.Count > 0)
                {
                    var userWorkStats = pipeStepsWithWork
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

                    if (userWorkStats != null)
                    {
                        workOpTime = userWorkStats.LastOpTime?.ToString("yyyy-MM-dd") ?? "";
                        workUserName = userWorkStats.UserName ?? "";
                        workCompanyName = userWorkStats.CompanyName ?? "";
                    }
                }

                foreach (var step in result.Steps)
                {
                    if (processedGuids.Contains(step.NodeId)) continue;

                    //_logger.Information($" [STEP 정보] Node ID:{step.NodeId} Node Type:{step.NodeType} Node Name: {step.NodeName} StepIndex: {step.StepIndex}");

                    bool isPipe = step.NodeType.Equals("PipeSeg", StringComparison.OrdinalIgnoreCase);
                    bool isOthers = step.NodeType.Equals("Fitting", StringComparison.OrdinalIgnoreCase) || step.NodeType.Equals("Damper", StringComparison.OrdinalIgnoreCase);

                    if (isPipe || isOthers)
                    {
                        string currentHookup = step.HookupNumber ?? "";
                        string status = "OK";
                        string objName = isPipe ? "Pipe" : step.NodeName;

                                                                        // [수정] N/A(No ConnectorTag)인 경우 MISMATCH 대신 DATA_ERROR
                        if (!isValidExpectedHookup)
                        {
                            // 표준 규칙을 구할 수 없는 경우
                            status = "DATA_ERROR";
                        }
                        else if (string.IsNullOrWhiteSpace(currentHookup))
                        {
                            status = "MISSING";
                        }
                        else
                        {
                            if (!currentHookup.Equals(expectedHookup, StringComparison.OrdinalIgnoreCase) &&
                                !currentHookup.StartsWith(expectedHookup, StringComparison.OrdinalIgnoreCase))
                            {
                                status = "MISMATCH";
                            }
                        }

                                                // 🌟 [로깅] 개별 객체 검증 결과 출력 (에러일 경우 눈에 띄게)
                        if (status == "OK")
                        {
                            okCount++;
                        }
                        else if (status == "MISSING")
                        {
                            errCount++;
                        }
                        else if (status == "MISMATCH")
                        {
                            errCount++;
                        }

                        // [수정] Connector Tag가 비어있어도 Hookup Number 검수가 우선
                        // MISMATCH > MISSING > TAG_ERROR 순으로 중요도
                        string finalStatus = status;
                        if (hasConnectorTagError && status == "OK")
                        {
                            // Hookup Number가 정상일 때만 TAG_ERROR 표시
                            finalStatus = "TAG_ERROR";
                        }

                        validationResults.Add(new HookupValidationResult
                        {
                            ObjectId = step.NodeId,
                            ObjectName = objName,
                            CurrentHookup = currentHookup,
                            ExpectedHookup = expectedHookup,
                            Status = finalStatus,
                            // [신규] 26.05.01 - startPoc의 UtilityName과 ModuleName
                            UtilityName = startPoc.UtilityName ?? "",
                            ModuleName = startPoc.ModuleName ?? "",
                            // [신규] Connector Tag (수동 입력값)
                            ConnectorTag = manualTag,
                            // [신규] PocNo (가공된 값)
                            PocNo = processedPocNo,
                            // [신규] Equipment Tag (통계용)
                            EquipmentTag = result.StartEquipmentTag,
                            // [신규] 작업자 정보
                            WorkOpTime = workOpTime,
                            WorkUserName = workUserName,
                            WorkCompanyName = workCompanyName
                        });

                        processedGuids.Add(step.NodeId);
                    }
                }

                //_logger.Information("  🏁 [라인 검증 완료] 설비 {Tag} 라인 검수 - 정상: {OK}건, 오류: {ERR}건", result.StartEquipmentTag, okCount, errCount);
            }

            int totalErrors = validationResults.Count(x => x.Status != "OK");
            _logger.Information("📊 [Hookup 검증 종료] 총 {Total}개 객체 검증 완료. 발견된 속성 오류: {ErrCount}건", validationResults.Count, totalErrors);

            return validationResults;
        }
    }
}