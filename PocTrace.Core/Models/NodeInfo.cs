using System;

using PocTrace.Core.Enums;

namespace PocTrace.Core.Models
{
    public class NodeInfo
    {
        public Guid InstanceId { get; set; }
        /// <summary>WEBDB.WEBDEV5D.GetDotNetHashCode(InstanceId). BinaryCache v6+. v5 로드 시 0.</summary>
        public long InstanceNumericId { get; set; }
        public string Tag { get; set; }
        public string Name { get; set; }
        public NodeType Type { get; set; }

        // [New] 비즈니스 로직 및 엑셀용 확장 필드
        public int Division { get; set; }      // 0: Equipment, 3: Plumbing, etc.
        public string Sub1Name { get; set; }   // S/T: Subdivision1
        public string Sub2Name { get; set; }   // S/T: Subdivision2
        public string QCode { get; set; }
        /// <summary>ObjectInstanceLog.CONSTRUCTIONCODE. BinaryCache v6+. v5 로드 시 null.</summary>
        public string ConstructionCode { get; set; }
        /// <summary>ObjectInstanceLog.BIMID. BinaryCache v7+. v6 이하 로드 시 null.</summary>
        public string BimId { get; set; }
        /// <summary>ConfigurationLog.Name (BIM Group / Revit 파일 경로명). BinaryCache v8+.</summary>
        public string BimGroupName { get; set; }
        public long PZ { get; set; } //26.05.26 높이 z값
    }
}