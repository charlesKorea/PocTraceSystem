using System;
using PocTrace.Core.Models.ReverseTrace;

namespace PocTrace.NozzleTrace.Input
{
  public sealed class NozzleTraceInputRow
  {
    public int RowNumber { get; set; }
    public string DbName { get; set; }
    public NozzleLookupKeyKind KeyKind { get; set; }
    public string KeyValue { get; set; }
    public string LoadStatus { get; set; } = "Pending";
    public string TraceStatus { get; set; } = "Pending";

    public bool IsValid =>
      !string.IsNullOrWhiteSpace(DbName)
      && !string.IsNullOrWhiteSpace(KeyValue);

    public NozzleTraceInput ToInput()
    {
      return new NozzleTraceInput
      {
        DbName = DbName?.Trim(),
        KeyKind = KeyKind,
        KeyValue = KeyValue?.Trim()
      };
    }
  }
}
