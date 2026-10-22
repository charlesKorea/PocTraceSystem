using OfficeOpenXml;
using OfficeOpenXml.Style;
using PocTrace.Core.Engine.Validation;
using PocTrace.Core.Models.ReverseTrace;
using Serilog;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace PocTrace.NozzleTrace.Export
{
  public static class NozzleTraceExcelExporter
  {
    public static readonly string[] SummaryHeaders =
    {
      "DB", "KeyKind", "KeyValue",
      "Nozzle BIM ID", "Nozzle Instance ID", "Nozzle GUID", "Nozzle Name",
      "Start POC GUID", "Connection Tag", "Connector Tag",
      "Equipment Tag", "Equipment Sub1", "Equipment Sub2", "Equipment BIM ID", "Equipment GUID",
      "Hop Count", "Pipe Length", "Status", "Primary", "Detail"
    };

    public static readonly string[] DetailHeaders =
    {
      "DB", "KeyValue", "Connection Tag", "Path#", "Step", "NodeType", "NodeName", "Node GUID", "Length"
    };

    public static void Export(string path, IList<NozzleTraceRunResult> runs)
    {
      if (runs == null) runs = Array.Empty<NozzleTraceRunResult>();
      path = ResolveWritablePath(path);
      ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

      using (var package = new ExcelPackage())
      {
        package.Workbook.CalcMode = ExcelCalcMode.Manual;
        WriteSummary(package.Workbook.Worksheets.Add("Summary"), runs);
        WriteDetail(package.Workbook.Worksheets.Add("Path_Detail"), runs);
        package.SaveAs(new FileInfo(path));
      }

      Log.Information("NozzleTrace Excel export completed: {Path}", path);
    }

    private static void WriteSummary(ExcelWorksheet ws, IList<NozzleTraceRunResult> runs)
    {
      for (int c = 0; c < SummaryHeaders.Length; c++)
        ws.Cells[1, c + 1].Value = SummaryHeaders[c];

      using (var header = ws.Cells[1, 1, 1, SummaryHeaders.Length])
      {
        header.Style.Font.Bold = true;
        header.Style.Fill.PatternType = ExcelFillStyle.Solid;
        header.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0xb5, 0xd6, 0x92));
      }

      int row = 2;
      foreach (var run in runs)
      {
        if (run == null) continue;
        var input = run.Input;
        var resolution = run.Resolution;

        if (resolution == null || !resolution.Success)
        {
          ws.Cells[row, 1].Value = input?.DbName ?? string.Empty;
          ws.Cells[row, 2].Value = input?.KeyKind.ToString() ?? string.Empty;
          ws.Cells[row, 3].Value = input?.KeyValue ?? string.Empty;
          ws.Cells[row, 18].Value = resolution?.ErrorCode ?? "ERROR";
          ws.Cells[row, 20].Value = resolution?.ErrorMessage ?? string.Empty;
          ApplyRedFont(ws, row, SummaryHeaders.Length);
          row++;
          continue;
        }

        if (run.Traces == null || run.Traces.Count == 0)
        {
          WriteSummaryRow(ws, row++, input, resolution, null);
          continue;
        }

        foreach (var trace in run.Traces)
          WriteSummaryRow(ws, row++, input, resolution, trace);
      }

      ws.View.FreezePanes(2, 1);
      ws.Cells.AutoFitColumns();
    }

    private static void WriteSummaryRow(
      ExcelWorksheet ws,
      int row,
      NozzleTraceInput input,
      NozzleStartResolution resolution,
      ReverseTraceResult trace)
    {
      var nozzle = resolution.NozzleNode;
      var startPoc = trace?.Start?.StartPoc ?? resolution.StartPocs?.FirstOrDefault();
      var eqp = trace?.TerminalEquipment;

      ws.Cells[row, 1].Value = input?.DbName ?? string.Empty;
      ws.Cells[row, 2].Value = input?.KeyKind.ToString() ?? string.Empty;
      ws.Cells[row, 3].Value = input?.KeyValue ?? string.Empty;
      ws.Cells[row, 4].Value = nozzle?.BimId ?? string.Empty;
      ws.Cells[row, 5].Value = FormatInstanceId(nozzle?.InstanceNumericId ?? 0);
      ws.Cells[row, 6].Value = nozzle?.InstanceId.ToString() ?? string.Empty;
      ws.Cells[row, 7].Value = nozzle?.Name ?? string.Empty;
      ws.Cells[row, 8].Value = startPoc?.InstanceId.ToString() ?? string.Empty;
      ws.Cells[row, 9].Value = startPoc?.ReferenceTag ?? string.Empty;
      ws.Cells[row, 10].Value = startPoc?.Tag ?? string.Empty;
      ws.Cells[row, 11].Value = eqp?.Tag ?? string.Empty;
      ws.Cells[row, 12].Value = eqp?.Sub1Name ?? string.Empty;
      ws.Cells[row, 13].Value = eqp?.Sub2Name ?? string.Empty;
      ws.Cells[row, 14].Value = eqp?.BimId ?? string.Empty;
      ws.Cells[row, 15].Value = eqp?.InstanceId.ToString() ?? string.Empty;
      ws.Cells[row, 16].Value = trace?.Steps?.Count ?? 0;
      ws.Cells[row, 17].Value = trace?.TotalPipeLength ?? 0;
      string status = trace?.Status ?? resolution.ErrorCode ?? "NO_PATH";
      ws.Cells[row, 18].Value = status;
      ws.Cells[row, 19].Value = trace != null && trace.PrimaryPath ? "Yes" : "No";
      ws.Cells[row, 20].Value = trace?.StatusDetail ?? resolution.ErrorMessage ?? string.Empty;

      // non-OK 행 + Connection Tag↔Equipment Tag mismatch 행 빨간 글씨
      if (!string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase)
          || IsConnectionEquipmentMismatch(resolution, trace))
        ApplyRedFont(ws, row, SummaryHeaders.Length);
    }

    private static void WriteDetail(ExcelWorksheet ws, IList<NozzleTraceRunResult> runs)
    {
      for (int c = 0; c < DetailHeaders.Length; c++)
        ws.Cells[1, c + 1].Value = DetailHeaders[c];

      int row = 2;
      int pathIndex = 0;
      foreach (var run in runs)
      {
        if (run?.Traces == null) continue;
        foreach (var trace in run.Traces)
        {
            pathIndex++;
            if (trace?.Steps == null) continue;
            bool mismatch = IsConnectionEquipmentMismatch(run.Resolution, trace)
                            || !string.Equals(trace.Status, "OK", StringComparison.OrdinalIgnoreCase);
            foreach (var step in trace.Steps)
            {
              ws.Cells[row, 1].Value = run.Input?.DbName ?? string.Empty;
              ws.Cells[row, 2].Value = run.Input?.KeyValue ?? string.Empty;
              ws.Cells[row, 3].Value = trace.Start?.ReferenceTag ?? string.Empty;
              ws.Cells[row, 4].Value = pathIndex;
              ws.Cells[row, 5].Value = step.StepIndex;
              ws.Cells[row, 6].Value = step.NodeType ?? string.Empty;
              ws.Cells[row, 7].Value = step.NodeName ?? string.Empty;
              ws.Cells[row, 8].Value = step.NodeId.ToString();
              ws.Cells[row, 9].Value = step.Length;
              if (mismatch)
                ApplyRedFont(ws, row, DetailHeaders.Length);
              row++;
            }
        }
      }

      ws.View.FreezePanes(2, 1);
      ws.Cells.AutoFitColumns();
    }

    internal static bool IsConnectionEquipmentMismatch(
      NozzleStartResolution resolution,
      ReverseTraceResult trace)
    {
      string status = trace?.Status ?? resolution?.ErrorCode ?? "NO_PATH";
      if (string.Equals(status, "NO_PATH", StringComparison.OrdinalIgnoreCase)
          || string.Equals(status, "REF_TAG_MISMATCH", StringComparison.OrdinalIgnoreCase))
        return true;

      var startPoc = trace?.Start?.StartPoc ?? resolution?.StartPocs?.FirstOrDefault();
      string connectionTag = startPoc?.ReferenceTag ?? trace?.Start?.ReferenceTag ?? string.Empty;
      string equipmentTag = trace?.TerminalEquipment?.Tag ?? string.Empty;
      return !EquipmentTagMatcher.AreRelated(connectionTag, equipmentTag);
    }

    private static void ApplyRedFont(ExcelWorksheet ws, int row, int columnCount)
    {
      ws.Cells[row, 1, row, columnCount].Style.Font.Color.SetColor(Color.Red);
    }

    private static string FormatInstanceId(long id)
    {
      if (id == 0) return string.Empty;
      return id.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string ResolveWritablePath(string path)
    {
      if (!File.Exists(path)) return path;
      try
      {
        File.Delete(path);
        return path;
      }
      catch (IOException)
      {
        return Path.Combine(
          Path.GetDirectoryName(path) ?? AppDomain.CurrentDomain.BaseDirectory,
          Path.GetFileNameWithoutExtension(path) + "_" + DateTime.Now.Ticks + ".xlsx");
      }
    }
  }
}
