using System;

using PocTrace.Core.Enums;

namespace PocTrace.Core.Models
{
    public class TakeoffData
    {
        // [Key] Pipe.Outlet과 매칭될 ID (TakeoffID)
        public Guid TakeoffId { get; set; }
        public string Tag { get; set; }
        public Guid PipeId { get; set; }
        //public string ConnectorTag { get; set; }

        public TakeoffData() { }
    }
}