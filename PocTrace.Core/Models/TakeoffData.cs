using System;

using PocTrace.Core.Enums;

namespace PocTrace.Core.Models
{
    public class TakeoffData
    {
        // [Key] Pipe.Outlet과 매칭될 ID (TakeoffID)
        public Guid TakeoffId { get; set; }
        public string Tag { get; set; }
        public string ConnectionTag { get; set; }
        /// <summary>Takeoff POC id. tag와 connection tag의 owner.</summary>
        public Guid PocId { get; set; }
        /// <summary>하류로 이어 걷는 배관. 엑셀 pipe number의 기준이 아니다.</summary>
        public Guid PipeId { get; set; }
        /// <summary>Takeoff POC의 connected poc를 한 번만 따라간 뒤 그 owner인 duct pipe id.</summary>
        public Guid DuctPipeId { get; set; }
        //public string ConnectorTag { get; set; }

        public TakeoffData() { }
    }
}