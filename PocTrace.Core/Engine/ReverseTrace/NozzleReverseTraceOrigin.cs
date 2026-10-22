using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Models.ReverseTrace;

namespace PocTrace.Core.Engine.ReverseTrace
{
    public sealed class NozzleReverseTraceOrigin : IReverseTraceOrigin
    {
        public ReverseTraceOriginKind OriginKind => ReverseTraceOriginKind.Nozzle;

        public NozzleStartResolution ResolveStarts(IGraphCache cache, NozzleTraceInput input)
        {
            return NozzleStartResolver.Resolve(cache, input);
        }
    }
}
