using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using PocTrace.Core.Infrastructure;
using Serilog;

namespace PocTrace.Core.Infrastructure.Data
{
    /// <summary>
    /// TbStar 또는 WebDdw 한 프로파일만 실행한다. 컬럼이 없으면 다른 프로파일로 넘어가지 않는다.
    /// Hookup은 D-03 전까지 빈 목록이다.
    /// </summary>
    public sealed class DdwGraphQueryProvider : IGraphQueryProvider
    {
        private readonly SchemaKind _kind;

        public DdwGraphQueryProvider(SchemaKind kind)
        {
            if (kind != SchemaKind.TbStar && kind != SchemaKind.WebDdw)
                throw new ArgumentOutOfRangeException(nameof(kind), "DDW provider requires TbStar or WebDdw.");
            _kind = kind;
        }

        public SchemaKind Kind { get { return _kind; } }

        public async Task<GraphLoadBundle> LoadAsync(IDbConnection connection, string dbName, IProgress<string> progress)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));
            if (connection.State != ConnectionState.Open)
                connection.Open();

            string quoted = SqlIdentifier.QuoteDatabase(dbName);
            if (_kind == SchemaKind.WebDdw)
            {
                progress?.Report("   - WebDdw 컬럼 확인...");
                await RequireWebColumns(connection, quoted).ConfigureAwait(false);
            }

            progress?.Report("   - Fetching DDW POCs...");
            var modelPocs = (await Query<DdwPocRow>(connection, DdwSql.SelectModelPocs(quoted, _kind)).ConfigureAwait(false)).ToList();
            var pipePocs = (await Query<DdwPocRow>(connection, DdwSql.SelectPipePocs(quoted)).ConfigureAwait(false)).ToList();

            progress?.Report("   - Fetching DDW Pipes...");
            var pipeRows = (await Query<DdwPipeRow>(connection, DdwSql.SelectPipes(quoted)).ConfigureAwait(false)).ToList();

            progress?.Report("   - Fetching DDW Nodes...");
            var models = (await Query<DdwModelRow>(connection, DdwSql.SelectModels(quoted, _kind)).ConfigureAwait(false)).ToList();

            progress?.Report("   - Fetching DDW attributes...");
            var attributes = (await Query<DdwAttributeRow>(connection, DdwSql.SelectAttributes(quoted)).ConfigureAwait(false)).ToList();

            var bundle = DdwGraphNormalizer.Build(models, modelPocs, pipePocs, pipeRows, attributes);
            var diag = bundle.Diagnostics;
            Log.Information(
                "[DDW] {Profile} nodes={Nodes} pocs={Pocs} pipes={Pipes} takeoffs={Takeoffs} modelPocs={ModelPocs} pipePocs={PipePocs} connected={Connected} unconnected={Unconnected} dangling={Dangling} pipes4plus={Pipes4} maxEnds={MaxEnds} orderDup={OrderDup} unknown={Unknown} unknownEmitted={UnknownEmitted} moduleChildren={ModuleChildren} deletedSkipped={Deleted} attrDup={AttrDup}",
                _kind,
                bundle.Nodes.Count,
                bundle.Pocs.Count,
                bundle.Pipes.Count,
                bundle.Takeoffs.Count,
                diag.ModelPocCount,
                diag.PipeOwnedPocCount,
                diag.ConnectedCount,
                diag.UnconnectedCount,
                diag.DanglingCount,
                diag.PipesWithFourOrMore,
                diag.MaxEndpoints,
                diag.DuplicateConnectionOrders,
                diag.UnknownVocabulary,
                diag.UnknownNodesEmitted,
                diag.ModuleChildrenSkipped,
                diag.DeletedRowsSkipped,
                diag.AttributeDuplicates);
            if (diag.UnknownSamples.Count > 0)
            {
                Log.Information("[DDW] unresolved vocabulary samples={Count} first={Sample}",
                    diag.UnknownVocabulary, diag.UnknownSamples[0]);
            }
            Log.Information("[DDW] Hookup/ConnectorUse is empty until owner-domain D-03. Lookup stays Y when ConnectorUse is blank.");
            progress?.Report("   - DDW graph mapped (hookup deferred)");
            return bundle;
        }

        private static async Task<System.Collections.Generic.IEnumerable<T>> Query<T>(IDbConnection connection, string sql)
        {
            return await connection.QueryAsync<T>(new CommandDefinition(sql, commandTimeout: 0)).ConfigureAwait(false);
        }

        private static async Task RequireWebColumns(IDbConnection connection, string quotedDb)
        {
            await Require(connection, quotedDb, "TB_MODELINSTANCES", "tag").ConfigureAwait(false);
            await Require(connection, quotedDb, "TB_MODELINSTANCES", "s5_eqcode").ConfigureAwait(false);
            await Require(connection, quotedDb, "TB_MODELINSTANCES", "construction_code").ConfigureAwait(false);
            await Require(connection, quotedDb, "TB_MODELINSTANCES", "elementId_number").ConfigureAwait(false);
            await Require(connection, quotedDb, "TB_MODELTEMPLATES", "Subdivision_1_name").ConfigureAwait(false);
            await Require(connection, quotedDb, "TB_MODELTEMPLATES", "Subdivision_2_name").ConfigureAwait(false);
            await Require(connection, quotedDb, "TB_POCINSTANCES", "tag").ConfigureAwait(false);
            await Require(connection, quotedDb, "TB_POCINSTANCES", "connectionTag").ConfigureAwait(false);
        }

        private static async Task Require(IDbConnection connection, string quotedDb, string table, string column)
        {
            int count = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    DdwSql.ColumnExists(quotedDb),
                    new { table, column },
                    commandTimeout: 60)).ConfigureAwait(false);
            if (count < 1)
            {
                throw new InvalidOperationException(
                    "WebDdw column " + table + "." + column + " was not found. SchemaKind stays WebDdw; TbStar SQL was not used.");
            }
        }
    }
}
