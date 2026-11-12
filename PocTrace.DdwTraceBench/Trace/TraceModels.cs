using System;
using System.Collections.Generic;
using PocTrace.DdwTraceBench.Data;

namespace PocTrace.DdwTraceBench.Trace
{
    public sealed class TraceRunOptions
    {
        public string Tag = "rme010n03";
        public int MaxObjects = 3000;
        public int MaxDepth = 300;

        public bool EquipByModelType = true;
        public HashSet<long> EquipmentModelTypes = new HashSet<long> { 50594049L };
        public bool EquipByFamilyKey = true;
        public string EquipmentFamilyKey = "equipment";
        public bool EquipByBimYnZero;

        public long EndBimYn = 1;
        public HashSet<long> PassThroughModelTypes = new HashSet<long> { 51380485L };
        public bool ForceStartWhenNotEquipment;
    }

    public sealed class TagOwnerInfo
    {
        public TagOwnerRow Owner;
        public ModelRow Model;
        public TemplateRow Template;
        public bool ByModelType;
        public bool ByFamilyKey;
        public bool ByBimYnZero;
        public bool IsEquipment;
        public bool Traced;
        public string Note;
    }

    public sealed class HopRecord
    {
        public int Seq;
        public int Depth;
        public string StartEquipmentId;
        public string FromPocId;
        public string FromPocOwnerId;
        public string FromPocOwnerTable;
        public string ToPocId;
        public string ToPocTemplateId;
        public long? ToPocOwnerType;
        public string ToPocOwnerId;
        public string ToPocOwnerTable;
        public string Kind;
        public string Action;
        public string Status;
    }

    public sealed class ObjectRecord
    {
        public string StartEquipmentId;
        public int Depth;
        public string Id;
        public string Table;
        public string Role;
        public string ArrivedPocId;
        public long? ModelType;
        public string TemplateId;
        public string DisplayName;
        public string FamilyKey;
        public long? BimYn;
        public bool ByModelType;
        public bool ByFamilyKey;
        public bool ByBimYnZero;
        public bool PassThrough;
        public int PocCount;
        public string Note;
    }

    public sealed class PipeRecord
    {
        public string StartEquipmentId;
        public int Depth;
        public string ViaPocId;
        public PipeRow Pipe;
        public string SizeName;
    }

    public sealed class TraceResult
    {
        public TraceRunOptions Options;
        public DateTime Started;
        public DateTime Finished;
        public List<TagOwnerInfo> TagOwners = new List<TagOwnerInfo>();
        public List<HopRecord> Hops = new List<HopRecord>();
        public List<ObjectRecord> Objects = new List<ObjectRecord>();
        public List<PipeRecord> Pipes = new List<PipeRecord>();
        public Dictionary<string, PocRow> Pocs = new Dictionary<string, PocRow>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, PocTemplateRow> PocTemplates = new Dictionary<string, PocTemplateRow>(StringComparer.OrdinalIgnoreCase);
        public List<string> Notes = new List<string>();
        public bool LimitHit;
    }
}
