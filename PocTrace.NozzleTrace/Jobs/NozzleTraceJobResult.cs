using System;
using System.Collections.Generic;
using PocTrace.Core.Models.ReverseTrace;

namespace PocTrace.NozzleTrace.Jobs
{
  public sealed class NozzleTraceJobProgress
  {
    public string Message { get; set; }
    public string DbName { get; set; }
    public int RowsDone { get; set; }
    public int RowsTotal { get; set; }
    public double Fraction { get; set; }
  }

  public sealed class NozzleTraceJobResult
  {
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }
    public string OutputExcelPath { get; set; }
    public int InputsProcessed { get; set; }
    public int TracePathCount { get; set; }
    public List<string> Warnings { get; set; } = new List<string>();
    public List<NozzleTraceRunResult> Runs { get; set; } = new List<NozzleTraceRunResult>();
  }
}
