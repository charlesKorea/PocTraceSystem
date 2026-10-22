using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OfficeOpenXml;
using Serilog;

namespace PocTrace.Demolition.Input
{
  /// <summary>
  /// 철거설비 Input Excel 로더.
  /// 헤더(1행): Site | 공정 | 건설코드 | EQPID (영문/한글 alias 허용)
  /// </summary>
  public static class DemolitionExcelReader
  {
    public sealed class LoadResult
    {
      public List<DemolitionInputRow> Rows { get; set; } = new List<DemolitionInputRow>();
      public string ErrorMessage { get; set; }
      public bool Success => string.IsNullOrEmpty(ErrorMessage) && Rows != null;
      public int ValidCount => Rows?.Count(r => r.IsValid) ?? 0;
      public int InvalidCount => Rows?.Count(r => !r.IsValid) ?? 0;
      public int DistinctEqpCount =>
        Rows?.Where(r => r.IsValid)
          .Select(r => (r.MappedLine + "|" + r.EqpId).ToUpperInvariant())
          .Distinct()
          .Count() ?? 0;
    }

    public static LoadResult Load(string filePath)
    {
      var result = new LoadResult();
      if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
      {
        result.ErrorMessage = "파일을 찾을 수 없습니다.";
        return result;
      }

      try
      {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using (var package = new ExcelPackage(new FileInfo(filePath)))
        {
          var ws = package.Workbook.Worksheets.FirstOrDefault();
          if (ws == null || ws.Dimension == null)
          {
            result.ErrorMessage = "시트가 비어 있습니다.";
            return result;
          }

          int colSite = FindHeaderCol(ws, 1,
            "site", "라인", "line", "db", "project");
          int colProcess = FindHeaderCol(ws, 1,
            "공정", "process", "proc", "subdivision2", "sub2");
          int colConst = FindHeaderCol(ws, 1,
            "건설코드", "건설code", "constructioncode", "construction code", "constcode", "code");
          int colEqp = FindHeaderCol(ws, 1,
            "eqpid", "eqp id", "eqp", "tag", "설비", "설비id");

          if (colSite < 0 || colEqp < 0)
          {
            result.ErrorMessage =
              "헤더에 Site(또는 라인)와 EQPID(또는 Tag) 열이 필요합니다.\n" +
              "권장 헤더: Site | 공정 | 건설코드 | EQPID";
            return result;
          }

          int startRow = 2;
          int endRow = ws.Dimension.End.Row;
          for (int r = startRow; r <= endRow; r++)
          {
            string site = CellText(ws, r, colSite);
            string process = colProcess > 0 ? CellText(ws, r, colProcess) : string.Empty;
            string constCode = colConst > 0 ? CellText(ws, r, colConst) : string.Empty;
            string eqp = CellText(ws, r, colEqp);

            if (string.IsNullOrWhiteSpace(site) &&
                string.IsNullOrWhiteSpace(eqp) &&
                string.IsNullOrWhiteSpace(process) &&
                string.IsNullOrWhiteSpace(constCode))
              continue;

            var row = new DemolitionInputRow
            {
              RowNumber = r,
              SiteRaw = site,
              Process = process,
              ConstructionCode = constCode,
              EqpId = eqp
            };

            if (string.IsNullOrWhiteSpace(eqp))
            {
              row.LoadStatus = "InvalidEqpId";
              row.TraceStatus = "-";
            }
            else if (!SiteLineMapper.TryMap(site, out var dbKey))
            {
              row.MappedLine = string.Empty;
              row.LoadStatus = "InvalidSite";
              row.TraceStatus = "-";
            }
            else
            {
              row.MappedLine = dbKey;
              row.LoadStatus = "OK";
              row.TraceStatus = "Pending";
            }

            result.Rows.Add(row);
          }
        }

        if (result.Rows.Count == 0)
          result.ErrorMessage = "데이터 행이 없습니다. (헤더 다음 행부터 읽습니다)";

        Log.Information(
          "[DemolitionInput] Loaded {Total} rows from {File}. Valid={Valid}, Invalid={Invalid}, DistinctEqp={Distinct}",
          result.Rows.Count, Path.GetFileName(filePath), result.ValidCount, result.InvalidCount, result.DistinctEqpCount);
      }
      catch (Exception ex)
      {
        Log.Error(ex, "[DemolitionInput] Failed to load {Path}", filePath);
        result.ErrorMessage = "Excel 읽기 실패: " + ex.Message;
      }

      return result;
    }

    private static string CellText(ExcelWorksheet ws, int row, int col)
    {
      var v = ws.Cells[row, col].Value;
      return v == null ? string.Empty : Convert.ToString(v).Trim();
    }

    private static int FindHeaderCol(ExcelWorksheet ws, int headerRow, params string[] aliases)
    {
      if (ws.Dimension == null) return -1;
      int endCol = ws.Dimension.End.Column;
      for (int c = 1; c <= endCol; c++)
      {
        string h = NormalizeHeader(CellText(ws, headerRow, c));
        if (string.IsNullOrEmpty(h)) continue;
        foreach (var a in aliases)
        {
          if (h == NormalizeHeader(a))
            return c;
        }
      }
      return -1;
    }

    private static string NormalizeHeader(string s)
    {
      if (string.IsNullOrWhiteSpace(s)) return string.Empty;
      return new string(s.Trim().ToLowerInvariant()
        .Where(ch => !char.IsWhiteSpace(ch) && ch != '_' && ch != '-' && ch != ':')
        .ToArray());
    }
  }
}
