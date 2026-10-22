using System;

namespace PocTrace.NozzleTrace
{
  internal static class NozzleTraceUserMessages
  {
    public static string Both(string korean, string chinese)
    {
      if (string.IsNullOrEmpty(korean)) return chinese ?? string.Empty;
      if (string.IsNullOrEmpty(chinese)) return korean;
      return korean + "(" + chinese + ")";
    }

    public static string Step(int step, int total, string description)
    {
      return string.Format("[{0}/{1}] {2}", step, total, description);
    }

    public static string ForErrorCode(string code, string fallback = null)
    {
      if (string.IsNullOrWhiteSpace(code))
        return fallback ?? Both("알 수 없는 오류", "未知错误");

      switch (code.ToUpperInvariant())
      {
        case "EMPTY_KEY":
          return Both("BIM ID, Instance ID 또는 Node GUID를 입력하세요.", "请输入 BIM ID、Instance ID 或 Node GUID。");
        case "NODE_NOT_FOUND":
          return Both(
            "입력한 ID에 해당하는 노드가 캐시에 없습니다. DB·키 종류(BIM / Instance / Node GUID)를 확인하세요.",
            "缓存中没有与输入 ID 对应的节点。请确认数据库和键类型（BIM / Instance / Node GUID）。");
        case "NO_REFERENCE_NOZZLE_POC":
          return fallback ?? Both(
            "노드는 찾았으나 Connection Tag가 있는 Nozzle POC가 없습니다. (ReferenceTag = Excel Connection Tag)",
            "已找到节点，但没有带 Connection Tag 的 Nozzle POC。(ReferenceTag = Excel Connection Tag)");
        case "EXCEPTION":
          return fallback ?? Both("실행 중 예외가 발생했습니다.", "执行过程中发生异常。");
        default:
          return fallback ?? code;
      }
    }
  }
}
