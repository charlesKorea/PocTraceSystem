using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using OfficeOpenXml;

namespace PocTrace.Core.Engine.Reporting
{
    /// <summary>배치 Excel에서 eq code 헤더 열을 설비 Tag로 읽는다.</summary>
    public static class EqCodeSheet
    {
        public sealed class ReadResult
        {
            public List<string> Values = new List<string>();
            public int NonBlank;
            public int Duplicates;
            public string SheetName = "";
            public bool HeaderFound;
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
                int column = 0;
                for (int col = sheet.Dimension.Start.Column; col <= lastCol; col++)
                {
                    if (!IsEqCodeHeader(CellText(sheet, headerRow, col)))
                        continue;
                    column = col;
                    result.HeaderFound = true;
                    break;
                }
                if (!result.HeaderFound)
                    return result;

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int lastRow = sheet.Dimension.End.Row;
                for (int row = headerRow + 1; row <= lastRow; row++)
                {
                    string text = CellText(sheet, row, column);
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

        public static bool IsEqCodeHeader(string header)
        {
            if (string.IsNullOrWhiteSpace(header))
                return false;
            var chars = new List<char>(header.Length);
            foreach (char ch in header.Trim().ToLowerInvariant())
            {
                if (char.IsWhiteSpace(ch) || ch == '_' || ch == '-' || ch == ':')
                    continue;
                chars.Add(ch);
            }
            return new string(chars.ToArray()) == "eqcode";
        }

        private static string CellText(ExcelWorksheet sheet, int row, int column)
        {
            object raw = sheet.Cells[row, column].Value;
            string text = raw == null ? "" : Convert.ToString(raw, CultureInfo.InvariantCulture);
            return (text ?? "").Trim();
        }
    }
}
