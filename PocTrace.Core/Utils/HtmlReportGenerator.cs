using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using PocTrace.Core.Infrastructure;
using Serilog;

namespace PocTrace.Core.Utils
{
  /// <summary>
  /// DailyReport HTML/JS 생성.
  /// - assets·layout: Templates/ 로컬 번들만 사용 (CDN 없음, echarts 로컬)
  /// - data_*.js / History JSON: DailyReport/data/ 에 적재, HTML은 ./data/... 참조
  /// </summary>
  public class HtmlReportGenerator
  {
    public const string DataFolderName = "data";

    private readonly string _sourceTemplatePath;
    private readonly string _destinationPath;

    /// <summary>배포 시마다 덮어쓸 휘발성 파일 (UI 수정 반영).</summary>
    private static readonly string[] VolatileCss =
    {
      "style.css",
      "bootstrap.min.css",
      "dataTables.bootstrap5.min.css",
      "buttons.dataTables.min.css",
      "bootstrap-icons.min.css",
      "bootstrap-icons.css"
    };

    private static readonly string[] VolatileJs =
    {
      "poc-core.js",
      "poc-charts.js",
      "poc-grid.js",
      "poc-similar.js",
      "poc-action.js",
      "poc-rules.js",
      "poc-progress.js",
      "app.js",
      "lang.js",
      "echarts.min.js",
      "jquery.min.js",
      "bootstrap.bundle.min.js",
      "jszip.min.js",
      "lodash.min.js",
      "dayjs.min.js",
      "jquery.dataTables.min.js",
      "dataTables.min.js",
      "dataTables.bootstrap5.min.js",
      "dataTables.buttons.min.js",
      "buttons.html5.min.js"
    };

    public HtmlReportGenerator(string destinationPath)
    {
      _sourceTemplatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
      _destinationPath = destinationPath;
    }

    /// <summary>DailyReport/data — data_*.js 및 History JSON.</summary>
    private string EnsureDataDir()
    {
      string dir = Path.Combine(_destinationPath, DataFolderName);
      Directory.CreateDirectory(dir);
      return dir;
    }

    /// <summary>historyFolder가 data면 그대로, 아니면 data 하위로. 레거시 루트 파일은 1회 이전.</summary>
    private string ResolveHistoryPath(string historyFolder, string fileName)
    {
      string root = historyFolder ?? _destinationPath;
      string dataDir;
      string leaf = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
      if (string.Equals(leaf, DataFolderName, StringComparison.OrdinalIgnoreCase))
        dataDir = root;
      else
        dataDir = Path.Combine(root, DataFolderName);

      Directory.CreateDirectory(dataDir);
      string path = Path.Combine(dataDir, fileName);
      string legacy = Path.Combine(
        string.Equals(leaf, DataFolderName, StringComparison.OrdinalIgnoreCase)
          ? Path.GetDirectoryName(root) ?? _destinationPath
          : root,
        fileName);
      if (!File.Exists(path) && File.Exists(legacy) &&
          !string.Equals(path, legacy, StringComparison.OrdinalIgnoreCase))
      {
        try { File.Copy(legacy, path, overwrite: false); }
        catch (Exception ex)
        {
          Log.Warning(ex, "[HtmlReport] Legacy history migrate failed: {File}", fileName);
        }
      }
      return path;
    }

    private static string DataScriptTag(string fileName)
    {
      return $"<script src=\"./data/{fileName}\"></script>";
    }

