using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PocTrace.Core.Models;

namespace PocTrace.Core.Engine
{
  public interface ITraceEngine
  {
    Task<List<TraceResult>> RunTraceAsync(string targetTag, int limitCount, bool detailedLog = false,
      bool includePartialTagMatch = false);

    Task<List<TraceResult>> RunTraceAsync(
      string targetTag,
      int limitCount,
      bool detailedLog,
      CancellationToken cancellationToken,
      bool includePartialTagMatch = false);

    /// <summary>
    /// Synchronous trace. Batch callers use this so Parallel.ForEach does not
    /// block a pool thread waiting on another Task.Run inside RunTraceAsync.
    /// </summary>
    List<TraceResult> RunTrace(
      string targetTag,
      int limitCount,
      bool detailedLog,
      CancellationToken cancellationToken,
      bool includePartialTagMatch = false);
  }
}
