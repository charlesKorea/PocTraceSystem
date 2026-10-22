using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PocTrace.Core.Models
{
        public class DynamicPropertiesData
    {
        // [수정] ID -> PointID로 변경 (쿼리에서 PointID로 가져옴)
        public Guid PointID { get; set; }
        public string HookupNumber { get; set; }
        public string PocNo { get; set; }
        /// <summary>WEBDB CONNECTORUSE. Hookup/PocNo는 PocID, ConnectorUse는 ConnectionPoint InstanceId로 조회.</summary>
        public string ConnectorUse { get; set; }

        public DynamicPropertiesData() { }

        /// <summary>N(대소문자 무시)만 N. Y·공백·null 및 그 외는 Y.</summary>
        public static string NormalizeConnectorUse(string raw)
        {
            if (!string.IsNullOrWhiteSpace(raw)
                && string.Equals(raw.Trim(), "N", StringComparison.OrdinalIgnoreCase))
                return "N";
            return "Y";
        }
    }
}
