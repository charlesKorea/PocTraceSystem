using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Engine.Logic;
using PocTrace.Core.Engine.Validation.Rules;
using PocTrace.Core.Enums;
using PocTrace.Core.Models;
using PocTrace.Core.Utils;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PocTrace.Core.Engine.Validation
{ 
    public class TraceValidator
    {
        private readonly Util util = new Util();

        private readonly ILogger _logger;
        private readonly ValidationPipeline _pipeline;

        public TraceValidator(ILogger logger)
        {
            _logger = logger;
            // 검수 규칙은 ValidationPipeline에 있다. 여기 private 메서드는 매핑과 검수제외만 남긴다.
            _pipeline = ValidationPipeline.CreateDefault();
        }

        public void Validate(IGraphCache cache, TraceResult result, PocData startPoc, NodeInfo startNode, 
                        NodeInfo endNode, PocData endPoc, List<TraceResult> pathResults,
                        Dictionary<string, Dictionary<string, string>> dbGroupMap)
        {
            // 1. 매핑
            MapSourceInfo(cache, result, startPoc, startNode, pathResults); // startNode 전달 필수
            MapTargetInfo(cache, result, endNode, endPoc, pathResults);

            //RAWDATA_REQ_EXCEL poc = MapTraceResult2RawDataReq(result);

            // 3. 검수 로직
            if (검수제외PASS체크(result)) return; // EMP & 비대상

            //26.07.08
            if (util.IsStringContainsOneOfList(result.STATUS, new List<string> { "EQP", "NOZ", "TOF" }))
            {
                if (util.예외대상이름여부(result.StartPocUtilityName))
                {
                    SetInvalidPOC(result, " 기준정보이슈 ", $" POC Utility 상이 ");
                }
            }
            else if (result.STATUS == "ING")
            {
                if (util.예외대상이름여부(result.StartPocUtilityName) || util.IsStringContains(result.StartPocUtilityGroup, "elec"))
                {
                    SetInvalidPOC(result, " 모델링 미완료 ", $" {result.STATUS} ");
                }
            }

            //guid로 커넥터리포트에서 status확인
            var bdSameGuidConList = pathResults.FindAll(x => x.StartPocId == result.StartPocId);
            result.BranchedPipes = bdSameGuidConList.Count;            

            /*
            if (bdSameGuidConList.Count > 1)
            {
                _logger.Information($"Validate: pathResults.Count{pathResults.Count} EA bdSameGuidConList:{bdSameGuidConList.Count} EA");
                foreach (var g in bdSameGuidConList)
                    _logger.Information($"[DEBUG] result.StartPocId:{result.StartPocId.ToString()} g.StartPocId:{g.StartPocId.ToString()} g.STATUS:{g.STATUS} g.TargetNodeTag:{g.TargetNodeTag}");
            }
            */

            // 파이프라인: Err / 모델링미완료 / 설비정보상이 / POCUT / 모델링없음 / 연결누락 / 연결상이
            var vctx = new ValidationContext
            {
                Cache = cache,
                Result = result,
                StartPoc = startPoc,
                StartNode = startNode,
                EndNode = endNode,
                EndPoc = endPoc,
                SamePocBranchResults = bdSameGuidConList,
                UtilityGroupMap = dbGroupMap
            };
            _pipeline.Execute(vctx, (title, feedback) => SetInvalidPOC(result, title, feedback));

            // FixResolve / UT상이는 ValidationPipeline(FixResolveRule, UtilityMismatchRule)으로 이관됨.

            result.FailCount = result.IssueList.Count;
        }


        private void SetInvalidPOC(TraceResult r, string msg, string fdk = "")
        {
            if (!string.IsNullOrEmpty(msg))
            {
                /*
                if (r.IssueList == null || r.IssueList.Count == 0)
                    r.IssueList.Add(msg);
                else if (!r.IssueList.Contains(msg))
                    r.IssueList += " , " + msg;
                else */if (r.IssueList.Contains(msg) && !r.feedback.Contains(fdk)) //같은 Msg인데 오류가 여러 개인 경우 23.06.16
                    r.IssueList.Add("+");
                else
                    r.IssueList.Add(msg);
            }

            if (!string.IsNullOrEmpty(fdk))
            {
                if (string.IsNullOrEmpty(r.feedback))
                    r.feedback = fdk;
                else if (!r.feedback.Contains(fdk))
                    r.feedback += " , " + fdk;
            }

            //23.01.30, 기준정보 이상은 훅업사 귀책이 아님.
            //23.02.15, 장비 이상에 대해서도 훅업사 귀책이 아닌것으로 뺀다.
            //23.03.13, 개조 등에 대해서 금회 설계 대상이 아닌 경우도 훅업사 귀책에서 제외한다.
            //
            // IssueList는 List<string> → Contains("기준정보")는 완전일치라 " 기준정보 이슈 "에 매칭 실패했음.
            // 현재 이슈(msg) 기준으로 귀책을 판정하고, 설계 귀책이 한 번이라도 잡히면 SCS로 덮어쓰지 않는다.
            bool currentIsMasterOrEquip = IsMasterOrEquipmentIssueTitle(msg);
            if (!currentIsMasterOrEquip)
            {
                if (r.invalidCountForHU == 0)
                    r.invalidCountForHU++; //다중이슈는 운영팀 Summary Data를 위해 Counting 함으로, POC 개수로 검수 이력에 이슈 표기되어야 함(현재는 전체 이슈 수량으로 카운팅)
                r.IssueBelongTo = "설계사(设计社)";
            }
            else if (r.invalidCountForHU == 0)
            {
                r.IssueBelongTo = "SCS";
            }

            r.totalInvalidCount++;
        }

        /// <summary>기준정보/장비 이슈 제목이면 true (부분문자열, 공백 변형 허용).</summary>
        private static bool IsMasterOrEquipmentIssueTitle(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return false;
            return msg.IndexOf("기준정보", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("장비", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void MapSourceInfo(IGraphCache cache, TraceResult r, PocData sPoc, NodeInfo sNode, List<TraceResult> pathResults)
        {
            // [Fix 1] 누락되었던 S: Subdivision 복구
            if (sNode != null)
            {
                r.StartNodeSubDivision1 = sNode.Sub1Name;
                r.StartNodeSubDivision2 = sNode.Sub2Name;
                r.InstanceNumericId = sNode.InstanceNumericId;
                r.ConstructionCode = sNode.ConstructionCode;
                r.BimId = sNode.BimId;
                if (sNode.Division == 0) r.StartNodeDivision = "Equipment";
                else if (sNode.Division == 3) r.StartNodeDivision = "PlumbingComponent";
                else r.StartNodeDivision = sNode.Division.ToString();
                // Scrubber 등: 검색어("a")가 아닌 DB 정규 Tag("a,b")로 정규화
                if (!string.IsNullOrWhiteSpace(sNode.Tag))
                    r.StartEquipmentTag = sNode.Tag;
            }
            if (sPoc == null) return;
            r.StartPocTag = sPoc.Tag;
            r.StartPocName = sPoc.PocName;
            r.StartPocUtilityName = sPoc.UtilityName;
            r.StartPocUtilityGroup = sPoc.UtilityGroupName;
            
            double calcSize = sPoc.Size * 1.0; 
            r.StartPocSize = $"{(int)calcSize} mm";
            r.StartPocInformation = sPoc.Information;

            // [신규] S: Module Name
            r.StartPocModuleName = sPoc.ModuleName;

            // [신규] S: PocNo - PointID(PocID)로 DynamicProperties에서 조회
            var startDp = cache.GetDynamicProperties(sPoc.PointID);
            r.StartPocNo = startDp?.PocNo ?? "";

            // S: Connector Use — OWNERID = ConnectionPointInstanceLog.InstanceID
            var useDp = cache.GetDynamicProperties(sPoc.InstanceId);
            r.StartPocConnectorUse = DynamicPropertiesData.NormalizeConnectorUse(useDp?.ConnectorUse);

            r.StartEquipmentPZ = sNode.PZ;

            if (r.STATUS != "EMP")
            {
                r.StartPocConnectedUtility = GetFirstPipeUtility(cache, r, sPoc);
            }

            r.StartPocConnectionTag = sPoc.ReferenceTag; //26.05.27
        }

        private void MapTargetInfo(IGraphCache cache, TraceResult r, NodeInfo eNode, PocData ePoc, List<TraceResult> pathResults)
        {
            // [Case 1] EMP (Empty) - T열 모두 공백 처리
            if (r.STATUS == "EMP")
            {
                r.TargetNodeName = "";
                r.TargetNodeTag = "";
                r.TargetPocConnectionTag = "";
                r.TargetPocTag = "";
                r.TargetConstructionCode = "";
                r.TargetPocUtilityName = "";
                r.TargetNodeDivision = "";
                r.TargetNodeSubDivision1 = "";
                r.TargetNodeSubDivision2 = "";
                r.TargetNodeGUID = Guid.Empty;
                r.TargetPocName = string.Empty;
                return; // 종료
            }

            // [Case 2] TOF (Takeoff)
            if (r.STATUS == "TOF")
            {
                r.TargetNodeName = "TakeOff";       // [요청] T: Name 강제 지정
                r.TargetNodeDivision = "MEP";

                // TraceAlgorithm에서 전달한 Tag값 (EndName에 들어있음)
                r.TargetNodeTag = r.EndName; //TOF Takeoff tag 값

                // TOF는 Node/Poc 객체가 없을 수 있으므로 나머지는 공백
                //r.TargetPocUtilityName = ePoc.UtilityName;

                //r.TargetPocConnectedUtility = $"TOF ({r.StartPocUtilityName})";
                //r.TargetPocConnectedUtility = $"{r.TargetPocConnectedUtility}";
                r.TargetPocUtilityName = r.StartPocUtilityName;

                return; // 종료
            }

            // [Case 3] CAP
            if (r.STATUS == "CAP")
            {
                if (r.TargetNodeDivision != "Equipment")
                    r.TargetNodeDivision = "MEP";
                // CAP은 NodeInfo가 있을 수 있음 (EndName에 "Cap(이름)" 형태로 들어옴)
                r.TargetNodeName = r.EndName;
            }

            if (r.STATUS == "ING")
            {
                if (eNode == null || string.IsNullOrEmpty(r.TargetNodeName))
                {
                    r.TargetNodeName = "Open Pipe";
                }
            }

            // [Case 4] 일반 Node (NOZ, EQP, Fitting 등) - NodeInfo 기반 매핑
            if (eNode != null)
            {
                r.TargetNodeName = eNode.Name;
                r.TargetNodeTag = eNode.Tag;
                r.TargetNodeSubDivision1 = eNode.Sub1Name;
                r.TargetNodeSubDivision2 = eNode.Sub2Name;
                r.TargetConstructionCode = eNode.ConstructionCode;

                // Division Logic
                if (r.STATUS != "CAP" && r.STATUS != "TOF") // 위에서 처리 안 된 경우만
                {
                    if (eNode.Division == 0) r.TargetNodeDivision = "Equipment";
                    else if (eNode.Division == 3) r.TargetNodeDivision = "PlumbingComponent";
                    else r.TargetNodeDivision = eNode.Division.ToString();
                }
            }

            // [Case 5] End POC 정보 (T: Connection Tag, T: Utility)
            // NOZ 등의 경우 EndPoc가 존재함. S:Utility와 동일한 로직으로 가져옴.
            if (ePoc != null)
            {
                r.TargetPocConnectionTag = ePoc.ReferenceTag;
                r.TargetPocUtilityName = ePoc.UtilityName; // [Fix] S:Utility와 동일하게 가져옴

                r.TargetConnectorInformation = ePoc.Information;

                r.TargetPocGUID = ePoc.InstanceId.ToString();

                // [신규] T: Module Name
                r.TargetPocModuleName = ePoc.ModuleName;

                // [신규] T: PocNo - PointID로 DynamicProperties에서 조회
                var targetDp = cache.GetDynamicProperties(ePoc.PointID);
                r.TargetPocNo = targetDp?.PocNo ?? "";

                r.TargetPocName = ePoc.PocName; //26.07.08

                r.TargetPocTag = ePoc.Tag; //260717

                //if (r.STATUS == "NOZ")
                //    _logger.Information($"ePoc.UtilityName {ePoc.UtilityName} InstanceId:{ePoc.InstanceId} OwnerId:{ePoc.OwnerId}");
            }

            // [EQP 디버깅용 로직]
            if (r.STATUS == "EQP")
            {
                if (ePoc != null)
                {
                    // Case 2: 객체는 있는데 Tag가 비어있음
                    if (!string.IsNullOrEmpty(ePoc.ReferenceTag))
                    {
                        // 정상 케이스
                        r.TargetPocConnectionTag = ePoc.ReferenceTag;
                    }
                }
            }
            else
            {
                // EQP가 아닐 때 일반적인 매핑
                if (ePoc != null)
                {
                    r.TargetPocConnectionTag = ePoc.ReferenceTag;
                }
            }
            if (r.STATUS != "EMP" && r.STATUS != "ING") //26.05.03 ING 상태 값에 대해서도 
            {
                r.TargetPocConnectedUtility = GetLastPipeUtility(cache, r, ePoc);
            }
        }


        private string GetFirstPipeUtility(IGraphCache cache, TraceResult r, PocData poc)
        {
            if (r.Steps == null || r.Steps.Count == 0) return "";

            // Steps에서 첫 번째 Pipe 찾기
            var firstPipe = r.Steps.FirstOrDefault(s =>
                s.NodeType.Contains("PipeSeg"));

            //foreach (var step in r.Steps)
            //{
            //    _logger.Information($"[DEBUG]GetFirstPipeUtility {step.StepIndex} {step.NodeName} {step.NodeType} {step.NodeId}");
            //}

            //26.05.26 connected ut에 대해서 s5d 형식이 아니라 순수한 ut name만 남기도록 변경한다.
            //기존 로직은 백업 차원에서 보류
            /*if (firstPipe != null && cache.TryGetPipe(firstPipe.NodeId, out var pipe))
            {
                //_logger.Information($"[DEBUG] FOUND FIRST PIPE! GetFirstPipeUtility {pipe.PipeId} {pipe.UtilityName}");
                return $"{poc.PocName} ({pipe.UtilityName})";
            }
            return $"{poc.PocName}({poc.UtilityName})";
            */

            if (firstPipe != null && cache.TryGetPipe(firstPipe.NodeId, out var pipe))
            {
                return $"{pipe.UtilityName}";
            }
            return $"{poc.UtilityName}";
        }

        private string GetLastPipeUtility(IGraphCache cache, TraceResult r, PocData poc)
        {
            if (r.Steps == null || r.Steps.Count == 0) return "";

            // Steps에서 마지막 Pipe 찾기 (역순)
            var lastPipe = r.Steps.LastOrDefault(s =>
                s.NodeType.Contains("PipeSeg"));

            //foreach (var step in r.Steps)
            //{
            //    _logger.Information($"[DEBUG]GetLastPipeUtility [{step.StepIndex}] <{step.NodeType}> {step.NodeName} {step.NodeId} ");
            //}

            //26.05.26 connected ut에 대해서 s5d 형식이 아니라 순수한 ut name만 남기도록 변경한다.
            //기존 로직은 백업 차원에서 보류
            /*if (lastPipe != null && cache.TryGetPipe(lastPipe.NodeId, out var pipe))
            {
                //_logger.Information($"[DEBUG] FOUND LAST PIPE! GetLastPipeUtility {pipe.PipeId} {pipe.UtilityName}");

                return $"{poc.PocName} ({pipe.UtilityName})";
            }
            return $"{poc.PocName}({poc.UtilityName})";*/


            if (lastPipe != null && cache.TryGetPipe(lastPipe.NodeId, out var pipe))
            {
                return $"{pipe.UtilityName}";
            }
            return $"{poc.UtilityName}";
        }


        // ---------------------------------------------------------
        // [Validation Rules]
        // ---------------------------------------------------------
        //24.05.14 수정
        //검수제외(PASS) 로직
        private bool 검수제외PASS체크(TraceResult r)
        {
            if (r.StartEquipmentPZ > 100000) //10만 이상의 설비는 검수 제외
                return true;

            // Connector Use = N 은 미사용 POC — 검수 제외 (PASS)
            if (string.Equals(r.StartPocConnectorUse, "N", StringComparison.OrdinalIgnoreCase))
            {
                r.IsValidationTarget = false;
                return true;
            }

            // 01-0. 미 검수 항목 (제외항목)
            // Utility name / Group / Connecter Name에 Spare, 미사용 등 
            if (r.STATUS.Equals("EMP"))
            // && !poc.connectorUse.Equals("N")) //24.05.14 connector use N이어도 체크 대상
            {
                if (util.예외대상이름여부(r.StartPocUtilityName) ||
                    util.예외대상이름여부(r.StartPocName) ||
                    util.예외대상이름여부(r.StartPocUtilityGroup))
                {
                    //24.05.14 3차 검수 대상 포함
                    if (util.IsStringContains(r.StartPocUtilityGroup, "etc") && util.IsStringContains(r.StartPocUtilityName, "3rd"))
                        return false;

                    return true;
                }
            }           

            /*
            //26.05.26 신규 검수제외 룰 02
            // - T: Tag가 Tag1,Tag2 의 양식이고 T: Sub Division 2가 SCRUBBER일 경우 S: Tag가 Tag1 또는 Tag2 인 경우
            if (r.STATUS.Equals("EQP"))
            {
                if (EquipmentTagMatcher.IsScrubberSubdivision(r.TargetNodeSubDivision2)
                    && r.TargetNodeTag != null && r.TargetNodeTag.IndexOf(',') >= 0)
                {
                    if (EquipmentTagMatcher.AreRelated(r.TargetNodeTag, r.StartEquipmentTag)) return true;
                }
            }

            //26.05.26 신규 검수제외 룰 03 set            
            if (r.STATUS.Equals("NOZ"))
            {
                //1-1. S: Division = Equipment, 설비의 Z값이 <5400일 때, S: Utility가 GO2, T: Lateral Number가 O2인 경우
                if (r.StartEquipmentPZ < 5400 && r.StartPocUtilityName == "GO2" && r.TargetPocConnectedUtility == "O2") return true;
                //1 - 2.S: Division = Equipment, 설비의 Z값이 >= 5400일 때, S: Utility가 PO2, T: Lateral Number가 O2인 경우
                else if (r.StartEquipmentPZ >= 5400 && r.StartPocUtilityName == "PO2" && r.TargetPocConnectedUtility == "O2") return true;
                //2 - 1.S: Division = Equipment, S: Utility가 IWW1, T: Lateral Number가 FWW인 경우
                else if (r.StartPocUtilityName == "IWW1" && r.TargetPocConnectedUtility == "FWW") return true;
                //2 - 2.S: Division = Equipment, S: Utility가 IWW2, T: Lateral Number가 NFW인 경우
                else if (r.StartPocUtilityName == "IWW2" && r.TargetPocConnectedUtility == "NFW") return true;
                //3.S: Subdivison 2 = CMP, S: Utility Group = Exhaust, T: Lateral = 폐수
                else if (r.StartNodeSubDivision2 == "CMP" && util.IsStringContains(r.StartPocUtilityGroup, "exhaust") && util.IsStringContains(r.TargetNodeSubDivision2, "폐수")) return true;
                //4.S: Subdivision 1 = GCS 포함, S: Ut = SN2, T: Lateral Number가 GN2의 Lateral 인 경우
                else if (util.IsStringContains(r.StartNodeSubDivision1, "GCS") && r.StartPocUtilityName == "SN2" && r.TargetPocConnectedUtility == "GN2") return true;
                //5.S: Ut = GN2, T: Lateral의 Ut가 PN2(단, 반대의 경우 FAIL)
                else if (r.StartPocUtilityName == "GN2" && r.TargetPocConnectedUtility == "PN2") return true;
            }

            //26.05.26 신규 검수제외 룰 04
            //'- S: Subdivision 1 = GCS 포함, S: Ut = He(L), Status ING인 경우 (향후 S5D ISSUE 해결시 내용 변동)
            if (r.STATUS.Equals("ING"))
            {
                if (util.IsStringContains(r.StartNodeSubDivision1, "gcs") && r.StartPocUtilityName == "he") return true;
            }

            //26.05.26 신규 검수제외 룰 05 set
            if (r.STATUS.Equals("TOF"))
            {
                //1. S: Name = PUMP_LL 포함, S: Ut Group = Exhaust, S: UT = T: UT, S: Connected UT = Exhaust or VAC
                //(예: S: UT(HF)-- > S: Connected UT(Exhaust) --> T: Connected UT(HF) --> T: UT(HF)
                //S: UT(ACID)-- > S: Connected UT(VAC) --> T: Connected UT(ACID) --> T: UT(ACID)

                //if (util.IsStringContains(r.StartNodeName, "pump_ll") && util.IsStringContains(r.StartPocUtilityGroup, "exhaust") && )
            }      
            */


            return false;
        }

    }
}
