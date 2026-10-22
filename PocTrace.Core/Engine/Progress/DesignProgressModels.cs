using System;
using System.Collections.Generic;

namespace PocTrace.Core.Engine.Progress
{
  /// <summary>Design Progress 설정 (GlobalConfig / AppSettings).</summary>
  public sealed class DesignProgressOptions
  {
    public bool Enabled { get; set; } = true;
    /// <summary>LastPipeDesignAt &gt;= Today - InProgressDays → 진행중.</summary>
    public int InProgressDays { get; set; } = 2;
    /// <summary>완료 코호트로 쓰려면 최근 IdleStableDays 동안 OpTime 갱신 없음.</summary>
    public int IdleStableDays { get; set; } = 14;
    public int MinCohortSize { get; set; } = 3;
    /// <summary>ProgressPct = LengthWeight * LengthPct + (1-LengthWeight) * BomPct</summary>
    public double LengthWeight { get; set; } = 0.6;
    public DateTime AsOfDate { get; set; } = DateTime.Today;
  }

  public sealed class ProgressReportDto
  {
    public string DbName { get; set; }
    public string GenDate { get; set; }
    public int TotalEquip { get; set; }
    public int InProgressCount { get; set; }
    public int LowConfidenceCount { get; set; }
    public double AvgProgressPct { get; set; }
    public List<EquipmentProgressDto> Equipments { get; set; } = new List<EquipmentProgressDto>();
  }

  public sealed class EquipmentProgressDto
  {
    public string Tag { get; set; }
    public string Guid { get; set; }
    public string MainName { get; set; }
    public string Process { get; set; }
    /// <summary>Trace 경로 Pipe OpTime 최솟값 (yyyy-MM-dd). 없으면 빈 문자열.</summary>
    public string FirstDesignAt { get; set; }
    public string LastDesignAt { get; set; }
    /// <summary>최근설계일 − 설계시작일 (일). 둘 다 없으면 null.</summary>
    public int? DesignDays { get; set; }
    public bool IsInProgress { get; set; }
    public bool IsCohortEligible { get; set; }
    public bool LowConfidence { get; set; }
    public bool Overrun { get; set; }
    public double CurrentLength { get; set; }
    public int CurrentBomCount { get; set; }
    public double BaselineLengthMedian { get; set; }
    public double BaselineBomMedian { get; set; }
    public int CohortN { get; set; }
    public double LengthPct { get; set; }
    public double BomPct { get; set; }
    public double ProgressPct { get; set; }
    public string FrmStatus { get; set; }
    public List<UtilityProgressDto> Utilities { get; set; } = new List<UtilityProgressDto>();
  }

  public sealed class UtilityProgressDto
  {
    public string UtilityName { get; set; }
    public double CurrentLength { get; set; }
    public int CurrentBomCount { get; set; }
    public double BaselineLengthMedian { get; set; }
    public double BaselineBomMedian { get; set; }
    public double LengthPct { get; set; }
    public double BomPct { get; set; }
    public double ProgressPct { get; set; }
    public bool Overrun { get; set; }
  }

  public sealed class CohortBaseline
  {
    public string MainName { get; set; }
    public int N { get; set; }
    public double LengthMedian { get; set; }
    public double LengthP25 { get; set; }
    public double LengthP75 { get; set; }
    public double BomMedian { get; set; }
    public double BomP25 { get; set; }
    public double BomP75 { get; set; }
    public Dictionary<string, UtilityBaseline> ByUtility { get; set; }
      = new Dictionary<string, UtilityBaseline>(StringComparer.OrdinalIgnoreCase);
  }

  public sealed class UtilityBaseline
  {
    public double LengthMedian { get; set; }
    public double BomMedian { get; set; }
    public int N { get; set; }
  }

  /// <summary>설비당 Trace union footprint (배관/피팅).</summary>
  public sealed class EquipmentFootprint
  {
    public string Tag { get; set; }
    public Guid EquipGuid { get; set; }
    public string MainName { get; set; }
    public string Process { get; set; }
    public double TotalLength { get; set; }
    public int BomCount { get; set; }
    /// <summary>설비 Trace footprint Pipe OpTime 최솟값.</summary>
    public DateTime? FirstPipeDesignAt { get; set; }
    public DateTime? LastPipeDesignAt { get; set; }
    public Dictionary<string, UtilityFootprintSlice> ByUtility { get; set; }
      = new Dictionary<string, UtilityFootprintSlice>(StringComparer.OrdinalIgnoreCase);
  }

  public sealed class UtilityFootprintSlice
  {
    public double Length { get; set; }
    public int BomCount { get; set; }
    public DateTime? FirstPipeDesignAt { get; set; }
    public DateTime? LastPipeDesignAt { get; set; }
  }
}
