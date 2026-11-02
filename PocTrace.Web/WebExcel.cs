using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace PocTrace.Web
{
    /// <summary>화면에서 쓰는 작은 Excel 입출력: 배치 Trace 업로드(A열)와 검수 기록 내려받기.</summary>
    internal static class WebExcel
    {
        internal sealed class ColumnAResult
        {
            public List<string> Values = new List<string>();
            public int NonBlank;
            public int Duplicates;
            public string SheetName = "";
        }

        /// <summary>첫 시트 A열을 2행부터 읽는다. 1행은 제목으로 보고 건너뛴다.</summary>
        public static ColumnAResult ReadColumnA(byte[] bytes)
        {
            var result = new ColumnAResult();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var stream = new MemoryStream(bytes))
            using (var package = new ExcelPackage(stream))
            {
                ExcelWorksheet sheet = null;
                foreach (ExcelWorksheet candidate in package.Workbook.Worksheets)
                {
                    sheet = candidate;
                    break;
                }
                if (sheet == null || sheet.Dimension == null)
                    return result;

                result.SheetName = sheet.Name;
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int last = sheet.Dimension.End.Row;
                for (int row = 2; row <= last; row++)
                {
                    object raw = sheet.Cells[row, 1].Value;
                    string text = raw == null ? "" : Convert.ToString(raw, CultureInfo.InvariantCulture);
                    text = (text ?? "").Trim();
                    if (text.Length == 0)
                        continue;

                    result.NonBlank++;
                    if (!seen.Add(text))
                    {
                        result.Duplicates++;
                        continue;
                    }
                    result.Values.Add(text);
                }
            }
            return result;
        }

        public static byte[] BuildHistory(List<HistoryRow> rows, bool zh, Func<string, string> methodName)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var package = new ExcelPackage())
            {
                ExcelWorksheet sheet = package.Workbook.Worksheets.Add(zh ? "检查记录" : "검수기록");
                string[] heads = zh
                    ? new[] { "日期", "用户", "检查方式", "线路", "检查对象" }
                    : new[] { "날짜", "사용자", "검수 방식", "라인", "검수대상" };
                for (int c = 0; c < heads.Length; c++)
                    sheet.Cells[1, c + 1].Value = heads[c];

                int r = 2;
                foreach (HistoryRow row in rows)
                {
                    sheet.Cells[r, 1].Value = row.AtUtc.ToLocalTime();
                    sheet.Cells[r, 1].Style.Numberformat.Format = "yyyy-mm-dd hh:mm";
                    sheet.Cells[r, 2].Value = row.UserName;
                    sheet.Cells[r, 3].Value = methodName == null ? row.Method : methodName(row.Method);
                    sheet.Cells[r, 4].Value = row.Line;
                    sheet.Cells[r, 5].Value = row.Target;
                    r++;
                }

                using (ExcelRange header = sheet.Cells[1, 1, 1, heads.Length])
                {
                    header.Style.Font.Bold = true;
                    header.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    header.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0xE8, 0xEE, 0xF2));
                }
                sheet.View.FreezePanes(2, 1);
                if (r > 2)
                    sheet.Cells[1, 1, r - 1, heads.Length].AutoFilter = true;
                sheet.Column(1).Width = 18;
                sheet.Column(2).Width = 16;
                sheet.Column(3).Width = 14;
                sheet.Column(4).Width = 10;
                sheet.Column(5).Width = 32;
                return package.GetAsByteArray();
            }
        }
    }
}
