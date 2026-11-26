using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OfficeOpenXml;

namespace PocTrace.Core.Engine.Reporting
{
    /// <summary>배치 Excel에서 eq code 헤더 열을 설비 Tag로 읽는다.</summary>
    public static class EqCodeSheet
    {
        public sealed class ReadResult
        {
            public List<string> Values = new List<string>();
            public List<BatchUploadRow> Rows = new List<BatchUploadRow>();
            public int NonBlank;
            public int Duplicates;
            public string SheetName = "";
            public bool HeaderFound;
        }

        /// <summary>x2-m 배치 업로드 한 행. 헤더 위치와 무관하게 읽는다.</summary>
        public sealed class BatchUploadRow
        {
            public string EqCode { get; set; }
            public string Major { get; set; }
            public string Minor { get; set; }
            public string ConstructionCode { get; set; }
            public string Designer { get; set; }
            public string MoveInDate { get; set; }
            public string DrawingNeedDate { get; set; }
            public bool ConstructionPresent { get; set; }
        }

        public static byte[] Write(string header, params string[] tags)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var package = new ExcelPackage())
            {
                var sheet = package.Workbook.Worksheets.Add("tags");
                sheet.Cells[1, 2].Value = header;
                for (int i = 0; i < tags.Length; i++)
                    sheet.Cells[i + 2, 2].Value = tags[i];
                return package.GetAsByteArray();
            }
        }

        public static ReadResult Read(byte[] bytes)
        {
            var result = new ReadResult();
            if (bytes == null || bytes.Length == 0)
                return result;

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
                int headerRow = sheet.Dimension.Start.Row;
                int lastCol = sheet.Dimension.End.Column;
                int eqColumn = 0;
                int major = 0, minor = 0, construction = 0, designer = 0, moveIn = 0, drawingNeed = 0;
                for (int col = sheet.Dimension.Start.Column; col <= lastCol; col++)
                {
                    string key = Compact(CellText(sheet, headerRow, col));
                    if (key.Length == 0)
                        continue;
                    if (eqColumn == 0 && key == "eqcode")
                    {
                        eqColumn = col;
                        result.HeaderFound = true;
                    }
                    else if (major == 0 && (key == "大分类" || key == "대분류"))
                        major = col;
                    else if (minor == 0 && (key == "小分类" || key == "소분류"))
                        minor = col;
                    else if (construction == 0 && (key == "건설code" || key == "건설코드" || key == "建设代码" || key == "建设code"))
                        construction = col;
                    else if (designer == 0 && (key == "设计社" || key == "설계사"))
                        designer = col;
                    else if (moveIn == 0 && (key == "设备搬入日" || key == "설비반입일"))
                        moveIn = col;
                    else if (drawingNeed == 0 && (key == "出图需求日" || key == "출도요구일"))
                        drawingNeed = col;
                }
                if (!result.HeaderFound)
                    return result;

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int lastRow = sheet.Dimension.End.Row;
                for (int row = headerRow + 1; row <= lastRow; row++)
                {
                    string text = CellText(sheet, row, eqColumn);
                    if (text.Length == 0)
                        continue;
                    result.NonBlank++;
                    if (!seen.Add(text))
                    {
                        result.Duplicates++;
                        continue;
                    }
                    result.Values.Add(text);
                    result.Rows.Add(new BatchUploadRow
                    {
                        EqCode = text,
                        Major = ReadOptional(sheet, row, major),
                        Minor = ReadOptional(sheet, row, minor),
                        ConstructionCode = ReadOptional(sheet, row, construction),
                        Designer = ReadOptional(sheet, row, designer),
                        MoveInDate = ReadOptional(sheet, row, moveIn),
                        DrawingNeedDate = ReadOptional(sheet, row, drawingNeed),
                        ConstructionPresent = construction > 0
                    });
                }
            }
            return result;
        }

        public static bool IsEqCodeHeader(string header)
        {
            return Compact(header) == "eqcode";
        }

        private static string Compact(string header)
        {
            if (string.IsNullOrWhiteSpace(header))
                return "";
            var chars = new StringBuilder(header.Length);
            foreach (char ch in header.Trim())
            {
                if (char.IsWhiteSpace(ch) || ch == '_' || ch == '-' || ch == ':' || ch == '　')
                    continue;
                chars.Append(char.ToLowerInvariant(ch));
            }
            return chars.ToString();
        }

        private static string ReadOptional(ExcelWorksheet sheet, int row, int column)
        {
            if (column <= 0)
                return "";
            return CellText(sheet, row, column);
        }

        private static string CellText(ExcelWorksheet sheet, int row, int column)
        {
            var cell = sheet.Cells[row, column];
            object raw = cell.Value;
            if (raw is DateTime date)
                return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (raw is double serial)
            {
                string format = cell.Style.Numberformat.Format ?? "";
                if (format.IndexOf("yy", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    try
                    {
                        return DateTime.FromOADate(serial).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    }
                    catch (ArgumentException)
                    {
                    }
                }
            }
            string text = raw == null ? "" : Convert.ToString(raw, CultureInfo.InvariantCulture);
            return (text ?? "").Trim();
        }
    }
}
