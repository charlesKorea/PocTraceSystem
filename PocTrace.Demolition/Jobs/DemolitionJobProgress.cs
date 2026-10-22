namespace PocTrace.Demolition.Jobs
{
  /// <summary>Demolition Job 진행 상태 (ProgressBar / % / ETA / Grid TraceStatus).</summary>
  public sealed class DemolitionJobProgress
  {
    public string Message { get; set; }
    public string CurrentLine { get; set; }
    public int LinesDone { get; set; }
    public int LinesTotal { get; set; }
    public int EqpDone { get; set; }
    public int EqpTotal { get; set; }
    /// <summary>전체 작업 0~100.</summary>
    public int Percent { get; set; }
    /// <summary>예상 남은 시간(초). null이면 아직 추정 불가.</summary>
    public int? EtaSeconds { get; set; }
  }
}
