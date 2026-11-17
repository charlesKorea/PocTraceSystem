using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using OfficeOpenXml;
using PocTrace.Core.Infrastructure;
using PocTrace.Core.Infrastructure.Data;
using Serilog;

namespace PocTrace.Admin.Jobs
{
    public static class DdwUnknownTypeExcel
    {
        public static string Write(IList<DdwUnknownTypeHit> hits)
        {
            if (hits == null || hits.Count == 0)
                return null;
            string dir = GlobalConfig.GetCacheDirectory(DdwLine.CacheKey);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "ddw-undefined-types.xlsx");
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add("undefined");
                ws.Cells[1, 1].Value = "kind";
                ws.Cells[1, 2].Value = "code";
                ws.Cells[1, 3].Value = "count";
                ws.Cells[1, 4].Value = "note";
                for (int i = 0; i < hits.Count; i++)
                {
                    DdwUnknownTypeHit hit = hits[i];
                    ws.Cells[i + 2, 1].Value = hit.Kind;
                    ws.Cells[i + 2, 2].Value = hit.Code.ToString(CultureInfo.InvariantCulture);
                    ws.Cells[i + 2, 3].Value = hit.Count;
                    ws.Cells[i + 2, 4].Value = "정의가 없다. 값을 받으면 DdwTypeCodes 에 추가한다.";
                }
                ws.Cells[ws.Dimension.Address].AutoFitColumns(12, 80);
                package.SaveAs(new FileInfo(path));
            }
            Log.Information("[DDW] 정의되지 않은 타입 {Count}종을 {Path} 에 적었다.", hits.Count, path);
            return path;
        }
    }
}
