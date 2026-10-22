using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dapper;
using PocTrace.Core.Infrastructure;

namespace PocTrace.Core.Infrastructure.Data
{
    /// <summary>
    /// 스키마 사전점검. 공유 캐시는 쓰지 않는다.
    /// 활성 SchemaKind 조회와 함께 S5D·DDW·WebDdw 준비율을 항상 센다.
    /// </summary>
    public static class SchemaReadinessProbe
    {
        public static readonly string[] S5dTables = new[]
        {
            "ObjectInstanceLog", "ObjectLog", "SubdivisionLog",
            "ConnectionPointInstanceLog", "ConnectionPointLog",
            "UtilityLog", "UtilityGroupLog", "CompositeObjectItemLog",
            "PipeLog", "TakeoffLog", "USERLOG"
        };

        public static readonly string[] DdwTables = new[]
        {
            "TB_MODELINSTANCES", "TB_MODELTEMPLATES", "TB_DIVISION",
            "TB_POCINSTANCES", "TB_POCTEMPLATES", "TB_PIPESIZE",
            "TB_UTILITIES", "TB_UTILITY_GROUPS", "TB_PIPEINSTANCES",
            "TB_NEW_USERS", "TB_NEW_USERGROUPS", "TB_ATTRIBUTES"
        };

        public static readonly string[][] WebColumns = new[]
        {
            new[] { "TB_MODELINSTANCES", "tag" },
            new[] { "TB_MODELINSTANCES", "s5_eqcode" },
            new[] { "TB_MODELINSTANCES", "construction_code" },
            new[] { "TB_MODELINSTANCES", "elementId_number" },
            new[] { "TB_MODELTEMPLATES", "Subdivision_1_name" },
            new[] { "TB_MODELTEMPLATES", "Subdivision_2_name" },
            new[] { "TB_POCINSTANCES", "tag" },
            new[] { "TB_POCINSTANCES", "connectionTag" }
        };

        public static async Task<SchemaReadinessReport> CollectAsync(string connectionString, IProgress<string> progress)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("PocTraceDB 연결 문자열이 없습니다.");

            var report = new SchemaReadinessReport
            {
                ActiveSchema = GlobalConfig.ActiveSchema
            };
            var databases = GlobalConfig.TargetDatabases ?? new string[0];
            if (databases.Length == 0)
            {
                var empty = new DatabaseReadiness { DbName = "(없음)" };
                empty.Gates.Add(new GateRow
                {
                    Group = "설정",
                    Name = "TargetDatabases",
                    Required = true,
                    Passed = false,
                    Detail = "대상 DB가 없습니다."
                });
                GraphFillMetrics.ApplyVerdict(empty, report.ActiveSchema);
                report.Databases.Add(empty);
                return report;
            }

            using (var conn = new SqlConnection(connectionString))
            {
                progress?.Report("SQL 연결");
                await conn.OpenAsync().ConfigureAwait(false);
                for (int i = 0; i < databases.Length; i++)
                {
                    string dbName = databases[i];
                    progress?.Report(dbName + " 점검");
                    report.Databases.Add(await ProbeOneAsync(conn, dbName, report.ActiveSchema, progress).ConfigureAwait(false));
                }
            }
            return report;
        }

        public static string SafeError(Exception ex)
        {
            if (ex == null)
                return "";
            string msg = ex.Message ?? "";
            if (ex.InnerException != null && !string.IsNullOrEmpty(ex.InnerException.Message))
                msg = msg + " | " + ex.InnerException.Message;
            msg = Regex.Replace(msg, "Password\\s*=\\s*[^;\\s]*", "Password=***", RegexOptions.IgnoreCase);
            if (msg.Length > 400)
                msg = msg.Substring(0, 400);
            return ex.GetType().Name + ": " + msg;
        }

