using System;
using System.IO;
using System.Text;
using WinFormsOffline.Core.Jobs;

namespace WinFormsOffline.Core.Reporting
{
  /// <summary>
  /// CDN 금지. Templates/report.html + 로컬 assets 만 사용.
  /// assets는 C:\OfflineDev\web-assets 에서 복사하세요.
  /// </summary>
  public static class LocalHtmlWriter
  {
    public static string Write(string outputHtmlPath, SampleJobResult job)
    {
      if (string.IsNullOrWhiteSpace(outputHtmlPath))
        throw new ArgumentException("outputHtmlPath required", nameof(outputHtmlPath));

      var dir = Path.GetDirectoryName(outputHtmlPath);
      if (string.IsNullOrEmpty(dir))
        dir = AppDomain.CurrentDomain.BaseDirectory;
      if (!Directory.Exists(dir))
        Directory.CreateDirectory(dir);

      var templateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
      var templateFile = Path.Combine(templateDir, "report.html");
      string html;
      if (File.Exists(templateFile))
        html = File.ReadAllText(templateFile, Encoding.UTF8);
      else
        html = FallbackHtml();

      var sb = new StringBuilder();
      if (job != null && job.Rows != null)
      {
        foreach (var row in job.Rows)
          sb.Append("<li>").Append(Escape(row)).Append("</li>");
      }

      html = html.Replace("{{TITLE}}", "Offline Report");
      html = html.Replace("{{GENERATED}}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
      html = html.Replace("{{MESSAGE}}", Escape(job != null ? job.Message : ""));
      html = html.Replace("{{ITEMS}}", sb.ToString());

      File.WriteAllText(outputHtmlPath, html, new UTF8Encoding(true));
      CopyAssetsIfPresent(templateDir, Path.Combine(dir, "assets"));
      return outputHtmlPath;
    }

    private static void CopyAssetsIfPresent(string templateDir, string destAssets)
    {
      var src = Path.Combine(templateDir, "assets");
      if (!Directory.Exists(src)) return;
      foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
      {
        var rel = file.Substring(src.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var target = Path.Combine(destAssets, rel);
        var td = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(td) && !Directory.Exists(td))
          Directory.CreateDirectory(td);
        File.Copy(file, target, true);
      }
    }

    private static string Escape(string s)
    {
      if (string.IsNullOrEmpty(s)) return string.Empty;
      return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }

    private static string FallbackHtml()
    {
      return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>{{TITLE}}</title></head>"
        + "<body><p>{{GENERATED}}</p><p>{{MESSAGE}}</p><ul>{{ITEMS}}</ul></body></html>";
    }
  }
}
