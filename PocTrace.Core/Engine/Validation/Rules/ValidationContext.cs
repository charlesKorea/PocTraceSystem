using System.Collections.Generic;
using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Models;

namespace PocTrace.Core.Engine.Validation.Rules
{
  /// <summary>
  /// 검수 규칙에 전달되는 불변 스냅샷.
  /// 규칙 추가 시 이 컨텍스트만 확장하면 된다.
  /// </summary>
  public sealed class ValidationContext
  {
    public IGraphCache Cache { get; set; }
    public TraceResult Result { get; set; }
    public PocData StartPoc { get; set; }
    public NodeInfo StartNode { get; set; }
    public NodeInfo EndNode { get; set; }
    public PocData EndPoc { get; set; }

    /// <summary>동일 StartPocId의 분기(BreakDown) 결과들.</summary>
    public List<TraceResult> SamePocBranchResults { get; set; }

    /// <summary>DB별 Utility 그룹 매핑 (UtMapping.csv).</summary>
    public Dictionary<string, Dictionary<string, string>> UtilityGroupMap { get; set; }
  }
}