        private static async Task<DatabaseReadiness> ProbeOneAsync(
            SqlConnection conn,
            string dbName,
            SchemaKind active,
            IProgress<string> progress)
        {
            var db = new DatabaseReadiness { DbName = dbName };
            string quoted;
            try
            {
                quoted = SqlIdentifier.QuoteDatabase(dbName);
            }
            catch (Exception ex)
            {
                db.Gates.Add(new GateRow
                {
                    Group = "설정",
                    Name = "TargetDatabases",
                    Required = true,
                    Passed = false,
                    Detail = SafeError(ex)
                });
                GraphFillMetrics.ApplyVerdict(db, active);
                return db;
            }

            var schemas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            await InventoryAsync(conn, quoted, db, "S5D 테이블", S5dTables, active == SchemaKind.S5d, schemas, progress).ConfigureAwait(false);
            await InventoryAsync(conn, quoted, db, "DDW 테이블", DdwTables, active != SchemaKind.S5d, schemas, progress).ConfigureAwait(false);
            await InventoryColumnsAsync(conn, quoted, db, active, progress).ConfigureAwait(false);

            bool requiredOk = true;
            for (int i = 0; i < db.Gates.Count; i++)
            {
                if (db.Gates[i].Required && !db.Gates[i].Passed)
                    requiredOk = false;
            }

            GraphLoadBundle bundle = null;
            if (!requiredOk)
            {
                db.Queries.Add(new QueryRow
                {
                    Name = "그래프 로드",
                    Ok = false,
                    Error = "필수 게이트가 실패해 활성 프로파일 조회를 생략했습니다."
                });
            }
            else
            {
                progress?.Report(dbName + " 그래프 로드");
                var sw = Stopwatch.StartNew();
                try
                {
                    var provider = GraphQueryProviders.Create(active);
                    bundle = await provider.LoadAsync(conn, dbName, progress).ConfigureAwait(false);
                    sw.Stop();
                    int nodes = bundle.Nodes != null ? bundle.Nodes.Count : 0;
                    int pocs = bundle.Pocs != null ? bundle.Pocs.Count : 0;
                    int pipes = bundle.Pipes != null ? bundle.Pipes.Count : 0;
                    int takeoffs = bundle.Takeoffs != null ? bundle.Takeoffs.Count : 0;
                    db.Queries.Add(new QueryRow
                    {
                        Name = "그래프 로드",
                        Ok = true,
                        ElapsedMs = sw.ElapsedMilliseconds,
                        RowCount = nodes + pocs + pipes + takeoffs,
                        Note = "nodes=" + nodes.ToString("N0") + " pocs=" + pocs.ToString("N0")
                            + " pipes=" + pipes.ToString("N0") + " takeoffs=" + takeoffs.ToString("N0")
                    });
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    db.Queries.Add(new QueryRow
                    {
                        Name = "그래프 로드",
                        Ok = false,
                        ElapsedMs = sw.ElapsedMilliseconds,
                        Error = SafeError(ex)
                    });
                }
            }

            if (schemas.ContainsKey("TB_PIPEINSTANCES"))
                await DistributionAsync(conn, quoted, schemas["TB_PIPEINSTANCES"], "TB_PIPEINSTANCES", "PIPE_TYPE", "DDW PIPE_TYPE", db, progress).ConfigureAwait(false);
            if (schemas.ContainsKey("TB_POCINSTANCES"))
                await DistributionAsync(conn, quoted, schemas["TB_POCINSTANCES"], "TB_POCINSTANCES", "OWNER_TYPE", "DDW OWNER_TYPE", db, progress).ConfigureAwait(false);

            GraphFillMetrics.Append(db, bundle, active);
            GraphFillMetrics.ApplyVerdict(db, active);
            return db;
        }

        private static async Task InventoryAsync(
            SqlConnection conn,
            string quotedDb,
            DatabaseReadiness db,
            string group,
            string[] tables,
            bool required,
            Dictionary<string, string> schemas,
            IProgress<string> progress)
        {
            for (int i = 0; i < tables.Length; i++)
            {
                string table = tables[i];
                progress?.Report(group + " " + table);
                var sw = Stopwatch.StartNew();
                try
                {
                    string schema = await FindSchemaAsync(conn, quotedDb, table).ConfigureAwait(false);
                    sw.Stop();
                    bool exists = !string.IsNullOrEmpty(schema);
                    db.Gates.Add(new GateRow
                    {
                        Group = group,
                        Name = table,
                        Required = required,
                        Passed = exists,
                        Detail = exists ? schema : "테이블 없음"
                    });
                    db.Queries.Add(new QueryRow
                    {
                        Name = "존재 " + table,
                        Ok = true,
                        ElapsedMs = sw.ElapsedMilliseconds,
                        RowCount = exists ? 1 : 0,
                        Note = exists ? schema : "없음"
                    });
                    if (!exists)
                        continue;
                    schemas[table] = schema;
                    await LiveCountAsync(conn, quotedDb, schema, table, db, group == "DDW 테이블").ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    db.Gates.Add(new GateRow
                    {
                        Group = group,
                        Name = table,
                        Required = required,
                        Passed = false,
                        Detail = SafeError(ex)
                    });
                    db.Queries.Add(new QueryRow
                    {
                        Name = "존재 " + table,
                        Ok = false,
                        ElapsedMs = sw.ElapsedMilliseconds,
                        Error = SafeError(ex)
                    });
                }
            }
        }

        private static async Task<string> FindSchemaAsync(SqlConnection conn, string quotedDb, string table)
        {
            const string tail = ".INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @table";
            var rows = (await conn.QueryAsync<string>(
                new CommandDefinition(
                    "SELECT TABLE_SCHEMA FROM " + quotedDb + tail,
                    new { table },
                    commandTimeout: 60)).ConfigureAwait(false)).ToList();
            if (rows.Count == 0)
                return null;
            for (int i = 0; i < rows.Count; i++)
            {
                if (string.Equals(rows[i], "dbo", StringComparison.OrdinalIgnoreCase))
                    return rows[i];
            }
            return rows[0];
        }

