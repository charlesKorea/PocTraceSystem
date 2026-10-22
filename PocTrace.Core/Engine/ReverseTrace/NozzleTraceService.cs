using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Engine.ReverseTrace;
using PocTrace.Core.Models;
using PocTrace.Core.Models.ReverseTrace;
using Serilog;
using System.Collections.Generic;

namespace PocTrace.Core.Engine.ReverseTrace
{
    /// <summary>Nozzle 역추적 오케스트레이션 (Resolve → N POC × Trace).</summary>
    public sealed class NozzleTraceService
    {
        private readonly IGraphCache _cache;
        private readonly ReverseGraphIndex _reverseIndex;
        private readonly IReverseTraceOrigin _origin;
        private readonly ILogger _logger;

        public NozzleTraceService(
            IGraphCache cache,
            ReverseGraphIndex reverseIndex,
            IReverseTraceOrigin origin,
            ILogger logger)
        {
            _cache = cache;
            _reverseIndex = reverseIndex;
            _origin = origin;
            _logger = logger;
        }

        public NozzleTraceRunResult Run(NozzleTraceInput input)
        {
            var run = new NozzleTraceRunResult { Input = input };
            run.Resolution = _origin.ResolveStarts(_cache, input);
            if (!run.Resolution.Success)
                return run;

            var engine = new ReverseTraceEngine(_cache, _reverseIndex, _logger);
            foreach (var startPoc in run.Resolution.StartPocs)
            {
                var context = new StartPocContext
                {
                    OriginKind = _origin.OriginKind,
                    OriginNode = run.Resolution.NozzleNode,
                    StartPoc = startPoc,
                    ReferenceTag = NozzleStartResolver.GetConnectionTag(startPoc),
                    Input = input
                };

                var traces = engine.TraceFromStartPoc(input, context);
                run.Traces.AddRange(traces);

                _logger.Information(
                    "[NozzleTrace] BimId={BimId}, Poc={PocId}, ConnTag={ConnTag}, Paths={Count}",
                    run.Resolution.NozzleNode?.BimId ?? "-",
                    startPoc.InstanceId,
                    NozzleStartResolver.GetConnectionTag(startPoc) ?? "-",
                    traces.Count);
            }

            return run;
        }
    }
}
