using LargeXlsx;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using PocTrace.Core.Infrastructure;
using PocTrace.Core.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;

namespace PocTrace.Core.Engine.Reporting
{
    /// <summary>
    /// TraceResult Details 시트 단일 구현 (Admin 대용량 LoadFromArrays 경로 = SoT).
    /// Viewer 멀티시트 리포트와 Admin Full/Error 덤프가 동일 스키마·쓰기 경로를 사용한다.
    /// </summary>
    public static class TraceResultExcelExporter
    {
        public enum TraceExcelWriterEngine
        {
            /// <summary>행 수·설정에 따라 EPPlus 또는 LargeXlsx 자동 선택.</summary>
            Auto,
            EpPlus,
            LargeXlsx
        }

        public sealed class ExcelExportMetrics
        {
            public string OutputPath { get; set; }
            public int RowCount { get; set; }
            public long TunedElapsedMs { get; set; }
            public long? LegacyElapsedMs { get; set; }
            public string WriterEngine { get; set; } = "EpPlus";
            public double TunedRowsPerSec => RowCount > 0 && TunedElapsedMs > 0
                ? RowCount / (TunedElapsedMs / 1000.0)
                : 0;
            public double? LegacyRowsPerSec => RowCount > 0 && LegacyElapsedMs.HasValue && LegacyElapsedMs.Value > 0
                ? RowCount / (LegacyElapsedMs.Value / 1000.0)
                : (double?)null;
            public double? SpeedupX => LegacyElapsedMs.HasValue && LegacyElapsedMs.Value > 0
                ? (double)LegacyElapsedMs.Value / Math.Max(1, TunedElapsedMs)
                : (double?)null;
        }

        /// <summary>Viewer Details 스키마(Module/PocNo 포함) — Admin Full/Error도 동일.
        /// VOC: S:BIM ID, S:Instance ID를 S:GUID 왼편에 추가.</summary>
        public static readonly string[] DetailHeaders =
        {
            "Check Result", "Fail Count", "Issue Type", "Feedback", "Issue Owner",
            "S: Name", "S: Subdivision1", "S: Subdivision2", "S: Tag", "S: Construction Code",
            "S:BIM ID", "S:Instance ID",
            "S: GUID",
            "S: Connector GUID", "분기배관수",
            "S: Connector Tag", "S: Connector Name", "S: Connector Use", "S: Connector Information", "S: Connection Tag",
            "S: Module Name", "S: PocNo",
            "Status",
            "T: Name", "T: Tag", "T: Construction Code", "T: GUID",
            "T: Connector GUID", "T: Connector Name", "T: Connector Information", "T: Connection Tag", "T: Connector Tag",
            "T: Module Name", "T: PocNo",
            "T: Division", "T: Subdivision1", "T: Subdivision2",
            "S: Utility Group", "S: Utility", "S: Connected UT", "T: Connected UT", "T: Utility", "S: Size"
        };

        private static readonly int[] ColumnWidths =
        {
            14, 10, 16, 30, 18,
            24, 20, 20, 20, 18,
            18, 14,
            38,
            38, 12,
            20, 24, 12, 30, 18,
            18, 10,
            10,
            24, 20, 18, 38,
            38, 24, 30, 18, 20,
            18, 10,
            16, 20, 20,
            18, 14, 16, 16, 14, 12
        };

        public static ExcelExportMetrics Export(
            string path,
            IList<TraceResult> data,
            bool enableLegacyComparison = false,
            int compareRowThreshold = 50000,
            TraceExcelWriterEngine writerEngine = TraceExcelWriterEngine.Auto)
        {
            if (data == null || data.Count == 0)
            {
                Log.Warning("Excel export skipped (no rows): {Path}", path);
                return new ExcelExportMetrics
                {
                    OutputPath = path,
                    RowCount = 0,
                    TunedElapsedMs = 0,
                    WriterEngine = ResolveWriterEngineLabel(writerEngine, 0)
                };
            }

            if (ShouldUseLargeXlsx(data.Count, writerEngine))
                return ExportLargeXlsx(path, data, enableLegacyComparison, compareRowThreshold);

            return ExportEpPlus(path, data, enableLegacyComparison, compareRowThreshold);
        }

        private static bool ShouldUseLargeXlsx(int rowCount, TraceExcelWriterEngine writerEngine)
        {
            if (writerEngine == TraceExcelWriterEngine.LargeXlsx)
                return true;
            if (writerEngine == TraceExcelWriterEngine.EpPlus)
                return false;

            return GlobalConfig.ExcelUseLargeXlsxForFullList
                   && rowCount >= GlobalConfig.ExcelLargeExportRowThreshold;
        }

