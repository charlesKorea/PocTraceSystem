using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using PocTrace.Core.Infrastructure;
using PocTrace.Core.Models;

namespace PocTrace.Core.Infrastructure.Data
{
    public sealed class GraphLoadBundle
    {
        public List<NodeInfo> Nodes { get; set; }
        public List<PocData> Pocs { get; set; }
        public List<PipeData> Pipes { get; set; }
        public List<TakeoffData> Takeoffs { get; set; }
        public List<DynamicPropertiesData> HookupNumbers { get; set; }
        public DdwLoadDiagnostics Diagnostics { get; set; }
    }

    public interface IGraphQueryProvider
    {
        SchemaKind Kind { get; }
        Task<GraphLoadBundle> LoadAsync(IDbConnection connection, string dbName, IProgress<string> progress);
    }

    public static class GraphQueryProviders
    {
        public static IGraphQueryProvider Create(SchemaKind kind)
        {
            switch (kind)
            {
                case SchemaKind.S5d:
                    return new S5dGraphQueryProvider();
                case SchemaKind.TbStar:
                case SchemaKind.WebDdw:
                    return new DdwGraphQueryProvider(kind);
                default:
                    throw new InvalidOperationException("SchemaKind is not S5d, TbStar, or WebDdw.");
            }
        }

        public static IGraphQueryProvider CreateFromConfig()
        {
            return Create(GlobalConfig.ActiveSchema);
        }
    }
}
