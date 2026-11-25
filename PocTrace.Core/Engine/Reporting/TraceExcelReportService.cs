// [Refactor] 기존 PocTrace.UI/ViewModels/MainViewModel.cs의 Excel 리포트 생성 로직
// (SaveSingleExcel / ExportBatchSummaryExcel)을 Core 리포팅 계층으로 이전한 것.
// UI(ViewModel)는 이 서비스를 호출만 한다.
//   - Goal 2: UI 계층의 도메인(리포팅) 침범 제거
//   - Goal 1: 거대 메서드를 올바른 계층으로 이동 + 요약 집계는 EquipmentSummaryBuilder로 단일화
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Engine.Validation;
using PocTrace.Core.Infrastructure;
using PocTrace.Core.Infrastructure.Data;
using PocTrace.Core.Enums;
using PocTrace.Core.Models;
using Serilog;

namespace PocTrace.Core.Engine.Reporting
{
    public class TraceExcelReportService
    {
        private readonly ILogger _logger;

        public TraceExcelReportService(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 단일 파일 리포트(Summary / Details / Path Analytics / Hookup_Validation / 5D_Update_Upload)를 생성한다.
        /// 기존 MainViewModel.SaveSingleExcel 로직을 그대로 이전.
        /// </summary>
        public void SaveSingleFileReport(string path, List<TraceResult> details, IGraphCache cache)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            // 요약 데이터 생성 (Core 공통 빌더 사용)
            List<EquipmentTraceSummary> summaries = EquipmentSummaryBuilder.Build(details);

            using (var package = new ExcelPackage(new FileInfo(path)))
            {
                // =========================================================
                // Sheet 1: Summary
                // =========================================================
                var wsSum = package.Workbook.Worksheets.Add("Summary");
                wsSum.Cells["V1"].Value = TraceAppVersion.Display();

                string lineName = cache != null && !string.IsNullOrEmpty(cache.DbName)
                    ? cache.DbName
                    : (details != null && details.Count > 0 ? details[0].Line : "");
                int row = WriteSummaryTable(wsSum, summaries, lineName, 1);
                WriteNameParts(wsSum, summaries, row + 4);

                // Hookup 통계, 검증 시트, 치유 효과는 필요할 때 아래 참조 블록의 주석을 푼다.

                // =========================================================
                // 📊 [참조] Hookup Validation 통계. 호출하지 않는다.
                // =========================================================
                /*
                var validationService = new HookupValidationService(_logger);
                var validationData = validationService.Validate(details, cache);

                int hookupStatRow = row + 3;
                wsSum.Cells[hookupStatRow, 1].Value = "📊 Hookup Validation 통계";
                wsSum.Cells[hookupStatRow, 1].Style.Font.Bold = true;
                wsSum.Cells[hookupStatRow, 1].Style.Font.Size = 12;
                wsSum.Cells[hookupStatRow, 1, hookupStatRow, 4].Merge = true;

                string[] hookupStatHeaders = { "구분", "OK", "MISSING", "MISMATCH", "TAG_ERROR", "DATA_ERROR" };
                hookupStatRow++;
                for (int i = 0; i < hookupStatHeaders.Length; i++)
                {
                    var cell = wsSum.Cells[hookupStatRow, i + 1];
                    cell.Value = hookupStatHeaders[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(Color.LightCyan);
                }

                hookupStatRow++;
                int okCount = validationData.Count(x => x.Status == "OK");
                int missingCount = validationData.Count(x => x.Status == "MISSING");
                int mismatchCount = validationData.Count(x => x.Status == "MISMATCH");
                int tagErrorCount = validationData.Count(x => x.Status == "TAG_ERROR");
                int dataErrorCount = validationData.Count(x => x.Status == "DATA_ERROR");
                int totalCount = validationData.Count;

                // [신규] 전체 설비 수량
                int totalEquipCount = validationData.Select(x => x.EquipmentTag).Distinct().Count();

                wsSum.Cells[hookupStatRow, 1].Value = "수량";
                wsSum.Cells[hookupStatRow, 2].Value = okCount;
                wsSum.Cells[hookupStatRow, 3].Value = missingCount;
                wsSum.Cells[hookupStatRow, 4].Value = mismatchCount;
                wsSum.Cells[hookupStatRow, 5].Value = tagErrorCount;
                wsSum.Cells[hookupStatRow, 6].Value = dataErrorCount;

                // 색상 적용
                wsSum.Cells[hookupStatRow, 2].Style.Font.Color.SetColor(Color.Green);
                if (missingCount > 0) wsSum.Cells[hookupStatRow, 3].Style.Font.Color.SetColor(Color.Red);
                if (mismatchCount > 0) wsSum.Cells[hookupStatRow, 4].Style.Font.Color.SetColor(Color.Red);
                if (tagErrorCount > 0) wsSum.Cells[hookupStatRow, 5].Style.Font.Color.SetColor(Color.Orange);
                if (dataErrorCount > 0) wsSum.Cells[hookupStatRow, 6].Style.Font.Color.SetColor(Color.Purple);

                wsSum.Cells[hookupStatRow, 6].Value = $"Total: {totalCount}";
                wsSum.Cells[hookupStatRow, 6].Style.Font.Bold = true;

                // [신규] 전체 설비 수량 표시
                hookupStatRow++;
                wsSum.Cells[hookupStatRow, 1].Value = "대상 설비 수";
                wsSum.Cells[hookupStatRow, 2].Value = totalEquipCount;
                wsSum.Cells[hookupStatRow, 1].Style.Font.Bold = true;
                wsSum.Cells[hookupStatRow, 2].Style.Font.Bold = true;

                // [신규] 치유 효과 분석 (통계표 오른쪽에 추가)
                int healStartCol = 8; // H열부터 시작
                wsSum.Cells[hookupStatRow - 3, healStartCol].Value = "💊 치유 효과 분석 (5D 업데이트 적용 시)";
                wsSum.Cells[hookupStatRow - 3, healStartCol].Style.Font.Bold = true;
                wsSum.Cells[hookupStatRow - 3, healStartCol].Style.Font.Size = 12;
                wsSum.Cells[hookupStatRow - 3, healStartCol, hookupStatRow - 3, healStartCol + 3].Merge = true;

                string[] healHeaders = { "구분", "현재 오류", "치유 후 OK", "개선율" };
                for (int i = 0; i < healHeaders.Length; i++)
                {
                    var cell = wsSum.Cells[hookupStatRow - 2, healStartCol + i];
                    cell.Value = healHeaders[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
                }

                // 치유 가능한 데이터 (MISSING + MISMATCH)
                int curableCount = missingCount + mismatchCount;
                int okAfterHeal = okCount + curableCount;
                double improvementRate = totalCount > 0 ? Math.Round((double)curableCount / totalCount * 100, 1) : 0;

                // MISSING 치유 효과
                wsSum.Cells[hookupStatRow - 1, healStartCol].Value = "MISSING → OK";
                wsSum.Cells[hookupStatRow - 1, healStartCol + 1].Value = missingCount;
                wsSum.Cells[hookupStatRow - 1, healStartCol + 2].Value = missingCount;
                wsSum.Cells[hookupStatRow - 1, healStartCol + 3].Value = totalCount > 0 ? Math.Round((double)missingCount / totalCount * 100, 1) + "%" : "0%";

                // MISMATCH 치유 효과
                wsSum.Cells[hookupStatRow, healStartCol].Value = "MISMATCH → OK";
                wsSum.Cells[hookupStatRow, healStartCol + 1].Value = mismatchCount;
                wsSum.Cells[hookupStatRow, healStartCol + 2].Value = mismatchCount;
                wsSum.Cells[hookupStatRow, healStartCol + 3].Value = totalCount > 0 ? Math.Round((double)mismatchCount / totalCount * 100, 1) + "%" : "0%";

                // 전체 치유 효과
                wsSum.Cells[hookupStatRow + 1, healStartCol].Value = "총 치유 효과";
                wsSum.Cells[hookupStatRow + 1, healStartCol].Style.Font.Bold = true;
                wsSum.Cells[hookupStatRow + 1, healStartCol + 1].Value = curableCount;
                wsSum.Cells[hookupStatRow + 1, healStartCol + 1].Style.Font.Bold = true;
                wsSum.Cells[hookupStatRow + 1, healStartCol + 2].Value = okAfterHeal;
                wsSum.Cells[hookupStatRow + 1, healStartCol + 2].Style.Font.Bold = true;
                wsSum.Cells[hookupStatRow + 1, healStartCol + 2].Style.Font.Color.SetColor(Color.Green);
                wsSum.Cells[hookupStatRow + 1, healStartCol + 3].Value = improvementRate + "%";
                wsSum.Cells[hookupStatRow + 1, healStartCol + 3].Style.Font.Bold = true;
                wsSum.Cells[hookupStatRow + 1, healStartCol + 3].Style.Font.Color.SetColor(Color.Green);

                // [신규] 각 설비별 Hookup 검증 결과
                hookupStatRow += 3;
                wsSum.Cells[hookupStatRow, 1].Value = "📋 설비별 Hookup 검증 결과";
                wsSum.Cells[hookupStatRow, 1].Style.Font.Bold = true;
                wsSum.Cells[hookupStatRow, 1].Style.Font.Size = 12;
                wsSum.Cells[hookupStatRow, 1, hookupStatRow, 6].Merge = true;

                hookupStatRow++;
                string[] equipStatHeaders = { "EQP ID", "OK", "MISSING", "MISMATCH", "TAG_ERROR", "DATA_ERROR" };
                for (int i = 0; i < equipStatHeaders.Length; i++)
                {
                    var cell = wsSum.Cells[hookupStatRow, i + 1];
                    cell.Value = equipStatHeaders[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(Color.LightYellow);
                }

                // 설비별 통계
                var equipStats = validationData
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

                hookupStatRow++;
                foreach (var eq in equipStats)
                {
                    wsSum.Cells[hookupStatRow, 1].Value = eq.EquipTag;
                    wsSum.Cells[hookupStatRow, 2].Value = eq.OK;
                    wsSum.Cells[hookupStatRow, 3].Value = eq.MISSING;
                    wsSum.Cells[hookupStatRow, 4].Value = eq.MISMATCH;
                    wsSum.Cells[hookupStatRow, 5].Value = eq.TAG_ERROR;
                    wsSum.Cells[hookupStatRow, 6].Value = eq.DATA_ERROR;

                    // 오류가 있으면 빨간색
                    if (eq.MISSING > 0 || eq.MISMATCH > 0)
                    {
                        wsSum.Cells[hookupStatRow, 1].Style.Font.Color.SetColor(Color.Red);
                    }

                    for (int c = 1; c <= 6; c++)
                    {
                        wsSum.Cells[hookupStatRow, c].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    }

                    hookupStatRow++;
                }
                */

                wsSum.Cells.AutoFitColumns();
                wsSum.View.FreezePanes(2, 1);

                // =========================================================
                // Sheet 2: Poc
                // =========================================================
                var wsDet = package.Workbook.Worksheets.Add("Poc");
                TraceResultExcelExporter.WriteDetailSheet(
                    wsDet, details, styleIssueColumns: false, headerBackground: Color.LightGray);

                // =========================================================
                // 🛠️ Sheet 3: Path Analytics (BOM 및 배관 길이 집계) - 신규
                // =========================================================
                var wsPath = package.Workbook.Worksheets.Add("BOM");

                // 모든 탐색 결과에서 거쳐간 발자국(Steps)을 하나의 리스트로 평탄화(Flatten)
                var allSteps = details.Where(d => d.Steps != null).SelectMany(d => d.Steps).ToList();

                // 1. UT group별 Fitting 사용 횟수
                wsPath.Cells["A1"].Value = "Fitting Usage by UT Group";
                wsPath.Cells["A1:D1"].Merge = true;
                wsPath.Cells["A1"].Style.Font.Bold = true;

                string[] fittingHeaders = { "UT Group", "Fitting Name", "QCode", "Usage Count" };
                for (int i = 0; i < fittingHeaders.Length; i++)
                {
                    wsPath.Cells[2, i + 1].Value = fittingHeaders[i];
                    wsPath.Cells[2, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    wsPath.Cells[2, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                    wsPath.Cells[2, i + 1].Style.Font.Bold = true;
                }

                var fittingStats = details
                    .Where(d => d != null && d.Steps != null)
                    .SelectMany(d => d.Steps
                        .Where(s => s.NodeType == "Fitting" && !string.IsNullOrEmpty(s.NodeName))
                        .Select(s => new
                        {
                            Group = d.StartPocUtilityGroup ?? "",
                            s.NodeName,
                            s.QCode
                        }))
                    .GroupBy(s => new { s.Group, s.NodeName, s.QCode })
                    .Select(g => new
                    {
                        Group = g.Key.Group,
                        Name = g.Key.NodeName,
                        QCode = g.Key.QCode,
                        Count = g.Count()
                    })
                    .OrderBy(x => x.Group)
                    .ThenByDescending(x => x.Count)
                    .ToList();

                int pathRow = 3;
                foreach (var f in fittingStats)
                {
                    wsPath.Cells[pathRow, 1].Value = f.Group;
                    wsPath.Cells[pathRow, 2].Value = f.Name;
                    wsPath.Cells[pathRow, 3].Value = f.QCode;
                    wsPath.Cells[pathRow, 4].Value = f.Count;
                    pathRow++;
                }

                // 2. 배관(Pipe) 누적 길이 집계. 피팅 표가 D열까지라 F열부터 둔다.
                wsPath.Cells["F1"].Value = "Pipe Length Summary";
                wsPath.Cells["F1:G1"].Merge = true;
                wsPath.Cells["F1"].Style.Font.Bold = true;

                string[] pipeHeaders = { "Pipe Name / ID", "Total Length (m)" };
                for (int i = 0; i < pipeHeaders.Length; i++)
                {
                    wsPath.Cells[2, i + 6].Value = pipeHeaders[i];
                    wsPath.Cells[2, i + 6].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    wsPath.Cells[2, i + 6].Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                    wsPath.Cells[2, i + 6].Style.Font.Bold = true;
                }

                var pipeStats = allSteps.Where(s => s.NodeType.Contains("Pipe"))
                                        .GroupBy(s => s.NodeName ?? s.NodeId.ToString())
                                        .Select(g => new { Name = g.Key, TotalLen = g.Sum(x => x.Length) })
                                        .OrderByDescending(x => x.TotalLen).ToList();

                int pipeRow = 3;
                foreach (var p in pipeStats)
                {
                    wsPath.Cells[pipeRow, 6].Value = p.Name;
                    wsPath.Cells[pipeRow, 7].Value = Math.Round(p.TotalLen / EquipmentSummaryBuilder.MillimetersPerMeter, 1);
                    wsPath.Cells[pipeRow, 7].Style.Numberformat.Format = "0.0";
                    pipeRow++;
                }
                wsPath.Cells.AutoFitColumns();

                if (cache != null && DdwLine.IsCacheKey(cache.DbName))
                {
                    var wsTrace = package.Workbook.Worksheets.Add("Pipe");
                    for (int c = 0; c < DdwTraceSheet.Headers.Length; c++)
                    {
                        wsTrace.Cells[1, c + 1].Value = DdwTraceSheet.Headers[c];
                        wsTrace.Cells[1, c + 1].Style.Font.Bold = true;
                        wsTrace.Cells[1, c + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        wsTrace.Cells[1, c + 1].Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                    }
                    var traceRows = DdwTraceSheet.Build(cache, details);
                    for (int i = 0; i < traceRows.Count; i++)
                    {
                        var line = traceRows[i];
                        for (int c = 0; c < line.Length; c++)
                            wsTrace.Cells[i + 2, c + 1].Value = line[c];
                    }
                    wsTrace.View.FreezePanes(2, 1);
                    wsTrace.Cells.AutoFitColumns();
                }

                /*
                // Hookup_Validation, 5D_Update_Upload. 위 참조 블록과 함께 연다.
                if (validationData.Any())
                {
                    HookupExcelReportWriter.WriteDetailSheet(
                        package.Workbook.Worksheets.Add("Hookup_Validation"), validationData);
                    HookupExcelReportWriter.WriteUploadSheet(
                        package.Workbook.Worksheets.Add("5D_Update_Upload"),
                        HookupExcelReportWriter.FilterUploadCandidates(validationData));
                }
                */

                _logger.Information("💾 [Excel Export] 엑셀 파일 저장을 시도합니다. Path: {Path}", path);
                package.Save();
                _logger.Information("🎉 [Excel Export] 엑셀 파일이 성공적으로 생성되었습니다.");
            }
        }

        /// <summary>
        /// 배치 실행 시 통합 요약 파일(Summary 시트)을 생성한다.
        /// </summary>
        public void SaveBatchSummaryReport(string filePath, List<EquipmentTraceSummary> summaries, string line = null)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                var ws = package.Workbook.Worksheets.Add("Summary");
                ws.Cells["V1"].Value = TraceAppVersion.Display();
                var ordered = summaries == null
                    ? new List<EquipmentTraceSummary>()
                    : summaries.OrderBy(x => x.EquipmentTag).ToList();
                int row = WriteSummaryTable(ws, ordered, line, 1);
                WriteNameParts(ws, ordered, row + 4);
                ws.Cells.AutoFitColumns();
                package.Save();
            }
        }

        private static readonly string[] SummaryHeaders =
        {
            "Line", "EQ Code", "Result", "Issues", "eqp count", "Pocs",
            "BreakDown", "BOM(Hop)", "Length(m)", "Date", "Worker", "Company"
        };

        private static readonly string[] NameHeaders =
        {
            "Name", "Division", "Subdivision1", "Subdivision2",
            "Poc Count", "Error Poc Count", "Length(m)"
        };

        private static int WriteSummaryTable(ExcelWorksheet ws, List<EquipmentTraceSummary> summaries, string line, int startRow)
        {
            for (int i = 0; i < SummaryHeaders.Length; i++)
            {
                var cell = ws.Cells[startRow, i + 1];
                cell.Value = SummaryHeaders[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }

            int row = startRow + 1;
            if (summaries == null)
                return row;
            foreach (var s in summaries)
            {
                ws.Cells[row, 1].Value = line ?? "";
                ws.Cells[row, 2].Value = s.EquipmentTag;
                ws.Cells[row, 3].Value = s.CheckResult;
                if (s.CheckResult == "FAIL")
                    ws.Cells[row, 3].Style.Font.Color.SetColor(Color.Red);
                else if (s.CheckResult == "PASS")
                    ws.Cells[row, 3].Style.Font.Color.SetColor(Color.Green);
                ws.Cells[row, 4].Value = s.CountERR;
                ws.Cells[row, 5].Value = s.EquipmentCount;
                ws.Cells[row, 6].Value = s.TotalPocCount;
                ws.Cells[row, 7].Value = s.DerivedBranchCount;
                ws.Cells[row, 8].Value = s.TotalHopCount;
                ws.Cells[row, 9].Value = Math.Round(s.TotalLength, 1);
                ws.Cells[row, 9].Style.Numberformat.Format = "0.0";
                ws.Cells[row, 10].Value = s.LatestOpTime;
                ws.Cells[row, 11].Value = s.LatestUserName;
                ws.Cells[row, 12].Value = s.LatestCompanyName;
                for (int c = 1; c <= SummaryHeaders.Length; c++)
                    ws.Cells[row, c].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                row++;
            }
            return row;
        }

        private static int WriteNameParts(ExcelWorksheet ws, List<EquipmentTraceSummary> summaries, int startRow)
        {
            const int firstCol = 2;
            for (int i = 0; i < NameHeaders.Length; i++)
            {
                var cell = ws.Cells[startRow, firstCol + i];
                cell.Value = NameHeaders[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }

            int row = startRow + 1;
            if (summaries == null)
                return row;
            foreach (var s in summaries)
            {
                if (s.Names == null)
                    continue;
                foreach (var part in s.Names)
                {
                    ws.Cells[row, firstCol].Value = part.Name;
                    ws.Cells[row, firstCol + 1].Value = part.Division;
                    ws.Cells[row, firstCol + 2].Value = part.Subdivision1;
                    ws.Cells[row, firstCol + 3].Value = part.Subdivision2;
                    ws.Cells[row, firstCol + 4].Value = part.PocCount;
                    ws.Cells[row, firstCol + 5].Value = part.ErrorPocCount;
                    ws.Cells[row, firstCol + 6].Value = part.LengthM;
                    ws.Cells[row, firstCol + 6].Style.Numberformat.Format = "0.0";
                    for (int c = 0; c < NameHeaders.Length; c++)
                        ws.Cells[row, firstCol + c].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    row++;
                }
            }
            return row;
        }
    }
}
