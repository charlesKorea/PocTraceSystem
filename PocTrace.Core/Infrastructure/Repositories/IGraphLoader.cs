using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using PocTrace.Core.Models; // NodeInfo, PocData 등을 위해 필수

namespace PocTrace.Core.Infrastructure.Repositories
{
    public interface IGraphLoader
    {
        // [수정] 반환 튜플의 각 요소에 이름(IsStale, SavedAt, Nodes...)을 명시했습니다.
        Task<(bool IsStale, DateTime SavedAt, 
            IEnumerable<NodeInfo> Nodes, IEnumerable<PocData> Pocs, IEnumerable<PipeData> Pipes, 
            IEnumerable<TakeoffData> Takeoffs, IEnumerable<DynamicPropertiesData> HookupNumbers)>
            LoadGraphDataAsync(string dbName, IProgress<string> progress);

        // [Fix] 이 줄을 추가해야 MainViewModel에서 호출 가능합니다.
        Dictionary<string, Dictionary<string, Dictionary<string, string>>> LoadUtMapping();
    }
}