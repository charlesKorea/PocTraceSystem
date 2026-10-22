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
                // 📊 Sheet 1: Summary (정답지 비교용 Status 집계표)
                // =========================================================
                var wsSum = package.Workbook.Worksheets.Add("Summary");

                // 💡 [개편] 사용자 요청 규격에 맞춘 심플한 헤더 구성
                string[] statusHeaders = { "설비id", "cap", "emp", "eqp", "ing", "noz", "tof", "err", "total" };
                // Header Styling
                for (int i = 0; i < statusHeaders.Length; i++)
                {
                    var cell = wsSum.Cells[1, i + 1];
                    cell.Value = statusHeaders[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    cell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                }

                int row = 2;
                foreach (var s in summaries)
                {
                    wsSum.Cells[row, 1].Value = s.EquipmentTag; // 설비id
                    wsSum.Cells[row, 2].Value = s.CountCAP;     // cap
                    wsSum.Cells[row, 3].Value = s.CountEMP;     // emp
                    wsSum.Cells[row, 4].Value = s.CountEQP;     // eqp
                    wsSum.Cells[row, 5].Value = s.CountING;     // ing
                    wsSum.Cells[row, 6].Value = s.CountNOZ;     // noz
                    wsSum.Cells[row, 7].Value = s.CountTOF;     // tof
                    wsSum.Cells[row, 8].Value = s.CountERR;     // err

                    // 💡 Total 수량 계산 (전체 분기 수와 동일)
                    wsSum.Cells[row, 9].Value = s.DerivedBranchCount;

                    // [수정] 모든 셀 가운데 정렬
                    for (int c = 1; c <= 9; c++)
                    {
                        wsSum.Cells[row, c].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    }

                    row++;
                }

                wsSum.Cells.AutoFitColumns();
                wsSum.View.FreezePanes(2, 1);

                // Header 정의
                int startRowSum = row + 3; // Status 표 아래에 여백을 두고 시작
                string[] sumHeaders = {
                    "설비ID",      // 1
		            "검수결과",     // 2
		            "Issue Count", // 3
		            "설비수량",     // 4
		            "POC수량",     // 5
		            "BreakDown",    // 6
				    "HOP",
                    "Length",      // 8
                    "작업날짜",     // 9 [신규]
                    "작업자",       // 10 [신규]
                    "소속회사"      // 11 [신규]
                };

                // Header Styling
                for (int i = 0; i < sumHeaders.Length; i++)
                {
                    var cell = wsSum.Cells[startRowSum, i + 1];
                    cell.Value = sumHeaders[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    cell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                }

                row = startRowSum + 1;
                foreach (var s in summaries)
                {
                    // 1) 설비ID
                    wsSum.Cells[row, 1].Value = s.EquipmentTag;

                    // 2) 검수결과 & 3) Issue Count
                    wsSum.Cells[row, 2].Value = s.CheckResult; // PASS or FAIL
                    wsSum.Cells[row, 3].Value = s.CountERR;

                    // 색상 처리 (FAIL = RED)
                    if (s.CheckResult == "FAIL")
                    {
                        wsSum.Cells[row, 2].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                    }
                    else
                    {
                        wsSum.Cells[row, 2].Style.Font.Color.SetColor(System.Drawing.Color.Green);
                    }

                    // 4) 설비수량
                    wsSum.Cells[row, 4].Value = s.EquipmentCount;

                    // 5) POC수량
                    wsSum.Cells[row, 5].Value = s.TotalPocCount;

                    // 6) BreakDown
                    wsSum.Cells[row, 6].Value = s.DerivedBranchCount;

                    wsSum.Cells[row, 7].Value = s.TotalHopCount;
                    wsSum.Cells[row, 8].Value = s.TotalLength;

                    // [신규] 작업자 정보
                    wsSum.Cells[row, 9].Value = s.LatestOpTime;
                    wsSum.Cells[row, 10].Value = s.LatestUserName;
                    wsSum.Cells[row, 11].Value = s.LatestCompanyName;

                    // [수정] 모든 셀 가운데 정렬
                    for (int c = 1; c <= 11; c++)
                    {
                        wsSum.Cells[row, c].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    }

                    row++;
                }

                // =========================================================
                // 📊 [신규] Sheet 1-2: Hookup Validation 통계 요약
                // =========================================================
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

                wsSum.Cells.AutoFitColumns();
                wsSum.View.FreezePanes(2, 1); // 1행 고정

                // =========================================================
                // Sheet 2: Details — Core TraceResultExcelExporter (LoadFromArrays)
                // =========================================================
                var wsDet = package.Workbook.Worksheets.Add("Details");
                TraceResultExcelExporter.WriteDetailSheet(
                    wsDet, details, styleIssueColumns: false, headerBackground: Color.LightGray);

                // =========================================================
                // 🛠️ Sheet 3: Path Analytics (BOM 및 배관 길이 집계) - 신규
                // =========================================================
                var wsPath = package.Workbook.Worksheets.Add("Path Analytics");

                // 모든 탐색 결과에서 거쳐간 발자국(Steps)을 하나의 리스트로 평탄화(Flatten)
                var allSteps = details.Where(d => d.Steps != null).SelectMany(d => d.Steps).ToList();

                // 1. Fitting 사용 횟수 집계
                wsPath.Cells["A1"].Value = "🔧 Fitting Usage (BOM)";
                wsPath.Cells["A1:B1"].Merge = true;
                wsPath.Cells["A1"].Style.Font.Bold = true;

                string[] fittingHeaders = { "Fitting Name", "QCode", "Usage Count" };
                for (int i = 0; i < fittingHeaders.Length; i++)
                {
                    wsPath.Cells[2, i + 1].Value = fittingHeaders[i];
                    wsPath.Cells[2, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    wsPath.Cells[2, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                    wsPath.Cells[2, i + 1].Style.Font.Bold = true;
                }

                // 💡 [핵심 보완] Name과 QCode를 동시에 묶어서(GroupBy) 집계해야 정확한 수량이 나옵니다.
                var fittingStats = allSteps.Where(s => s.NodeType == "Fitting" && !string.IsNullOrEmpty(s.NodeName))
                                           .GroupBy(s => new { s.NodeName, s.QCode })
                                           .Select(g => new
                                           {
                                               Name = g.Key.NodeName,
                                               QCode = g.Key.QCode,
                                               Count = g.Count()
                                           })
                                           .OrderByDescending(x => x.Count).ToList();

                int pathRow = 3;
                foreach (var f in fittingStats)
                {
                    wsPath.Cells[pathRow, 1].Value = f.Name;
                    wsPath.Cells[pathRow, 2].Value = f.QCode; // 💡 B열에 QCode 출력
                    wsPath.Cells[pathRow, 3].Value = f.Count; // 💡 C열에 수량 출력
                    pathRow++;
                }

                // 2. 배관(Pipe) 누적 길이 집계 (컬럼 우측으로 이동)
                wsPath.Cells["E1"].Value = "📏 Pipe Length Summary";
                wsPath.Cells["E1:F1"].Merge = true;
                wsPath.Cells["E1"].Style.Font.Bold = true;

                string[] pipeHeaders = { "Pipe Name / ID", "Total Length (m)" };
                for (int i = 0; i < pipeHeaders.Length; i++)
                {
                    // 💡 시작 열을 5(E열)로 변경
                    wsPath.Cells[2, i + 5].Value = pipeHeaders[i];
                    wsPath.Cells[2, i + 5].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    wsPath.Cells[2, i + 5].Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                    wsPath.Cells[2, i + 5].Style.Font.Bold = true;
                }

                var pipeStats = allSteps.Where(s => s.NodeType.Contains("Pipe"))
                                        .GroupBy(s => s.NodeName ?? s.NodeId.ToString())
                                        .Select(g => new { Name = g.Key, TotalLen = g.Sum(x => x.Length) })
                                        .OrderByDescending(x => x.TotalLen).ToList();

                int pipeRow = 3;
                foreach (var p in pipeStats)
                {
                    wsPath.Cells[pipeRow, 5].Value = p.Name;     // E열
                    wsPath.Cells[pipeRow, 6].Value = p.TotalLen; // F열
                    pipeRow++;
                }
                wsPath.Cells.AutoFitColumns();

                // =========================================================
                // 🛠️ Sheet 4–5: Hookup — Core HookupExcelReportWriter
                // =========================================================
                if (validationData.Any())
                {
                    HookupExcelReportWriter.WriteDetailSheet(
                        package.Workbook.Worksheets.Add("Hookup_Validation"), validationData);
                    HookupExcelReportWriter.WriteUploadSheet(
                        package.Workbook.Worksheets.Add("5D_Update_Upload"),
                        HookupExcelReportWriter.FilterUploadCandidates(validationData));
                }

                _logger.Information("💾 [Excel Export] 엑셀 파일 저장을 시도합니다. Path: {Path}", path);
                package.Save();
                _logger.Information("🎉 [Excel Export] 엑셀 파일이 성공적으로 생성되었습니다.");
            }
        }

        /// <summary>
        /// 배치 실행 시 통합 요약 파일(Total_Summary 단일 시트)을 생성한다.
        /// 기존 MainViewModel.ExportBatchSummaryExcel 로직을 그대로 이전(동기 버전).
        /// </summary>
        public void SaveBatchSummaryReport(string filePath, List<EquipmentTraceSummary> summaries)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                using (var package = new ExcelPackage(new FileInfo(filePath)))
                {
                    // Sheet 1: Summary (Details 시트는 생성하지 않음)
                    var ws = package.Workbook.Worksheets.Add("Total_Summary");

                    // Header (기존과 동일하게 맞춤)
                    string[] headers = {
                        "설비ID", "검수결과", "Issue Count", "설비수량", "POC수량", "BreakDown", "비고"
                    };

                    // Header Style
                    for (int i = 0; i < headers.Length; i++)
                    {
                        ws.Cells[1, i + 1].Value = headers[i];
                        ws.Cells[1, i + 1].Style.Font.Bold = true;
                        ws.Cells[1, i + 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        ws.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    }

                    // Data
                    int row = 2;
                    // Tag순 정렬
                    foreach (var s in summaries.OrderBy(x => x.EquipmentTag))
                    {
                        ws.Cells[row, 1].Value = s.EquipmentTag;

                        ws.Cells[row, 2].Value = s.CheckResult; // PASS/FAIL
                        if (s.CheckResult == "FAIL")
                            ws.Cells[row, 2].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                        else
                            ws.Cells[row, 2].Style.Font.Color.SetColor(System.Drawing.Color.Green);

                        ws.Cells[row, 3].Value = s.CountERR;
                        ws.Cells[row, 4].Value = s.EquipmentCount;
                        ws.Cells[row, 5].Value = s.TotalPocCount;
                        ws.Cells[row, 6].Value = s.DerivedBranchCount;
                        ws.Cells[row, 7].Value = s.ExcelFilePath; // 비고(개별파일경로 등)

                        row++;
                    }

                    ws.Cells.AutoFitColumns();
                    package.Save();
                }
        }
    }
}
