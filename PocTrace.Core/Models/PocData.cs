using System;

namespace PocTrace.Core.Models
{
        public class PocData
    {
        public Guid InstanceId { get; set; }
        public Guid OwnerId { get; set; }
        public Guid? Outlet { get; set; }
        public string ReferenceTag { get; set; } // T: Connection Tag

        // [신규] PointID (DynamicProperties 조회용)
        public Guid PointID { get; set; }

        // [New Fields]
        public string Tag { get; set; }          // S: Connector Tag
        public string PocName { get; set; }      // [New] S: Connector Name
        public string Information { get; set; }  // Information
        public double Size { get; set; }         // S: Size
        public string UtilityName { get; set; }  // S: Utility
        public string UtilityGroupName { get; set; } // S: Utility Group

        public string ModuleName { get; set; } //26.05.01

        /// <summary>DDW tb_pocinstances.connection_order. v10 이하 캐시에서는 null.</summary>
        public int? ConnectionOrder { get; set; }
    }
}