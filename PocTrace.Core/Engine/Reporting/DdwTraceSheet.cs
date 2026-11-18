using System;
using System.Collections.Generic;
using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Infrastructure;
using PocTrace.Core.Infrastructure.Data;
using PocTrace.Core.Models;

namespace PocTrace.Core.Engine.Reporting
{
    /// <summary>
    /// x2_m 추적 시트. Testbench 추적 시트의 한 홉을 UI 경로 스텝으로 적는다. Num은 1부터다.
    /// </summary>
    public static class DdwTraceSheet
    {
        public static readonly string[] Headers =
        {
            "Start POC", "End Poc", "End Poc Instance id", "Num", "Start Poc Instance id",
            "End Type", "End Model Name", "Division", "Subdivision1", "Subdivision2",
            "Utility Group", "Utility", "Pipe-Size", "length",
            "pipe number", "tag", "connection tag", "hookup number"
        };

        public static List<object[]> Build(IGraphCache cache, IList<TraceResult> results)
        {
            var rows = new List<object[]>();
            if (cache == null || results == null || !DdwLine.IsCacheKey(cache.DbName))
                return rows;

            for (int r = 0; r < results.Count; r++)
            {
                var result = results[r];
                if (result == null || result.Steps == null)
                    continue;
                for (int i = 1; i < result.Steps.Count; i++)
                {
                    var prev = result.Steps[i - 1];
                    var cur = result.Steps[i];
                    if (cur == null)
                        continue;
                    Guid startPocId = ResolveStartPoc(cache, result, prev, cur, i);
                    Guid endPocId = ResolveEndPoc(cache, prev, cur, startPocId);
                    bool endPipe = IsPipe(cur);
                    string tag;
                    string connectionTag;
                    Guid attributeOwner;
                    ResolveAttributes(cache, cur, endPocId, out tag, out connectionTag, out attributeOwner);
                    rows.Add(new object[]
                    {
                        PocLabel(cache, startPocId),
                        EndLabel(cache, endPocId),
                        DdwId.Text(endPocId),
                        i,
                        DdwId.Text(startPocId),
                        EndType(cache, cur, endPipe),
                        EndModelName(cache, cur, endPipe),
                        DivisionText(cache, cur, endPipe),
                        endPipe ? "" : (cache.GetSubdivision1(cur.NodeId) ?? ""),
                        endPipe ? "" : (cache.GetSubdivision2(cur.NodeId) ?? ""),
                        UtilityGroup(cache, endPocId),
                        Utility(cache, endPocId, cur, endPipe),
                        PipeSize(cache, cur, endPipe),
                        endPipe ? PipeLength(cache, cur.NodeId) : 0d,
                        Prop(cache, attributeOwner, true),
                        tag,
                        connectionTag,
                        Prop(cache, attributeOwner, false)
                    });
                }
            }
            return rows;
        }

        private static Guid ResolveStartPoc(IGraphCache cache, TraceResult result, TraceStep prev, TraceStep cur, int index)
        {
            if (index == 1)
                return result.StartPocId;
            if (prev == null)
                return Guid.Empty;
            if (IsPipe(prev))
            {
                var ep = EndpointLeaving(cache, prev.NodeId, prev.OutletUsed, cur.NodeId);
                return ep == null ? Guid.Empty : ep.PocInstanceId;
            }
            PocData poc = PocOnOwner(cache, prev.NodeId, prev.OutletUsed, cur.NodeId);
            return poc == null ? Guid.Empty : poc.InstanceId;
        }

        private static Guid ResolveEndPoc(IGraphCache cache, TraceStep prev, TraceStep cur, Guid startPocId)
        {
            if (IsPipe(cur))
            {
                Guid from = startPocId != Guid.Empty ? startPocId : (prev == null ? Guid.Empty : prev.NodeId);
                var ep = EndpointArriving(cache, cur.NodeId, from, prev == null ? Guid.Empty : prev.NodeId);
                return ep == null ? Guid.Empty : ep.PocInstanceId;
            }
            if (prev != null && prev.OutletUsed.HasValue && cache.TryGetPoc(prev.OutletUsed.Value, out _))
                return prev.OutletUsed.Value;
            PocData poc = PocOnOwner(cache, cur.NodeId, prev == null ? (Guid?)null : prev.OutletUsed, Guid.Empty);
            return poc == null ? Guid.Empty : poc.InstanceId;
        }

