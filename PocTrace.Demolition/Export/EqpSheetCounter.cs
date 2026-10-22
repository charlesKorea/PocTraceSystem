using System;
using System.Collections.Generic;
using System.Linq;

namespace PocTrace.Demolition.Export
{
  /// <summary>
  /// EQPID × 7개 상세 시트 수량 집계 (종합 시트 / Preview용).
  /// </summary>
  public sealed class EqpSheetCounter
  {
    public const int SheetCount = 7;

    public static readonly string[] DetailSheetNames =
    {
      EquipmentInfoProjector.SheetName,
      StatusEmpProjector.SheetName,
      FullListNozzleProjector.SheetName,
      FullListStickProjector.SheetName,
      FullListTakeOffProjector.SheetName,
      HookupNumberProjector.SheetName,
      BimGroupEqpProjector.SheetName
    };

    private readonly List<string> _eqpOrder;
    private readonly Dictionary<string, int[]> _counts;

    public EqpSheetCounter(IEnumerable<string> uploadedEqpIds)
    {
      _eqpOrder = (uploadedEqpIds ?? Enumerable.Empty<string>())
        .Where(e => !string.IsNullOrWhiteSpace(e))
        .Select(e => e.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
        .ToList();

      _counts = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
      foreach (var eqp in _eqpOrder)
        _counts[eqp] = new int[SheetCount];
    }

    public IReadOnlyList<string> EqpIds => _eqpOrder;

    public void Add(string eqpId, int sheetIndex, int delta = 1)
    {
      if (string.IsNullOrWhiteSpace(eqpId) || sheetIndex < 0 || sheetIndex >= SheetCount)
        return;
      if (delta == 0) return;

      string key = eqpId.Trim();
      int[] arr;
      if (!_counts.TryGetValue(key, out arr))
      {
        // 업로드 목록에 없던 EQPID는 종합에 추가하지 않음
        return;
      }

      arr[sheetIndex] += delta;
    }

    /// <summary>헤더 포함 종합 시트 행. [0]=헤더, 이후 EQPID별.</summary>
    public List<object[]> ToSummaryRows()
    {
      var rows = new List<object[]>();
      var header = new object[1 + SheetCount];
      header[0] = "EQPID";
      for (int i = 0; i < SheetCount; i++)
        header[i + 1] = DetailSheetNames[i];
      rows.Add(header);

      foreach (var eqp in _eqpOrder)
      {
        var arr = _counts[eqp];
        var row = new object[1 + SheetCount];
        row[0] = eqp;
        for (int i = 0; i < SheetCount; i++)
          row[i + 1] = arr[i];
        rows.Add(row);
      }

      return rows;
    }

    public int TotalForSheet(int sheetIndex)
    {
      if (sheetIndex < 0 || sheetIndex >= SheetCount) return 0;
      int sum = 0;
      foreach (var kv in _counts)
        sum += kv.Value[sheetIndex];
      return sum;
    }
  }
}
