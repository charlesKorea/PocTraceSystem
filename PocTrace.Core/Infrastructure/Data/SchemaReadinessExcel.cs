using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using PocTrace.Core.Infrastructure;

namespace PocTrace.Core.Infrastructure.Data
{
    public static class SchemaReadinessExcel
    {
        public static string Write(SchemaReadinessReport report, string directory)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("directory is required", nameof(directory));
            Directory.CreateDirectory(directory);

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            string path = Path.Combine(directory, "SchemaProbe_" + report.CreatedAt.ToString("yyyyMMdd_HHmmss") + ".xlsx");
            using (var package = new ExcelPackage())
            {
                WriteSummary(package.Workbook.Worksheets.Add("요약"), report);
                WriteGates(package.Workbook.Worksheets.Add("게이트"), report);
                WriteQueries(package.Workbook.Worksheets.Add("쿼리"), report);
                WriteFills(package.Workbook.Worksheets.Add("채움"), report);
                WriteGaps(package.Workbook.Worksheets.Add("부족"), report);
                WriteDist(package.Workbook.Worksheets.Add("분포"), report);
                WriteGuide(package.Workbook.Worksheets.Add("안내"), report);
                package.SaveAs(new FileInfo(path));
            }
            return path;
        }

        public static string ResolveOutputDirectory()
        {
            string[] candidates = new[]
            {
                Path.Combine(GlobalConfig.DailyReportPath ?? "", "SchemaProbe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SchemaProbe")
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(candidates[i]))
                    continue;
                try
                {
                    Directory.CreateDirectory(candidates[i]);
                    string probe = Path.Combine(candidates[i], ".write-test");
                    File.WriteAllText(probe, "ok");
                    File.Delete(probe);
                    return candidates[i];
                }
                catch (Exception)
                {
                }
            }
            throw new IOException("보고서를 저장할 폴더를 만들지 못했습니다.");
        }

        private static void WriteSummary(ExcelWorksheet sheet, SchemaReadinessReport report)
        {
            string[] headers = new[]
            {
                "DB", "SchemaKind", "판정", "한줄 설명", "시각"
            };
            Header(sheet, headers);
            int row = 2;
            var databases = report.Databases ?? new List<DatabaseReadiness>();
            for (int i = 0; i < databases.Count; i++)
            {
                var db = databases[i];
                sheet.Cells[row, 1].Value = db.DbName;
                sheet.Cells[row, 2].Value = report.ActiveSchema.ToString();
                sheet.Cells[row, 3].Value = db.Verdict;
                sheet.Cells[row, 4].Value = db.Headline;
                sheet.Cells[row, 5].Value = report.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
                PaintVerdict(sheet.Cells[row, 3], db.Verdict);
                row++;
            }
            if (databases.Count == 0)
                sheet.Cells[2, 1].Value = "대상 DB 없음";
            Finish(sheet, headers.Length, Math.Max(row - 1, 2));
            sheet.Column(4).Width = 88;
        }

        private static void WriteGates(ExcelWorksheet sheet, SchemaReadinessReport report)
        {
            string[] headers = new[] { "DB", "구분", "항목", "필수", "결과", "설명" };
            Header(sheet, headers);
            int row = 2;
            foreach (var db in report.Databases)
            {
                foreach (var gate in db.Gates)
                {
                    sheet.Cells[row, 1].Value = db.DbName;
                    sheet.Cells[row, 2].Value = gate.Group;
                    sheet.Cells[row, 3].Value = gate.Name;
                    sheet.Cells[row, 4].Value = gate.Required ? "필수" : "참고";
                    sheet.Cells[row, 5].Value = gate.Passed ? "있음" : "없음";
                    sheet.Cells[row, 6].Value = gate.Detail;
                    PaintVerdict(sheet.Cells[row, 5], gate.Passed ? "통과" : (gate.Required ? "실패" : "주의"));
                    row++;
                }
            }
            Finish(sheet, headers.Length, Math.Max(row - 1, 2));
            sheet.Column(6).Width = 56;
        }

        private static void WriteQueries(ExcelWorksheet sheet, SchemaReadinessReport report)
        {
            string[] headers = new[] { "DB", "쿼리", "성공", "ms", "행수", "메모", "오류" };
            Header(sheet, headers);
            int row = 2;
            foreach (var db in report.Databases)
            {
                foreach (var query in db.Queries)
                {
                    sheet.Cells[row, 1].Value = db.DbName;
                    sheet.Cells[row, 2].Value = query.Name;
                    sheet.Cells[row, 3].Value = query.Ok ? "성공" : "실패";
                    sheet.Cells[row, 4].Value = query.ElapsedMs;
                    if (query.RowCount >= 0)
                        sheet.Cells[row, 5].Value = query.RowCount;
                    sheet.Cells[row, 6].Value = query.Note;
                    sheet.Cells[row, 7].Value = query.Error;
                    PaintVerdict(sheet.Cells[row, 3], query.Ok ? "통과" : "실패");
                    row++;
                }
            }
            Finish(sheet, headers.Length, Math.Max(row - 1, 2));
            sheet.Column(7).Width = 72;
        }

        private static void WriteFills(ExcelWorksheet sheet, SchemaReadinessReport report)
        {
            string[] headers = new[] { "DB", "항목", "분자", "분모", "표시", "수준", "설명" };
            Header(sheet, headers);
            int row = 2;
            foreach (var db in report.Databases)
            {
                foreach (var fill in db.Fills)
                {
                    sheet.Cells[row, 1].Value = db.DbName;
                    sheet.Cells[row, 2].Value = fill.Name;
                    sheet.Cells[row, 3].Value = fill.Numerator;
                    sheet.Cells[row, 4].Value = fill.Denominator;
                    sheet.Cells[row, 5].Value = fill.PercentText;
                    sheet.Cells[row, 6].Value = fill.Level;
                    sheet.Cells[row, 7].Value = fill.Note;
                    PaintVerdict(sheet.Cells[row, 6], fill.Level);
                    row++;
                }
            }
            Finish(sheet, headers.Length, Math.Max(row - 1, 2));
            sheet.Column(7).Width = 64;
        }

        private static void WriteGaps(ExcelWorksheet sheet, SchemaReadinessReport report)
        {
            string[] headers = new[] { "DB", "분류", "키", "설명" };
            Header(sheet, headers);
            int row = 2;
            int written = 0;
            foreach (var db in report.Databases)
            {
                foreach (var gap in db.Gaps)
                {
                    sheet.Cells[row, 1].Value = db.DbName;
                    sheet.Cells[row, 2].Value = gap.Category;
                    sheet.Cells[row, 3].Value = gap.Key;
                    sheet.Cells[row, 4].Value = gap.Detail;
                    row++;
                    written++;
                }
            }
            if (written == 0)
            {
                sheet.Cells[2, 1].Value = "—";
                sheet.Cells[2, 2].Value = "부족 표본 없음";
                sheet.Cells[2, 4].Value = "게이트 실패로 조회를 생략했거나, 조건에 맞는 행이 없습니다.";
            }
            Finish(sheet, headers.Length, Math.Max(row - 1, 2));
            sheet.Column(3).Width = 42;
            sheet.Column(4).Width = 48;
        }

        private static void WriteDist(ExcelWorksheet sheet, SchemaReadinessReport report)
        {
            string[] headers = new[] { "DB", "구분", "키", "건수" };
            Header(sheet, headers);
            int row = 2;
            foreach (var db in report.Databases)
            {
                foreach (var dist in db.Distribution)
                {
                    sheet.Cells[row, 1].Value = db.DbName;
                    sheet.Cells[row, 2].Value = dist.Group;
                    sheet.Cells[row, 3].Value = dist.Key;
                    sheet.Cells[row, 4].Value = dist.Count;
                    row++;
                }
            }
            Finish(sheet, headers.Length, Math.Max(row - 1, 2));
        }

        private static void WriteGuide(ExcelWorksheet sheet, SchemaReadinessReport report)
        {
            string[] lines = new[]
            {
                "이 파일은 Admin [사전점검 (엑셀)] 결과입니다. 공유 캐시 GraphCache 는 만들지 않았습니다.",
                "SchemaKind = " + report.ActiveSchema + ". 쿼리 실패로 다른 프로파일을 실행하지 않습니다.",
                "판정 통과: 활성 프로파일의 필수 테이블/컬럼이 있고 그래프 조회가 끝났습니다.",
                "판정 주의: 조회는 끝났지만 DDW가 아직 없거나, 배관 연결끝점·Takeoff·dangling·미분류 어휘를 봐야 합니다.",
                "판정 실패: 활성 SchemaKind 에 필요한 테이블/컬럼이 없거나 그래프 로드가 예외로 끝났습니다.",
                "DDW 테이블 비율은 항상 표시합니다. SchemaKind 가 S5d 인 동안 0% 는 현장 DDW 가 아직 없다는 뜻입니다.",
                "WebDdw 컬럼이 없으면 WebDdw SQL 은 실행하지 않습니다. TbStar 로 바꿔 타지 않습니다.",
                "TbStar Equipment Tag 0% 는 실패가 아닙니다. Tag 속성 이름이 확정되기 전에는 비어 있습니다.",
                "채움 시트의 배관 연결끝점% 는 유효 NormalizedTarget 이 1개 이상인 배관 비율입니다.",
                "부족 시트는 분류당 최대 " + GraphFillMetrics.GapCap + "행입니다. 총건수는 채움 시트를 보세요.",
                "DDW live 건수는 OPERATIONTYPE < 2 입니다. S5D live 건수는 필터 없는 전체 건수입니다. 오류 칸에는 연결 비밀번호를 넣지 않습니다.",
                "캐시를 만들려면 이 보고서의 활성 프로파일이 통과한 뒤, v10 을 읽는 프로그램을 먼저 배포하고 Cache Only 를 실행합니다."
            };
            sheet.Cells[1, 1].Value = "항목";
            sheet.Cells[1, 1].Style.Font.Bold = true;
            for (int i = 0; i < lines.Length; i++)
                sheet.Cells[i + 2, 1].Value = lines[i];
            sheet.Column(1).Width = 120;
            sheet.View.FreezePanes(2, 1);
        }

        private static void Header(ExcelWorksheet sheet, string[] headers)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = sheet.Cells[1, i + 1];
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(226, 232, 240));
            }
        }

        private static void Finish(ExcelWorksheet sheet, int columns, int lastRow)
        {
            sheet.View.FreezePanes(2, 1);
            sheet.Cells[1, 1, lastRow, columns].AutoFilter = true;
            for (int i = 1; i <= columns; i++)
            {
                if (sheet.Column(i).Width < 14)
                    sheet.Column(i).Width = 18;
            }
        }

        private static void PaintVerdict(ExcelRange cell, string verdict)
        {
            if (string.IsNullOrEmpty(verdict))
                return;
            Color color;
            if (verdict == "실패" || verdict == "없음")
                color = Color.FromArgb(254, 226, 226);
            else if (verdict == "주의")
                color = Color.FromArgb(254, 243, 199);
            else if (verdict == "통과" || verdict == "있음" || verdict == "성공")
                color = Color.FromArgb(220, 252, 231);
            else
                return;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(color);
        }
    }
}
