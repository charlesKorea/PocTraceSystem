using System;
using System.Collections.Generic;
using System.Linq;
using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Models;
using PocTrace.Core.Utils;

namespace PocTrace.Demolition.Export
{
  /// <summary>
  /// GraphCache에서 업로드 EQPID의 설비 시작 POC/노드를 해석 (역매칭 결과의 S: 열용).
  /// </summary>
  internal static class CacheEqpStartResolver
  {
    public static bool TryResolve(IGraphCache cache, string eqpId, out NodeInfo node, out PocData startPoc)
    {
      node = null;
      startPoc = null;
      if (cache == null || string.IsNullOrWhiteSpace(eqpId)) return false;

      var starts = cache.FindStartPocs(eqpId.Trim(), includePartialMatch: false);
      if (starts == null) return false;

      foreach (var poc in starts)
      {
        if (poc == null) continue;
        NodeInfo n;
        if (!cache.TryGetNode(poc.OwnerId, out n) || n == null) continue;
        node = n;
        startPoc = poc;
        return true;
      }

      return false;
    }

    public static string FormatDivision(NodeInfo node)
    {
      if (node == null) return string.Empty;
      if (node.Division == 0) return "Equipment";
      if (node.Division == 1 || node.Division == 3) return "PlumbingComponent";
      return node.Division.ToString();
    }

    public static long InstanceId(NodeInfo node)
    {
      if (node == null) return 0;
      return node.InstanceNumericId != 0
        ? node.InstanceNumericId
        : DotNetGuidHash.FromGuid(node.InstanceId);
    }
  }
}
