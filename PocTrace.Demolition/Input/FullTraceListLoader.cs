using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OfficeOpenXml;
using PocTrace.Core.Infrastructure;
using Serilog;

namespace PocTrace.Demolition.Input
{
  /// <summary>
  /// Admin Daily Full Trace Excel ({line}_Full_List.xlsx) 로더.
  /// 10만행+ 대응: 시트 파싱 후 필요 컬럼만 slim DTO로 메모리 유지, ExcelPackage는 즉시 Dispose.
  /// Sheet Nozzle/Stick/TakeOff가 동일 스냅샷을 재사용한다.
  /// </summary>
  public static class FullTraceListLoader
  {
    public sealed class FullTraceRow
    {
      public string Status { get; set; }
      public string SBimId { get; set; }
      public string SInstanceId { get; set; }
      public string SGuid { get; set; }
      public string SConnectorGuid { get; set; }
      public string STag { get; set; }
      public string SConnectorInformation { get; set; }
      public string SConnectionTag { get; set; }
      public string TConnectionTag { get; set; }
      public string TConnectorInformation { get; set; }
      public string TDivision { get; set; }
      public string TGuid { get; set; }
      public string TTag { get; set; }
      public string TName { get; set; }
    }

    public sealed class LoadResult
    {
      public string SourcePath { get; set; }
      public List<FullTraceRow> Rows { get; set; } = new List<FullTraceRow>();
      public string ErrorMessage { get; set; }
      public bool Success => string.IsNullOrEmpty(ErrorMessage) && Rows != null;
    }

    public static LoadResult LoadForLine(string line)
    {
      var result = new LoadResult();
      string path = ResolveFullListPath(line);
      if (string.IsNullOrEmpty(path))
      {
        result.ErrorMessage = "Full List Excel을 찾지 못함: " + (line ?? "");
        return result;
      }

      result.SourcePath = path;
      try
      {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using (var package = new ExcelPackage(new FileInfo(path)))
        {
          var ws = package.Workbook.Worksheets.FirstOrDefault();
          if (ws == null || ws.Dimension == null)
          {
            result.ErrorMessage = "시트가 비어 있음: " + path;
            return result;
          }

          int colCount = ws.Dimension.End.Column;
          int rowCount = ws.Dimension.End.Row;
          var map = BuildHeaderMap(ws, colCount);

          // 필수 컬럼(구버전 Full List에도 존재)
          int cStatus = Col(map, "Status");
          int cSGuid = Col(map, "S: GUID", "S:GUID");
          int cSConn = Col(map, "S: Connector GUID", "S:Connector GUID");
          int cSTag = Col(map, "S: Tag", "S:Tag");
          int cSInfo = Col(map, "S: Connector Information", "S:Connector Information");
          int cSConnTag = Col(map, "S: Connection Tag", "S:Connection Tag");
          int cTConnTag = Col(map, "T: Connection Tag", "T:Connection Tag");
          int cTInfo = Col(map, "T: Connector Information", "T:Connector Information", "T: Connector Info", "T:Connector Info");
          int cTDiv = Col(map, "T: Division", "T:Division");
          int cTGuid = Col(map, "T: GUID", "T:GUID");
          int cTTag = Col(map, "T: Tag", "T:Tag");
          int cTName = Col(map, "T: Name", "T:Name", "T: Instance Name", "T:Instance Name");
          // 신규(Admin VOC 이후) — 없으면 공란
          int cSBim = Col(map, "S:BIM ID", "S: BIM ID", "S:Bim Id", "S: Bim Id");
          int cSInst = Col(map, "S:Instance ID", "S: Instance ID", "S:Instance Id");

          var rows = new List<FullTraceRow>(Math.Max(0, rowCount - 1));
          for (int r = 2; r <= rowCount; r++)
          {
            rows.Add(new FullTraceRow
            {
              Status = Cell(ws, r, cStatus),
              SBimId = Cell(ws, r, cSBim),
              SInstanceId = Cell(ws, r, cSInst),
              SGuid = Cell(ws, r, cSGuid),
              SConnectorGuid = Cell(ws, r, cSConn),
              STag = Cell(ws, r, cSTag),
              SConnectorInformation = Cell(ws, r, cSInfo),
              SConnectionTag = Cell(ws, r, cSConnTag),
              TConnectionTag = Cell(ws, r, cTConnTag),
              TConnectorInformation = Cell(ws, r, cTInfo),
              TDivision = Cell(ws, r, cTDiv),
              TGuid = Cell(ws, r, cTGuid),
              TTag = Cell(ws, r, cTTag),
              TName = Cell(ws, r, cTName)
            });
          }

          result.Rows = rows;
          Log.Information(
            "[Demolition] FullList loaded: Line={Line}, File={File}, Rows={Rows:N0}",
            line, Path.GetFileName(path), rows.Count);
        }
      }
      catch (Exception ex)
      {
        result.ErrorMessage = ex.Message;
        Log.Error(ex, "[Demolition] FullList load failed: {Path}", path);
      }

      return result;
    }