        private static async Task LiveCountAsync(
            SqlConnection conn,
            string quotedDb,
            string schema,
            string table,
            DatabaseReadiness db,
            bool operationTypeFilter)
        {
            var sw = Stopwatch.StartNew();
            string filter = operationTypeFilter ? " WHERE OPERATIONTYPE < 2" : "";
            string sql = "SELECT COUNT_BIG(1) FROM " + quotedDb + "." + Bracket(schema) + "." + Bracket(table) + filter;
            try
            {
                long count = await conn.ExecuteScalarAsync<long>(new CommandDefinition(sql, commandTimeout: 0)).ConfigureAwait(false);
                sw.Stop();
                db.Queries.Add(new QueryRow
                {
                    Name = "live 건수 " + table,
                    Ok = true,
                    ElapsedMs = sw.ElapsedMilliseconds,
                    RowCount = count,
                    Note = operationTypeFilter ? "OPERATIONTYPE < 2" : "전체 건수"
                });
                db.Distribution.Add(new DistRow
                {
                    Group = "live 건수",
                    Key = table,
                    Count = count
                });
            }
            catch (Exception ex)
            {
                sw.Stop();
                db.Queries.Add(new QueryRow
                {
                    Name = "live 건수 " + table,
                    Ok = false,
                    ElapsedMs = sw.ElapsedMilliseconds,
                    Error = SafeError(ex)
                });
            }
        }

        private static async Task InventoryColumnsAsync(
            SqlConnection conn,
            string quotedDb,
            DatabaseReadiness db,
            SchemaKind active,
            IProgress<string> progress)
        {
            for (int i = 0; i < WebColumns.Length; i++)
            {
                string table = WebColumns[i][0];
                string column = WebColumns[i][1];
                progress?.Report("WebDdw 컬럼 " + table + "." + column);
                var sw = Stopwatch.StartNew();
                try
                {
                    int count = await conn.ExecuteScalarAsync<int>(
                        new CommandDefinition(
                            DdwSql.ColumnExists(quotedDb),
                            new { table, column },
                            commandTimeout: 60)).ConfigureAwait(false);
                    sw.Stop();
                    bool passed = count > 0;
                    db.Gates.Add(new GateRow
                    {
                        Group = "WebDdw 컬럼",
                        Name = table + "." + column,
                        Required = active == SchemaKind.WebDdw,
                        Passed = passed,
                        Detail = passed ? "있음" : "없음. TbStar SQL에는 넣지 않습니다."
                    });
                    db.Queries.Add(new QueryRow
                    {
                        Name = "컬럼 " + table + "." + column,
                        Ok = true,
                        ElapsedMs = sw.ElapsedMilliseconds,
                        RowCount = count
                    });
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    db.Gates.Add(new GateRow
                    {
                        Group = "WebDdw 컬럼",
                        Name = table + "." + column,
                        Required = active == SchemaKind.WebDdw,
                        Passed = false,
                        Detail = SafeError(ex)
                    });
                    db.Queries.Add(new QueryRow
                    {
                        Name = "컬럼 " + table + "." + column,
                        Ok = false,
                        ElapsedMs = sw.ElapsedMilliseconds,
                        Error = SafeError(ex)
                    });
                }
            }
        }

        private static async Task DistributionAsync(
            SqlConnection conn,
            string quotedDb,
            string schema,
            string table,
            string column,
            string group,
            DatabaseReadiness db,
            IProgress<string> progress)
        {
            progress?.Report(group);
            var sw = Stopwatch.StartNew();
            string sql = "SELECT CAST(ISNULL(" + Bracket(column) + ", -1) AS nvarchar(40)) AS K, COUNT_BIG(1) AS C FROM "
                + quotedDb + "." + Bracket(schema) + "." + Bracket(table)
                + " WHERE OPERATIONTYPE < 2 GROUP BY " + Bracket(column);
            try
            {
                var rows = (await conn.QueryAsync<DistSqlRow>(new CommandDefinition(sql, commandTimeout: 0)).ConfigureAwait(false)).ToList();
                sw.Stop();
                db.Queries.Add(new QueryRow
                {
                    Name = "분포 " + column,
                    Ok = true,
                    ElapsedMs = sw.ElapsedMilliseconds,
                    RowCount = rows.Count
                });
                int limit = Math.Min(rows.Count, 100);
                for (int i = 0; i < limit; i++)
                {
                    db.Distribution.Add(new DistRow
                    {
                        Group = group,
                        Key = rows[i].K ?? "",
                        Count = rows[i].C
                    });
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                db.Queries.Add(new QueryRow
                {
                    Name = "분포 " + column,
                    Ok = false,
                    ElapsedMs = sw.ElapsedMilliseconds,
                    Error = SafeError(ex)
                });
            }
        }

        private static string Bracket(string identifier)
        {
            if (string.IsNullOrEmpty(identifier) || identifier.IndexOf(']') >= 0 || identifier.IndexOf('[') >= 0)
                throw new InvalidOperationException("식별자를 인용할 수 없습니다.");
            for (int i = 0; i < identifier.Length; i++)
            {
                char c = identifier[i];
                bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok)
                    throw new InvalidOperationException("식별자를 인용할 수 없습니다.");
            }
            return "[" + identifier + "]";
        }

        private sealed class DistSqlRow
        {
            public string K { get; set; }
            public long C { get; set; }
        }
    }
}
