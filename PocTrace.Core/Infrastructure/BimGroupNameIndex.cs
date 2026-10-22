using System;
using System.Collections.Generic;
using System.Linq;

namespace PocTrace.Core.Infrastructure
{
  /// <summary>
  /// ConfigurationGroupLog sidecar 인덱스.
  /// EQPID 존재: OperationType &lt; 2 AND Name = EQPID (정확 일치).
  /// Path: ParentID 체인을 GroupId로 따라 올라가며 Name을 붙임 → parent\...\parent\eqpId
  /// </summary>
  public sealed class BimGroupNameIndex
  {
    private readonly IReadOnlyList<BimGroupRecord> _records;
    private readonly Dictionary<Guid, BimGroupRecord> _byId;
    private readonly Dictionary<string, BimGroupRecord> _byName;

    public BimGroupNameIndex(IEnumerable<string> names)
      : this(FromLegacyNames(names))
    {
    }

    public BimGroupNameIndex(IEnumerable<BimGroupRecord> records)
    {
      _records = (records ?? Enumerable.Empty<BimGroupRecord>())
        .Where(r => r != null && !string.IsNullOrWhiteSpace(r.Name))
        .Select(r => new BimGroupRecord
        {
          GroupId = r.GroupId,
          Name = r.Name.Trim(),
          ParentId = r.ParentId
        })
        .ToList();

      _byId = new Dictionary<Guid, BimGroupRecord>();
      _byName = new Dictionary<string, BimGroupRecord>(StringComparer.OrdinalIgnoreCase);
      foreach (var r in _records)
      {
        if (r.GroupId != Guid.Empty && !_byId.ContainsKey(r.GroupId))
          _byId[r.GroupId] = r;
        if (!_byName.ContainsKey(r.Name))
          _byName[r.Name] = r;
      }
    }

    public int NameCount => _records.Count;

    /// <summary>업로드 EQPID 중 Group에 존재하는 목록 (Path 유효).</summary>
    public List<string> MatchEqpIds(IEnumerable<string> eqpIds)
    {
      return BuildRows(eqpIds, includeUnmatchedAsX: false)
        .Select(r => r.EqpId)
        .ToList();
    }

    /// <summary>
    /// EQPID별 Path 행. Group 존재 시 path, 없으면 "x"(종합 수량 0).
    /// </summary>
    public List<BimGroupPathRow> BuildRows(IEnumerable<string> eqpIds, bool includeUnmatchedAsX = true)
    {
      var result = new List<BimGroupPathRow>();
      if (eqpIds == null) return result;

      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var raw in eqpIds)
      {
        if (string.IsNullOrWhiteSpace(raw)) continue;
        string eqp = raw.Trim();
        if (!seen.Add(eqp)) continue;

        string path;
        bool matched = TryResolvePath(eqp, out path);
        if (!matched)
        {
          if (!includeUnmatchedAsX) continue;
          path = "x";
        }

        result.Add(new BimGroupPathRow { EqpId = eqp, Path = path, Matched = matched });
      }

      return result;
    }

    public string ResolvePath(string eqp)
    {
      string path;
      return TryResolvePath(eqp, out path) ? path : null;
    }

    /// <summary>
    /// Name = eqpId 인 ConfigurationGroupLog 행을 찾고,
    /// ParentID → GroupId 로 부모 Name을 순회해 path를 만든다.
    /// </summary>
    public bool TryResolvePath(string eqp, out string path)
    {
      path = null;
      if (string.IsNullOrWhiteSpace(eqp)) return false;
      string key = eqp.Trim();

      // v1 legacy(names only): GroupId/ParentId 없음 → path 산출 불가
      BimGroupRecord hit;
      if (!_byName.TryGetValue(key, out hit) || hit == null)
        return false;
      if (hit.GroupId == Guid.Empty)
        return false;

      var ancestors = new List<string>();
      var guard = new HashSet<Guid>();
      Guid pid = hit.ParentId;
      while (pid != Guid.Empty && guard.Add(pid))
      {
        BimGroupRecord parent;
        if (!_byId.TryGetValue(pid, out parent) || parent == null)
          break;
        if (!string.IsNullOrWhiteSpace(parent.Name))
          ancestors.Add(parent.Name.Trim());
        pid = parent.ParentId;
      }

      ancestors.Reverse();
      if (ancestors.Count == 0)
        path = key;
      else
        path = string.Join("\\", ancestors) + "\\" + key;

      return true;
    }

    private static IEnumerable<BimGroupRecord> FromLegacyNames(IEnumerable<string> names)
    {
      return (names ?? Enumerable.Empty<string>())
        .Where(n => !string.IsNullOrWhiteSpace(n))
        .Select(n => new BimGroupRecord
        {
          GroupId = Guid.Empty,
          Name = n.Trim(),
          ParentId = Guid.Empty
        });
    }
  }

  public sealed class BimGroupPathRow
  {
    public string EqpId { get; set; }
    public string Path { get; set; }
    /// <summary>Group 존재 + path 산출 성공. false면 Excel Path="x", 종합 수량 0.</summary>
    public bool Matched { get; set; }
  }
}