    /// <summary>
    /// Templates/assets → DailyReport/assets.
    /// 최초: 전체 복사. 이후: 휘발성 css/js만 갱신(운영 커스텀 유지 + UI 핫픽스 반영).
    /// </summary>
    public void DeployAssets()
    {
      string sourceAssets = Path.Combine(_sourceTemplatePath, "assets");
      string destAssets = Path.Combine(_destinationPath, "assets");

      if (!Directory.Exists(sourceAssets))
      {
        Log.Warning("[HtmlReport] Templates/assets missing: {Path}", sourceAssets);
        return;
      }

      if (!Directory.Exists(destAssets))
      {
        CopyDirectory(sourceAssets, destAssets, true);
        Log.Information("[HtmlReport] Assets initial deploy -> {Dest}", destAssets);
      }
      else
      {
        SyncVolatileFiles(Path.Combine(sourceAssets, "css"), Path.Combine(destAssets, "css"), VolatileCss);
        SyncVolatileFiles(Path.Combine(sourceAssets, "js"), Path.Combine(destAssets, "js"), VolatileJs);
        // fonts (bootstrap-icons)
        string srcFonts = Path.Combine(sourceAssets, "fonts");
        string dstFonts = Path.Combine(destAssets, "fonts");
        if (Directory.Exists(srcFonts))
        {
          Directory.CreateDirectory(dstFonts);
          foreach (var f in Directory.GetFiles(srcFonts))
            File.Copy(f, Path.Combine(dstFonts, Path.GetFileName(f)), true);
        }
        Log.Information("[HtmlReport] Volatile assets synced -> {Dest}", destAssets);
      }

      EnsureAssetAliases(destAssets);
      NormalizeBootstrapIconFontPaths(destAssets);

      // layout.html: Templates가 SoT — 배포 시마다 동기화
      string destLayout = Path.Combine(_destinationPath, "layout.html");
      string sourceLayout = Path.Combine(_sourceTemplatePath, "layout.html");
      if (File.Exists(sourceLayout))
        File.Copy(sourceLayout, destLayout, true);
    }

    /// <summary>
    /// bootstrap-icons.css 폰트 url을 ../fonts/ 로 강제 (css/fonts 잘못된 상대경로 방지).
    /// </summary>
    private static void NormalizeBootstrapIconFontPaths(string destAssets)
    {
      string cssDir = Path.Combine(destAssets, "css");
      if (!Directory.Exists(cssDir)) return;

      foreach (var name in new[] { "bootstrap-icons.css", "bootstrap-icons.min.css" })
      {
        string path = Path.Combine(cssDir, name);
        if (!File.Exists(path)) continue;

        string text = File.ReadAllText(path, Encoding.UTF8);
        string fixedText = text
          .Replace("url(\"fonts/", "url(\"../fonts/")
          .Replace("url('fonts/", "url('../fonts/")
          .Replace("url(fonts/", "url(../fonts/")
          .Replace("url(\"./fonts/", "url(\"../fonts/")
          .Replace("url('./fonts/", "url('../fonts/");

        if (!string.Equals(text, fixedText, StringComparison.Ordinal))
        {
          File.WriteAllText(path, fixedText, Encoding.UTF8);
          Log.Information("[HtmlReport] Normalized icon font paths in {File}", name);
        }
      }
    }

    /// <summary>jquery.dataTables / bootstrap-icons.css 별칭 보장.</summary>
    private static void EnsureAssetAliases(string destAssets)
    {
      string js = Path.Combine(destAssets, "js");
      string css = Path.Combine(destAssets, "css");
      Directory.CreateDirectory(js);
      Directory.CreateDirectory(css);

      string dt = Path.Combine(js, "dataTables.min.js");
      string jqDt = Path.Combine(js, "jquery.dataTables.min.js");
      if (File.Exists(dt) && !File.Exists(jqDt))
        File.Copy(dt, jqDt, true);
      if (File.Exists(jqDt) && !File.Exists(dt))
        File.Copy(jqDt, dt, true);

      string iconsMin = Path.Combine(css, "bootstrap-icons.min.css");
      string icons = Path.Combine(css, "bootstrap-icons.css");
      if (File.Exists(iconsMin) && !File.Exists(icons))
        File.Copy(iconsMin, icons, true);
      if (File.Exists(icons) && !File.Exists(iconsMin))
        File.Copy(icons, iconsMin, true);
    }

    private static void SyncVolatileFiles(string srcDir, string dstDir, string[] names)
    {
      if (!Directory.Exists(srcDir)) return;
      Directory.CreateDirectory(dstDir);
      foreach (var name in names)
      {
        string src = Path.Combine(srcDir, name);
        if (!File.Exists(src)) continue;
        File.Copy(src, Path.Combine(dstDir, name), true);
      }
    }

