using System;
using System.IO;
using OfficeOpenXml;
using Serilog;
using WinFormsOffline.Core.Jobs;

namespace WinFormsOffline.Core.Reporting
{
  public static class SampleExcelExporter
  {
    public static string Export(string outputPath, SampleJobResult job)
    {
      if (string.IsNullOrWhiteSpace(outputPath))
        throw new ArgumentException("outputPath required", nameof(outputPath));

      ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
      var dir = Path.GetDirectoryName(outputPath);
      if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        Directory.CreateDirectory(dir);

      using (var pkg = new ExcelPackage())
      {
        var ws = pkg.Workbook.Worksheets.Add("Result");
        ws.Cells[1, 1].Value = "Item";
        ws.Cells[1, 1].Style.Font.Bold = true;
        int r = 2;
        if (job != null && job.Rows != null)
        {
          foreach (var row in job.Rows)
            ws.Cells[r++, 1].Value = row;
        }
        ws.Cells.AutoFitColumns();
        pkg.SaveAs(new FileInfo(outputPath));
      }

      Log.Information("Excel saved {Path}", outputPath);
      return outputPath;
    }
  }
}
