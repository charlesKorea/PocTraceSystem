using System;
using System.Collections.Generic;

namespace PocTrace.Core.Engine.DeletedPipe
{
    /// <summary>
    /// EQPID는 Tag 정확 일치 또는 쉼표·세미콜론 토큰 하나와 일치한다. 부분 문자열은 보지 않는다.
    /// 설비/피팅 구분은 캐시용 GetSelectNodes의 Subdivision 이름과 같다.
    /// </summary>
    public static class DeletedPipeClassifier
    {
        public const int MaxHops = 2000;

        private static readonly string[] EquipmentSub1Names =
        {
            "1-MAIN TOOL",
            "2-SUB TOOL",
            "3-TEMPLATE",
            "4-PCS",
            "5-ELECTRICAL",
            "6-GCS-S.GAS",
            "6-GCS-CHEMICAL"
        };

        public static bool IsDeletedInWindow(int operationType, DateTime? opTime, DateTime cutover)
        {
            return operationType == 2 && opTime.HasValue && opTime.Value >= cutover;
        }

        public static double Millimeters(double lengthMeters)
        {
            if (lengthMeters <= 0)
                return 0;
            return Math.Round(lengthMeters * 1000d, 1, MidpointRounding.AwayFromZero);
        }

        public static bool TagMatches(string storedTag, string eqpId)
        {
            if (string.IsNullOrWhiteSpace(storedTag) || string.IsNullOrWhiteSpace(eqpId))
                return false;

            string want = eqpId.Trim();
            foreach (var token in SplitTokens(storedTag))
            {
                if (string.Equals(token, want, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static List<string> SplitTokens(string tag)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(tag))
                return list;

            var parts = tag.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string token = parts[i].Trim();
                if (token.Length > 0)
                    list.Add(token);
            }
            return list;
        }

        /// <summary>
        /// Division 0 이면서 설비 Subdivision이면 다른 설비로 보고 탐색을 멈춘다.
        /// Damper, TAKE-OFF, 그 밖 이름은 피팅·노즐로 통과한다.
        /// </summary>
        public static bool IsEquipment(DeletedNodeRow node)
        {
            if (node == null || node.Division != 0)
                return false;

            string sub1 = (node.Sub1Name ?? string.Empty).Trim();
            if (sub1.Equals("Damper", StringComparison.OrdinalIgnoreCase))
                return false;
            if (sub1.Equals("TAKE-OFF", StringComparison.OrdinalIgnoreCase))
                return false;
            if (IsEquipmentSub1(sub1))
                return true;
            if (sub1.Length == 0)
                return true;
            return false;
        }

        public static DeletedNodeRow ToNode(DeletedEquipmentRow equipment)
        {
            if (equipment == null)
                return null;
            return new DeletedNodeRow
            {
                InstanceId = equipment.InstanceId,
                Tag = equipment.Tag,
                Name = equipment.Name,
                OpTime = equipment.OpTime,
                OperationType = equipment.OperationType,
                Division = equipment.Division,
                Sub1Name = equipment.Sub1Name,
                Sub2Name = equipment.Sub2Name
            };
        }

        private static bool IsEquipmentSub1(string sub1)
        {
            for (int i = 0; i < EquipmentSub1Names.Length; i++)
            {
                if (sub1.Equals(EquipmentSub1Names[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return sub1.StartsWith("1-", StringComparison.OrdinalIgnoreCase)
                || sub1.StartsWith("2-", StringComparison.OrdinalIgnoreCase)
                || sub1.StartsWith("3-", StringComparison.OrdinalIgnoreCase)
                || sub1.StartsWith("4-", StringComparison.OrdinalIgnoreCase)
                || sub1.StartsWith("5-", StringComparison.OrdinalIgnoreCase)
                || sub1.StartsWith("6-", StringComparison.OrdinalIgnoreCase);
        }
    }
}
