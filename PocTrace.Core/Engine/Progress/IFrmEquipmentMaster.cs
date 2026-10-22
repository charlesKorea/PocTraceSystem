using System;
using System.Collections.Generic;

namespace PocTrace.Core.Engine.Progress
{
  /// <summary>
  /// FRM 설비 마스터 (계획량·공식 Status). 스키마 확정 전에도 어댑터로 연결.
  /// </summary>
  public interface IFrmEquipmentMaster
  {
    IReadOnlyList<FrmEquipmentRow> Load(string lineDb);
  }

  public sealed class FrmEquipmentRow
  {
    public string Tag { get; set; }
    /// <summary>MainName / 기종코드 대응 키.</summary>
    public string ModelKey { get; set; }
    public double? PlannedLengthMm { get; set; }
    public int? PlannedBomCount { get; set; }
    /// <summary>Complete / InProgress / Unknown 등.</summary>
    public string OfficialStatus { get; set; }
    public DateTime? DesignMilestoneDate { get; set; }
  }

  /// <summary>FRM 미연결 시 빈 마스터.</summary>
  public sealed class NullFrmEquipmentMaster : IFrmEquipmentMaster
  {
    public static readonly NullFrmEquipmentMaster Instance = new NullFrmEquipmentMaster();
    public IReadOnlyList<FrmEquipmentRow> Load(string lineDb) => Array.Empty<FrmEquipmentRow>();
  }
}
