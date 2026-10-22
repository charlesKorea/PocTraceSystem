using PocTrace.Core.Enums;
using PocTrace.Core.Models;

namespace PocTrace.Core.Engine.ReverseTrace
{
    /// <summary>
    /// 역추적 종단 판정. PO: 모든 Subdivision(1-MAIN, 2-SUB 등) 수용 — Sub1 prefix 필터 없음.
    /// </summary>
    public static class ReverseTraceTerminal
    {
        public static bool IsEquipmentTerminal(NodeInfo node, NodeType nodeType)
        {
            if (node == null)
                return false;

            if (nodeType == NodeType.Equipment)
                return true;

            // NodeType 미분류 시 Sub1Name 힌트로 Equipment 추정 (캐시 품질 대비)
            if (nodeType == NodeType.Unknown && !string.IsNullOrWhiteSpace(node.Sub1Name))
            {
                string sub1 = node.Sub1Name.Trim();
                if (sub1.StartsWith("1-", System.StringComparison.OrdinalIgnoreCase)
                    || sub1.StartsWith("2-", System.StringComparison.OrdinalIgnoreCase)
                    || sub1.StartsWith("4-", System.StringComparison.OrdinalIgnoreCase)
                    || sub1.StartsWith("6-", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
