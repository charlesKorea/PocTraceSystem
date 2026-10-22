using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Models;
using PocTrace.Core.Models.ReverseTrace;
using System;
using System.Collections.Generic;

namespace PocTrace.Core.Engine.ReverseTrace
{
    /// <summary>
    /// POC.Outlet / Pipe 끝점 역인덱스. Initialize 시 1회 구축.
    /// </summary>
    public sealed class ReverseGraphIndex
    {
        private readonly Dictionary<Guid, List<IncomingEdge>> _incomingByTarget =
            new Dictionary<Guid, List<IncomingEdge>>();

        public static ReverseGraphIndex Build(IGraphCache cache)
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));

            var index = new ReverseGraphIndex();

            foreach (var poc in cache.EnumeratePocs())
            {
                if (poc == null || !poc.Outlet.HasValue || poc.Outlet.Value == Guid.Empty)
                    continue;

                index.AddEdge(poc.Outlet.Value,
                    new IncomingEdge(poc.InstanceId, poc.OwnerId, IncomingEdgeType.Poc));
            }

            foreach (var pipe in cache.EnumeratePipes())
            {
                if (pipe == null) continue;

                foreach (var target in PipeEndpointTraversal.ConnectedTargets(pipe))
                    index.RegisterPipeOutlet(pipe.PipeId, target);

                // POC.Outlet == Pipe.PipeId 로 배관 본체에 진입하는 경우
                index.AddEdge(pipe.PipeId,
                    new IncomingEdge(pipe.PipeId, pipe.PipeId, IncomingEdgeType.Pipe));
            }

            return index;
        }

        private void RegisterPipeOutlet(Guid pipeId, Guid? outlet)
        {
            if (!outlet.HasValue || outlet.Value == Guid.Empty)
                return;

            AddEdge(outlet.Value, new IncomingEdge(pipeId, pipeId, IncomingEdgeType.Pipe));
        }

        private void AddEdge(Guid targetId, IncomingEdge edge)
        {
            if (!_incomingByTarget.TryGetValue(targetId, out var list))
            {
                list = new List<IncomingEdge>();
                _incomingByTarget[targetId] = list;
            }

            list.Add(edge);
        }

        public IReadOnlyList<IncomingEdge> GetIncomingEdges(Guid targetId)
        {
            if (_incomingByTarget.TryGetValue(targetId, out var list))
                return list;

            return Array.Empty<IncomingEdge>();
        }
    }
}
