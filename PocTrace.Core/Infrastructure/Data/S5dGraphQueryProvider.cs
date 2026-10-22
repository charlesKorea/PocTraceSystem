using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using PocTrace.Core.Infrastructure;
using PocTrace.Core.Models;

namespace PocTrace.Core.Infrastructure.Data
{
    /// <summary>S5D SqlQueries 텍스트를 그대로 실행한다. DDW로 재시도하지 않는다.</summary>
    public sealed class S5dGraphQueryProvider : IGraphQueryProvider
    {
        public SchemaKind Kind { get { return SchemaKind.S5d; } }

        public async Task<GraphLoadBundle> LoadAsync(IDbConnection connection, string dbName, IProgress<string> progress)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));
            if (connection.State != ConnectionState.Open)
                connection.Open();

            progress?.Report("   - Fetching Nodes...");
            var nodes = (await connection.QueryAsync<NodeInfo>(SqlQueries.GetSelectNodes(dbName)).ConfigureAwait(false)).ToList();

            progress?.Report("   - Fetching POCs...");
            var pocs = (await connection.QueryAsync<PocData>(SqlQueries.GetSelectPocs(dbName)).ConfigureAwait(false)).ToList();

            progress?.Report("   - Fetching Pipes...");
            var pipes = (await connection.QueryAsync<PipeData>(SqlQueries.GetSelectPipes(dbName)).ConfigureAwait(false)).ToList();
            for (int i = 0; i < pipes.Count; i++)
                pipes[i].EnsureThreeS5dSlots();

            progress?.Report("   - Fetching Takeoffs...");
            var takeoffs = (await connection.QueryAsync<TakeoffData>(SqlQueries.GetSelectTakeoffs(dbName)).ConfigureAwait(false)).ToList();

            progress?.Report("   - Fetching HookupNumbers...");
            var dyn = (await connection.QueryAsync<DynamicPropertiesData>(SqlQueries.GetDynamicPropertiesQuery(dbName)).ConfigureAwait(false)).ToList();

            return new GraphLoadBundle
            {
                Nodes = nodes,
                Pocs = pocs,
                Pipes = pipes,
                Takeoffs = takeoffs,
                HookupNumbers = dyn
            };
        }
    }
}
