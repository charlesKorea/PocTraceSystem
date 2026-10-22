using System;

namespace PocTrace.Admin.Jobs
{
    /// <summary>Admin WinForm / Serilog 공용 진행 보고.</summary>
    public sealed class JobProgressReport
    {
        public string Phase { get; set; }
        public string Detail { get; set; }
        public int Percent { get; set; }
        public TimeSpan? StepElapsed { get; set; }
        public string DbName { get; set; }
        public bool IsComplete { get; set; }
        public bool IsError { get; set; }
    }

    public static class AdminJobProgress
    {
        public static event Action<JobProgressReport> Reported;

        public static void Report(string phase, int percent, string detail = null, TimeSpan? stepElapsed = null, string dbName = null)
        {
            Reported?.Invoke(new JobProgressReport
            {
                Phase = phase,
                Detail = detail,
                Percent = Math.Max(0, Math.Min(100, percent)),
                StepElapsed = stepElapsed,
                DbName = dbName
            });
        }

        public static void Complete(string message, TimeSpan totalElapsed)
        {
            Reported?.Invoke(new JobProgressReport
            {
                Phase = "완료",
                Detail = message,
                Percent = 100,
                StepElapsed = totalElapsed,
                IsComplete = true
            });
        }

        public static void Fail(string message, Exception ex = null)
        {
            Reported?.Invoke(new JobProgressReport
            {
                Phase = "오류",
                Detail = ex != null ? $"{message}: {ex.Message}" : message,
                Percent = 0,
                IsError = true
            });
        }
    }
}
