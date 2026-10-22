using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PocTrace.Core.Models
{
    public class UTMapping
    {
        public string Project { get; set; }
        public string SourceUT { get; set; }
        public string TargetUT { get; set; }
        public string UTinLN { get; set; }
    }
}
