using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OfficeOpenXml;

namespace PocTrace.DeletedPipeLength.Input
{
    public static class DeletedPipeExcelReader
    {
        public sealed class LoadResult
        {
            public List<DeletedPipeInputRow> Rows { get; set; } = new List<DeletedPipeInputRow>();
            public string ErrorMessage { get; set; }
            public bool Success => string.IsNullOrEmpty(ErrorMessage);
        }

        public static LoadResult Load(string filePath, string fallbackLine)
        {
            var result = new LoadResult();
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                result.ErrorMessage = "파일을 찾을 수 없습니다.";
                return result;
            }

            try
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using (var package = new ExcelPackage(new FileInfo(filePath)))
                {
                    var ws = package.Workbook.Worksheets.FirstOrDefault();
                    if (ws == null || ws.Dimension == null)
                    {
                        result.ErrorMessage = "시트가 비어 있습니다.";
                        return result;
                    }

                    int colSite = FindHeaderCol(ws, 1, "site", "라인", "line", "db", "project");
                    int colEqp = FindHeaderCol(ws, 1, "eqpid", "eqp id", "eqp", "tag", "설비", "설비id");
                    if (colEqp < 0)
                    {
                        result.ErrorMessage = "EQPID 헤더가 없습니다.";
                        return result;
                    }

                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (int r = 2; r <= ws.Dimension.End.Row; r++)
                    {
                        string eqp = CellText(ws, r, colEqp);
                        if (string.IsNullOrWhiteSpace(eqp))
                            continue;

                        string site = colSite > 0 ? CellText(ws, r, colSite) : string.Empty;
                        var row = new DeletedPipeInputRow
                        {
                            RowNumber = r,
                            SiteRaw = site,
                            EqpId = eqp.Trim()
                        };

                        if (string.IsNullOrWhiteSpace(site))
                        {
                            row.MappedLine = fallbackLine;
                            row.IsValid = !string.IsNullOrWhiteSpace(fallbackLine);
                            row.LoadStatus = row.IsValid ? "OK" : "NoLine";
                        }
                        else if (SiteLineMapper.TryMap(site, out string dbKey))
                        {
                            row.MappedLine = dbKey;
                            row.IsValid = true;
                            row.LoadStatus = "OK";
                        }
                        else
                        {
                            row.LoadStatus = "InvalidSite";
                            row.IsValid = false;
                        }

                        if (row.IsValid)
                        {
                            string key = row.MappedLine + "|" + row.EqpId;
                            if (!seen.Add(key))
                                continue;
                        }

                        result.Rows.Add(row);
                    }
                }

                if (result.Rows.Count == 0)
                    result.ErrorMessage = "EQPID 행이 없습니다.";
            }
            catch (Exception ex)
            {
                result.ErrorMessage = "Excel 읽기 실패: " + ex.Message;
            }

            return result;
        }

        private static string CellText(ExcelWorksheet ws, int row, int col)
        {
            var value = ws.Cells[row, col].Value;
            return value == null ? string.Empty : Convert.ToString(value).Trim();
        }

        private static int FindHeaderCol(ExcelWorksheet ws, int headerRow, params string[] aliases)
        {
            int endCol = ws.Dimension.End.Column;
            for (int c = 1; c <= endCol; c++)
            {
                string header = Normalize(CellText(ws, headerRow, c));
                if (header.Length == 0)
                    continue;
                for (int i = 0; i < aliases.Length; i++)
                {
                    if (header == Normalize(aliases[i]))
                        return c;
                }
            }
            return -1;
        }

        private static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            var chars = text.Trim().ToLowerInvariant().Where(ch => !char.IsWhiteSpace(ch) && ch != '_' && ch != '-' && ch != ':').ToArray();
            return new string(chars);
        }
    }
}
