using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using PocTrace.Core.Infrastructure;
using Serilog;

namespace PocTrace.Core.Engine.Progress
{
  /// <summary>
  /// FRM 설비 마스터 조회 (Phase B).
  /// AppSettings: FrmConnectionString, FrmEquipmentView (기본 dbo.V_FRM_EquipmentProgress).
  /// 기대 컬럼: Tag, ModelKey, PlannedLengthMm, PlannedBomCount, OfficialStatus, DesignMilestoneDate(optional).
  /// 뷰/연결이 없으면 빈 목록 + Warning — Graph-only Progress는 계속 동작.
  /// </summary>
  public sealed class FrmEquipmentMasterRepository : IFrmEquipmentMaster
  {
    private readonly string _connectionString;
    private readonly string _viewName;
    private readonly ILogger _log;

    public FrmEquipmentMasterRepository(string connectionString = null, string viewName = null, ILogger log = null)
    {
      _connectionString = connectionString ?? GlobalConfig.FrmConnectionString;
      _viewName = string.IsNullOrWhiteSpace(viewName) ? GlobalConfig.FrmEquipmentView : viewName.Trim();
      _log = log ?? Log.Logger;
    }

    public static IFrmEquipmentMaster CreateFromConfig(ILogger log = null)
    {
      if (string.IsNullOrWhiteSpace(GlobalConfig.FrmConnectionString))
        return NullFrmEquipmentMaster.Instance;
      return new FrmEquipmentMasterRepository(log: log);
    }

    public IReadOnlyList<FrmEquipmentRow> Load(string lineDb)
    {
      if (string.IsNullOrWhiteSpace(_connectionString))
        return Array.Empty<FrmEquipmentRow>();

      // 식별자 화이트리스트 (SQL injection 방지): 영숫자·밑줄·점만
      if (!IsSafeSqlIdentifier(_viewName))
      {
        _log.Warning("[FRM] Unsafe FrmEquipmentView name rejected: {View}", _viewName);
        return Array.Empty<FrmEquipmentRow>();
      }

      var list = new List<FrmEquipmentRow>();
      try
      {
        using (var conn = new SqlConnection(_connectionString))
        {
          conn.Open();
          // LineDb 필터 컬럼이 있을 수 있어 optional — 없으면 전체
          string sql = $@"
SELECT Tag, ModelKey, PlannedLengthMm, PlannedBomCount, OfficialStatus, DesignMilestoneDate
FROM {_viewName}";
          using (var cmd = new SqlCommand(sql, conn))
          {
            cmd.CommandTimeout = 120;
            using (var rd = cmd.ExecuteReader(CommandBehavior.CloseConnection))
            {
              int iTag = Ordinal(rd, "Tag");
              int iModel = Ordinal(rd, "ModelKey");
              int iLen = Ordinal(rd, "PlannedLengthMm");
              int iBom = Ordinal(rd, "PlannedBomCount");
              int iStatus = Ordinal(rd, "OfficialStatus");
              int iDate = Ordinal(rd, "DesignMilestoneDate");
              while (rd.Read())
              {
                list.Add(new FrmEquipmentRow
                {
                  Tag = iTag >= 0 && !rd.IsDBNull(iTag) ? Convert.ToString(rd.GetValue(iTag)) : null,
                  ModelKey = iModel >= 0 && !rd.IsDBNull(iModel) ? Convert.ToString(rd.GetValue(iModel)) : null,
                  PlannedLengthMm = ReadDouble(rd, iLen),
                  PlannedBomCount = ReadInt(rd, iBom),
                  OfficialStatus = iStatus >= 0 && !rd.IsDBNull(iStatus) ? Convert.ToString(rd.GetValue(iStatus)) : null,
                  DesignMilestoneDate = ReadDate(rd, iDate)
                });
              }
            }
          }
        }

        if (!string.IsNullOrWhiteSpace(lineDb))
        {
          // Optional LineDb column filtering is not assumed; keep all rows.
          // Callers may filter by Tag against current DB equipment set.
        }

        _log.Information("[FRM] Loaded {Count} equipment rows from {View}", list.Count, _viewName);
      }
      catch (Exception ex)
      {
        _log.Warning(ex, "[FRM] Load failed for view {View}. Falling back to Graph-only baselines.", _viewName);
        return Array.Empty<FrmEquipmentRow>();
      }

      return list.Where(r => !string.IsNullOrWhiteSpace(r.Tag)).ToList();
    }

    private static bool IsSafeSqlIdentifier(string name)
    {
      if (string.IsNullOrWhiteSpace(name)) return false;
      foreach (char c in name)
      {
        if (!(char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '[' || c == ']'))
          return false;
      }
      return true;
    }

    private static int Ordinal(IDataRecord rd, string name)
    {
      try { return rd.GetOrdinal(name); }
      catch { return -1; }
    }

    private static double? ReadDouble(IDataRecord rd, int i)
    {
      if (i < 0 || rd.IsDBNull(i)) return null;
      try { return Convert.ToDouble(rd.GetValue(i)); }
      catch { return null; }
    }

    private static int? ReadInt(IDataRecord rd, int i)
    {
      if (i < 0 || rd.IsDBNull(i)) return null;
      try { return Convert.ToInt32(rd.GetValue(i)); }
      catch { return null; }
    }

    private static DateTime? ReadDate(IDataRecord rd, int i)
    {
      if (i < 0 || rd.IsDBNull(i)) return null;
      try { return Convert.ToDateTime(rd.GetValue(i)); }
      catch { return null; }
    }
  }
}
