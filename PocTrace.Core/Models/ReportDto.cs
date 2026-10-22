using System.Collections.Generic;

namespace PocTrace.Core.Models
{
  /// <summary>
  /// HTML data_*.js 용 경량 DTO (전체 TraceResult 대신 — 대용량 DB 대응).
  /// Admin DailyTraceJob이 집계 후 HtmlReportGenerator에 전달.
  /// </summary>
  public class ReportDto
  {
    public List<EquipmentSummaryDto> Equipments { get; set; } = new List<EquipmentSummaryDto>();
    public Dictionary<string, int> ErrorStats { get; set; } = new Dictionary<string, int>();
    public Dictionary<string, ProcessStatDto> ProcessStats { get; set; } = new Dictionary<string, ProcessStatDto>();
    public Dictionary<string, int> BelongStats { get; set; } = new Dictionary<string, int>();
    public HookupStatsDto HookupStats { get; set; } = new HookupStatsDto();
  }

  public class HookupStatsDto
  {
    public int OK { get; set; }
    public int MISSING { get; set; }
    public int MISMATCH { get; set; }
    public int TAG_ERROR { get; set; }
    public int DATA_ERROR { get; set; }
    public int TotalEquip { get; set; }
    public int TotalObjects { get; set; }
  }

  public class EquipmentSummaryDto
  {
    public string Guid { get; set; }
    public string Tag { get; set; }
    public string ConstCode { get; set; }
    public string Process { get; set; }
    public string Result { get; set; }
    public int IssueCount { get; set; }
    public int EqpQty { get; set; }
    public int TotalPoc { get; set; }
    public int PassPoc { get; set; }
    public int FailPoc { get; set; }
    public string MainIssues { get; set; }
    public string MainName { get; set; }
    public string Length { get; set; }
  }

  /// <summary>공정(Process)별 검수 집계 — 바 차트용.</summary>
  public class ProcessStatDto
  {
    /// <summary>설비 PASS 건수</summary>
    public int Pass { get; set; }
    /// <summary>설비 FAIL 건수</summary>
    public int Fail { get; set; }
    /// <summary>공정별 검수 설비 수량 (Pass+Fail)</summary>
    public int EqpCount { get; set; }
    public int PassPoc { get; set; }
    public int FailPoc { get; set; }
    public int TotalPoc { get; set; }
    /// <summary>H/U No Pass (OK)</summary>
    public int HookupPass { get; set; }
    /// <summary>H/U No Fail (비-OK)</summary>
    public int HookupFail { get; set; }
  }
}
