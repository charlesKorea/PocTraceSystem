using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using PocTrace.Core.Infrastructure;
using PocTrace.Demolition.Jobs;
using Serilog;

namespace PocTrace.Demolition.Export
{
  /// <summary>
  /// 철거설비 검수 Excel — 첫 시트=종합(EQPID×7수량), 이후 상세 7시트.
  /// </summary>
  public static class DemolitionExcelExporter
  {
    public static readonly string SummarySheetName = DemolitionUiText.SheetSummary;

    public sealed class ExportResult
    {
      public string OutputPath { get; set; }
      public int SummaryRowCount { get; set; }
      public int Sheet1RowCount { get; set; }
      public int Sheet2RowCount { get; set; }
      public int Sheet3RowCount { get; set; }
      public int Sheet4RowCount { get; set; }
      public int Sheet5RowCount { get; set; }
      public int Sheet6RowCount { get; set; }
      public int Sheet7RowCount { get; set; }
      public long ElapsedMs { get; set; }
      public bool Success { get; set; }
      public string ErrorMessage { get; set; }
    }

    public static ExportResult Export(string path, DemolitionJobResult jobResult)
    {
      var result = new ExportResult { OutputPath = path };
      if (string.IsNullOrWhiteSpace(path))
      {
        result.Success = false;
        result.ErrorMessage = "출력 경로가 비어 있습니다.";
        return result;
      }

      try
      {
        path = ResolveWritablePath(path);
        result.OutputPath = path;

        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        var summary = jobResult?.SummaryRows;
        if (summary == null || summary.Count == 0)
        {
          var counter = jobResult?.SummaryCounter
            ?? new EqpSheetCounter(Enumerable.Empty<string>());
          summary = counter.ToSummaryRows();
        }

        var s1 = jobResult?.EquipmentInfoRows ?? new List<object[]>();
        var s2 = jobResult?.StatusEmpRows ?? new List<object[]>();
        var s3 = jobResult?.NozzleRows ?? new List<object[]>();
        var s4 = jobResult?.StickRows ?? new List<object[]>();
        var s5 = jobResult?.TakeOffRows ?? new List<object[]>();
        var s6 = jobResult?.HookupRows ?? new List<object[]>();
        var s7 = jobResult?.BimGroupEqpRows ?? new List<object[]>();

        result.SummaryRowCount = Math.Max(0, summary.Count - 1);
        result.Sheet1RowCount = s1.Count;
        result.Sheet2RowCount = s2.Count;
        result.Sheet3RowCount = s3.Count;
        result.Sheet4RowCount = s4.Count;
        result.Sheet5RowCount = s5.Count;
        result.Sheet6RowCount = s6.Count;
        result.Sheet7RowCount = s7.Count;

        var sw = Stopwatch.StartNew();
        using (var package = new ExcelPackage())
        {
          package.Workbook.CalcMode = ExcelCalcMode.Manual;

          WriteSummarySheet(package.Workbook.Worksheets.Add(SummarySheetName), summary);
          WriteDataSheet(package.Workbook.Worksheets.Add(EquipmentInfoProjector.SheetName),
            EquipmentInfoProjector.Headers, s1, Color.FromArgb(68, 114, 196));
          WriteDataSheet(package.Workbook.Worksheets.Add(StatusEmpProjector.SheetName),
            StatusEmpProjector.Headers, s2, Color.FromArgb(112, 173, 71));
          WriteDataSheet(package.Workbook.Worksheets.Add(FullListNozzleProjector.SheetName),
            FullListNozzleProjector.Headers, s3, Color.FromArgb(237, 125, 49));
          WriteDataSheet(package.Workbook.Worksheets.Add(FullListStickProjector.SheetName),
            FullListStickProjector.Headers, s4, Color.FromArgb(91, 155, 213));
          WriteDataSheet(package.Workbook.Worksheets.Add(FullListTakeOffProjector.SheetName),
            FullListTakeOffProjector.Headers, s5, Color.FromArgb(165, 165, 165));
          WriteDataSheet(package.Workbook.Worksheets.Add(HookupNumberProjector.SheetName),
            HookupNumberProjector.Headers, s6, Color.FromArgb(112, 48, 160));
          WriteDataSheet(package.Workbook.Worksheets.Add(BimGroupEqpProjector.SheetName),
            BimGroupEqpProjector.Headers, s7, Color.FromArgb(192, 80, 77));

          package.SaveAs(new FileInfo(path));
        }

        sw.Stop();
        result.ElapsedMs = sw.ElapsedMilliseconds;
        result.Success = true;
        Log.Information(
          "[DemolitionExcel] Saved {Path}: Summary={S0} Equip={R1} EMP={R2} Noz={R3} Stick={R4} TOF={R5} HU={R6} BG={R7} {Ms}ms",
          path, result.SummaryRowCount, result.Sheet1RowCount, result.Sheet2RowCount, result.Sheet3RowCount,
          result.Sheet4RowCount, result.Sheet5RowCount, result.Sheet6RowCount, result.Sheet7RowCount,
          result.ElapsedMs);
      }
      catch (Exception ex)
      {
        Log.Error(ex, "[DemolitionExcel] Export failed: {Path}", path);
        result.Success = false;
        result.ErrorMessage = ex.Message;
      }

      return result;
    }