        private static PipeEndpoint EndpointLeaving(IGraphCache cache, Guid pipeId, Guid? outlet, Guid nextId)
        {
            PipeData pipe;
            if (!cache.TryGetPipe(pipeId, out pipe) || pipe.Endpoints == null)
                return null;
            for (int i = 0; i < pipe.Endpoints.Count; i++)
            {
                var ep = pipe.Endpoints[i];
                if (ep == null || !ep.NormalizedTarget.HasValue)
                    continue;
                if ((outlet.HasValue && ep.NormalizedTarget.Value == outlet.Value) || ep.NormalizedTarget.Value == nextId)
                    return ep;
            }
            return null;
        }

        private static PipeEndpoint EndpointArriving(IGraphCache cache, Guid pipeId, Guid fromPoc, Guid fromNode)
        {
            PipeData pipe;
            if (!cache.TryGetPipe(pipeId, out pipe) || pipe.Endpoints == null)
                return null;
            for (int i = 0; i < pipe.Endpoints.Count; i++)
            {
                var ep = pipe.Endpoints[i];
                if (ep == null || !ep.NormalizedTarget.HasValue)
                    continue;
                if (ep.NormalizedTarget.Value == fromPoc || ep.NormalizedTarget.Value == fromNode)
                    return ep;
            }
            return null;
        }

        private static PocData PocOnOwner(IGraphCache cache, Guid ownerId, Guid? outlet, Guid nextId)
        {
            var list = cache.GetPocsByOwner(ownerId);
            if (list == null)
                return null;
            PocData first = null;
            foreach (var poc in list)
            {
                if (poc == null)
                    continue;
                if (first == null)
                    first = poc;
                if (outlet.HasValue && (poc.InstanceId == outlet.Value || (poc.Outlet.HasValue && poc.Outlet.Value == outlet.Value)))
                    return poc;
                if (poc.Outlet.HasValue && poc.Outlet.Value == nextId)
                    return poc;
            }
            return first;
        }