    public static string ResolveFullListPath(string line)
    {
      if (string.IsNullOrWhiteSpace(line)) return null;
      string db = line.Trim();

      var dirs = new List<string>();
      string primary = Path.Combine(GlobalConfig.DailyReportPath, "Excel");
      dirs.Add(primary);
      // 현장 경로 표기 차이: "Daily Report" 공백
      string altRoot = GlobalConfig.DailyReportPath.Replace("DailyReport", "Daily Report");
      if (!string.Equals(altRoot, GlobalConfig.DailyReportPath, StringComparison.OrdinalIgnoreCase))
        dirs.Add(Path.Combine(altRoot, "Excel"));

      foreach (var dir in dirs.Distinct(StringComparer.OrdinalIgnoreCase))
      {
        if (!Directory.Exists(dir)) continue;

        string[] exact =
        {
          Path.Combine(dir, db + "_Full_List.xlsx"),
          Path.Combine(dir, db + "_full_list.xlsx"),
          Path.Combine(dir, db.ToLowerInvariant() + "_Full_List.xlsx"),
          Path.Combine(dir, db.ToLowerInvariant() + "_full_list.xlsx"),
          Path.Combine(dir, db.ToUpperInvariant() + "_Full_List.xlsx")
        };
        foreach (var p in exact)
        {
          if (File.Exists(p)) return p;
        }

        try
        {
          var hit = Directory.GetFiles(dir, "*.xlsx")
            .FirstOrDefault(f =>
            {
              string name = Path.GetFileNameWithoutExtension(f);
              return name.StartsWith(db, StringComparison.OrdinalIgnoreCase)
                     && name.IndexOf("full", StringComparison.OrdinalIgnoreCase) >= 0
                     && name.IndexOf("list", StringComparison.OrdinalIgnoreCase) >= 0;
            });
          if (hit != null) return hit;
        }
        catch
        {
          // ignore enumeration errors
        }
      }

      return null;
    }

    private static Dictionary<string, int> BuildHeaderMap(ExcelWorksheet ws, int colCount)
    {
      var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
      for (int c = 1; c <= colCount; c++)
      {
        string h = ws.Cells[1, c].Text?.Trim();
        if (string.IsNullOrEmpty(h)) continue;
        if (!map.ContainsKey(h)) map[h] = c;
      }
      return map;
    }

    private static int Col(Dictionary<string, int> map, params string[] names)
    {
      foreach (var n in names)
      {
        int c;
        if (map.TryGetValue(n, out c)) return c;
      }
      return -1;
    }

    private static string Cell(ExcelWorksheet ws, int row, int col)
    {
      if (col < 1) return string.Empty;
      var v = ws.Cells[row, col].Text;
      return v == null ? string.Empty : v.Trim();
    }
  }
}