    public void GenerateReport(string dbName, object results, object historyData, int checkRulesCount = 0)
    {
      if (checkRulesCount <= 0)
        checkRulesCount = PocTrace.Core.Engine.Validation.Rules.ValidationRuleRegistry.RegisteredCount;

      string jsFilePath = Path.Combine(EnsureDataDir(), $"data_{dbName}.js");

      using (var fs = new FileStream(jsFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
      using (var sw = new StreamWriter(fs, Encoding.UTF8, bufferSize: 65536))
      {
        var meta = new
        {
          dbName = dbName,
          genDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
          checkRulesCount = checkRulesCount
        };
        sw.Write("const REPORT_META = ");
        sw.Write(JsonConvert.SerializeObject(meta));
        sw.WriteLine(";");

        sw.Write("const AVAILABLE_LINES = ");
        sw.Write(JsonConvert.SerializeObject(
          PocTrace.Core.Infrastructure.GlobalConfig.TargetDatabases ?? new string[0]));
        sw.WriteLine(";");

        sw.Write("const REPORT_DATA = ");
        sw.Flush();
        using (var jw = new JsonTextWriter(sw))
        {
          jw.CloseOutput = false;
          jw.Formatting = Formatting.None;
          new JsonSerializer().Serialize(jw, results);
        }
        sw.WriteLine(";");

        sw.Write("const HISTORY_DATA = ");
        sw.Write(JsonConvert.SerializeObject(historyData));
        sw.WriteLine(";");
      }

      GenerateHtmlFile(dbName);
    }

    private void GenerateHtmlFile(string dbName)
    {
      string layoutContent = GetLayoutContent();
      string activeKey = dbName.ToUpperInvariant().Replace('-', '_');

      string html = layoutContent.Replace("{{DATA_SCRIPT}}", DataScriptTag($"data_{dbName}.js"));
      html = html.Replace("{{TITLE}}", $"M-Link : Report [{dbName}]");
      html = html.Replace($"{{{{ACTIVE_{activeKey}}}}}", "active");
      html = CleanTokens(html);

      File.WriteAllText(Path.Combine(_destinationPath, $"report_{dbName}.html"), html, Encoding.UTF8);
    }

    public void GenerateSummary(List<dynamic> summaryStats, object allHistories)
    {
      string jsFilePath = Path.Combine(EnsureDataDir(), "data_summary.js");

      using (var fs = new FileStream(jsFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
      using (var sw = new StreamWriter(fs, Encoding.UTF8, bufferSize: 65536))
      {
        sw.Write("const SUMMARY_DATA = ");
        using (var jw = new JsonTextWriter(sw))
        {
          jw.CloseOutput = false;
          jw.Formatting = Formatting.None;
          new JsonSerializer().Serialize(jw, summaryStats);
        }
        sw.WriteLine(";");

        sw.Write("const HISTORY_DATA_MAP = ");
        using (var jw = new JsonTextWriter(sw))
        {
          jw.CloseOutput = false;
          jw.Formatting = Formatting.None;
          new JsonSerializer().Serialize(jw, allHistories);
        }
        sw.WriteLine(";");

        sw.Write("const AVAILABLE_LINES = ");
        sw.Write(JsonConvert.SerializeObject(
          PocTrace.Core.Infrastructure.GlobalConfig.TargetDatabases ?? new string[0]));
        sw.WriteLine(";");

        sw.Write("const CHECK_RULES_COUNT = ");
        sw.Write(PocTrace.Core.Engine.Validation.Rules.ValidationRuleRegistry.RegisteredCount);
        sw.WriteLine(";");

        sw.WriteLine("const IS_SUMMARY = true;");
      }

      string layoutContent = GetLayoutContent();
      string html = layoutContent.Replace("{{DATA_SCRIPT}}", DataScriptTag("data_summary.js"));
      html = html.Replace("{{TITLE}}", "M-Link : Integrated Summary");
      html = html.Replace("{{ACTIVE_SUMMARY}}", "active");
      html = CleanTokens(html);

      File.WriteAllText(Path.Combine(_destinationPath, "index.html"), html, Encoding.UTF8);
    }

    /// <summary>
    /// Check Rules 카탈로그 + 일별 Rule 수 히스토리 → rules.html / data/data_rules.js
    /// </summary>
    public void GenerateRulesPage(
      IList<PocTrace.Core.Engine.Validation.Rules.ValidationRuleInfo> catalog,
      string historyFolder = null)
    {
      string historyFile = ResolveHistoryPath(historyFolder, "Rules_History.json");

      var today = DateTime.Now.ToString("yyyy-MM-dd");
      int ruleCount = catalog?.Count ?? PocTrace.Core.Engine.Validation.Rules.ValidationRuleRegistry.RegisteredCount;
      long totalHits = catalog?.Sum(r => r.HitCount) ?? 0;
      var hitsMap = (catalog ?? new List<PocTrace.Core.Engine.Validation.Rules.ValidationRuleInfo>())
        .ToDictionary(r => r.RuleId, r => r.HitCount);

      var todayEntry = new
      {
        Date = today,
        RuleCount = ruleCount,
        TotalHits = totalHits,
        Hits = hitsMap
      };

      var history = new List<object>();
      if (File.Exists(historyFile))
      {
        try
        {
          var loaded = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(TextFileEncoding.ReadAllText(historyFile));
          if (loaded != null)
          {
            foreach (var row in loaded)
            {
              if (row.ContainsKey("Date") && string.Equals(Convert.ToString(row["Date"]), today, StringComparison.Ordinal))
                continue;
              history.Add(row);
            }
          }
        }
        catch (Exception ex)
        {
          Log.Warning(ex, "[HtmlReport] Rules_History.json read failed");
        }
      }
      history.Add(todayEntry);
      File.WriteAllText(historyFile, JsonConvert.SerializeObject(history, Formatting.Indented), Encoding.UTF8);

      string jsPath = Path.Combine(EnsureDataDir(), "data_rules.js");
      using (var fs = new FileStream(jsPath, FileMode.Create, FileAccess.Write, FileShare.Read))
      using (var sw = new StreamWriter(fs, Encoding.UTF8, bufferSize: 65536))
      {
        sw.WriteLine("const IS_RULES_PAGE = true;");
        sw.Write("const RULES_CATALOG = ");
        sw.Write(JsonConvert.SerializeObject(catalog ?? new List<PocTrace.Core.Engine.Validation.Rules.ValidationRuleInfo>()));
        sw.WriteLine(";");
        sw.Write("const RULES_HISTORY = ");
        sw.Write(JsonConvert.SerializeObject(history));
        sw.WriteLine(";");
        sw.Write("const AVAILABLE_LINES = ");
        sw.Write(JsonConvert.SerializeObject(
          PocTrace.Core.Infrastructure.GlobalConfig.TargetDatabases ?? new string[0]));
        sw.WriteLine(";");
        sw.Write("const RULES_META = ");
        sw.Write(JsonConvert.SerializeObject(new
        {
          genDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
          ruleCount = ruleCount,
          totalHits = totalHits
        }));
        sw.WriteLine(";");
      }

      string layoutContent = GetLayoutContent();
      string html = layoutContent.Replace("{{DATA_SCRIPT}}", DataScriptTag("data_rules.js"));
      html = html.Replace("{{TITLE}}", "M-Link : Check Rules");
      html = html.Replace("{{ACTIVE_RULES}}", "active");
      html = CleanTokens(html);
      File.WriteAllText(Path.Combine(_destinationPath, "rules.html"), html, Encoding.UTF8);
      Log.Information("[HtmlReport] rules.html generated. Rules={Count}, Hits={Hits}", ruleCount, totalHits);
    }

    /// <summary>
    /// Design Progress 리포트: data/data_progress_{db}.js + report_progress_{db}.html + data/Progress_History_{db}.json
    /// </summary>
    public void GenerateProgressReport(PocTrace.Core.Engine.Progress.ProgressReportDto report, string historyFolder = null)
    {
      if (report == null) return;
      string dbName = report.DbName ?? "unknown";
      string historyFile = ResolveHistoryPath(historyFolder, $"Progress_History_{dbName}.json");

      var today = DateTime.Now.ToString("yyyy-MM-dd");
      var todayEntry = new
      {
        Date = today,
        InProgressCount = report.InProgressCount,
        TotalEquip = report.TotalEquip,
        AvgProgressPct = report.AvgProgressPct,
        LowConfidenceCount = report.LowConfidenceCount
      };

      var history = new List<object>();
      if (File.Exists(historyFile))
      {
        try
        {
          var loaded = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(TextFileEncoding.ReadAllText(historyFile));
          if (loaded != null)
          {
            foreach (var row in loaded)
            {
              if (row.ContainsKey("Date") && string.Equals(Convert.ToString(row["Date"]), today, StringComparison.Ordinal))
                continue;
              history.Add(row);
            }
          }
        }
        catch (Exception ex)
        {
          Log.Warning(ex, "[HtmlReport] Progress_History read failed for {Db}", dbName);
        }
      }
      history.Add(todayEntry);
      File.WriteAllText(historyFile, JsonConvert.SerializeObject(history, Formatting.Indented), Encoding.UTF8);

      string jsPath = Path.Combine(EnsureDataDir(), $"data_progress_{dbName}.js");
      using (var fs = new FileStream(jsPath, FileMode.Create, FileAccess.Write, FileShare.Read))
      using (var sw = new StreamWriter(fs, Encoding.UTF8, bufferSize: 65536))
      {
        sw.WriteLine("const IS_PROGRESS_PAGE = true;");
        sw.Write("const PROGRESS_META = ");
        sw.Write(JsonConvert.SerializeObject(new
        {
          dbName = report.DbName,
          genDate = report.GenDate,
          totalEquip = report.TotalEquip,
          inProgressCount = report.InProgressCount,
          lowConfidenceCount = report.LowConfidenceCount,
          avgProgressPct = report.AvgProgressPct
        }));
        sw.WriteLine(";");
        sw.Write("const PROGRESS_DATA = ");
        sw.Flush();
        using (var jw = new JsonTextWriter(sw))
        {
          jw.CloseOutput = false;
          jw.Formatting = Formatting.None;
          new JsonSerializer().Serialize(jw, report.Equipments ?? new List<PocTrace.Core.Engine.Progress.EquipmentProgressDto>());
        }
        sw.WriteLine(";");
        sw.Write("const PROGRESS_HISTORY = ");
        sw.Write(JsonConvert.SerializeObject(history));
        sw.WriteLine(";");
        sw.Write("const AVAILABLE_LINES = ");
        sw.Write(JsonConvert.SerializeObject(
          PocTrace.Core.Infrastructure.GlobalConfig.TargetDatabases ?? new string[0]));
        sw.WriteLine(";");
      }

      string layoutContent = GetLayoutContent();
      string activeKey = dbName.ToUpperInvariant().Replace('-', '_');
      string html = layoutContent.Replace("{{DATA_SCRIPT}}", DataScriptTag($"data_progress_{dbName}.js"));
      html = html.Replace("{{TITLE}}", $"M-Link : Design Progress [{dbName}]");
      html = html.Replace("{{ACTIVE_PROGRESS}}", "active");
      html = html.Replace($"{{{{ACTIVE_{activeKey}}}}}", "");
      html = CleanTokens(html);
      File.WriteAllText(Path.Combine(_destinationPath, $"report_progress_{dbName}.html"), html, Encoding.UTF8);
      Log.Information("[HtmlReport] report_progress_{Db}.html generated. Equip={Eq}", dbName, report.TotalEquip);
    }

    /// <summary>라인 통합 Design Progress: progress.html + data/data_progress.js</summary>
    public void GenerateProgressSummary(IList<PocTrace.Core.Engine.Progress.ProgressReportDto> reports)
    {
      reports = reports ?? new List<PocTrace.Core.Engine.Progress.ProgressReportDto>();
      var allEquip = reports.SelectMany(r =>
      {
        var list = r.Equipments ?? new List<PocTrace.Core.Engine.Progress.EquipmentProgressDto>();
        foreach (var e in list)
        {
          // stamp line for summary table
        }
        return list.Select(e => new
        {
          e.Tag,
          e.Guid,
          e.MainName,
          e.Process,
          e.FirstDesignAt,
          e.LastDesignAt,
          e.DesignDays,
          e.IsInProgress,
          e.LowConfidence,
          e.Overrun,
          e.CurrentLength,
          e.CurrentBomCount,
          e.BaselineLengthMedian,
          e.BaselineBomMedian,
          e.CohortN,
          e.LengthPct,
          e.BomPct,
          e.ProgressPct,
          e.FrmStatus,
          e.Utilities,
          DbName = r.DbName
        });
      }).ToList();

      int totalEq = allEquip.Count;
      int inProg = allEquip.Count(e => e.IsInProgress);
      int low = allEquip.Count(e => e.LowConfidence);
      double avg = totalEq > 0 ? Math.Round(allEquip.Average(e => e.ProgressPct), 1) : 0;

      string jsPath = Path.Combine(EnsureDataDir(), "data_progress.js");
      using (var fs = new FileStream(jsPath, FileMode.Create, FileAccess.Write, FileShare.Read))
      using (var sw = new StreamWriter(fs, Encoding.UTF8, bufferSize: 65536))
      {
        sw.WriteLine("const IS_PROGRESS_PAGE = true;");
        sw.Write("const PROGRESS_META = ");
        sw.Write(JsonConvert.SerializeObject(new
        {
          dbName = "ALL",
          genDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
          totalEquip = totalEq,
          inProgressCount = inProg,
          lowConfidenceCount = low,
          avgProgressPct = avg
        }));
        sw.WriteLine(";");
        sw.Write("const PROGRESS_DATA = ");
        sw.Write(JsonConvert.SerializeObject(allEquip));
        sw.WriteLine(";");
        sw.Write("const PROGRESS_HISTORY = ");
        sw.Write(JsonConvert.SerializeObject(reports.Select(r => new
        {
          Date = DateTime.Now.ToString("yyyy-MM-dd"),
          DbName = r.DbName,
          InProgressCount = r.InProgressCount,
          TotalEquip = r.TotalEquip,
          AvgProgressPct = r.AvgProgressPct
        })));
        sw.WriteLine(";");
        sw.Write("const AVAILABLE_LINES = ");
        sw.Write(JsonConvert.SerializeObject(
          PocTrace.Core.Infrastructure.GlobalConfig.TargetDatabases ?? new string[0]));
        sw.WriteLine(";");
      }

      string layoutContent = GetLayoutContent();
      string html = layoutContent.Replace("{{DATA_SCRIPT}}", DataScriptTag("data_progress.js"));
      html = html.Replace("{{TITLE}}", "M-Link : Design Progress");
      html = html.Replace("{{ACTIVE_PROGRESS}}", "active");
      html = CleanTokens(html);
      File.WriteAllText(Path.Combine(_destinationPath, "progress.html"), html, Encoding.UTF8);
      Log.Information("[HtmlReport] progress.html generated. Equip={Eq}", totalEq);
    }

    private string GetLayoutContent()
    {
      // Templates = SoT
      string sourceLayout = Path.Combine(_sourceTemplatePath, "layout.html");
      if (File.Exists(sourceLayout))
      {
        string destLayout = Path.Combine(_destinationPath, "layout.html");
        Directory.CreateDirectory(_destinationPath);
        File.Copy(sourceLayout, destLayout, true);
        return File.ReadAllText(sourceLayout, Encoding.UTF8);
      }

      string destOnly = Path.Combine(_destinationPath, "layout.html");
      if (File.Exists(destOnly))
        return File.ReadAllText(destOnly, Encoding.UTF8);

      throw new FileNotFoundException("layout.html not found in Templates or Destination.");
    }

    private static string CleanTokens(string html)
    {
      return System.Text.RegularExpressions.Regex.Replace(html, @"\{\{ACTIVE_.*?\}\}", "");
    }

    private static void CopyDirectory(string sourceDir, string destDir, bool recursive)
    {
      var dir = new DirectoryInfo(sourceDir);
      if (!dir.Exists) return;
      Directory.CreateDirectory(destDir);
      foreach (FileInfo file in dir.GetFiles())
        file.CopyTo(Path.Combine(destDir, file.Name), true);
      if (!recursive) return;
      foreach (DirectoryInfo subDir in dir.GetDirectories())
        CopyDirectory(subDir.FullName, Path.Combine(destDir, subDir.Name), true);
    }
  }
}
