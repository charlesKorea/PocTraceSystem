using System;
using System.Collections.Generic;
using System.IO;
using OfficeOpenXml;
using PocTrace.Core.Engine.DeletedPipe;

namespace PocTrace.DeletedPipeLength.Export
{
    public static class DeletedPipeExcelWriter
    {
        public static void Write(string path, IList<DeletedPipeReport> reports)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            var file = new FileInfo(path);
            if (file.Exists)
                file.Delete();

            using (var package = new ExcelPackage(file))
            {
                WriteSummary(package.Workbook.Worksheets.Add("요약"), reports);
                WriteBom(package.Workbook.Worksheets.Add("BOM"), reports);
                WriteDetails(package.Workbook.Worksheets.Add("배관"), reports);
                WriteUnmatched(package.Workbook.Worksheets.Add("미매칭"), reports);
                package.Save();
            }
        }

        private static void WriteSummary(ExcelWorksheet ws, IList<DeletedPipeReport> reports)
        {
            string[] headers =
            {
                "DB", "EQPID", "삭제설비수", "삭제POC수", "배관수", "길이합_m", "길이합_mm", "피팅수", "구분", "비고"
            };
            WriteHeaders(ws, headers);
            int row = 2;
            for (int i = 0; i < reports.Count; i++)
            {
                var items = reports[i].Summaries;
                for (int s = 0; s < items.Count; s++)
                {
                    var item = items[s];
                    ws.Cells[row, 1].Value = item.DbName;
                    ws.Cells[row, 2].Value = item.EqpId;
                    ws.Cells[row, 3].Value = item.EquipmentCount;
                    ws.Cells[row, 4].Value = item.PocCount;
                    ws.Cells[row, 5].Value = item.PipeCount;
                    ws.Cells[row, 6].Value = item.LengthMeters;
                    ws.Cells[row, 7].Value = item.LengthMillimeters;
                    ws.Cells[row, 8].Value = item.FittingCount;
                    ws.Cells[row, 9].Value = item.Kind;
                    ws.Cells[row, 10].Value = item.Note;
                    row++;
                }
            }
            ws.Cells[ws.Dimension.Address].AutoFitColumns();
        }

        private static void WriteBom(ExcelWorksheet ws, IList<DeletedPipeReport> reports)
        {
            string[] headers = { "DB", "EQPID", "Fitting Name", "QCode", "수량" };
            WriteHeaders(ws, headers);
            int row = 2;
            for (int i = 0; i < reports.Count; i++)
            {
                var items = reports[i].Bom;
                for (int b = 0; b < items.Count; b++)
                {
                    var item = items[b];
                    ws.Cells[row, 1].Value = item.DbName;
                    ws.Cells[row, 2].Value = item.EqpId;
                    ws.Cells[row, 3].Value = item.FittingName;
                    ws.Cells[row, 4].Value = item.QCode;
                    ws.Cells[row, 5].Value = item.Count;
                    row++;
                }
            }
            if (ws.Dimension != null)
                ws.Cells[ws.Dimension.Address].AutoFitColumns();
        }

        private static void WriteDetails(ExcelWorksheet ws, IList<DeletedPipeReport> reports)
        {
            string[] headers =
            {
                "DB", "EQPID", "설비InstanceId", "설비Tag", "설비삭제시각", "시작POC",
                "배관ID", "Utility", "길이_m", "길이_mm", "배관삭제시각", "순서", "공유", "작업자", "회사"
            };
            WriteHeaders(ws, headers);
            int row = 2;
            for (int i = 0; i < reports.Count; i++)
            {
                var items = reports[i].Details;
                for (int d = 0; d < items.Count; d++)
                {
                    var item = items[d];
                    ws.Cells[row, 1].Value = item.DbName;
                    ws.Cells[row, 2].Value = item.EqpId;
                    ws.Cells[row, 3].Value = item.EquipmentInstanceId.ToString();
                    ws.Cells[row, 4].Value = item.EquipmentTag;
                    ws.Cells[row, 5].Value = FormatTime(item.EquipmentDeletedAt);
                    ws.Cells[row, 6].Value = item.StartPocId.ToString();
                    ws.Cells[row, 7].Value = item.PipeId.ToString();
                    ws.Cells[row, 8].Value = item.UtilityName;
                    ws.Cells[row, 9].Value = item.LengthMeters;
                    ws.Cells[row, 10].Value = item.LengthMillimeters;
                    ws.Cells[row, 11].Value = FormatTime(item.PipeDeletedAt);
                    ws.Cells[row, 12].Value = item.Order;
                    ws.Cells[row, 13].Value = item.Shared ? "Y" : "";
                    ws.Cells[row, 14].Value = item.UserName;
                    ws.Cells[row, 15].Value = item.CompanyName;
                    row++;
                }
            }
            if (ws.Dimension != null)
                ws.Cells[ws.Dimension.Address].AutoFitColumns();
        }

        private static void WriteUnmatched(ExcelWorksheet ws, IList<DeletedPipeReport> reports)
        {
            string[] headers = { "DB", "EQPID", "사유" };
            WriteHeaders(ws, headers);
            int row = 2;
            for (int i = 0; i < reports.Count; i++)
            {
                var items = reports[i].Unmatched;
                for (int u = 0; u < items.Count; u++)
                {
                    ws.Cells[row, 1].Value = items[u].DbName;
                    ws.Cells[row, 2].Value = items[u].EqpId;
                    ws.Cells[row, 3].Value = items[u].Reason;
                    row++;
                }
            }
            if (ws.Dimension != null)
                ws.Cells[ws.Dimension.Address].AutoFitColumns();
        }

        private static void WriteHeaders(ExcelWorksheet ws, string[] headers)
        {
            for (int i = 0; i < headers.Length; i++)
                ws.Cells[1, i + 1].Value = headers[i];
            ws.Row(1).Style.Font.Bold = true;
        }

        private static string FormatTime(DateTime? value)
        {
            if (!value.HasValue)
                return string.Empty;
            return value.Value.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
