using System;
using System.Collections.Generic;
using PocTrace.Core.Models;
using PocTrace.Demolition.Export;

namespace PocTrace.Demolition.Jobs
{
  /// <summary>철거설비 검수 Job 결과 (VOC 시트 재설계).</summary>
  public sealed class DemolitionJobResult
  {
    /// <summary>Sheet 설비정보</summary>
    public List<object[]> EquipmentInfoRows { get; set; } = new List<object[]>();
    /// <summary>Sheet Status EMP — Trace 결과(≠EMP)</summary>
    public List<object[]> StatusEmpRows { get; set; } = new List<object[]>();
    /// <summary>Sheet Nozzle — Full List 필터</summary>
    public List<object[]> NozzleRows { get; set; } = new List<object[]>();
    /// <summary>Sheet Stick — Full List 필터</summary>
    public List<object[]> StickRows { get; set; } = new List<object[]>();
    /// <summary>Sheet TakeOff — Full List 필터</summary>
    public List<object[]> TakeOffRows { get; set; } = new List<object[]>();
    /// <summary>Sheet Hook Up Number</summary>
    public List<object[]> HookupRows { get; set; } = new List<object[]>();
    /// <summary>Sheet BIM Group — EQPID only (no header form)</summary>
    public List<object[]> BimGroupEqpRows { get; set; } = new List<object[]>();
    /// <summary>Sheet 종합 — EQPID × 7시트 수량 (첫 행=헤더)</summary>
    public List<object[]> SummaryRows { get; set; } = new List<object[]>();
    /// <summary>종합 집계기 (Preview용)</summary>
    public EqpSheetCounter SummaryCounter { get; set; }

    /// <summary>Trace 원본 (Status EMP용 / 진단).</summary>
    public List<TraceResult> Results { get; set; } = new List<TraceResult>();

    public int LinesProcessed { get; set; }
    public int EqpAttempted { get; set; }
    public int EqpTraced { get; set; }
    public int EqpMissingTag { get; set; }
    public int EqpNoResult { get; set; }
    public int EqpError { get; set; }

    public int EquipmentInfoRowCount => EquipmentInfoRows?.Count ?? 0;
    public int StatusEmpRowCount => StatusEmpRows?.Count ?? 0;
    public int NozzleRowCount => NozzleRows?.Count ?? 0;
    public int StickRowCount => StickRows?.Count ?? 0;
    public int TakeOffRowCount => TakeOffRows?.Count ?? 0;
    public int HookupRowCount => HookupRows?.Count ?? 0;
    public int BimGroupEqpRowCount => BimGroupEqpRows?.Count ?? 0;
    public int ResultRowCount => Results?.Count ?? 0;

    public TimeSpan Elapsed { get; set; }
    public List<string> Warnings { get; set; } = new List<string>();
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }
  }
}
