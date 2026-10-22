using System;
using System.Collections.Generic;
using System.Linq;
using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Infrastructure;
using PocTrace.Core.Models;
using PocTrace.Core.Utils;
using Serilog;

namespace PocTrace.Demolition.Export
{
  /// <summary>
  /// Sheet "Hook Up Number": dynprops HookupNumber에 업로드 EQPID 토큰이 있는
  /// Pipe + PlumbingComponent(피팅/클램프 등)만 속성 스캔.
  /// 연결(그래프) 추적은 하지 않음 — HU는 OWNERID 기준 속성값만 본다.
  /// </summary>
  public static class HookupNumberProjector
  {
    public const string SheetName = "Hook Up Number";

    public static readonly string[] Headers =
    {
      "Instance ID",
      "Instance Name",
      "Hookup Number",
      "GUID",
      "Division",
      "SubDivision1"
    };

    public static List<object[]> Collect(
      IGraphCache cache,
      IEnumerable<string> eqpIds,
      EqpSheetCounter counter = null)
    {
      var rows = new List<object[]>();
      if (cache == null || eqpIds == null) return rows;

      var eqpList = EqpTextMatcher.NormalizeEqps(eqpIds);
      if (eqpList.Count == 0) return rows;

      bool verbose = GlobalConfig.EnableDetailedDebug;
      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      int pipeScanned = 0, pipeMatched = 0;
      int fitScanned = 0, fitWithHu = 0, fitMatched = 0;
      int fitNoHu = 0, fitHuNoEqp = 0;

      foreach (var pipe in cache.EnumeratePipes())
      {
        if (pipe == null || pipe.PipeId == Guid.Empty) continue;
        pipeScanned++;

        string hookup = cache.GetHookupNumber(pipe.PipeId) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(hookup)) continue;

        var matchedEqps = EqpTextMatcher.FindMatches(hookup, eqpList);
        if (matchedEqps.Count == 0) continue;

        foreach (var eqp in matchedEqps)
        {
          string key = pipe.PipeId.ToString("N") + "|" + eqp;
          if (!seen.Add(key)) continue;

          if (counter != null) counter.Add(eqp, 5);
          pipeMatched++;

          long instanceId = DotNetGuidHash.FromGuid(pipe.PipeId);
          string name = string.IsNullOrWhiteSpace(pipe.UtilityName)
            ? "Pipe"
            : "Pipe(" + pipe.UtilityName + ")";

          if (verbose)
          {
            Log.Information(
              "[HU] MATCH Pipe {Label} hu=\"{Hu}\" eqp={Eqp}",
              DescribePipe(pipe),
              hookup,
              eqp);
          }

          rows.Add(new object[]
          {
            instanceId,
            name,
            hookup,
            pipe.PipeId.ToString(),
            "MEP",
            string.Empty
          });
        }
      }

      // PlumbingComponent(Div1 Nozzle계 / Div3 Fitting·Clamp 등): 자체 dynprops HU만 검사
      foreach (var owner in cache.EnumerateNodes())
      {
        if (owner == null || owner.InstanceId == Guid.Empty) continue;
        if (owner.Division != 1 && owner.Division != 3) continue;

        fitScanned++;
        bool interestingName = IsInterestingFittingName(owner.Name);

        string hookup = cache.GetHookupNumber(owner.InstanceId) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(hookup))
        {
          fitNoHu++;
          if (verbose && interestingName)
          {
            Log.Information(
              "[HU] SKIP Fitting(no own HU) {Label}",
              DescribeNode(owner));
          }
          continue;
        }

        fitWithHu++;
        var matchedEqps = EqpTextMatcher.FindMatches(hookup, eqpList);
        if (matchedEqps.Count == 0)
        {
          fitHuNoEqp++;
          if (verbose && interestingName)
          {
            Log.Information(
              "[HU] SKIP Fitting(HU no EQPID match) {Label} hu=\"{Hu}\"",
              DescribeNode(owner),
              hookup);
          }
          continue;
        }

        foreach (var eqp in matchedEqps)
        {
          string key = owner.InstanceId.ToString("N") + "|" + hookup + "|" + eqp;
          if (!seen.Add(key)) continue;

          if (counter != null) counter.Add(eqp, 5);
          fitMatched++;

          long instanceId = owner.InstanceNumericId != 0
            ? owner.InstanceNumericId
            : DotNetGuidHash.FromGuid(owner.InstanceId);

          if (verbose || interestingName)
          {
            Log.Information(
              "[HU] MATCH Fitting {Label} hu=\"{Hu}\" eqp={Eqp}",
              DescribeNode(owner),
              hookup,
              eqp);
          }

          rows.Add(new object[]
          {
            instanceId,
            owner.Name ?? "PlumbingComponent",
            hookup,
            owner.InstanceId.ToString(),
            "PlumbingComponent",
            owner.Sub1Name ?? string.Empty
          });
        }
      }

      Log.Information(
        "[HU] summary line={Line}: pipes scanned={PipeScan} matched={PipeHit}; " +
        "fittings scanned={FitScan} withHu={FitHu} matched={FitHit} noHu={NoHu} huNoEqp={NoEqp}; rows={Rows}",
        cache.DbName ?? "?",
        pipeScanned,
        pipeMatched,
        fitScanned,
        fitWithHu,
        fitMatched,
        fitNoHu,
        fitHuNoEqp,
        rows.Count);

      return rows;
    }

    /// <summary>클램프·피팅처럼 눈으로 쫓기 쉬운 이름만 상세 로그.</summary>
    private static bool IsInterestingFittingName(string name)
    {
      if (string.IsNullOrWhiteSpace(name)) return false;
      var n = name.Trim();
      return n.IndexOf("clamp", StringComparison.OrdinalIgnoreCase) >= 0
        || n.IndexOf("클램프", StringComparison.OrdinalIgnoreCase) >= 0
        || n.IndexOf("fitting", StringComparison.OrdinalIgnoreCase) >= 0
        || n.IndexOf("피팅", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string ShortId(Guid id)
    {
      if (id == Guid.Empty) return "-";
      return id.ToString("D").Substring(0, 8);
    }

    private static string DescribePipe(PipeData pipe)
    {
      if (pipe == null) return "Pipe(?)";
      string ut = string.IsNullOrWhiteSpace(pipe.UtilityName) ? "-" : pipe.UtilityName.Trim();
      return string.Format("name=Pipe({0}) guid={1}…", ut, ShortId(pipe.PipeId));
    }

    private static string DescribeNode(NodeInfo node)
    {
      if (node == null) return "Node(?)";
      string name = string.IsNullOrWhiteSpace(node.Name) ? "(no-name)" : node.Name.Trim();
      string tag = string.IsNullOrWhiteSpace(node.Tag) ? "-" : node.Tag.Trim();
      string sub1 = string.IsNullOrWhiteSpace(node.Sub1Name) ? "-" : node.Sub1Name.Trim();
      return string.Format(
        "name=\"{0}\" tag={1} div={2} sub1={3} guid={4}…",
        name,
        tag,
        node.Division,
        sub1,
        ShortId(node.InstanceId));
    }
  }
}
