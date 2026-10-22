using System;
using System.Collections.Generic;
using Serilog;

namespace WinFormsOffline.Core.Jobs
{
  public sealed class SampleJobResult
  {
    public DateTime RanAt { get; set; }
    public List<string> Rows { get; set; }
    public string Message { get; set; }
  }

  /// <summary>
  /// 업무 로직은 여기에. System.Windows.Forms 참조 금지.
  /// </summary>
  public static class SampleJob
  {
    public static SampleJobResult Run(string inputHint)
    {
      Log.Information("SampleJob start. hint={Hint}", inputHint ?? "(none)");
      var result = new SampleJobResult
      {
        RanAt = DateTime.Now,
        Rows = new List<string>
        {
          "offline-kit / C:\\OfflineDev\\nuget-packages",
          "HTML assets: C:\\OfflineDev\\web-assets",
          "Core에는 WinForms를 넣지 않음"
        },
        Message = string.IsNullOrWhiteSpace(inputHint)
          ? "샘플 작업 완료"
          : "샘플 작업 완료: " + inputHint.Trim()
      };
      Log.Information("SampleJob done. rows={Count}", result.Rows.Count);
      return result;
    }
  }
}
