using System;
using System.Collections.Generic;

using PocTrace.Core.Models;

namespace PocTrace.Core.Infrastructure.Data
{
    // JSON 직렬화를 위한 컨테이너
    public class GraphDataDto
    {
        public DateTime SavedAt { get; set; }
        public List<NodeInfo> Nodes { get; set; }
        public List<PocData> Pocs { get; set; }
        public List<PipeData> Pipes { get; set; }
        public List<TakeoffData> Takeoffs { get; set; }
    }
}