    public static string SuggestDefaultPath(string inputPath)
    {
      string dir = ResolveDirectory(inputPath);
      string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
      return Path.Combine(dir, "Demolition_Inspection_" + stamp + ".xlsx");
    }

    public static string SuggestHtmlPath(string excelOrBasePath)
    {
      string dir = ResolveDirectory(excelOrBasePath);
      string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
      return Path.Combine(dir, "Demolition_Summary_" + stamp + ".html");
    }

    private static string ResolveDirectory(string pathOrDir)
    {
      if (!string.IsNullOrWhiteSpace(pathOrDir))
      {
        if (Directory.Exists(pathOrDir))
          return pathOrDir;
        string dir = Path.GetDirectoryName(pathOrDir);
        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
          return dir;
      }
      return PocTraceLocalPaths.AppDirectory(PocTraceLocalPaths.Demolition);
    }

    /// <summary>종합 시트: 1행=헤더(EQPID + 시트명), 2행~=수량.</summary>
    private static void WriteSummarySheet(ExcelWorksheet ws, IList<object[]> summaryRows)
    {
      if (summaryRows == null || summaryRows.Count == 0) return;

      var header = summaryRows[0];
      int cols = header.Length;
      for (int c = 0; c < cols; c++)
      {
        var cell = ws.Cells[1, c + 1];
        cell.Value = header[c];
        cell.Style.Font.Bold = true;
        cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
        cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(47, 84, 150));
        cell.Style.Font.Color.SetColor(Color.White);
        cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
      }

      if (summaryRows.Count > 1)
      {
        var data = summaryRows.Skip(1).ToList();
        ws.Cells[2, 1].LoadFromArrays(data);
      }

      ws.View.FreezePanes(2, 2);
      AutoFitSheet(ws, minWidth: 12, maxWidth: 48);
    }

    private static void WriteDataSheet(
      ExcelWorksheet ws,
      string[] headers,
      IList<object[]> data,
      Color headerBg)
    {
      for (int c = 0; c < headers.Length; c++)
      {
        var cell = ws.Cells[1, c + 1];
        cell.Value = headers[c];
        cell.Style.Font.Bold = true;
        cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
        cell.Style.Fill.BackgroundColor.SetColor(headerBg);
        cell.Style.Font.Color.SetColor(Color.White);
        cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
      }

      if (data != null && data.Count > 0)
        ws.Cells[2, 1].LoadFromArrays(data);

      ws.View.FreezePanes(2, 1);
      AutoFitSheet(ws, minWidth: 12, maxWidth: 56);
    }

    private static void WriteHeaderlessSheet(ExcelWorksheet ws, IList<object[]> data)
    {
      if (data != null && data.Count > 0)
        ws.Cells[1, 1].LoadFromArrays(data);
      AutoFitSheet(ws, minWidth: 12, maxWidth: 56);
    }

    private static void AutoFitSheet(ExcelWorksheet ws, double minWidth, double maxWidth)
    {
      if (ws == null || ws.Dimension == null) return;

      ws.Cells[ws.Dimension.Address].AutoFitColumns(minWidth, maxWidth);
    }