        private static string ResolveWriterEngineLabel(TraceExcelWriterEngine engine, int rowCount)
        {
            return ShouldUseLargeXlsx(rowCount, engine) ? "LargeXlsx" : "EpPlus";
        }

        private static ExcelExportMetrics ExportEpPlus(
            string path,
            IList<TraceResult> data,
            bool enableLegacyComparison,
            int compareRowThreshold)
        {
            path = ResolveWritablePath(path);
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            var metrics = new ExcelExportMetrics
            {
                OutputPath = path,
                RowCount = data.Count,
                WriterEngine = "EpPlus"
            };

            var sw = Stopwatch.StartNew();
            using (var package = new ExcelPackage())
            {
                package.Workbook.CalcMode = ExcelCalcMode.Manual;
                var ws = package.Workbook.Worksheets.Add("Trace_Data");
                WriteDetailSheet(ws, data, styleIssueColumns: true);
                package.SaveAs(new FileInfo(path));
            }
            sw.Stop();
            metrics.TunedElapsedMs = sw.ElapsedMilliseconds;

            Log.Information(
                "[ExcelPerf] EpPlus export done: File={File}, Rows={Rows:N0}, ElapsedMs={ElapsedMs:N0}, RowsPerSec={Rps:N0}",
                Path.GetFileName(path), metrics.RowCount, metrics.TunedElapsedMs, metrics.TunedRowsPerSec);

            AppendLegacyComparisonIfNeeded(metrics, data, enableLegacyComparison, compareRowThreshold);
            Log.Information("Excel export completed: {Path} ({Count} rows, engine={Engine})", path, data.Count, metrics.WriterEngine);
            return metrics;
        }

        private static ExcelExportMetrics ExportLargeXlsx(
            string path,
            IList<TraceResult> data,
            bool enableLegacyComparison,
            int compareRowThreshold)
        {
            path = ResolveWritablePath(path);
            var metrics = new ExcelExportMetrics
            {
                OutputPath = path,
                RowCount = data.Count,
                WriterEngine = "LargeXlsx"
            };

            var headerBg = Color.FromArgb(0xb5, 0xd6, 0x92);
            var headerStyle = XlsxStyle.Default
                .With(new XlsxFont("Calibri", 11, Color.Black, bold: true))
                .With(new XlsxFill(headerBg))
                .With(XlsxBorder.Around(new XlsxBorder.Line(Color.Black, XlsxBorder.Style.Double)))
                .With(new XlsxAlignment(horizontal: XlsxAlignment.Horizontal.Center));

            // 데이터 행: 기본 흑색 글자 / 흰 배경. 헤더 스타일이 다음 행으로 이어지지 않게 명시.
            var dataStyle = XlsxStyle.Default;

            var columns = ColumnWidths
                .Take(DetailHeaders.Length)
                .Select(w => XlsxColumn.Formatted(count: 1, width: w))
                .ToArray();

            var sw = Stopwatch.StartNew();
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new XlsxWriter(
                stream,
                compressionLevel: (SharpCompress.Compressors.Deflate.CompressionLevel)System.IO.Compression.CompressionLevel.Fastest,
                true,
                requireCellReferences: true))
            {
                writer.BeginWorksheet("Trace_Data", columns: columns);

                writer.BeginRow().SetDefaultStyle(headerStyle);
                foreach (var header in DetailHeaders)
                    writer.Write(header);

                foreach (var result in data)
                {
                    var values = ToDetailRow(result);
                    writer.BeginRow().SetDefaultStyle(dataStyle);
                    foreach (var value in values)
                        WriteLargeXlsxCell(writer, value);
                }
            }
            sw.Stop();
            metrics.TunedElapsedMs = sw.ElapsedMilliseconds;

            Log.Information(
                "[ExcelPerf] LargeXlsx export done: File={File}, Rows={Rows:N0}, ElapsedMs={ElapsedMs:N0}, RowsPerSec={Rps:N0}",
                Path.GetFileName(path), metrics.RowCount, metrics.TunedElapsedMs, metrics.TunedRowsPerSec);

            AppendLegacyComparisonIfNeeded(metrics, data, enableLegacyComparison, compareRowThreshold);
            Log.Information("Excel export completed: {Path} ({Count} rows, engine={Engine})", path, data.Count, metrics.WriterEngine);
            return metrics;
        }

