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
        /// <summary>
        /// 한 설비 인스턴스에서 새로 밟는 연결 ID 상한.
        /// 이미 큐에 있거나 밟은 ID는 세지 않는다.
        /// </summary>
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

        /// <summary>
        /// OperationType 1은 수정이라 5D에 남아 있다. 건설코드에 Delete가 있으면 철거 대상으로 본다.
        /// </summary>
        public static bool ConstructionCodeMarksDelete(string constructionCode)
        {
            return !string.IsNullOrEmpty(constructionCode)
                && constructionCode.IndexOf("delete", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsPendingDelete(int operationType, string constructionCode)
        {
            return operationType == 1 && ConstructionCodeMarksDelete(constructionCode);
        }

        public static bool Accepts(DeletedWalkMode mode, int operationType, DateTime? opTime, DateTime cutover)
        {
            if (mode == DeletedWalkMode.StillPresent)
                return operationType < 2;
            return IsDeletedInWindow(operationType, opTime, cutover);
        }

        /// <summary>
        /// 캐시 Fitting(Type=3)과 같다. Division 0·3이고 설비가 아니며,
        /// Damper와 노즐(TAKE-OFF + Sub2 Nozzle/ValveBox, 이름에 Nozzle)은 제외한다.
        /// </summary>
        public static bool IsFitting(DeletedNodeRow node)
        {
            if (node == null || IsEquipment(node))
                return false;
            if (node.Division != 0 && node.Division != 3)
                return false;
            if ((node.Name ?? string.Empty).IndexOf("Nozzle", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            if (FittingLabel(node) == "(이름 없음)")
                return false;

            string sub1 = (node.Sub1Name ?? string.Empty).Trim();
            if (sub1.Equals("Damper", StringComparison.OrdinalIgnoreCase))
                return false;
            if (IsNozzleTakeoff(node))
                return false;
            return true;
        }

        private static bool IsNozzleTakeoff(DeletedNodeRow node)
        {
            string sub1 = (node.Sub1Name ?? string.Empty).Trim();
            if (!sub1.Equals("TAKE-OFF", StringComparison.OrdinalIgnoreCase))
                return false;
            string sub2 = node.Sub2Name ?? string.Empty;
            return sub2.IndexOf("Nozzle", StringComparison.OrdinalIgnoreCase) >= 0
                || sub2.IndexOf("valvebox", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string FittingLabel(DeletedNodeRow node)
        {
            if (node == null)
                return string.Empty;
            if (!string.IsNullOrWhiteSpace(node.Name))
                return node.Name.Trim();
            if (!string.IsNullOrWhiteSpace(node.Sub1Name))
                return node.Sub1Name.Trim();
            return "(이름 없음)";
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
        /// Damper와 일반 피팅은 통과한다. 노즐과 캡은 Trace와 같이 끝이다.
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
                Sub2Name = equipment.Sub2Name,
                ConstructionCode = equipment.ConstructionCode,
                QCode = equipment.QCode
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

        /// <summary>
        /// TraceAlgorithm이 노즐로 끝내고 통과하지 않는 지점.
        /// 여기 도착한 2차 훅업 배관은 이미 기록돼 있고, 노즐 반대쪽 1차 훅업 배관은 따라가지 않는다.
        /// </summary>
        public static bool IsNozzleEnd(DeletedNodeRow node, DeletedPocRow arrivedPoc)
        {
            if (arrivedPoc != null && Contains(arrivedPoc.Information, "Nozzle"))
                return true;
            if (node == null)
                return false;
            if (IsNozzleTakeoff(node))
                return true;
            if (IsChemicalTakeoff(node))
                return true;
            return Contains(node.Name, "Nozzle");
        }

        /// <summary>
        /// TraceAlgorithm의 캡 마감. 캡 너머로는 가지 않는다.
        /// </summary>
        public static bool IsCapEnd(DeletedNodeRow node)
        {
            if (node == null || node.Division == 0)
                return false;

            string name = node.Name ?? string.Empty;
            string sub1 = (node.Sub1Name ?? string.Empty).Trim();
            string sub2 = (node.Sub2Name ?? string.Empty).Trim();

            if (sub1.Equals("FLANGE", StringComparison.OrdinalIgnoreCase)
                && (sub2.Equals("MF FLANGE", StringComparison.OrdinalIgnoreCase)
                    || sub2.Equals("KF FLANGE", StringComparison.OrdinalIgnoreCase))
                && name.IndexOf("END", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (name.IndexOf("미사용", StringComparison.OrdinalIgnoreCase) >= 0
                && sub2.Equals("기타", StringComparison.OrdinalIgnoreCase))
                return true;

            return IsCapName(name, sub1, sub2);
        }

        /// <summary>
        /// 일반 테이크오프는 Trace에서 끝이다. Water 그룹과 vent/vac/foreline만 하류를 계속 본다.
        /// </summary>
        public static bool ContinuesPastTakeoff(DeletedPocRow startPoc)
        {
            if (startPoc == null)
                return false;
            string group = startPoc.UtilityGroupName ?? string.Empty;
            if (group.IndexOf("water", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            string utility = startPoc.UtilityName ?? string.Empty;
            return utility.IndexOf("vent", StringComparison.OrdinalIgnoreCase) >= 0
                || utility.IndexOf("vac", StringComparison.OrdinalIgnoreCase) >= 0
                || utility.IndexOf("fore line", StringComparison.OrdinalIgnoreCase) >= 0
                || utility.IndexOf("fore-line", StringComparison.OrdinalIgnoreCase) >= 0
                || utility.IndexOf("foreline", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsChemicalTakeoff(DeletedNodeRow node)
        {
            string sub1 = (node.Sub1Name ?? string.Empty).Replace(" ", string.Empty);
            if (!sub1.Equals("TAKE-OFF", StringComparison.OrdinalIgnoreCase))
                return false;
            return Contains(node.Sub2Name, "Chemical");
        }

        private static bool IsCapName(string name, string sub1, string sub2)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            if (name.IndexOf("TEE", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            if (Contains(name, "Nozzle") && Contains(sub2, "Nozzle"))
                return false;

            return name.Equals("기타", StringComparison.OrdinalIgnoreCase)
                || Contains(name, "Valve Box 80A_B_Valve Box")
                || Contains(name, "cap")
                || Contains(sub1, "cap")
                || Contains(name, "vac-030")
                || Contains(name, "vac-012")
                || Contains(name, "vac-013")
                || Contains(name, "bulk-057")
                || (Contains(name, "nw25") && Contains(name, "pot"));
        }

        private static bool Contains(string value, string part)
        {
            return !string.IsNullOrEmpty(value)
                && value.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