    private static string ResolveWritablePath(string path)
    {
      if (!File.Exists(path))
        return path;

      try
      {
        File.Delete(path);
        return path;
      }
      catch (IOException)
      {
        var alt = Path.Combine(
          Path.GetDirectoryName(path) ?? AppDomain.CurrentDomain.BaseDirectory,
          Path.GetFileNameWithoutExtension(path) + "_" + DateTime.Now.Ticks + ".xlsx");
        Log.Warning("Excel file locked. Writing to {Alt}", alt);
        return alt;
      }
    }
  }

  /// <summary>오프라인용 종합 HTML 요약 (CDN 없음).</summary>
  public static class DemolitionHtmlSummaryWriter
  {
    public static string Write(string path, DemolitionJobResult job)
    {
      if (string.IsNullOrWhiteSpace(path))
        throw new ArgumentException("path required", nameof(path));

      var summary = job?.SummaryRows;
      if (summary == null || summary.Count == 0)
        summary = (job?.SummaryCounter ?? new EqpSheetCounter(Enumerable.Empty<string>())).ToSummaryRows();

      var sb = new StringBuilder(16 * 1024);
      sb.AppendLine("<!DOCTYPE html>");
      sb.AppendLine("<html lang=\"ko\"><head><meta charset=\"utf-8\"/>");
      sb.AppendFormat("<title>{0}</title>\n", System.Net.WebUtility.HtmlEncode(DemolitionUiText.HtmlPageTitle));
      sb.AppendLine("<style>");
      sb.AppendLine("body{font-family:Segoe UI,Malgun Gothic,sans-serif;margin:24px;background:#f5f7fa;color:#222}");
      sb.AppendLine("h1{font-size:20px;margin:0 0 8px}");
      sb.AppendLine(".meta{color:#555;margin-bottom:16px;font-size:13px}");
      sb.AppendLine("table{border-collapse:collapse;background:#fff;box-shadow:0 1px 3px rgba(0,0,0,.08)}");
      sb.AppendLine("th,td{border:1px solid #d0d7de;padding:6px 10px;font-size:12px}");
      sb.AppendLine("th{background:#2f5496;color:#fff;position:sticky;top:0}");
      sb.AppendLine("tr:nth-child(even){background:#f6f8fa}");
      sb.AppendLine("td.num{text-align:right;font-variant-numeric:tabular-nums}");
      sb.AppendLine("td.eqp{font-weight:600}");
      sb.AppendLine(".cards{display:flex;flex-wrap:wrap;gap:10px;margin:12px 0 20px}");
      sb.AppendLine(".card{background:#fff;border:1px solid #d0d7de;border-radius:6px;padding:10px 14px;min-width:120px}");
      sb.AppendLine(".card b{display:block;font-size:18px;margin-top:4px}");
      sb.AppendLine("</style></head><body>");
      sb.AppendFormat("<h1>{0}</h1>\n", System.Net.WebUtility.HtmlEncode(DemolitionUiText.HtmlHeading));
      sb.AppendFormat(
        "<div class=\"meta\">{4}: {0:yyyy-MM-dd HH:mm:ss} · Line {1} · EQPID {2} · Trace {3:0.0}s</div>\n",
        DateTime.Now,
        job?.LinesProcessed ?? 0,
        job?.EqpAttempted ?? 0,
        job?.Elapsed.TotalSeconds ?? 0,
        System.Net.WebUtility.HtmlEncode(DemolitionUiText.Kz("생성", "生成")));

      var counter = job?.SummaryCounter;
      sb.AppendLine("<div class=\"cards\">");
      if (counter != null)
      {
        for (int i = 0; i < EqpSheetCounter.SheetCount; i++)
        {
          sb.AppendFormat(
            "<div class=\"card\">{0}<b>{1:N0}</b></div>\n",
            System.Net.WebUtility.HtmlEncode(EqpSheetCounter.DetailSheetNames[i]),
            counter.TotalForSheet(i));
        }
      }
      sb.AppendLine("</div>");

      sb.AppendLine("<table><thead><tr>");
      if (summary.Count > 0)
      {
        foreach (var h in summary[0])
          sb.Append("<th>").Append(System.Net.WebUtility.HtmlEncode(Convert.ToString(h))).Append("</th>");
      }
      sb.AppendLine("</tr></thead><tbody>");
      for (int r = 1; r < summary.Count; r++)
      {
        var row = summary[r];
        sb.Append("<tr>");
        for (int c = 0; c < row.Length; c++)
        {
          string cls = c == 0 ? "eqp" : "num";
          sb.Append("<td class=\"").Append(cls).Append("\">")
            .Append(System.Net.WebUtility.HtmlEncode(Convert.ToString(row[c])))
            .Append("</td>");
        }
        sb.AppendLine("</tr>");
      }
      sb.AppendLine("</tbody></table></body></html>");

      File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
      Log.Information("[DemolitionHtml] Wrote {Path}", path);
      return path;
    }
  }
}
