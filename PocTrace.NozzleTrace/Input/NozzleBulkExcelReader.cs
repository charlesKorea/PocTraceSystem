using OfficeOpenXml;
using OfficeOpenXml.Style;
using PocTrace.Core.Infrastructure;
using PocTrace.Core.Models.ReverseTrace;
using Serilog;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
namespace PocTrace.NozzleTrace.Input
{
  /// <summary>
  /// 대량 입력 Excel 로더.
  /// 헤더: No | BimId | InstanceId | Guid (영문/한글 alias 허용)
  /// 키 우선순위(한 행에 여러 값): Guid &gt; BimId &gt; InstanceId
  /// </summary>
  public static class NozzleBulkExcelReader
  {
    public static readonly string[] TemplateHeaders = { "No", "BimId", "InstanceId", "Guid" };
    public sealed class LoadResult
    {
      public List<NozzleTraceInputRow> Rows { get; set; } = new List<NozzleTraceInputRow>();
      public List<string> Warnings { get; set; } = new List<string>();
      public string ErrorMessage { get; set; }
      public bool Success => string.IsNullOrEmpty(ErrorMessage) && Rows != null && Rows.Count > 0;
      public int SkippedEmpty { get; set; }
      public int SkippedInvalid { get; set; }
    }
    public static LoadResult Load(string filePath, string dbName)
    {
      var result = new LoadResult();
      if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
      {
        result.ErrorMessage = "파일을 찾을 수 없습니다.";
        return result;
      }
      if (string.IsNullOrWhiteSpace(dbName))
      {
        result.ErrorMessage = "DB가 선택되지 않았습니다.";
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
          int colNo = FindHeaderCol(ws, 1, "no", "번호", "#", "row", "순번");
          int colBim = FindHeaderCol(ws, 1, "bimid", "bim id", "bim", "bim_id");
          int colInst = FindHeaderCol(ws, 1,
            "instanceid", "instance id", "instance", "numericid", "numeric id", "instid");
          int colGuid = FindHeaderCol(ws, 1,
            "guid", "nodeguid", "node guid", "instanceguid", "node id");
          if (colBim < 0 && colInst < 0 && colGuid < 0)
          {
            result.ErrorMessage =
              "헤더에 BimId / InstanceId / Guid 중 하나 이상 필요합니다.\n" +
              "권장 헤더: No | BimId | InstanceId | Guid";
            return result;
          }
          int endRow = ws.Dimension.End.Row;
          for (int r = 2; r <= endRow; r++)
          {
            string noText = colNo > 0 ? CellText(ws, r, colNo) : r.ToString(CultureInfo.InvariantCulture);
            string bim = colBim > 0 ? CellText(ws, r, colBim) : string.Empty;
            string inst = colInst > 0 ? CellText(ws, r, colInst) : string.Empty;
            string guidText = colGuid > 0 ? CellText(ws, r, colGuid) : string.Empty;
            if (string.IsNullOrWhiteSpace(bim)
                && string.IsNullOrWhiteSpace(inst)
                && string.IsNullOrWhiteSpace(guidText)
                && string.IsNullOrWhiteSpace(noText))
            {
              result.SkippedEmpty++;
              continue;
            }
            if (string.IsNullOrWhiteSpace(bim)
                && string.IsNullOrWhiteSpace(inst)
                && string.IsNullOrWhiteSpace(guidText))
            {
              result.SkippedEmpty++;
              continue;
            }
            NozzleLookupKeyKind kind;
            string key;
            string pickNote;
            if (!TryResolveKey(guidText, bim, inst, out kind, out key, out pickNote))
            {
              result.SkippedInvalid++;
              result.Warnings.Add(string.Format(
                "행 {0}: 유효한 키 없음 ({1})",
                string.IsNullOrWhiteSpace(noText) ? r.ToString(CultureInfo.InvariantCulture) : noText,
                pickNote));
              continue;
            }
            int rowNumber = r - 1;
            int parsedNo;
            if (!string.IsNullOrWhiteSpace(noText)
                && int.TryParse(noText, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedNo))
              rowNumber = parsedNo;
            if (!string.IsNullOrEmpty(pickNote))
            {
              result.Warnings.Add(string.Format(
                "행 {0}: {1} → {2}={3}",
                rowNumber, pickNote, kind, key));
            }
            result.Rows.Add(new NozzleTraceInputRow
            {
              RowNumber = rowNumber,
              DbName = dbName.Trim(),
              KeyKind = kind,
              KeyValue = key
            });
          }
        }
        if (result.Rows.Count == 0)
        {
          result.ErrorMessage = "유효한 입력 행이 없습니다. BimId / InstanceId / Guid 중 하나를 채우세요.";
          return result;
        }
        Log.Information(
          "[NozzleBulk] Loaded {Count} rows from {File} (empty={Empty}, invalid={Invalid})",
          result.Rows.Count, Path.GetFileName(filePath), result.SkippedEmpty, result.SkippedInvalid);
      }
      catch (Exception ex)
      {
        Log.Error(ex, "[NozzleBulk] Load failed: {Path}", filePath);
        result.ErrorMessage = "Excel 읽기 실패: " + ex.Message;
      }
      return result;
    }
    /// <summary>우선순위: Guid &gt; BimId &gt; InstanceId</summary>
    public static bool TryResolveKey(
      string guidText,
      string bimId,
      string instanceId,
      out NozzleLookupKeyKind kind,
      out string key,
      out string note)
    {
      kind = NozzleLookupKeyKind.BimId;
      key = null;
      note = null;
      bool hasGuid = !string.IsNullOrWhiteSpace(guidText);
      bool hasBim = !string.IsNullOrWhiteSpace(bimId);
      bool hasInst = !string.IsNullOrWhiteSpace(instanceId);
      int filled = (hasGuid ? 1 : 0) + (hasBim ? 1 : 0) + (hasInst ? 1 : 0);
      if (hasGuid)
      {
        Guid g;
        if (Guid.TryParse(guidText.Trim(), out g))
        {
          kind = NozzleLookupKeyKind.NodeGuid;
          key = g.ToString();
          if (filled > 1)
            note = "복수 키 → Guid 우선";
          return true;
        }
        if (!hasBim && !hasInst)
        {
          note = "Guid 형식 오류: " + guidText.Trim();
          return false;
        }
      }
      if (hasBim)
      {
        kind = NozzleLookupKeyKind.BimId;
        key = bimId.Trim();
        if (filled > 1)
          note = "복수 키 → BimId 우선";
        return true;
      }
      if (hasInst)
      {
        long n;
        string raw = instanceId.Trim();
        // Excel 과학표기 대비
        if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)
            && !long.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out n))
        {
          double d;
          if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out d)
              && d == Math.Truncate(d)
              && d >= long.MinValue && d <= long.MaxValue)
            n = (long)d;
          else
          {
            note = "InstanceId 숫자 아님: " + raw;
            return false;
          }
        }
        kind = NozzleLookupKeyKind.InstanceNumericId;
        key = n.ToString(CultureInfo.InvariantCulture);
        return true;
      }
      note = "키 없음";
      return false;
    }
    public static string SaveTemplate(string directory)
    {
      if (string.IsNullOrWhiteSpace(directory))
        directory = PocTraceLocalPaths.AppDirectory(PocTraceLocalPaths.NozzleTrace);
      Directory.CreateDirectory(directory);
      string path = Path.Combine(directory, "NozzleTrace_Upload_Template.xlsx");
      ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
      using (var package = new ExcelPackage())
      {
        var ws = package.Workbook.Worksheets.Add("Input");
        for (int c = 0; c < TemplateHeaders.Length; c++)
          ws.Cells[1, c + 1].Value = TemplateHeaders[c];
        using (var header = ws.Cells[1, 1, 1, TemplateHeaders.Length])
        {
          header.Style.Font.Bold = true;
          header.Style.Fill.PatternType = ExcelFillStyle.Solid;
          header.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0xb5, 0xd6, 0x92));
        }
        // 샘플 3행 (각 키 타입 예시)
        ws.Cells[2, 1].Value = 1;
        ws.Cells[2, 2].Value = "5007474";
        ws.Cells[3, 1].Value = 2;
        ws.Cells[3, 3].Value = "123456789";
        ws.Cells[4, 1].Value = 3;
        ws.Cells[4, 4].Value = "00000000-0000-0000-0000-000000000001";
        ws.Cells[6, 1].Value = "규칙";
        ws.Cells[6, 2].Value = "한 행에 BimId/InstanceId/Guid 중 하나만 채우거나, 복수면 Guid > BimId > InstanceId 우선";
        ws.Cells.AutoFitColumns();
        package.SaveAs(new FileInfo(path));
      }
      return path;
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
        .Where(ch => !char.IsWhiteSpace(ch) && ch != '_' && ch != '-')
        .ToArray());
    }
    private static string CellText(ExcelWorksheet ws, int row, int col)
    {
      var val = ws.Cells[row, col].Value;
      if (val == null) return string.Empty;
      if (val is double d)
      {
        if (Math.Abs(d - Math.Truncate(d)) < 1e-9)
          return ((long)Math.Truncate(d)).ToString(CultureInfo.InvariantCulture);
        return d.ToString(CultureInfo.InvariantCulture);
      }
      return Convert.ToString(val, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
    }
  }
}
