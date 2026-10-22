using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Engine.Validation;
using PocTrace.Core.Enums;
using PocTrace.Core.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PocTrace.Core.Engine.Logic
{
    public partial class TraceAlgorithm
    {
        private readonly IGraphCache _cache;
        private readonly ILogger _logger;
        private const int MAX_HOP_LIMIT = 500; 

        public bool EnableDetailedDebug { get; set; } = true; 

        public TraceAlgorithm(IGraphCache cache, ILogger logger)
        {
            _cache = cache;
            _logger = logger;
        }
        public List<TraceResult> TracePath(PocData startPoc, string startEquipTag)
        {
            var results = new List<TraceResult>();
            var queue = new Queue<TraceState>();

            var startNodeId = startPoc.OwnerId;
            var globalMinHops = new Dictionary<Guid, int>();

            var startStep = new TraceStep
            {
                StepIndex = 1,
                NodeId = startNodeId,
                NodeName = _cache.GetNodeName(startNodeId),
                NodeType = _cache.GetNodeType(startNodeId).ToString(),
                OutletUsed = startPoc.Outlet
            };

            TraceStep lastProcessedStep = startStep; 

            queue.Enqueue(new TraceState 
            { 
                CurrentOutlet = startPoc.Outlet, 
                Steps = new List<TraceStep> { startStep }, 
                VisitedNodes = new HashSet<Guid> { startNodeId }, 
                AccumulatedLength = 0 
            });

            LogDetailed("=================================================");
            LogDetailed("🚀 [탐색 시작][Enqueue] Start EQP: {StartTag}, POC: {PocId} -> Outlet: {Outlet}", startEquipTag, startPoc.InstanceId, startPoc.Outlet);
            LogDetailed("=================================================");

            while (queue.Count > 0)
            {
                var state = queue.Dequeue();
                
                LogDetailed("⬇️ [Dequeue] 큐 항목 꺼냄. (대상 Outlet: {Outlet}, 남은 큐: {Count}개)", state.CurrentOutlet?.ToString() ?? "NULL", queue.Count);

                try
                {
                    var currentOutlet = state.CurrentOutlet;
                    var currentSteps = state.Steps; 
                    var currentLen = state.AccumulatedLength;

                    if (currentOutlet == null)
                    {
                        LogDetailed("  🛑 [중단] CurrentOutlet이 NULL (Empty) => EMP ");
                        results.Add(CreateResult(startEquipTag, startPoc, EndPointType.Empty, ValidationStatus.NotApplicable, currentSteps, currentLen, "", Guid.Empty));
                        continue;
                    }

                    if (currentSteps.Count > MAX_HOP_LIMIT)
                    {
                        LogDetailed("  🛑 [중단] 제한 횟수(MAX HOP) 초과로 강제 종료합니다. => Data ERROR ");
                        results.Add(CreateResult(startEquipTag, startPoc, EndPointType.DataError, ValidationStatus.Error, currentSteps, currentLen, "Max Hops", Guid.Empty));
                        continue;
                    }

                    if (_cache.TryGetTakeoff(currentOutlet.Value, out var takeoff))
                    {
                        // [VOC-1] MAIN vent/vac/foreline → Water와 동일하게 BFS 큐로 하류 계속(TOF 미종료).
                        if (TryContinueFromMainLineTakeoff(
                            takeoff, state, queue, startPoc, startEquipTag, results,
                            ref lastProcessedStep, currentLen))
                        {
                            continue;
                        }

                        if (TryContinueFromWaterTakeoff(
                            takeoff, state, queue, startPoc, startEquipTag, results,
                            ref lastProcessedStep, currentLen))
                        {
                            continue;
                        }

                        LogDetailed("  🎯 [Priority 1] Takeoff 매칭: {Tag}", takeoff.Tag);
                                                
                        var takeoffStep = new TraceStep { StepIndex = currentSteps.Count + 1, NodeId = takeoff.TakeoffId, NodeName = $"Damper[{takeoff.Tag}]", NodeType = "Takeoff" };
                        currentSteps.Add(takeoffStep);
                        lastProcessedStep = takeoffStep;
                        bool isMatch = EquipmentTagMatcher.AreRelated(takeoff.Tag, startEquipTag);
                        results.Add(CreateResult(startEquipTag, startPoc, EndPointType.Takeoff, isMatch ? ValidationStatus.Match : ValidationStatus.Mismatch, currentSteps, currentLen, $"Damper[{takeoff.Tag}]", takeoff.TakeoffId));
                        continue; 
                    }

                    // =========================================================
                    // 🌟 1. POC (설비/피팅) 진입 로직
                    // =========================================================
                    if (_cache.TryGetPoc(currentOutlet.Value, out PocData targetPoc))
                    {
                        var nodeId = targetPoc.OwnerId;

                        LogDetailed("▶ [진입 시도] 진입점(Entry Outlet): {Outlet}, 대상 POC: {PocId}", currentOutlet.Value, targetPoc.InstanceId);
                        LogDetailed("    ├─ 대상 설비/피팅 ID: {NodeId}", nodeId);
                        LogDetailed("    ├─ 현재까지 방문한 ID 목록: [{VisitedList}]", string.Join(", ", state.VisitedNodes.Select(v => v.ToString().Substring(0, 5) + "...")));

                        if (state.VisitedNodes.Contains(nodeId))
                        {
                            if (nodeId == startPoc.OwnerId && targetPoc.InstanceId != startPoc.InstanceId)
                            {
                                LogDetailed("  🎯 [귀환/EQP] 출발 설비의 '다른' 포트({PocId})로 귀환했습니다! EQP로 정상 마감합니다.", targetPoc.InstanceId);
                                var returnStep = new TraceStep { StepIndex = currentSteps.Count + 1, NodeId = nodeId, NodeName = _cache.GetNodeName(nodeId), NodeType = "Equipment" };
                                currentSteps.Add(returnStep);
                                results.Add(CreateResult(startEquipTag, startPoc, EndPointType.Equipment, ValidationStatus.Match, currentSteps, currentLen, "Returned to Start EQP", targetPoc.InstanceId));
                                continue; 
                            }
                            else 
                            {
                                string visitedNodeName = _cache.GetNodeName(nodeId) ?? "Unknown";
                                LogDetailed("  🔄 [막힘/ING] 이미 방문한 설비/피팅입니다. (진짜 루프 차단): {Name}", visitedNodeName);
                                string endInfo = $"Loop Blocked at {visitedNodeName} (ID: {nodeId})";
                                results.Add(CreateResult(startEquipTag, startPoc, EndPointType.Broken, ValidationStatus.Error, currentSteps, currentLen, endInfo, targetPoc.InstanceId));
                            }
                            continue;
                        }

                        var nodeType = _cache.GetNodeType(nodeId);
                        var nodeName = _cache.GetNodeName(nodeId) ?? "";
                        string sub1 = _cache.GetSubdivision1(nodeId) ?? "";
                        string sub2 = _cache.GetSubdivision2(nodeId) ?? "";
                        string qCode = _cache.GetQCode(nodeId) ?? "";
                        string division = _cache.GetDivision(nodeId) ?? "";
                        string hookup = _cache.GetHookupNumber(nodeId) ?? ""; // 🌟 캐시에서 훅업 가져오기

                        LogDetailed($"▶ [진입] Target POC로의 탐색 진행! Target POC:{targetPoc.InstanceId}(Information:{targetPoc.Information})  (OwnerID:{nodeId}),  " +
                            $"Owner Node Type: {nodeType}, Node Name: {nodeName}, Sub1:{sub1}, Sub2:{sub2} QCdoe:{qCode} ");
                        
                        var newStep = new TraceStep
                        {
                            StepIndex = currentSteps.Count + 1,
                            NodeId = nodeId,
                            NodeName = nodeName,
                            NodeType = nodeType.ToString(),
                            QCode = qCode,
                            Division = division,
                            HookupNumber = hookup
                        };
                        currentSteps.Add(newStep);
                        lastProcessedStep = newStep; 

                        if (sub1.Equals("FLANGE", StringComparison.OrdinalIgnoreCase) && 
                            (sub2.Equals("MF FLANGE", StringComparison.OrdinalIgnoreCase) || sub2.Equals("KF FLANGE", StringComparison.OrdinalIgnoreCase)) &&
                            nodeName.IndexOf("END", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            LogDetailed("  🛑 [마감] 종단용 KF/MF FLANGE(END) 도달. CAP으로 정상 종료. ({Name})", nodeName);
                            var step = new TraceStep { StepIndex = currentSteps.Count + 1, NodeId = nodeId, NodeName = nodeName, NodeType = nodeType.ToString() };
                            currentSteps.Add(step);
                            results.Add(CreateResult(startEquipTag, startPoc, EndPointType.Cap, ValidationStatus.Match, currentSteps, currentLen, $"Cap({nodeName})", targetPoc.InstanceId));
                            continue; 
                        }
                                                
                        bool isChemicalTakeOff = (sub1.Replace(" ", "").Equals("TAKE-OFF", StringComparison.OrdinalIgnoreCase)) &&
                                                 (sub2.IndexOf("Nozzle(Chemical)", StringComparison.OrdinalIgnoreCase) >= 0 || 
                                                  sub2.IndexOf("Chemical", StringComparison.OrdinalIgnoreCase) >= 0);
                        if (isChemicalTakeOff)
                        {
                            LogDetailed("  🌟 [특수] TakeOff + Nozzle(Chemical) 감지! NOZ로 강제 종료. ({Name})", nodeName);
                            nodeType = NodeType.Nozzle; 
                        }

                        if (nodeName.IndexOf("미사용", StringComparison.OrdinalIgnoreCase) >= 0 && 
                            sub2.Equals("기타", StringComparison.OrdinalIgnoreCase))
                        {
                            LogDetailed("  🛑 [마감] '미사용' 및 '기타' 조건 도달. CAP으로 정상 종료. ({Name})", nodeName);
                            var step = new TraceStep { StepIndex = currentSteps.Count + 1, NodeId = nodeId, NodeName = nodeName, NodeType = nodeType.ToString() };
                            currentSteps.Add(step);
                            results.Add(CreateResult(startEquipTag, startPoc, EndPointType.Cap, ValidationStatus.Match, currentSteps, currentLen, $"Cap({nodeName})", targetPoc.InstanceId));
                            continue; 
                        }

                        if (IsCapName(nodeName, division, sub1, sub2))
                        {
                            LogDetailed("  🌟 [특수] IsCapName 감지! CAP으로 강제 종료. ({Name})", nodeName);
                            var step = new TraceStep { StepIndex = currentSteps.Count + 1, NodeId = nodeId, NodeName = nodeName, NodeType = nodeType.ToString() };
                            currentSteps.Add(step);
                            results.Add(CreateResult(startEquipTag, startPoc, EndPointType.Cap, ValidationStatus.Match, currentSteps, currentLen, $"Cap({nodeName})", targetPoc.InstanceId));
                            continue;
                        }

                        bool hasNozzleInfo = !string.IsNullOrEmpty(targetPoc.Information) && 
                                             targetPoc.Information.IndexOf("Nozzle", StringComparison.OrdinalIgnoreCase) >= 0;
                                                
                        if (nodeType == NodeType.Nozzle && !hasNozzleInfo)
                        {
                            LogDetailed("  ❌ [데이터 누락] Nozzle Node({Name}) 도달했으나 POC Information에 정의가 없음. ING 처리.", nodeName);
                            string endInfo = $"Missing Nozzle Info at {nodeName}";
                            results.Add(CreateResult(startEquipTag, startPoc, EndPointType.Broken, ValidationStatus.Error, currentSteps, currentLen, endInfo, targetPoc.InstanceId));
                            continue; 
                        }

                        if (isChemicalTakeOff || hasNozzleInfo)
                        {
                            LogDetailed("  🌟 [특수 마감] POC Information 정의({Info}) 또는 특수 룰에 의해 NOZ로 강제 종료합니다.", targetPoc.Information ?? "Chemical");
                            nodeType = NodeType.Nozzle; 
                        }
                                                
                        if (nodeType == NodeType.Nozzle)
                        {
                            LogDetailed("  ✅ [종단] [NOZ] Nozzle 도착");
                            bool isMatch = EquipmentTagMatcher.AreRelated(targetPoc.ReferenceTag, startEquipTag);
                            string endName = targetPoc.ReferenceTag;
                            if (isChemicalTakeOff) endName = $"TakeOff_Nozzle({targetPoc.ReferenceTag ?? "Chemical"})";
                            else if (hasNozzleInfo) endName = $"Info_Nozzle({nodeName})"; 

                            results.Add(CreateResult(startEquipTag, startPoc, EndPointType.Nozzle, isMatch ? ValidationStatus.Match : ValidationStatus.Mismatch, currentSteps, currentLen, endName, targetPoc.InstanceId));
                            continue;
                        }
                        else if (nodeType == NodeType.Equipment)
                        {
                            LogDetailed("  ⚠️ [도착] Equipment 도착 ");
                            results.Add(CreateResult(startEquipTag, startPoc, EndPointType.Equipment, ValidationStatus.Warning, currentSteps, currentLen, targetPoc.ReferenceTag, targetPoc.InstanceId));
                            continue;
                        }
                        else if (nodeType == NodeType.Damper || nodeType == NodeType.Fitting || nodeType == NodeType.Pipe)
                        {
                            LogDetailed("  🔍 [관통] {Type} 판정. 해당 설비의 전체 포트를 탐색합니다...", nodeType);
                            
                            int currentHop = currentSteps.Count + 1; 
                            if (globalMinHops.TryGetValue(nodeId, out int minHop))
                            {
                                if (currentHop > minHop + 2)
                                {
                                    LogDetailed("  🌊 [파동 상쇄] 경유지({NodeName})에서 최단 경로 대비 역류 감지. 링(Ring) 순환 스레드를 조용히 소멸시킵니다.", nodeName);
                                    continue; 
                                }
                                if (currentHop < minHop) globalMinHops[nodeId] = currentHop;
                            }
                            else
                            {
                                globalMinHops[nodeId] = currentHop; 
                            }
                                                        
                            var newVisitedNodes = new HashSet<Guid>(state.VisitedNodes);
                            newVisitedNodes.Add(nodeId);
                                                        
                            ExecutePassThroughBFS(targetPoc, currentSteps, queue, startEquipTag, startPoc, results, nodeName, nodeType, currentLen, 
                                newVisitedNodes, sub1, sub2, division);
                        }
                        else
                        {
                            LogDetailed("  ❌ [오류] 알 수 없는 노드(Unknown Node) 발생: {Name}", nodeName);
                            results.Add(CreateResult(startEquipTag, startPoc, EndPointType.DataError, ValidationStatus.Error, currentSteps, currentLen, $"Unknown Node: {nodeName}", targetPoc.InstanceId));
                        }
                    }
                    // =================================================================
                    // 2. 배관 (Pipe) 진입 로직
                    // =================================================================
                    else if (_cache.TryGetPipe(currentOutlet.Value, out PipeData pipe))
                    {
                        if (state.VisitedNodes.Contains(pipe.PipeId))
                        {
                            LogDetailed("  🔄 [스킵] 이미 방문한 배관입니다. {currentOutlet.Value} => (역류/U턴 차단): {PipeId}", currentOutlet.Value, pipe.PipeId);
                            continue;
                        }

                        int currentHop = currentSteps.Count + 1;
                        if (globalMinHops.TryGetValue(pipe.PipeId, out int minHop))
                        {
                            if (currentHop > minHop + 2)
                            {
                                LogDetailed("  🌊 [파동 상쇄] 배관에서 링(Ring) 역류 파동 충돌 감지. 스레드 소멸.");
                                continue;
                            }
                            if (currentHop < minHop) globalMinHops[pipe.PipeId] = currentHop;
                        }
                        else
                        {
                            globalMinHops[pipe.PipeId] = currentHop;
                        }

                        var newVisitedNodes = new HashSet<Guid>(state.VisitedNodes);
                        newVisitedNodes.Add(pipe.PipeId);
                        
                        LogDetailed("▶ [배관 진입] Pipe: {PipeId}", pipe.PipeId);
                        int segmentLen = (pipe.Length > 0) ? (int)Math.Floor(pipe.Length * 1000) : 0;
                        int newTotalLen = currentLen + segmentLen;

                        string pipeHookup = _cache.GetHookupNumber(pipe.PipeId) ?? ""; // 🌟 캐시에서 배관 훅업 가져오기

                                                var newStep = new TraceStep { 
                            StepIndex = currentSteps.Count + 1, 
                            NodeId = pipe.PipeId, 
                            NodeName = "", 
                            NodeType = "PipeSeg", 
                            Length = segmentLen,
                            HookupNumber = pipeHookup,
                            // [신규] 작업자 정보
                            OpTime = pipe.OpTime,
                            UserName = pipe.UserName,
                            CompanyName = pipe.CompanyName
                        };
                        currentSteps.Add(newStep);
                        lastProcessedStep = newStep; 

                        var rawExits = PipeEndpointTraversal.ConnectedTargets(pipe).ToList();

                        var resolvedPipeExits = new List<(Guid Outlet, Guid TargetId)>();
                                                
                        foreach (var exit in rawExits.Distinct())
                        {
                            Guid targetId = exit;
                            if (_cache.TryGetPipe(exit, out var nextPipe)) targetId = nextPipe.PipeId;
                            else if (_cache.TryGetPoc(exit, out var nextPoc))
                            {
                                // 🌟 [치명적 버그 수정 1] 다중 간선 압축 시, 출발 설비 한정 초정밀 분리
                                // 출발 설비로 향할 때는 구멍(InstanceId)을 기준으로 분리하여 유효 EQP 귀환이 루프에 섞여 압축되는 것을 방지!
                                targetId = (nextPoc.OwnerId == startPoc.OwnerId) ? nextPoc.InstanceId : nextPoc.OwnerId;
                            }
                            resolvedPipeExits.Add((exit, targetId));
                        }
                                                
                        var deduplicatedExits = resolvedPipeExits.GroupBy(x => x.TargetId).Select(g => g.First().Outlet).ToList();
                                                
                        var validExits = new List<Guid?>();
                        bool enqueuedAnyForPipe = false; 

                        foreach (var exit in deduplicatedExits)
                        {
                            if (_cache.TryGetPoc(exit, out var poc))
                            {
                                if (poc.OwnerId == startPoc.OwnerId)
                                {
                                    if (poc.InstanceId != startPoc.InstanceId)
                                    {
                                        var returnSteps = CloneSteps(currentSteps);
                                        returnSteps.Add(new TraceStep { 
                                            StepIndex = returnSteps.Count + 1, 
                                            NodeId = poc.OwnerId, 
                                            NodeName = _cache.GetNodeName(poc.OwnerId), 
                                            NodeType = "Equipment" 
                                        });

                                        LogDetailed($"  출발 설비 회귀 [EQP마감] 배관 진입 이후 다음 목적지가 출발 설비인지 확인 완료 - 큐 등록 차단 {exit}");
                                        enqueuedAnyForPipe = true; 
                                        results.Add(CreateResult(startEquipTag, startPoc, EndPointType.Equipment, ValidationStatus.Match, returnSteps, 
                                            newTotalLen, "Returned to Start EQP", poc.InstanceId));
                                    }
                                    continue; 
                                }
                            }
                                                
                            Guid targetId = exit;
                            bool isLoop = false;
                            
                            if (_cache.TryGetPoc(exit, out var nextPoc))
                            {
                                targetId = nextPoc.OwnerId;
                                if (nextPoc.OwnerId == startPoc.OwnerId && nextPoc.InstanceId != startPoc.InstanceId)
                                {
                                    isLoop = false; 
                                    LogDetailed("    └─ ↳ [통과] 배관에서 출발 설비의 다른 POC({NextPocId})로 향하는 합법적 귀환 루트를 확인했습니다.", nextPoc.InstanceId);
                                }
                                else
                                {
                                    isLoop = state.VisitedNodes.Contains(nextPoc.OwnerId);
                                }
                            }
                            else if (_cache.TryGetPipe(exit, out var nextPipe))
                            {
                                targetId = nextPipe.PipeId;
                                isLoop = state.VisitedNodes.Contains(nextPipe.PipeId);
                            }
                            else if (_cache.TryGetTakeoff(exit, out var exitTakeoff))
                            {
                                targetId = exitTakeoff.TakeoffId;
                                isLoop = state.VisitedNodes.Contains(exitTakeoff.TakeoffId);
                            }

                            if (isLoop)
                            {
                                LogDetailed("    └─ ↳ [미리보기] [사전 차단] 현재 배관에서 나갈 포트({Outlet})는 이미 지나온 Node 혹은 Pipe (역류 방향)입니다.", exit);
                            }
                            else
                            {
                                validExits.Add(exit); 
                            }
                        }

                        foreach (var exit in validExits)
                        {
                            var newPathList = CloneSteps(currentSteps);
                            newPathList.Last().OutletUsed = exit;
                            queue.Enqueue(new TraceState { CurrentOutlet = exit, Steps = newPathList, VisitedNodes = newVisitedNodes, AccumulatedLength = newTotalLen });
                            enqueuedAnyForPipe = true; 
                            
                            LogDetailed("    └─ ↳ [전진][Enqueue] 배관({PipeId})에서 다음 연결({Outlet}) 큐에 등록 완료. 끝점 {EndpointCount}개, 순번 [{Orders}]",
                                pipe.PipeId,
                                exit,
                                pipe.Endpoints.Count,
                                pipe.DescribeOrders());
                        }

                        if (!enqueuedAnyForPipe)
                        {
                            if (deduplicatedExits.Count <= 1)
                            {
                                LogDetailed("  ❌ [오류/설계미완성] Pipe의 연결 끝이 허공입니다 (Open Pipe). Pipe ID: {PipeId}", pipe.PipeId);
                                string endInfo = $"Open Pipe (ID: {pipe.PipeId})";
                                results.Add(CreateResult(startEquipTag, startPoc, EndPointType.Broken, ValidationStatus.Error, currentSteps, newTotalLen, endInfo, pipe.PipeId));
                            }
                            else
                            {
                                LogDetailed("  🔄 [순환 마감] Pipe의 모든 연결이 이미 지나온 경로(루프)입니다. 에러 없이 스레드를 소멸시킵니다. Pipe ID: {PipeId}", pipe.PipeId);
                            }
                        }
                    }
                    else
                    {
                        if (currentSteps.Count == 1)
                        {
                            LogDetailed("  🛑 [중단] 시작 POC의 첫 번째 연결 대상이 Orphan입니다. EMP로 처리합니다.");
                            results.Add(CreateResult(startEquipTag, startPoc, EndPointType.Empty, ValidationStatus.NotApplicable, currentSteps, 
                                currentLen, "Empty Start (Orphan)", currentOutlet));
                        }
                        else
                        {
                            // 🌟 [데이터 무결성 예외 룰] Diaphragm Valve 종속 시스템 DB 누락 방어
                            // 직전 노드(현재 스레드가 막 출발한 곳)의 이름을 확인합니다.
                            var lastStep = currentSteps.LastOrDefault();
                            string lastNodeName = lastStep?.NodeName ?? "";

                            // 이름에 "diaphragm valve"가 포함되어 있다면, 이는 DB 매핑 자체가 유실된 알려진 오류이므로 에러를 뱉지 않습니다.
                            if (lastNodeName.IndexOf("diaphragm valve(vcr)", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                LogDetailed("  👻 [고스트 패스 우회] Diaphragm Valve({Name})에서 파생된 연결({Outlet})은 종속 DB 한계(누락)로 간주하여 에러(ING) 없이 스레드를 자연 소멸시킵니다.", lastNodeName, currentOutlet);
                                continue; // 💡 results.Add를 호출하지 않고 스레드 조용히 종료!
                            }

                            // 일반적인 다른 배관/피팅에서 발생한 고아 객체는 명백한 오류(ING)로 처리합니다.
                            LogDetailed("  ❌ [오류/ING] 대상 객체가 DB에 존재하지 않습니다 (Ghost/Orphan).//26.04.16 WWIMU6 ING 부족 이슈 ");
                            results.Add(CreateResult(startEquipTag, startPoc, EndPointType.Broken, ValidationStatus.Error, currentSteps, currentLen, 
                                "Open Pipe (Orphan)", currentOutlet));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "🚨 [치명적 오류] 해당 경로 탐색 중 예외 발생 (스레드 보호됨). 브랜치 소멸 방지 기록 추가.");
                    results.Add(CreateResult(startEquipTag, startPoc, EndPointType.DataError, ValidationStatus.Error, state.Steps, state.AccumulatedLength, "Crash: " + ex.Message, Guid.Empty));
                }
            } 

            string sourceUtilGroup = startPoc.UtilityGroupName ?? ""; 
            
            var itemsToRemove = results.Where(r => 
            {
                if (r.STATUS != "ING") return false;
                var lastStep = r.Steps.LastOrDefault();
                if (lastStep == null) return false;
                
                string targetSub1 = _cache.GetSubdivision1(lastStep.NodeId) ?? "";
                if (targetSub1.Equals("REGULATOR", StringComparison.OrdinalIgnoreCase)) return true;
                if (targetSub1.Equals("GLAND", StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }).ToList();

            if (itemsToRemove.Count > 0)
            {
                if (results.Count - itemsToRemove.Count == 0)
                {
                    LogDetailed("  🚨 [필터 보류] 필터링 적용 시 결과가 0개가 됩니다! 정답지(ING) 누락을 막기 위해 1건을 보존합니다.");
                    foreach (var item in itemsToRemove.Skip(1)) results.Remove(item);
                }
                else
                {
                    //foreach (var item in itemsToRemove) results.Remove(item);
                    LogDetailed("  🧹 [필터링] REGULATOR & GLAND 조건으로 {Count}개의 ING 데이터를 제거했습니다.", itemsToRemove.Count);
                    LogDetailed("  🧹 [필터링] 취소합니다. ING 살려둡니다 26.04.21 DKAM19 Case", itemsToRemove.Count); //
                }
            }

            int changedValveCount = 0;
            foreach (var r in results.Where(x => x.STATUS == "ING"))
            {
                var lastStep = r.Steps.LastOrDefault();
                if (lastStep != null)
                {
                    string targetSub1 = _cache.GetSubdivision1(lastStep.NodeId) ?? "";
                    string targetSub2 = _cache.GetSubdivision2(lastStep.NodeId) ?? "";
                    string targetName = _cache.GetNodeName(lastStep.NodeId) ?? "";

                    if (targetName.IndexOf("미사용", StringComparison.OrdinalIgnoreCase) >= 0 && 
                        targetName.IndexOf("POC", StringComparison.OrdinalIgnoreCase) >= 0 && 
                        targetSub1.Equals("기타", StringComparison.OrdinalIgnoreCase))
                    {
                        LogDetailed($"  🔧 [보정] 이름 내 미사용 POC (기타) 조건으로 ING 데이터를 CAP으로 로 변경했습니다. 부합조건:{targetName} {targetSub1}");
                        r.EndType = EndPointType.Cap;
                        changedValveCount++;
                    }
                    else if (targetName.IndexOf("마감", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        LogDetailed($"  🔧 [보정] 이름 내 마감 조건으로 ING 데이터를 CAP으로 로 변경했습니다. 부합조건:{targetName}");
                        r.EndType = EndPointType.Cap;
                        changedValveCount++;
                    }
                    else if (targetName.IndexOf("END", StringComparison.OrdinalIgnoreCase) >= 0 //26.04.16 END 이름에 있어야 보정 의미 있음
                        && targetSub1.Equals("FLANGE", StringComparison.OrdinalIgnoreCase)  
                        && targetSub2.Equals("KF FLANGE", StringComparison.OrdinalIgnoreCase))
                    {
                        LogDetailed($"  🔧 [보정] SubDivision 내 FLANGE/KF FLANGE 조건으로 ING 데이터를 CAP으로 로 변경했습니다. 부합조건:{targetSub1} {targetSub2}");
                        r.EndType = EndPointType.Cap;
                        changedValveCount++;
                    }
                    else if (targetSub1.Equals("기타", StringComparison.OrdinalIgnoreCase) 
                        && targetName.IndexOf("TEE_BOX", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        LogDetailed($"  🔧 [보정] SubDivision 내 기타 + 이름 내 TEE_BOX 조건으로 ING 데이터를 NOZ 로 변경했습니다. 부합조건:{targetSub1} {targetName}");
                        r.EndType = EndPointType.Nozzle;
                        changedValveCount++;
                    }
                }
            }

            if (changedValveCount > 0)
            {
                LogDetailed("  🔧 [보정] 엣지 케이스 조건으로 {Count}개의 ING 데이터를 CAP/NOZ으로 변경했습니다.", changedValveCount);
            }

            int beforeMergeCount = results.Count;

            var groupedResults = results.GroupBy(r => new
            {
                Status = r.EndType, 
                TargetNodeId = r.Steps.LastOrDefault()?.NodeId ?? Guid.Empty,
                TargetPocId = r.EndPocId ?? Guid.Empty, 
                EntryConnection = r.Steps.Count > 1 ? r.Steps[r.Steps.Count - 2].OutletUsed : Guid.Empty
            }).ToList();

            var finalResults = new List<TraceResult>();
            foreach (var g in groupedResults)
            {
                if (g.Count() > 1)
                {
                    string statusList = string.Join(", ", g.Select(x => x.EndType.ToString().ToUpper()));
                    LogDetailed("  🗜️ [물리적 압축] 다이아몬드 토폴로지 중복 도달 병합 (TargetNode: {Node}, TargetPoc: {Poc}). 압축 내역: [{Statuses}] -> 1개의 {FinalStatus}로 압축됨.", 
                        g.Key.TargetNodeId, g.Key.TargetPocId, statusList, g.Key.Status.ToString().ToUpper());
                }
                
                finalResults.Add(g.OrderBy(r => r.TotalLength).First());
            }
            results = finalResults;

            if (results.Count == 0)
            {
                var fallbackNodeId = lastProcessedStep.NodeId;
                var fallbackNodeName = string.IsNullOrEmpty(lastProcessedStep.NodeName) ? "Pipe/Unknown" : lastProcessedStep.NodeName;

                LogDetailed("  🔥 [경고] 탐색된 모든 경로가 소멸하여 결과가 0개입니다. 마지막 도달 지점을 ING로 기록합니다. (Node: {Name})", fallbackNodeName);
                string endInfo = $"All Paths Vanished. Last Point: {fallbackNodeName} (ID: {fallbackNodeId})";
                
                results.Add(CreateResult(startEquipTag, startPoc, EndPointType.Broken, ValidationStatus.Error, new List<TraceStep> { lastProcessedStep }, 0, endInfo, Guid.Empty));
            }

            // [VOC-1] MainLine: EQP 도달 시 그 외 경로 결과 폐기, EQP 1건만 반환
            results = PreferMainLineEqpResult(startPoc, results);
            
            LogDetailed("✅ [종료] Trace 완료. 최종 반환된 경로 수: {ResultCount}", results.Count);
            return results;
        }
    }
}