        private static bool IsPipe(TraceStep step)
        {
            return step != null && step.NodeType != null && step.NodeType.IndexOf("Pipe", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string PocLabel(IGraphCache cache, Guid pocId)
        {
            PocData poc;
            if (pocId != Guid.Empty && cache.TryGetPoc(pocId, out poc) && poc != null && !string.IsNullOrEmpty(poc.PocName))
                return poc.PocName;
            return "pipe";
        }

        private static string EndLabel(IGraphCache cache, Guid pocId)
        {
            return PocLabel(cache, pocId);
        }

        private static string EndType(IGraphCache cache, TraceStep step, bool endPipe)
        {
            if (endPipe)
            {
                PipeData pipe;
                if (!cache.TryGetPipe(step.NodeId, out pipe) || pipe.PipeType == 0)
                    return "";
                return DdwTypeCodes.PipeMeaning(pipe.PipeType) ?? "";
            }
            if (step.NodeType != null && step.NodeType.Equals("Takeoff", StringComparison.OrdinalIgnoreCase))
                return "takeoff";
            NodeInfo node;
            if (!cache.TryGetNode(step.NodeId, out node) || node.ModelType == 0)
                return "";
            return DdwTypeCodes.ModelMeaning(node.ModelType) ?? "";
        }

        private static string EndModelName(IGraphCache cache, TraceStep step, bool endPipe)
        {
            if (endPipe)
                return "pipe";
            if (!string.IsNullOrEmpty(step.NodeName))
                return step.NodeName;
            return cache.GetNodeName(step.NodeId) ?? "";
        }

        private static string DivisionText(IGraphCache cache, TraceStep step, bool endPipe)
        {
            if (endPipe)
                return "";
            NodeInfo node;
            if (cache.TryGetNode(step.NodeId, out node) && !string.IsNullOrEmpty(node.DivisionName))
                return node.DivisionName;
            return cache.GetDivision(step.NodeId) ?? "";
        }

        private static string UtilityGroup(IGraphCache cache, Guid pocId)
        {
            PocData poc;
            if (pocId != Guid.Empty && cache.TryGetPoc(pocId, out poc) && poc != null)
                return poc.UtilityGroupName ?? "";
            return "";
        }

        private static string Utility(IGraphCache cache, Guid pocId, TraceStep step, bool endPipe)
        {
            PocData poc;
            if (pocId != Guid.Empty && cache.TryGetPoc(pocId, out poc) && poc != null && !string.IsNullOrEmpty(poc.UtilityName))
                return poc.UtilityName;
            if (endPipe)
            {
                PipeData pipe;
                if (cache.TryGetPipe(step.NodeId, out pipe))
                    return pipe.UtilityName ?? "";
            }
            return "";
        }

        private static object PipeSize(IGraphCache cache, TraceStep step, bool endPipe)
        {
            if (!endPipe)
                return "";
            PipeData pipe;
            if (!cache.TryGetPipe(step.NodeId, out pipe) || pipe.PipeSizeMm <= 0)
                return "";
            return pipe.PipeSizeMm;
        }

        private static double PipeLength(IGraphCache cache, Guid pipeId)
        {
            PipeData pipe;
            if (!cache.TryGetPipe(pipeId, out pipe))
                return 0;
            return pipe.Length;
        }

        /// <summary>
        /// tag와 connection tag는 POC id. pipe number와 hookup number는 객체 id.
        /// 노즐 객체 id가 스텝에 있고, 스텝 id가 POC면 owner로 올린다.
        /// Takeoff는 그 POC의 connected poc를 한 번만 따라간 duct pipe id를 쓴다.
        /// </summary>
        private static void ResolveAttributes(IGraphCache cache, TraceStep step, Guid endPocId, out string tag, out string connectionTag, out Guid attributeOwner)
        {
            tag = "";
            connectionTag = "";
            attributeOwner = Guid.Empty;
            if (step == null)
                return;

            if (step.NodeType != null && step.NodeType.Equals("Takeoff", StringComparison.OrdinalIgnoreCase))
            {
                TakeoffData takeoff;
                if (cache.TryGetTakeoff(step.NodeId, out takeoff) && takeoff != null)
                {
                    tag = takeoff.Tag ?? "";
                    connectionTag = takeoff.ConnectionTag ?? "";
                    attributeOwner = takeoff.DuctPipeId;
                }
                return;
            }

            tag = PocTag(cache, endPocId);
            connectionTag = ConnectionTag(cache, endPocId);
            attributeOwner = step.NodeId;
            PocData poc;
            if (cache.TryGetPoc(step.NodeId, out poc) && poc != null && poc.OwnerId != Guid.Empty)
                attributeOwner = poc.OwnerId;
        }

        private static string PocTag(IGraphCache cache, Guid pocId)
        {
            PocData poc;
            if (pocId != Guid.Empty && cache.TryGetPoc(pocId, out poc) && poc != null)
                return poc.Tag ?? "";
            return "";
        }

        private static string ConnectionTag(IGraphCache cache, Guid pocId)
        {
            PocData poc;
            if (pocId != Guid.Empty && cache.TryGetPoc(pocId, out poc) && poc != null)
                return poc.ReferenceTag ?? "";
            return "";
        }

        private static string Prop(IGraphCache cache, Guid ownerId, bool pipeNumber)
        {
            var dp = cache.GetDynamicProperties(ownerId);
            if (dp == null)
                return "";
            return (pipeNumber ? dp.PipeNumber : dp.HookupNumber) ?? "";
        }
    }
}
