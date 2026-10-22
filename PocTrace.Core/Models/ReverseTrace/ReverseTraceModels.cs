using System;
using System.Collections.Generic;
using PocTrace.Core.Models;

namespace PocTrace.Core.Models.ReverseTrace
{
    public enum NozzleLookupKeyKind
    {
        BimId,
        InstanceNumericId,
        NodeGuid
    }

    public enum ReverseTraceOriginKind
    {
        Nozzle,
        Takeoff // v2
    }

    public enum IncomingEdgeType
    {
        Poc,
        Pipe,
        Takeoff
    }

    /// <summary>역그래프 BFS — targetId를 가리키는 이전 hop.</summary>
    public readonly struct IncomingEdge
    {
        public IncomingEdge(Guid sourceId, Guid sourceOwnerId, IncomingEdgeType edgeType)
        {
            SourceId = sourceId;
            SourceOwnerId = sourceOwnerId;
            EdgeType = edgeType;
        }

        public Guid SourceId { get; }
        public Guid SourceOwnerId { get; }
        public IncomingEdgeType EdgeType { get; }
    }

    public sealed class NozzleTraceInput
    {
        public string DbName { get; set; }
        public NozzleLookupKeyKind KeyKind { get; set; }
        public string KeyValue { get; set; }
    }

    public sealed class NozzleStartResolution
    {
        public bool Success { get; set; }
        public string ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public NodeInfo NozzleNode { get; set; }
        public IReadOnlyList<PocData> StartPocs { get; set; } = Array.Empty<PocData>();
    }

    public sealed class StartPocContext
    {
        public NozzleTraceInput Input { get; set; }
        public ReverseTraceOriginKind OriginKind { get; set; }
        public NodeInfo OriginNode { get; set; }
        public PocData StartPoc { get; set; }
        public string ReferenceTag { get; set; }
    }

    public sealed class ReverseTraceResult
    {
        public NozzleTraceInput Input { get; set; }
        public StartPocContext Start { get; set; }
        public NodeInfo TerminalEquipment { get; set; }
        public PocData TerminalPoc { get; set; }
        public List<TraceStep> Steps { get; set; } = new List<TraceStep>();
        public double TotalPipeLength { get; set; }
        public string Status { get; set; }
        public bool PrimaryPath { get; set; }
        public string StatusDetail { get; set; }
    }

    public sealed class NozzleTraceRunResult
    {
        public NozzleTraceInput Input { get; set; }
        public NozzleStartResolution Resolution { get; set; }
        public List<ReverseTraceResult> Traces { get; set; } = new List<ReverseTraceResult>();
    }
}
