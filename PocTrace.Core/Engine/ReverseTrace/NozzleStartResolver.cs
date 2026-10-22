using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Models;
using PocTrace.Core.Models.ReverseTrace;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PocTrace.Core.Engine.ReverseTrace
{
    public static class NozzleStartResolver
    {
        /// <summary>
        /// Excel T:/S: Connection Tag = DB ConnectionPointInstanceLog.ReferenceTag = <see cref="PocData.ReferenceTag"/>.
        /// Connector Tag는 <see cref="PocData.Tag"/> (별도 필드).
        /// </summary>
        public static string GetConnectionTag(PocData poc)
        {
            if (poc == null) return null;
            return string.IsNullOrWhiteSpace(poc.ReferenceTag) ? null : poc.ReferenceTag.Trim();
        }

        public static bool IsNozzleInformationPoc(PocData poc)
        {
            return poc != null
                   && !string.IsNullOrWhiteSpace(poc.Information)
                   && poc.Information.IndexOf("nozzle", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static NozzleStartResolution Resolve(IGraphCache cache, NozzleTraceInput input)
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            if (input == null) throw new ArgumentNullException(nameof(input));

            var result = new NozzleStartResolution();
            if (string.IsNullOrWhiteSpace(input.KeyValue))
            {
                result.ErrorCode = "EMPTY_KEY";
                result.ErrorMessage = "입력 키가 비어 있습니다.";
                return result;
            }

            NodeInfo node;
            if (!TryResolveNode(cache, input, out node))
            {
                result.ErrorCode = "NODE_NOT_FOUND";
                result.ErrorMessage = string.Format("노드를 찾을 수 없습니다. ({0}={1})",
                    input.KeyKind, input.KeyValue.Trim());
                return result;
            }

            var startPocs = SelectNozzleStartPocs(cache, node.InstanceId);
            if (startPocs.Count == 0)
            {
                result.NozzleNode = node;
                result.ErrorCode = "NO_REFERENCE_NOZZLE_POC";
                result.ErrorMessage = BuildNoStartPocMessage(cache, node);
                Log.Warning("[NozzleTrace] {Detail}", result.ErrorMessage);
                return result;
            }

            result.Success = true;
            result.NozzleNode = node;
            result.StartPocs = startPocs;
            return result;
        }

        public static bool TryResolveNode(IGraphCache cache, NozzleTraceInput input, out NodeInfo node)
        {
            node = null;
            string key = input.KeyValue?.Trim();
            if (string.IsNullOrWhiteSpace(key))
                return false;

            switch (input.KeyKind)
            {
                case NozzleLookupKeyKind.BimId:
                    return cache.TryGetNodeByBimId(key, out node);

                case NozzleLookupKeyKind.InstanceNumericId:
                    long numericId;
                    if (!long.TryParse(key, out numericId))
                        return false;
                    return cache.TryGetNodeByInstanceNumericId(numericId, out node);

                case NozzleLookupKeyKind.NodeGuid:
                    Guid nodeId;
                    if (!Guid.TryParse(key, out nodeId))
                        return false;
                    return cache.TryGetNode(nodeId, out node);

                default:
                    return false;
            }
        }

        /// <summary>
        /// 역추적 시작 POC: Connector Information ⊇ nozzle + Connection Tag(ReferenceTag) 비어 있지 않음.
        /// Demolition Rule03 / FullListNozzleProjector와 동일 기준.
        /// </summary>
        public static List<PocData> SelectNozzleStartPocs(IGraphCache cache, Guid ownerNodeId)
        {
            return SelectReferenceNozzlePocs(cache, ownerNodeId);
        }

        /// <summary>하위 호환 별칭.</summary>
        public static List<PocData> SelectReferenceNozzlePocs(IGraphCache cache, Guid ownerNodeId)
        {
            return cache.GetPocsByOwner(ownerNodeId)
                .Where(p => p != null
                            && IsNozzleInformationPoc(p)
                            && !string.IsNullOrWhiteSpace(GetConnectionTag(p)))
                .OrderBy(p => GetConnectionTag(p), StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.InstanceId)
                .ToList();
        }

        private static string BuildNoStartPocMessage(IGraphCache cache, NodeInfo node)
        {
            var pocs = cache.GetPocsByOwner(node.InstanceId).Where(p => p != null).ToList();
            int total = pocs.Count;
            int infoNozzle = pocs.Count(IsNozzleInformationPoc);
            int withConnTag = pocs.Count(p => !string.IsNullOrWhiteSpace(GetConnectionTag(p)));
            int infoAndConn = pocs.Count(p => IsNozzleInformationPoc(p) && !string.IsNullOrWhiteSpace(GetConnectionTag(p)));
            int infoOnlyConnEmpty = pocs.Count(p => IsNozzleInformationPoc(p) && string.IsNullOrWhiteSpace(GetConnectionTag(p)));
            int connOnlyNoInfo = pocs.Count(p => !IsNozzleInformationPoc(p) && !string.IsNullOrWhiteSpace(GetConnectionTag(p)));

            var sb = new StringBuilder();
            sb.AppendFormat(
                "노드는 찾았으나 역추적 시작 POC가 없습니다. (노드={0}, Tag={1})",
                node.Name ?? "-", node.Tag ?? "-");
            sb.AppendLine();
            sb.AppendFormat(
                "POC {0}개 — Information∋nozzle {1}개, Connection Tag 있음 {2}개, 둘 다 충족 {3}개.",
                total, infoNozzle, withConnTag, infoAndConn);

            if (infoOnlyConnEmpty > 0)
            {
                sb.AppendLine();
                sb.AppendFormat(
                    "※ Information은 nozzle이나 Connection Tag(ReferenceTag)가 비어 있는 POC {0}개 — DB ConnectionPointInstanceLog.ReferenceTag 확인.",
                    infoOnlyConnEmpty);
            }

            if (connOnlyNoInfo > 0)
            {
                sb.AppendLine();
                sb.AppendFormat(
                    "※ Connection Tag는 있으나 Information에 nozzle이 없는 POC {0}개 — 1차배관용 POC일 수 있어 제외됨.",
                    connOnlyNoInfo);
            }

            sb.AppendLine();
            sb.Append("조건: Information∋nozzle + Connection Tag(=ReferenceTag, Excel T:/S: Connection Tag). Connector Tag(Tag)는 사용하지 않음.");

            return sb.ToString().Trim();
        }
    }
}
