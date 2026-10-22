using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Engine.Validation;
using PocTrace.Core.Enums;
using PocTrace.Core.Models;
using PocTrace.Core.Models.ReverseTrace;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PocTrace.Core.Engine.ReverseTrace
{
    /// <summary>
    /// Nozzle → Fitting → Pipe → … → Equipment 역BFS.
    /// Fitting fan-out은 정방향 BFS처럼 동일 물리 Target(Pipe/Owner)당 1회만 enqueue.
    /// 분기 상태는 Steps/Visited를 clone — sibling이 같은 HashSet을 공유하면 서로 다른 경로가 잘린다.
    /// </summary>
    public sealed class ReverseTraceEngine
    {
        private const int MaxHopLimit = 500;

        private readonly IGraphCache _cache;
        private readonly ReverseGraphIndex _reverseIndex;
        private readonly ILogger _logger;

        public ReverseTraceEngine(IGraphCache cache, ReverseGraphIndex reverseIndex, ILogger logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _reverseIndex = reverseIndex ?? throw new ArgumentNullException(nameof(reverseIndex));
            _logger = logger;
        }

        public List<ReverseTraceResult> TraceFromStartPoc(
            NozzleTraceInput input,
            StartPocContext startContext)
        {
            if (startContext?.StartPoc == null || startContext.OriginNode == null)
                return new List<ReverseTraceResult>();

            var results = new List<ReverseTraceResult>();
            var queue = new Queue<ReverseTraceState>();

            var nozzleNodeId = startContext.OriginNode.InstanceId;
            var startPoc = startContext.StartPoc;

            var startStep = new TraceStep
            {
                StepIndex = 1,
                NodeId = nozzleNodeId,
                NodeName = _cache.GetNodeName(nozzleNodeId),
                NodeType = _cache.GetNodeType(nozzleNodeId).ToString(),
                OutletUsed = startPoc.InstanceId
            };

            queue.Enqueue(new ReverseTraceState
            {
                Mode = FrontierMode.LookIncomingTo,
                FrontierId = startPoc.InstanceId,
                Steps = new List<TraceStep> { startStep },
                VisitedNodeIds = new HashSet<Guid> { nozzleNodeId },
                AccumulatedLength = 0
            });

            while (queue.Count > 0)
            {
                var state = queue.Dequeue();
                if (state.Steps.Count > MaxHopLimit)
                {
                    results.Add(BuildResult(input, startContext, null, null, state.Steps, state.AccumulatedLength,
                        "MAX_HOPS", false, "Hop limit exceeded"));
                    continue;
                }

                if (state.Mode == FrontierMode.LookIncomingTo)
                    ExpandIncomingTo(state, startContext, queue, results);
                else if (state.Mode == FrontierMode.TraversePipe)
                    ExpandPipeOutlets(state, startContext, queue, results);
            }

            if (results.Count == 0)
            {
                results.Add(BuildResult(input, startContext, null, null,
                    new List<TraceStep>
                    {
                        new TraceStep
                        {
                            StepIndex = 1,
                            NodeId = nozzleNodeId,
                            NodeName = _cache.GetNodeName(nozzleNodeId),
                            NodeType = _cache.GetNodeType(nozzleNodeId).ToString()
                        }
                    },
                    0, "NO_PATH", false, "No equipment reached"));
            }

            return FinalizeResults(results);
        }

        private void ExpandIncomingTo(
            ReverseTraceState state,
            StartPocContext startContext,
            Queue<ReverseTraceState> queue,
            List<ReverseTraceResult> results)
        {
            var incoming = _reverseIndex.GetIncomingEdges(state.FrontierId);
            var seenPipe = new HashSet<Guid>();

            foreach (var edge in incoming)
            {
                if (edge.EdgeType == IncomingEdgeType.Poc)
                {
                    TryArriveViaPoc(edge.SourceId, state, startContext, queue, results);
                }
                else if (edge.EdgeType == IncomingEdgeType.Pipe)
                {
                    if (!seenPipe.Add(edge.SourceId))
                        continue;
                    TryEnterPipe(edge.SourceId, state.FrontierId, state, queue);
                }
                else if (edge.EdgeType == IncomingEdgeType.Takeoff)
                {
                    TryEnterTakeoff(edge.SourceId, state, queue);
                }
            }
        }

        private void ExpandPipeOutlets(
            ReverseTraceState state,
            StartPocContext startContext,
            Queue<ReverseTraceState> queue,
            List<ReverseTraceResult> results)
        {
            if (!_cache.TryGetPipe(state.FrontierId, out var pipe) || pipe == null)
                return;

            // 동일 물리 Target(Owner/Pipe)당 1회 — 정방향 GroupBy TargetId와 동일
            var seenTargets = new HashSet<Guid>();
            int enqueued = 0;

            foreach (var outlet in EnumeratePipeOutlets(pipe))
            {
                if (!outlet.HasValue || outlet.Value == Guid.Empty)
                    continue;
                if (state.EntryPortId.HasValue && outlet.Value == state.EntryPortId.Value)
                    continue;

                Guid next = outlet.Value;

                if (_cache.TryGetPoc(next, out var nextPoc) && nextPoc != null)
                {
                    if (!seenTargets.Add(nextPoc.OwnerId))
                        continue;
                    if (TryArriveViaPoc(nextPoc.InstanceId, state, startContext, queue, results))
                        enqueued++;
                    continue;
                }

                if (_cache.TryGetPipe(next, out var nextPipe) && nextPipe != null)
                {
                    if (!seenTargets.Add(nextPipe.PipeId))
                        continue;
                    if (TryEnterPipe(nextPipe.PipeId, state.FrontierId, state, queue))
                        enqueued++;
                    continue;
                }

                if (_cache.TryGetTakeoff(next, out var takeoff) && takeoff != null)
                {
                    if (!seenTargets.Add(takeoff.TakeoffId))
                        continue;
                    if (TryEnterTakeoff(takeoff.TakeoffId, state, queue))
                        enqueued++;
                    continue;
                }

                if (!seenTargets.Add(next))
                    continue;
                queue.Enqueue(CloneState(state, FrontierMode.LookIncomingTo, next, state.EntryPortId));
                enqueued++;
            }

            if (enqueued == 0)
            {
                _logger?.Debug(
                    "[ReverseTrace] Pipe {PipeId} dead-end (entry={Entry}, hops={Hops})",
                    pipe.PipeId, state.EntryPortId, state.Steps.Count);
            }
        }

        private bool TryArriveViaPoc(
            Guid pocInstanceId,
            ReverseTraceState state,
            StartPocContext startContext,
            Queue<ReverseTraceState> queue,
            List<ReverseTraceResult> results)
        {
            if (!_cache.TryGetPoc(pocInstanceId, out var sourcePoc) || sourcePoc == null)
                return false;

            Guid ownerId = sourcePoc.OwnerId;
            if (ownerId == startContext.OriginNode.InstanceId)
                return false;

            if (state.VisitedNodeIds.Contains(ownerId))
                return false;

            var nodeType = _cache.GetNodeType(ownerId);
            NodeInfo ownerNode;
            _cache.TryGetNode(ownerId, out ownerNode);

            var steps = CloneSteps(state.Steps);
            steps.Add(new TraceStep
            {
                StepIndex = steps.Count + 1,
                NodeId = ownerId,
                NodeName = _cache.GetNodeName(ownerId),
                NodeType = nodeType.ToString(),
                OutletUsed = sourcePoc.InstanceId,
                Division = _cache.GetDivision(ownerId),
                HookupNumber = _cache.GetHookupNumber(ownerId)
            });

            if (ReverseTraceTerminal.IsEquipmentTerminal(ownerNode, nodeType))
            {
                bool refMatch = ownerNode != null
                    && EquipmentTagMatcher.AreRelated(ownerNode.Tag, startContext.ReferenceTag);
                results.Add(BuildResult(startContext.Input, startContext, ownerNode, sourcePoc, steps,
                    state.AccumulatedLength, refMatch ? "OK" : "REF_TAG_MISMATCH", refMatch,
                    refMatch ? null : "Equipment tag differs from Connection Tag(ReferenceTag)"));
                return true;
            }

            if (nodeType == NodeType.Nozzle)
                return false;

            if (nodeType == NodeType.Unknown && ownerNode == null)
                return false;

            var nextVisited = new HashSet<Guid>(state.VisitedNodeIds) { ownerId };
            var ownerPocs = _cache.GetPocsByOwner(ownerId).Where(p => p != null).ToList();

            // 물리 Target(PipeId / next Owner)당 1회만 — A(Pipe→port)+B(port→Pipe) 이중 등록 방지
            var queuedTargets = new HashSet<Guid>();
            int fanOut = 0;
            int skippedDup = 0;

            foreach (var port in ownerPocs)
            {
                CollectPipeTargetsFromPort(port, queuedTargets, ref skippedDup,
                    (pipeId, entryPort) =>
                    {
                        if (TryEnterPipe(pipeId, entryPort,
                            BranchAtPort(state, port.InstanceId, steps, nextVisited), queue))
                        {
                            fanOut++;
                        }
                    });

                if (!port.Outlet.HasValue || port.Outlet.Value == Guid.Empty)
                    continue;

                Guid outlet = port.Outlet.Value;

                if (_cache.TryGetPipe(outlet, out var pipeViaOutlet) && pipeViaOutlet != null)
                {
                    if (!queuedTargets.Add(pipeViaOutlet.PipeId))
                    {
                        skippedDup++;
                        continue;
                    }

                    if (TryEnterPipe(pipeViaOutlet.PipeId, port.InstanceId,
                        BranchAtPort(state, port.InstanceId, steps, nextVisited), queue))
                    {
                        fanOut++;
                    }
                    continue;
                }

                if (_cache.TryGetPoc(outlet, out var linkedPoc) && linkedPoc != null)
                {
                    if (linkedPoc.OwnerId == startContext.OriginNode.InstanceId)
                        continue;
                    if (nextVisited.Contains(linkedPoc.OwnerId))
                        continue;
                    if (!queuedTargets.Add(linkedPoc.OwnerId))
                    {
                        skippedDup++;
                        continue;
                    }

                    if (TryArriveViaPoc(linkedPoc.InstanceId,
                        BranchAtPort(state, port.InstanceId, steps, nextVisited),
                        startContext, queue, results))
                    {
                        fanOut++;
                    }
                    continue;
                }

                if (_cache.TryGetTakeoff(outlet, out var takeoff) && takeoff != null)
                {
                    if (!queuedTargets.Add(takeoff.TakeoffId))
                    {
                        skippedDup++;
                        continue;
                    }

                    if (TryEnterTakeoff(takeoff.TakeoffId,
                        BranchAtPort(state, port.InstanceId, steps, nextVisited), queue))
                    {
                        fanOut++;
                    }
                }
            }

            _logger?.Information(
                "[ReverseTrace] Transit {Type} '{Name}' pocs={Pocs}, fanOut={Fan}, skippedDup={Dup}, hops={Hops}",
                nodeType, _cache.GetNodeName(ownerId), ownerPocs.Count, fanOut, skippedDup, steps.Count);

            if (fanOut == 0)
            {
                _logger?.Warning(
                    "[ReverseTrace] Fitting/transit dead-end after '{Name}' (owner={Owner}, entryPoc={Poc}, siblingPocs={Count}).",
                    _cache.GetNodeName(ownerId), ownerId, pocInstanceId, ownerPocs.Count);
                return false;
            }

            return true;
        }

        /// <summary>Pipe.OutletN == port 인 배관을 Target 집합에 넣고 콜백.</summary>
        private void CollectPipeTargetsFromPort(
            PocData port,
            HashSet<Guid> queuedTargets,
            ref int skippedDup,
            Action<Guid, Guid> onUniquePipe)
        {
            foreach (var edge in _reverseIndex.GetIncomingEdges(port.InstanceId))
            {
                if (edge.EdgeType != IncomingEdgeType.Pipe)
                    continue;

                if (!queuedTargets.Add(edge.SourceId))
                {
                    skippedDup++;
                    continue;
                }

                onUniquePipe(edge.SourceId, port.InstanceId);
            }
        }

        private bool TryEnterPipe(
            Guid pipeId,
            Guid? entryPortId,
            ReverseTraceState state,
            Queue<ReverseTraceState> queue)
        {
            if (!_cache.TryGetPipe(pipeId, out var pipe) || pipe == null)
                return false;

            if (state.VisitedNodeIds.Contains(pipe.PipeId))
                return false;

            var steps = CloneSteps(state.Steps);
            double segmentLen = pipe.Length > 0 ? pipe.Length : 0;
            steps.Add(new TraceStep
            {
                StepIndex = steps.Count + 1,
                NodeId = pipe.PipeId,
                NodeName = string.Empty,
                NodeType = "PipeSeg",
                Length = segmentLen,
                OutletUsed = entryPortId ?? Guid.Empty,
                HookupNumber = _cache.GetHookupNumber(pipe.PipeId),
                OpTime = pipe.OpTime,
                UserName = pipe.UserName,
                CompanyName = pipe.CompanyName
            });

            var nextVisited = new HashSet<Guid>(state.VisitedNodeIds) { pipe.PipeId };
            queue.Enqueue(new ReverseTraceState
            {
                Mode = FrontierMode.TraversePipe,
                FrontierId = pipe.PipeId,
                EntryPortId = entryPortId,
                Steps = steps,
                VisitedNodeIds = nextVisited,
                AccumulatedLength = state.AccumulatedLength + segmentLen
            });
            return true;
        }

        private bool TryEnterTakeoff(
            Guid takeoffId,
            ReverseTraceState state,
            Queue<ReverseTraceState> queue)
        {
            if (!_cache.TryGetTakeoff(takeoffId, out var takeoff) || takeoff == null)
                return false;

            if (state.VisitedNodeIds.Contains(takeoff.TakeoffId))
                return false;

            var steps = CloneSteps(state.Steps);
            steps.Add(new TraceStep
            {
                StepIndex = steps.Count + 1,
                NodeId = takeoff.TakeoffId,
                NodeName = "Damper[" + (takeoff.Tag ?? string.Empty) + "]",
                NodeType = "Takeoff"
            });

            var nextVisited = new HashSet<Guid>(state.VisitedNodeIds) { takeoff.TakeoffId };
            if (takeoff.PipeId != Guid.Empty && !nextVisited.Contains(takeoff.PipeId))
            {
                return TryEnterPipe(takeoff.PipeId, takeoff.TakeoffId,
                    new ReverseTraceState
                    {
                        Mode = FrontierMode.LookIncomingTo,
                        FrontierId = takeoff.TakeoffId,
                        Steps = steps,
                        VisitedNodeIds = nextVisited,
                        AccumulatedLength = state.AccumulatedLength
                    }, queue);
            }

            queue.Enqueue(new ReverseTraceState
            {
                Mode = FrontierMode.LookIncomingTo,
                FrontierId = takeoff.TakeoffId,
                Steps = steps,
                VisitedNodeIds = nextVisited,
                AccumulatedLength = state.AccumulatedLength
            });
            return true;
        }

        /// <summary>
        /// Fitting 분기마다 Steps/Visited를 복사한다.
        /// 공유 HashSet에 pipeId를 넣으면 sibling 경로가 서로를 방문한 것으로 오인한다.
        /// 동일 Target 재enqueue 금지는 queuedTargets가 담당.
        /// </summary>
        private static ReverseTraceState BranchAtPort(
            ReverseTraceState state,
            Guid portId,
            List<TraceStep> steps,
            HashSet<Guid> visited)
        {
            return new ReverseTraceState
            {
                Mode = FrontierMode.LookIncomingTo,
                FrontierId = portId,
                Steps = CloneSteps(steps),
                VisitedNodeIds = new HashSet<Guid>(visited),
                AccumulatedLength = state.AccumulatedLength
            };
        }

        private List<ReverseTraceResult> FinalizeResults(List<ReverseTraceResult> results)
        {
            if (results == null || results.Count == 0)
                return results ?? new List<ReverseTraceResult>();

            bool hasTerminal = results.Any(IsTerminalStatus);
            if (hasTerminal)
            {
                int before = results.Count;
                results = results.Where(r => !IsIncompleteStatus(r.Status)).ToList();
                if (before != results.Count)
                {
                    _logger?.Information(
                        "[ReverseTrace] Drop incomplete paths {Before} → {After} (terminal reached)",
                        before, results.Count);
                }
            }

            int beforeDedup = results.Count;
            results = DeduplicatePaths(results);
            if (beforeDedup > results.Count)
            {
                _logger?.Information(
                    "[ReverseTrace] Dedup paths {Before} → {After} (identical NodeId sequences)",
                    beforeDedup, results.Count);
            }

            results = results
                .OrderBy(r => StatusRank(r.Status))
                .ThenBy(r => r.Steps?.Count ?? int.MaxValue)
                .ThenBy(r => r.TotalPipeLength)
                .ToList();

            MarkPrimaryPaths(results);
            return results;
        }

        /// <summary>NodeId 시퀀스가 동일한 경로는 1개만 남김 (OK 우선, 그다음 짧은 hop).</summary>
        private static List<ReverseTraceResult> DeduplicatePaths(List<ReverseTraceResult> results)
        {
            if (results == null || results.Count <= 1)
                return results ?? new List<ReverseTraceResult>();

            return results
                .GroupBy(PathFingerprint, StringComparer.Ordinal)
                .Select(g => g
                    .OrderBy(r => StatusRank(r.Status))
                    .ThenBy(r => r.Steps?.Count ?? int.MaxValue)
                    .ThenBy(r => r.TotalPipeLength)
                    .First())
                .ToList();
        }

        private static string PathFingerprint(ReverseTraceResult r)
        {
            if (r?.Steps == null || r.Steps.Count == 0)
                return "empty|" + (r?.Status ?? "");

            var sb = new StringBuilder(r.Steps.Count * 40);
            foreach (var s in r.Steps)
            {
                if (sb.Length > 0) sb.Append('>');
                sb.Append(s.NodeId.ToString("N"));
            }

            // 동일 노드 순서면 동일 루트 (OutletUsed/포트 차이는 무시)
            return sb.ToString();
        }

        private static int StatusRank(string status)
        {
            if (string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase)) return 0;
            if (string.Equals(status, "REF_TAG_MISMATCH", StringComparison.OrdinalIgnoreCase)) return 1;
            return 2;
        }

        private static bool IsTerminalStatus(ReverseTraceResult r)
        {
            return string.Equals(r?.Status, "OK", StringComparison.OrdinalIgnoreCase)
                || string.Equals(r?.Status, "REF_TAG_MISMATCH", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsIncompleteStatus(string status)
        {
            return string.Equals(status, "MAX_HOPS", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "NO_PATH", StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<Guid?> EnumeratePipeOutlets(PipeData pipe)
        {
            foreach (var target in PipeEndpointTraversal.ConnectedTargets(pipe))
                yield return target;
        }

        private static ReverseTraceState CloneState(
            ReverseTraceState state,
            FrontierMode mode,
            Guid frontierId,
            Guid? entryPortId)
        {
            return new ReverseTraceState
            {
                Mode = mode,
                FrontierId = frontierId,
                EntryPortId = entryPortId,
                Steps = CloneSteps(state.Steps),
                VisitedNodeIds = new HashSet<Guid>(state.VisitedNodeIds),
                AccumulatedLength = state.AccumulatedLength
            };
        }

        private static void MarkPrimaryPaths(List<ReverseTraceResult> results)
        {
            if (results == null || results.Count == 0)
                return;

            foreach (var r in results)
                r.PrimaryPath = false;

            var best = results
                .Where(r => string.Equals(r.Status, "OK", StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.Steps?.Count ?? int.MaxValue)
                .ThenBy(r => r.TotalPipeLength)
                .FirstOrDefault()
                ?? results.First();

            best.PrimaryPath = true;
        }

        private static ReverseTraceResult BuildResult(
            NozzleTraceInput input,
            StartPocContext start,
            NodeInfo terminalNode,
            PocData terminalPoc,
            List<TraceStep> steps,
            double length,
            string status,
            bool primary,
            string detail)
        {
            return new ReverseTraceResult
            {
                Input = input,
                Start = start,
                TerminalEquipment = terminalNode,
                TerminalPoc = terminalPoc,
                Steps = steps ?? new List<TraceStep>(),
                TotalPipeLength = length,
                Status = status,
                PrimaryPath = primary,
                StatusDetail = detail
            };
        }

        private static List<TraceStep> CloneSteps(List<TraceStep> steps)
        {
            return steps.Select(s => new TraceStep
            {
                StepIndex = s.StepIndex,
                NodeId = s.NodeId,
                NodeName = s.NodeName,
                NodeType = s.NodeType,
                OutletUsed = s.OutletUsed,
                Length = s.Length,
                QCode = s.QCode,
                Division = s.Division,
                HookupNumber = s.HookupNumber,
                OpTime = s.OpTime,
                UserName = s.UserName,
                CompanyName = s.CompanyName
            }).ToList();
        }

        private enum FrontierMode
        {
            LookIncomingTo,
            TraversePipe
        }

        private sealed class ReverseTraceState
        {
            public FrontierMode Mode { get; set; }
            public Guid FrontierId { get; set; }
            public Guid? EntryPortId { get; set; }
            public List<TraceStep> Steps { get; set; }
            public HashSet<Guid> VisitedNodeIds { get; set; }
            public double AccumulatedLength { get; set; }
        }
    }
}
