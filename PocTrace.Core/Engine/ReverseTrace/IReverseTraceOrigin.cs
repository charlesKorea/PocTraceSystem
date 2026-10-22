using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Models.ReverseTrace;
using System.Collections.Generic;

namespace PocTrace.Core.Engine.ReverseTrace
{
    public interface IReverseTraceOrigin
    {
        ReverseTraceOriginKind OriginKind { get; }
        NozzleStartResolution ResolveStarts(IGraphCache cache, NozzleTraceInput input);
    }
}
