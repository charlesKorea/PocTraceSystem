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
    /// Hookup은 tb_attributes 의 S5_LATERAL_NUMBER 를 POC owner 로 채운다.
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

        public Task<GraphLoadBundle> LoadAsync(IDbConnection connection, string dbName, IProgress<string> progress)
        {
            return LoadCore(connection, SqlIdentifier.QuoteDatabase(dbName), progress);
        }

        public Task<GraphLoadBundle> LoadFromPrefixAsync(IDbConnection connection, string catalogPrefix, IProgress<string> progress)
        {
            if (string.IsNullOrWhiteSpace(catalogPrefix))
                throw new ArgumentException("catalogPrefix is required", nameof(catalogPrefix));
            return LoadCore(connection, catalogPrefix, progress);
        }

        private async Task<GraphLoadBundle> LoadCore(IDbConnection connection, string quoted, IProgress<string> progress)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));
            if (connection.State != ConnectionState.Open)
                connection.Open();
            if (_kind == SchemaKind.WebDdw)
            {
                progress?.Report("   - WebDdw 컬럼 확인...");
                await RequireWebColumns(connection, quoted).ConfigureAwait(false);
            }

            progress?.Report("   - Fetching DDW model POCs...");
            var modelPocs = await QueryStep<DdwPocRow>(connection, "model POC", DdwSql.SelectModelPocs(quoted, _kind)).ConfigureAwait(false);
            progress?.Report("   - Fetching DDW pipe POCs...");
            var pipePocs = await QueryStep<DdwPocRow>(connection, "pipe POC", DdwSql.SelectPipePocs(quoted)).ConfigureAwait(false);

            progress?.Report("   - Fetching DDW Pipes...");
            var pipeRows = await QueryStep<DdwPipeRow>(connection, "pipe", DdwSql.SelectPipes(quoted)).ConfigureAwait(false);

            progress?.Report("   - Fetching DDW Nodes...");
            var models = await QueryStep<DdwModelRow>(connection, "model", DdwSql.SelectModels(quoted, _kind)).ConfigureAwait(false);

            progress?.Report("   - Fetching DDW attributes...");
            var attributes = await QueryStep<DdwAttributeRow>(connection, "attribute", DdwSql.SelectAttributes(quoted)).ConfigureAwait(false);

            var bundle = DdwGraphNormalizer.Build(models, modelPocs, pipePocs, pipeRows, attributes);
            var diag = bundle.Diagnostics;
            Log.Information(
                "[DDW] {Profile} nodes={Nodes} pocs={Pocs} pipes={Pipes} takeoffs={Takeoffs} modelPocs={ModelPocs} pipePocs={PipePocs} connected={Connected} unconnected={Unconnected} dangling={Dangling} pipes4plus={Pipes4} maxEnds={MaxEnds} orderDup={OrderDup} unknown={Unknown} unknownEmitted={UnknownEmitted} moduleChildren={ModuleChildren} deletedSkipped={Deleted} attrDup={AttrDup} skipBim1={SkipBim1} skipArchitecture={SkipArch} skipDuctTray={SkipDuct}",
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
                diag.AttributeDuplicates,
                diag.SkippedBimYn1,
                diag.SkippedArchitecture,
                diag.SkippedOutsidePipes);
            if (diag.UnknownSamples.Count > 0)
            {
                Log.Information("[DDW] unresolved vocabulary samples={Count} first={Sample}",
                    diag.UnknownVocabulary, diag.UnknownSamples[0]);
            }
            Log.Information("[DDW] hookups={Hookups}. id는 uniqueidentifier로 변환해 읽었다.", bundle.HookupNumbers == null ? 0 : bundle.HookupNumbers.Count);
            progress?.Report("   - DDW graph mapped");
            return bundle;
        }

        private static async Task<System.Collections.Generic.List<T>> QueryStep<T>(IDbConnection connection, string step, string sql)
        {
            Log.Information("[DDW] 단계 {Step} 조회 시작", step);
            try
            {
                var rows = (await connection.QueryAsync<T>(new CommandDefinition(sql, commandTimeout: 0)).ConfigureAwait(false)).ToList();
                Log.Information("[DDW] 단계 {Step} 조회 {Count}행", step, rows.Count);
                return rows;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "DDW 캐시 단계 " + step + " 에서 열을 읽지 못했다. id는 uniqueidentifier, 정수와 길이는 int와 float 로 맞춰 둔다. " + ex.Message,
                    ex);
            }
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