        private static void AppendLegacyComparisonIfNeeded(
            ExcelExportMetrics metrics,
            IList<TraceResult> data,
            bool enableLegacyComparison,
            int compareRowThreshold)
        {
            if (!enableLegacyComparison || data.Count < Math.Max(1, compareRowThreshold))
                return;

            var legacyTempPath = Path.Combine(
                Path.GetTempPath(),
                $"PocTrace_LegacyBenchmark_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.xlsx");

            try
            {
                var sw = Stopwatch.StartNew();
                WriteLegacyBenchmark(legacyTempPath, data);
                sw.Stop();
                metrics.LegacyElapsedMs = sw.ElapsedMilliseconds;

                Log.Information(
                    "[ExcelPerf][Compare] File={File}, Rows={Rows:N0}, Engine={Engine}, TunedMs={TunedMs:N0}, LegacyMs={LegacyMs:N0}, Speedup={Speedup:0.00}x, TunedRps={TunedRps:N0}, LegacyRps={LegacyRps:N0}",
                    Path.GetFileName(metrics.OutputPath),
                    metrics.RowCount,
                    metrics.WriterEngine,
                    metrics.TunedElapsedMs,
                    metrics.LegacyElapsedMs.Value,
                    metrics.SpeedupX ?? 0,
                    metrics.TunedRowsPerSec,
                    metrics.LegacyRowsPerSec ?? 0);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[ExcelPerf][Compare] Legacy benchmark failed for {File}", Path.GetFileName(metrics.OutputPath));
            }
            finally
            {
                try
                {
                    if (File.Exists(legacyTempPath)) File.Delete(legacyTempPath);
                }
                catch
                {
                    // temp cleanup failure is non-fatal
                }
            }
        }

        private static void WriteLargeXlsxCell(XlsxWriter writer, object value)
        {
            if (value == null)
            {
                writer.Write(string.Empty);
                return;
            }

            if (value is int i)
                writer.Write(i);
            else
                writer.Write(value.ToString());
        }

        /// <summary>
        /// Design Progress 완료 후 Excel 직전 — TraceResult.Steps 해제로 메모리 확보.
        /// Progress는 Pipe OpTime이 Steps에 있으므로 이 호출보다 먼저 실행해야 한다.
        /// </summary>
        public static void ReleaseTraceStepMemory(IEnumerable<TraceResult> results)
        {
            if (results == null) return;
            int released = 0;
            foreach (var result in results)
            {
                if (result?.Steps == null || result.Steps.Count == 0)
                    continue;
                result.Steps.Clear();
                result.Steps = null;
                released++;
            }

            if (released > 0)
                Log.Information("[Memory] Released TraceResult.Steps for {Count} trace rows before Excel export", released);
        }

        /// <summary>
        /// 기존 워크북의 시트에 Details를 LoadFromArrays로 기록한다 (Viewer SaveSingleFileReport용).
        /// </summary>
        public static void WriteDetailSheet(
            ExcelWorksheet ws,
            IList<TraceResult> data,
            bool styleIssueColumns = false,
            Color? headerBackground = null)
        {
            if (ws == null) throw new ArgumentNullException(nameof(ws));
            data = data ?? Array.Empty<TraceResult>();

            var headerBg = headerBackground ?? Color.FromArgb(0xb5, 0xd6, 0x92);

            for (int col = 0; col < DetailHeaders.Length; col++)
                ws.Cells[1, col + 1].Value = DetailHeaders[col];

            using (var headerRange = ws.Cells[1, 1, 1, DetailHeaders.Length])
            {
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Font.Name = "Calibri";
                headerRange.Style.Font.Size = 11;
                headerRange.Style.Font.Color.SetColor(Color.Black);
                headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                headerRange.Style.Fill.BackgroundColor.SetColor(headerBg);
                headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                headerRange.Style.Border.Top.Style = ExcelBorderStyle.Double;
                headerRange.Style.Border.Bottom.Style = ExcelBorderStyle.Double;
                headerRange.Style.Border.Left.Style = ExcelBorderStyle.Double;
                headerRange.Style.Border.Right.Style = ExcelBorderStyle.Double;
            }

            if (data.Count > 0)
            {
                var rows = data.Select(ToDetailRow);
                ws.Cells[2, 1].LoadFromArrays(rows);
            }

            int lastRow = data.Count + 1;

            if (styleIssueColumns && data.Count > 0)
            {
                using (var issueRange = ws.Cells[2, 1, lastRow, 2])
                {
                    issueRange.Style.Font.Color.SetColor(Color.MediumVioletRed);
                    issueRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    issueRange.Style.Fill.BackgroundColor.SetColor(Color.LightPink);
                    issueRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    issueRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    issueRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    issueRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    issueRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                }
            }

            ApplyColumnWidths(ws);
            ApplyNumberFormats(ws, lastRow);
            ws.View.FreezePanes(2, 1);
        }

        public static object[] ToDetailRow(TraceResult r)
        {
            if (r == null) return new object[DetailHeaders.Length];

