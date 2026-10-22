using PocTrace.Core.Engine.Cache;
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
        private void ExecutePassThroughBFS(PocData currentPoc, List<TraceStep> currentSteps, Queue<TraceState> queue,
            string startTag, PocData startPoc, List<TraceResult> results, 
            string nodeName, NodeType nodeType, int currentLen, HashSet<Guid> visitedNodes,
            string targetSub1, string targetSub2, string targetDiv)
        {
            var allPocs = _cache.GetPocsByOwner(currentPoc.OwnerId).ToList();
            LogDetailed("    └─ 조회된 전체 POC 개수(진입점 포함): {Count}개", allPocs.Count);

            var resolvedExits = new List<(PocData Poc, Guid TargetId)>();        
            foreach (var p in allPocs.Where(x => x.Outlet.HasValue))
            {
                Guid targetId = p.Outlet.Value; 
                if (_cache.TryGetPipe(p.Outlet.Value, out var nextPipe)) targetId = nextPipe.PipeId;
                else if (_cache.TryGetPoc(p.Outlet.Value, out var nextPoc))
                {
                    // 🌟 [치명적 버그 수정 2] 다중 간선 압축 시, 출발 설비 한정 초정밀 분리
                    targetId = (nextPoc.OwnerId == startPoc.OwnerId) ? nextPoc.InstanceId : nextPoc.OwnerId;
                }

                resolvedExits.Add((p, targetId));
            }
                        
            var uniqueExitPaths = resolvedExits
                .GroupBy(x => x.TargetId)
                .Select(g => 
                {
                    if (g.Count() > 1) {
                        LogDetailed("    ├─ 🛡️ [다중 간선 차단] 동일한 물리적 대상(Target ID: {Target})으로 향하는 중복 포트 {Count}개를 1개로 압축합니다.", g.Key, g.Count());
                    }
                    return g.First().Poc;
                })
                .ToList();

            LogDetailed("    └─ 물리적 중복 제거 후 유효 출구 경로: {Count}개", uniqueExitPaths.Count);
            bool enqueuedAnyValidPath = false;
            int nullOutletCount = allPocs.Count(p => !p.Outlet.HasValue); 
            int blockedByVisitedCount = 0;
            string lastEmptyPocName = "Unknown";

            if (allPocs.Count == 0)
            {
                bool isCapName = IsCapName(nodeName, targetDiv, targetSub1, targetSub2); 
                if (isCapName)
                {
                    LogDetailed("    └─ 🛑 Cap으로 정상 마감");
                    results.Add(CreateResult(startTag, startPoc, EndPointType.Cap, ValidationStatus.Match, currentSteps, currentLen, $"Cap({nodeName})", currentPoc.InstanceId));
                }
                else
                {
                    LogDetailed("    └─ ❌ [오류/ING] Sibling이 0개입니다. 더 이상 전진할 수 없어 Broken 처리됩니다. (노드명: {Name})", nodeName);
                    string endNameMsg = nodeName.IndexOf("Take Off", StringComparison.OrdinalIgnoreCase) >= 0 ? $"Blocked at Take Off" : "Blocked Component";
                    results.Add(CreateResult(startTag, startPoc, EndPointType.Broken, ValidationStatus.Error, currentSteps, currentLen, endNameMsg, currentPoc.InstanceId));
                }
                return;
            }

            foreach (var exitPoc in uniqueExitPaths)                        
            {
                if (!exitPoc.Outlet.HasValue) continue;
                
                if (_cache.TryGetPoc(exitPoc.Outlet.Value, out var poc))
                {
                    if (poc.OwnerId == startPoc.OwnerId)
                    {
                        if (poc.InstanceId != startPoc.InstanceId)
                        {
                            var returnSteps = CloneSteps(currentSteps);
                            var returnNodeId = poc.OwnerId;
                            returnSteps.Add(new TraceStep { 
                                StepIndex = returnSteps.Count + 1, 
                                NodeId = returnNodeId, 
                                NodeName = _cache.GetNodeName(returnNodeId), 
                                NodeType = "Equipment" 
                            });

                            LogDetailed($"    └─ ↳ !!출발 설비 회귀 [EQP마감] ExecutePassThroughBFS 진입 이후 다음 목적지가 출발 설비인지 확인 완료 - 큐 등록 차단 {exitPoc.InstanceId}");
                            enqueuedAnyValidPath = true; 
                            results.Add(CreateResult(startTag, startPoc, EndPointType.Equipment, ValidationStatus.Match, returnSteps, currentLen, "Returned to Start EQP", poc.InstanceId));
                        }
                        continue; 
                    }
                }
                        
                if (exitPoc.Outlet.HasValue)
                {
                    LogDetailed("    ├─ [검증] 나갈 구멍 Outlet({Outlet}) 검사 중...", exitPoc.Outlet.Value);
                    bool isLoop = false;
                    
                    if (_cache.TryGetPipe(exitPoc.Outlet.Value, out var nextPipe))
                    {
                        isLoop = visitedNodes.Contains(nextPipe.PipeId);
                    }
                    else if (_cache.TryGetPoc(exitPoc.Outlet.Value, out var nextPoc))
                    {
                        if (nextPoc.OwnerId == startPoc.OwnerId && nextPoc.InstanceId != startPoc.InstanceId)
                        {
                            isLoop = false; 
                            LogDetailed("    │  └─ [통과] 출발 설비의 다른 POC({NextPocId})로 향하는 합법적 귀환 루트를 확인했습니다.", nextPoc.InstanceId);
                        }
                        else
                        {
                            isLoop = visitedNodes.Contains(nextPoc.OwnerId);
                        }
                    }
                    else if (_cache.TryGetTakeoff(exitPoc.Outlet.Value, out var nextTakeoff))
                    {
                        isLoop = visitedNodes.Contains(nextTakeoff.TakeoffId);
                    }

                    if (isLoop)
                    {
                        blockedByVisitedCount++;
                        LogDetailed("    └─ ↳ [사전 차단] {NodeName} 에서 포트({PocName})로의 연결({Outlet})은 이미 지나온 설비/배관(역류)입니다.",  
                            nodeName, exitPoc.PocName ?? "NoName", exitPoc.Outlet);
                        continue; 
                    }

                    var newPathList = CloneSteps(currentSteps);
                    newPathList.Last().OutletUsed = exitPoc.Outlet;

                    queue.Enqueue(new TraceState { CurrentOutlet = exitPoc.Outlet, Steps = newPathList, VisitedNodes = visitedNodes, AccumulatedLength = currentLen });
                    enqueuedAnyValidPath = true; 
                    LogDetailed("    └─ ↳ [전진][Enqueue] {NodeName} 에서'{PocName}' 포트를 통해 다음 연결({Outlet}) 큐에 등록 완료.", nodeName, exitPoc.PocName ?? "NoName", exitPoc.Outlet.Value);
                }
                else
                {
                    nullOutletCount++;
                    lastEmptyPocName = exitPoc.PocName ?? "NoName";
                    LogDetailed("    └─ ↳ [빈 포트] '{PocName}' 포트에 연결된 배관이 없습니다.", lastEmptyPocName);
                }
            }

            if (!enqueuedAnyValidPath)
            {
                var emptyPoc = allPocs.FirstOrDefault(p => !p.Outlet.HasValue);
                lastEmptyPocName = emptyPoc?.PocName ?? "Unknown";
                bool isCapName = IsCapName(nodeName, targetDiv, targetSub1, targetSub2);
                if (isCapName)
                {
                    results.Add(CreateResult(startTag, startPoc, EndPointType.Cap, ValidationStatus.Match, currentSteps, currentLen, $"Cap({nodeName})", currentPoc.InstanceId));
                }
                else
                {
                    if (nullOutletCount == 0 && blockedByVisitedCount > 0)
                    {
                        LogDetailed("    └─ ⚠️ [경고/ING] 모든 연결이 이미 지나온 경로입니다. 루프에 의해 막힘. (Node: {Name}, ID: {Id})", nodeName, currentPoc.OwnerId);
                        string endInfo = $"Blocked by Loop at {nodeName} (ID: {currentPoc.OwnerId})";
                        results.Add(CreateResult(startTag, startPoc, EndPointType.Broken, ValidationStatus.Error, currentSteps, currentLen, endInfo, currentPoc.InstanceId));
                    }
                    else
                    {
                        LogDetailed("    └─ ⚠️ [경고/ING] 전진 가능한 배관이 끊겨 있습니다. (Node: {Name}, ID: {Id})", nodeName, currentPoc.OwnerId);
                        string endInfo = $"Open Port: {lastEmptyPocName} at {nodeName} (ID: {currentPoc.OwnerId})";
                        results.Add(CreateResult(startTag, startPoc, EndPointType.Broken, ValidationStatus.Error, currentSteps, currentLen, endInfo, currentPoc.InstanceId));
                    }
                }
            }
        }
    }
}
