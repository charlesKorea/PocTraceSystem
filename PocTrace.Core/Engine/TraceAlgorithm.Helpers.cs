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
        private class TraceState
        {
            public Guid? CurrentOutlet { get; set; }
            public List<TraceStep> Steps { get; set; }
            public HashSet<Guid> VisitedNodes { get; set; } 
            public int AccumulatedLength { get; set; }
        }
        private void LogDetailed(string messageTemplate, params object[] propertyValues)
        {
            if (EnableDetailedDebug)
            {
                int threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                _logger.Information($"[Thread-{threadId}] {messageTemplate}", propertyValues);
            }
        }

        private bool IsCapName(string targetNodeName, string targetDiv, string targetSubDiv1, string targetSubDiv2)
        {
            if (string.IsNullOrEmpty(targetNodeName)) return false;
            if (targetNodeName.IndexOf("TEE", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (targetNodeName.IndexOf("Nozzle", StringComparison.OrdinalIgnoreCase) >= 0 &&
                targetSubDiv2.IndexOf("Nozzle", StringComparison.OrdinalIgnoreCase) >= 0) return false;

            if (targetDiv.IndexOf("equipment", StringComparison.OrdinalIgnoreCase) >= 0) return false;            

            var isCap = string.Equals(targetNodeName, "기타", StringComparison.OrdinalIgnoreCase) 
                   || targetNodeName.IndexOf("Valve Box 80A_B_Valve Box", StringComparison.OrdinalIgnoreCase) >= 0 //26.04.16 WENM03 case
                   || targetNodeName.IndexOf("cap", StringComparison.OrdinalIgnoreCase) >= 0
                   || targetSubDiv1.IndexOf("cap", StringComparison.OrdinalIgnoreCase) >= 0
                   || targetNodeName.IndexOf("vac-030", StringComparison.OrdinalIgnoreCase) >= 0
                   || targetNodeName.IndexOf("vac-012", StringComparison.OrdinalIgnoreCase) >= 0 //x1 taam03 kf-flange cap마감 처리
                   || targetNodeName.IndexOf("vac-013", StringComparison.OrdinalIgnoreCase) >= 0 //x1 slbm01 kf-flange cap마감 처리
                   || targetNodeName.IndexOf("bulk-057", StringComparison.OrdinalIgnoreCase) >= 0 //x1 dkam19 blind-kf-flange_sus_nw50 cap마감 처리
                   || (targetNodeName.IndexOf("nw25", StringComparison.OrdinalIgnoreCase) >= 0 && targetNodeName.IndexOf("pot", StringComparison.OrdinalIgnoreCase) >= 0)
                   ;

            if (isCap)
            {
                LogDetailed("    └─ ⚠️ [IsCapName] CAP 이름이 맞음! (Node: {Name}, T.Div: {Div}, T.SDiv1: {SDiv1}, T.SDiv2: {SDiv2})", 
                    targetNodeName, targetDiv, targetSubDiv1, targetSubDiv2);
            }

            return isCap;
        }

                private List<TraceStep> CloneSteps(List<TraceStep> source)
        {
            return source.Select(s => new TraceStep
            {
                StepIndex = s.StepIndex,
                NodeId = s.NodeId,
                NodeName = s.NodeName,
                NodeType = s.NodeType,
                OutletUsed = s.OutletUsed,
                QCode = s.QCode, 
                Length = s.Length,
                HookupNumber = s.HookupNumber,
                // [신규] 작업자 정보 복사
                OpTime = s.OpTime,
                UserName = s.UserName,
                CompanyName = s.CompanyName
            }).ToList();
        }

        private TraceResult CreateResult(string startTag, PocData startPoc, EndPointType type, ValidationStatus status, List<TraceStep> steps, int totalLength, 
            string endName, Guid? endPocId)
        {
            var result = new TraceResult
            {
                StartEquipmentTag = startTag,
                StartPocId = startPoc.InstanceId,
                EndPocId = endPocId, 
                EndType = type,
                IsMatched = status, 
                TotalLength = totalLength, 
                Steps = steps,
                EndName = endName ?? type.ToString(),
                TracedAt = DateTime.Now
            };

            string symbol;
            switch (type)
            {
                case EndPointType.Nozzle:
                    symbol = "🟢";
                    break;
                case EndPointType.Equipment:
                    symbol = "🔵";
                    break;
                case EndPointType.Cap:
                    symbol = "🟠";
                    break;
                case EndPointType.Broken:
                    symbol = "🔴"; 
                    break;
                case EndPointType.Empty:
                    symbol = "⚪"; 
                    break;
                case EndPointType.Takeoff:
                    symbol = "🟣"; 
                    break;
                case EndPointType.DataError:
                    symbol = "⚫"; 
                    break;
                default:
                    symbol = "❓";
                    break;
            }

            string statusStr = type == EndPointType.Broken ? "ING" : type.ToString().ToUpper();
            LogDetailed($"  {symbol} [RESULT: {statusStr}] 최종 상태 기록 완료! (EndName: {result.EndName}, EndPocId: {endPocId})");

            return result;
        }
    }
}
