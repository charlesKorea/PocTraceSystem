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

    /// <summary>
    /// 배치 한 태그. 캐시에 없으면 미배치설비, 설비는 있으나 연결 배관이 없으면 미설계 배치 설비로 결과 행을 남긴다.
    /// </summary>
    List<TraceResult> RunBatchTag(
      string targetTag,
      bool detailedLog,
      CancellationToken cancellationToken);
  }
}
