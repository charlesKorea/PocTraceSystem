using OfficeOpenXml;
using OfficeOpenXml.Style;
using PocTrace.Core.Engine.Validation;
using Serilog;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace PocTrace.Core.Engine.Reporting
{
    /// <summary>
    /// Hookup Validation / 5D_Update_Upload Excel 단일 구현 (Admin·Viewer 공용).
    /// </summary>
    public static class HookupExcelReportWriter
    {
        public static readonly string[] DetailHeaders =
        {
            "GUID", "Node Name", "Current Hookup", "Expected Hookup", "Status",
            "Utility Name", "Module Name", "Connector Tag", "PocNo",
            "작업날짜", "작업자", "소속회사"
        };

        /// <summary>Admin 전용: Summary + Error_Detail + 5D_Update_Upload 단일 파일.</summary>
        public static void Export(
            string path,
            IList<HookupValidationResult> allValidationResults,
            IList<HookupValidationResult> updateData)
        {
            if (allValidationResults == null) allValidationResults = Array.Empty<HookupValidationResult>();
            if (updateData == null) updateData = Array.Empty<HookupValidationResult>();

            path = TraceResultExcelExporter.ResolveWritablePath(path);
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            try
            {
                using (var package = new ExcelPackage())
                {
                    package.Workbook.CalcMode = ExcelCalcMode.Manual;
                    WriteSummarySheet(package.Workbook.Worksheets.Add("Summary"), allValidationResults);
                    WriteDetailSheet(package.Workbook.Worksheets.Add("Error_Detail"), allValidationResults);
                    WriteUploadSheet(package.Workbook.Worksheets.Add("5D_Update_Upload"), updateData);
                    package.SaveAs(new FileInfo(path));
                }

                Log.Information(
                    "Hookup Validation Excel Export Completed: {Path} ({Count} rows)",
                    path, allValidationResults.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Hookup Validation Excel Export Failed ({Path})", path);
            }
        }

        public static void WriteSummarySheet(ExcelWorksheet wsSummary, IList<HookupValidationResult> allValidationResults)
        {
            int okCount = allValidationResults.Count(x => x.Status == "OK");
            int missingCount = allValidationResults.Count(x => x.Status == "MISSING");
            int mismatchCount = allValidationResults.Count(x => x.Status == "MISMATCH");
            int tagErrorCount = allValidationResults.Count(x => x.Status == "TAG_ERROR");
            int dataErrorCount = allValidationResults.Count(x => x.Status == "DATA_ERROR");
            int totalCount = allValidationResults.Count;
            int totalEquipCount = allValidationResults.Select(x => x.EquipmentTag).Distinct().Count();

            wsSummary.Cells[1, 1].Value = "📊 Hookup Validation 통계";
            wsSummary.Cells[1, 1].Style.Font.Bold = true;
            wsSummary.Cells[1, 1].Style.Font.Size = 14;
            wsSummary.Cells[1, 1, 1, 4].Merge = true;

            string[] statHeaders = { "구분", "수량", "비율" };
            for (int i = 0; i < statHeaders.Length; i++)
            {
                var cell = wsSummary.Cells[3, i + 1];
                cell.Value = statHeaders[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
            }

            int row = 4;
            Action<string, int, Color> addStatRow = (label, count, fontColor) =>
            {
                wsSummary.Cells[row, 1].Value = label;
                wsSummary.Cells[row, 2].Value = count;
                wsSummary.Cells[row, 3].Value = totalCount > 0
                    ? Math.Round((double)count / totalCount * 100, 1) + "%"
                    : "0%";
                wsSummary.Cells[row, 2].Style.Font.Color.SetColor(fontColor);
                row++;
            };

            addStatRow("OK", okCount, Color.Green);
            addStatRow("MISSING", missingCount, Color.Red);
            addStatRow("MISMATCH", mismatchCount, Color.Red);
            addStatRow("TAG_ERROR", tagErrorCount, Color.Orange);
            addStatRow("DATA_ERROR", dataErrorCount, Color.Purple);

            wsSummary.Cells[row, 1].Value = "Total";
            wsSummary.Cells[row, 2].Value = totalCount;
            wsSummary.Cells[row, 3].Value = "100%";
            wsSummary.Cells[row, 1].Style.Font.Bold = true;
            wsSummary.Cells[row, 2].Style.Font.Bold = true;

            row++;
            wsSummary.Cells[row, 1].Value = "대상 설비 수";
            wsSummary.Cells[row, 2].Value = totalEquipCount;
            wsSummary.Cells[row, 1].Style.Font.Bold = true;
            wsSummary.Cells[row, 2].Style.Font.Bold = true;

            int healStartCol = 5;
            wsSummary.Cells[row - 3, healStartCol].Value = "💊 치유 효과 분석 (5D 업데이트 적용 시)";
            wsSummary.Cells[row - 3, healStartCol].Style.Font.Bold = true;
            wsSummary.Cells[row - 3, healStartCol].Style.Font.Size = 12;
            wsSummary.Cells[row - 3, healStartCol, row - 3, healStartCol + 3].Merge = true;

            string[] healHeaders = { "구분", "현재 오류", "치유 후 OK", "개선율" };
            for (int i = 0; i < healHeaders.Length; i++)
            {
                var cell = wsSummary.Cells[row - 2, healStartCol + i];
                cell.Value = healHeaders[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
            }

            int curableCount = missingCount + mismatchCount;
            int okAfterHeal = okCount + curableCount;
            double improvementRate = totalCount > 0
                ? Math.Round((double)curableCount / totalCount * 100, 1)
                : 0;

            wsSummary.Cells[row - 1, healStartCol].Value = "MISSING → OK";
            wsSummary.Cells[row - 1, healStartCol + 1].Value = missingCount;
            wsSummary.Cells[row - 1, healStartCol + 2].Value = missingCount;
            wsSummary.Cells[row - 1, healStartCol + 3].Value = totalCount > 0
                ? Math.Round((double)missingCount / totalCount * 100, 1) + "%"
                : "0%";

            wsSummary.Cells[row, healStartCol].Value = "MISMATCH → OK";
            wsSummary.Cells[row, healStartCol + 1].Value = mismatchCount;
            wsSummary.Cells[row, healStartCol + 2].Value = mismatchCount;
            wsSummary.Cells[row, healStartCol + 3].Value = totalCount > 0
                ? Math.Round((double)mismatchCount / totalCount * 100, 1) + "%"
                : "0%";

            wsSummary.Cells[row + 1, healStartCol].Value = "총 치유 효과";
            wsSummary.Cells[row + 1, healStartCol].Style.Font.Bold = true;
            wsSummary.Cells[row + 1, healStartCol + 1].Value = curableCount;
            wsSummary.Cells[row + 1, healStartCol + 1].Style.Font.Bold = true;
            wsSummary.Cells[row + 1, healStartCol + 2].Value = okAfterHeal;
            wsSummary.Cells[row + 1, healStartCol + 2].Style.Font.Bold = true;
            wsSummary.Cells[row + 1, healStartCol + 2].Style.Font.Color.SetColor(Color.Green);
            wsSummary.Cells[row + 1, healStartCol + 3].Value = improvementRate + "%";
            wsSummary.Cells[row + 1, healStartCol + 3].Style.Font.Bold = true;
            wsSummary.Cells[row + 1, healStartCol + 3].Style.Font.Color.SetColor(Color.Green);

            row += 3;
            wsSummary.Cells[row, 1].Value = "📋 설비별 Hookup 검증 결과";
            wsSummary.Cells[row, 1].Style.Font.Bold = true;
            wsSummary.Cells[row, 1].Style.Font.Size = 12;
            wsSummary.Cells[row, 1, row, 6].Merge = true;

            row++;
            string[] equipStatHeaders = { "EQP ID", "OK", "MISSING", "MISMATCH", "TAG_ERROR", "DATA_ERROR" };
            for (int i = 0; i < equipStatHeaders.Length; i++)
            {
                var cell = wsSummary.Cells[row, i + 1];
                cell.Value = equipStatHeaders[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.LightYellow);
            }

            var equipStats = allValidationResults
                .GroupBy(x => x.EquipmentTag)
                .Select(g => new
                {
                    EquipTag = g.Key,
                    OK = g.Count(x => x.Status == "OK"),
                    MISSING = g.Count(x => x.Status == "MISSING"),
                    MISMATCH = g.Count(x => x.Status == "MISMATCH"),
                    TAG_ERROR = g.Count(x => x.Status == "TAG_ERROR"),
                    DATA_ERROR = g.Count(x => x.Status == "DATA_ERROR")
                })
                .OrderBy(x => x.EquipTag)
                .ToList();

            row++;
            foreach (var eq in equipStats)
            {
                wsSummary.Cells[row, 1].Value = eq.EquipTag;
                wsSummary.Cells[row, 2].Value = eq.OK;
                wsSummary.Cells[row, 3].Value = eq.MISSING;
                wsSummary.Cells[row, 4].Value = eq.MISMATCH;
                wsSummary.Cells[row, 5].Value = eq.TAG_ERROR;
                wsSummary.Cells[row, 6].Value = eq.DATA_ERROR;

                if (eq.MISSING > 0 || eq.MISMATCH > 0)
                    wsSummary.Cells[row, 1].Style.Font.Color.SetColor(Color.Red);
                row++;
            }

            for (int c = 1; c <= 6; c++) wsSummary.Column(c).Width = 18;
            for (int c = 7; c <= 9; c++) wsSummary.Column(c).Width = 14;
        }

        /// <summary>Viewer Hookup_Validation / Admin Error_Detail 공통 상세 시트.</summary>
        public static void WriteDetailSheet(ExcelWorksheet ws, IList<HookupValidationResult> results)
        {
            if (results == null) results = Array.Empty<HookupValidationResult>();

            for (int i = 0; i < DetailHeaders.Length; i++)
            {
                ws.Cells[1, i + 1].Value = DetailHeaders[i];
                ws.Cells[1, i + 1].Style.Font.Bold = true;
                ws.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightYellow);
            }

            var ordered = results.OrderBy(x => x.Status).ToList();
            if (ordered.Count > 0)
            {
                var rows = ordered.Select(v => new object[]
                {
                    v.ObjectId.ToString(),
                    v.ObjectName ?? string.Empty,
                    v.CurrentHookup ?? string.Empty,
                    v.ExpectedHookup ?? string.Empty,
                    v.Status ?? string.Empty,
                    v.UtilityName ?? string.Empty,
                    v.ModuleName ?? string.Empty,
                    v.ConnectorTag ?? string.Empty,
                    v.PocNo ?? string.Empty,
                    v.WorkOpTime ?? string.Empty,
                    v.WorkUserName ?? string.Empty,
                    v.WorkCompanyName ?? string.Empty
                });
                ws.Cells[2, 1].LoadFromArrays(rows);

                int lastRow = ordered.Count + 1;
                for (int r = 2; r <= lastRow; r++)
                {
                    var status = ws.Cells[r, 5].Text;
                    if (status == "MISSING" || status == "MISMATCH" || status == "TAG_ERROR" || status == "DATA_ERROR")
                    {
                        ws.Cells[r, 5].Style.Font.Color.SetColor(Color.Red);
                        ws.Cells[r, 5].Style.Font.Bold = true;
                    }
                }
            }

            ws.Column(1).Width = 38;
            ws.Column(2).Width = 28;
            ws.Column(3).Width = 18;
            ws.Column(4).Width = 18;
            ws.Column(5).Width = 14;
            ws.Column(6).Width = 16;
            ws.Column(7).Width = 18;
            ws.Column(8).Width = 20;
            ws.Column(9).Width = 10;
            ws.Column(10).Width = 14;
            ws.Column(11).Width = 14;
            ws.Column(12).Width = 18;
            ws.View.FreezePanes(2, 1);
        }

        public static void WriteUploadSheet(ExcelWorksheet wsUpload, IList<HookupValidationResult> updateData)
        {
            if (updateData == null) updateData = Array.Empty<HookupValidationResult>();

            wsUpload.Cells[1, 1].Value = "GUID";
            wsUpload.Cells[1, 2].Value = "Hookup Number";
            wsUpload.Cells[1, 1].Style.Font.Bold = true;
            wsUpload.Cells[1, 2].Style.Font.Bold = true;
            wsUpload.Cells[1, 1, 1, 2].Style.Fill.PatternType = ExcelFillStyle.Solid;
            wsUpload.Cells[1, 1, 1, 2].Style.Fill.BackgroundColor.SetColor(Color.LightGreen);

            if (updateData.Count > 0)
            {
                var rows = updateData.Select(u => new object[]
                {
                    u.ObjectId.ToString(),
                    u.ExpectedHookup ?? string.Empty
                });
                wsUpload.Cells[2, 1].LoadFromArrays(rows);
            }

            wsUpload.Column(1).Width = 38;
            wsUpload.Column(2).Width = 18;
        }

        /// <summary>Viewer용: OK/TAG_ERROR/DATA_ERROR/N-A 제외한 업로드 대상.</summary>
        public static List<HookupValidationResult> FilterUploadCandidates(IEnumerable<HookupValidationResult> validationData)
        {
            if (validationData == null) return new List<HookupValidationResult>();
            return validationData
                .Where(x => x.Status != "OK" &&
                            x.Status != "TAG_ERROR" &&
                            x.Status != "DATA_ERROR" &&
                            x.ExpectedHookup != "N/A(No ConnectorTag)")
                .ToList();
        }
    }
}