            return new object[]
            {
                r.CheckResult ?? string.Empty,
                r.FailCount,
                r.IssueType ?? string.Empty,
                r.feedback ?? string.Empty,
                r.IssueBelongTo ?? string.Empty,
                r.StartNodeName ?? string.Empty,
                r.StartNodeSubDivision1 ?? string.Empty,
                r.StartNodeSubDivision2 ?? string.Empty,
                r.StartNodeTag ?? string.Empty,
                r.ConstructionCode ?? string.Empty,
                r.BimId ?? string.Empty,
                FormatInstanceIdText(r.InstanceNumericId),
                r.StartNodeGUID.ToString(),
                r.StartPocGUID ?? string.Empty,
                r.BranchedPipes,
                r.StartPocTag ?? string.Empty,
                r.StartPocName ?? string.Empty,
                DynamicPropertiesData.NormalizeConnectorUse(r.StartPocConnectorUse),
                r.StartPocInformation ?? string.Empty,
                r.StartPocConnectionTag ?? string.Empty,
                r.StartPocModuleName ?? string.Empty,
                r.StartPocNo ?? string.Empty,
                r.Status ?? string.Empty,
                r.TargetNodeName ?? string.Empty,
                r.TargetNodeTag ?? string.Empty,
                r.TargetConstructionCode ?? string.Empty,
                r.TargetNodeGUID.ToString(),
                r.TargetPocGUID ?? string.Empty,
                r.TargetPocName ?? string.Empty,
                r.TargetConnectorInformation ?? string.Empty,
                r.TargetPocConnectionTag ?? string.Empty,
                r.TargetPocTag ?? string.Empty,
                r.TargetPocModuleName ?? string.Empty,
                r.TargetPocNo ?? string.Empty,
                r.TargetNodeDivision ?? string.Empty,
                r.TargetNodeSubDivision1 ?? string.Empty,
                r.TargetNodeSubDivision2 ?? string.Empty,
                r.StartPocUtilityGroup ?? string.Empty,
                r.StartPocUtilityName ?? string.Empty,
                r.StartPocConnectedUtility ?? string.Empty,
                r.TargetPocConnectedUtility ?? string.Empty,
                r.TargetPocUtilityName ?? string.Empty,
                r.StartPocSize ?? string.Empty
            };
        }

        private static void WriteLegacyBenchmark(string path, IList<TraceResult> data)
        {
            using (var package = new ExcelPackage())
            {
                package.Workbook.CalcMode = ExcelCalcMode.Automatic;
                var ws = package.Workbook.Worksheets.Add("Trace_Data");

                for (int col = 0; col < DetailHeaders.Length; col++)
                    ws.Cells[1, col + 1].Value = DetailHeaders[col];

                int row = 2;
                foreach (var r in data)
                {
                    var values = ToDetailRow(r);
                    for (int c = 0; c < values.Length; c++)
                        ws.Cells[row, c + 1].Value = values[c];
                    row++;
                }

                ws.Cells.AutoFitColumns();
                package.SaveAs(new FileInfo(path));
            }
        }

        public static string ResolveWritablePath(string path)
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
                    Path.GetFileNameWithoutExtension(path) + $"_{DateTime.Now.Ticks}.xlsx");
                Log.Warning("Excel file locked. Writing to {Alt}", alt);
                return alt;
            }
        }

        private static void ApplyColumnWidths(ExcelWorksheet ws)
        {
            for (int i = 0; i < ColumnWidths.Length && i < DetailHeaders.Length; i++)
                ws.Column(i + 1).Width = ColumnWidths[i];
        }

        private static void ApplyNumberFormats(ExcelWorksheet ws, int lastRow)
        {
            if (lastRow < 2) return;

            // Fail Count (col 2), 분기배관수 (col 15) — S: Construction Code 삽입 반영
            ws.Cells[2, 2, lastRow, 2].Style.Numberformat.Format = "#,##0";
            ws.Cells[2, 15, lastRow, 15].Style.Numberformat.Format = "#,##0";
            // S:BIM ID (col 11), S:Instance ID (col 12) — 텍스트(콤마·과학 표기 방지)
            ws.Cells[2, 11, lastRow, 11].Style.Numberformat.Format = "@";
            ws.Cells[2, 12, lastRow, 12].Style.Numberformat.Format = "@";
        }

        /// <summary>S:Instance ID — Excel 숫자 서식(#,##0) 없이 전체 자릿수 텍스트.</summary>
        internal static string FormatInstanceIdText(long instanceNumericId)
        {
            if (instanceNumericId == 0) return string.Empty;
            return instanceNumericId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
