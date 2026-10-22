using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Threading.Tasks;
using Dapper;
using Serilog;
using PocTrace.Core.Models;
using PocTrace.Core.Infrastructure.Data;

using System.Linq; // FirstOrDefault 사용을 위해

using System.IO;
using PocTrace.Core.Infrastructure;

namespace PocTrace.Core.Infrastructure.Repositories
{
    public class GraphLoaderRepository : IGraphLoader
    {
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly BinaryCacheRepository _binaryRepo;

        // 생성자: 일반 모드일 때 connectionFactory는 null일 수 있음 (DI 설정에 따라 다름)
        public GraphLoaderRepository(IDbConnectionFactory connectionFactory = null)
        {
            _connectionFactory = connectionFactory;
            _binaryRepo = new BinaryCacheRepository();
        }

        // 반환값: (IsStale, SavedAt, Nodes, Pocs, Pipes, Takeoffs)
        public async Task<(bool IsStale, DateTime SavedAt, 
            IEnumerable<NodeInfo> Nodes, IEnumerable<PocData> Pocs, IEnumerable<PipeData> Pipes, 
            IEnumerable<TakeoffData> Takeoffs, IEnumerable<DynamicPropertiesData> HookupNumbers)>
            LoadGraphDataAsync(string dbName, IProgress<string> progress)
        {
            // [1] 바이너리 캐시 시도 (User/Admin 공통)
            try
            {
                progress?.Report($"📂 Loading Local Cache for {dbName}...");
                Log.Information("🚀 Attempting to load cache for DB: {Db}", dbName);
                
                var cacheResult = await _binaryRepo.LoadFromCacheAsync(dbName, progress);
                
                Log.Information("✅ Cache Loaded Successfully. Date: {Date}, Stale: {Stale}", cacheResult.SavedAt, cacheResult.IsStale);
                return (cacheResult.IsStale, cacheResult.SavedAt, 
                    cacheResult.Nodes, cacheResult.Pocs, cacheResult.Pipes, 
                    cacheResult.Takeoffs, cacheResult.HookupNumbers);
            }
            catch (Exception ex)
            {
                Log.Warning("⚠️ Cache load failed for {Db}. Error: {Msg}", dbName, ex.Message);

                if (!GlobalConfig.AllowDatabaseFallback)
                {
                    throw new FileNotFoundException(
                        $"Cache file for '{dbName}' was not found or is invalid. Set AllowDatabaseFallback=true in App.config to load from the database.",
                        ex);
                }
            }

            if (_connectionFactory == null)
                throw new InvalidOperationException("Database fallback requires a valid IDbConnectionFactory and AllowDatabaseFallback=true.");

            progress?.Report($"🌏 Connecting to MSSQL Database ({dbName})...");
            Log.Information("🔄 Cache failed/missing. Connecting to MSSQL: {Db}", dbName);

            IEnumerable<NodeInfo> nodes;
            IEnumerable<PocData> pocs;
            IEnumerable<PipeData> pipes;
            IEnumerable<TakeoffData> takeoffs;
            IEnumerable<DynamicPropertiesData> dynamicProperties;

            using (var conn = _connectionFactory.CreateSourceConnection())
            {
                if (conn.State != ConnectionState.Open) conn.Open();

                var bundle = await GraphQueryProviders.CreateFromConfig()
                    .LoadAsync(conn, dbName, progress)
                    .ConfigureAwait(false);
                nodes = bundle.Nodes;
                pocs = bundle.Pocs;
                pipes = bundle.Pipes;
                takeoffs = bundle.Takeoffs;
                dynamicProperties = bundle.HookupNumbers;
            }

            Log.Information("✅ Data Fetched from DB. Nodes: {N}, POCs: {P}, Pipes: {I}, TakeOffs: {T}, HookupNumbers: {H}", 
                nodes.AsList().Count, pocs.AsList().Count, pipes.AsList().Count, takeoffs.AsList().Count, dynamicProperties.AsList().Count);

            // Clients must not write the shared cache. Admin DailyTraceJob is the only publisher.
            Log.Warning(
                "[GraphLoader] DB fallback for {Db} is memory-only. Shared cache was not overwritten.",
                dbName);
            progress?.Report("DB fallback (공유 캐시는 쓰지 않음 / 不写入共享缓存)");

            // DB에서 막 가져온 것이므로 Stale=false, Date=Now
            return (false, DateTime.Now, nodes, pocs, pipes, takeoffs, dynamicProperties);
        }
		
		// [재구현] Context-based UT Mapping Load
        // Return Type: Dictionary<DBName, Dictionary<GroupName, Dictionary<UtName, MappedName>>>
        public Dictionary<string, Dictionary<string, Dictionary<string, string>>> LoadUtMapping()
        {
            var map = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);
            
            // 파일명 변경 (.dat -> .csv)
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UtMapping.csv");

            // [New] 제거할 문자 목록 정의 (공백, 탭, 쌍따옴표, 홑따옴표)
            char[] trimChars = { ' ', '\t', '"', '\'' };

            if (File.Exists(path))
            {
                try 
                {
                    var lines = TextFileEncoding.ReadAllLines(path);

                    // 라인별 처리
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i].Trim();
                        
                        // 1. 빈 줄 건너뛰기
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        // 2. 헤더(Header) 건너뛰기 (첫 줄이 'DBName'으로 시작하면 스킵)
                        //    또는 i == 0 일 때 무조건 스킵하도록 해도 됨
                        // 헤더 건너뛰기 로직 보완 (따옴표가 있어도 감지하도록 Contains 사용)
                        if (i == 0 && (line.IndexOf("DBName", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       line.IndexOf("Group", StringComparison.OrdinalIgnoreCase) >= 0)) continue;

                        // 3. 주석(#) 처리 (필요 시)
                        if (line.StartsWith("#") || line.StartsWith("//")) continue;

                        // 4. CSV 파싱 (콤마 기준)
                        var parts = line.Split(',');

                        if (parts.Length >= 4)
                        {
                            // [핵심 수정] Trim() -> Trim(trimChars) 로 변경하여 따옴표까지 제거
                            string dbName = parts[0].Trim(trimChars);
                            string groupName = parts[1].Trim(trimChars);
                            string u1 = parts[2].Trim(trimChars);
                            string u2 = parts[3].Trim(trimChars);

                            if (string.IsNullOrEmpty(dbName) || string.IsNullOrEmpty(groupName)) continue;
                            if (string.IsNullOrEmpty(u1) || string.IsNullOrEmpty(u2)) continue;

                            // 1. DB 레벨 초기화
                            if (!map.ContainsKey(dbName)) 
                                map[dbName] = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

                            // 2. Group 레벨 초기화
                            var dbMap = map[dbName];
                            if (!dbMap.ContainsKey(groupName))
                                dbMap[groupName] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                            // 3. 매핑 등록 (양방향 지원: A->B, B->A)
                            // "이 그룹(Context) 안에서는 u1과 u2가 같다"는 의미
                            var groupMap = dbMap[groupName];
                            
                            // 덮어쓰기 이슈 방지: 이미 있으면 Log를 찍거나 정책 결정. 여기서는 덮어씀.
                            groupMap[u1] = u2;
                            groupMap[u2] = u1;
                        }
                    }
                    Log.Information($"📂 Loaded UT Mapping (Context Mode): Configured for {map.Count} DBs.");

                }
                catch (Exception ex)
                {
                    Log.Warning($"⚠️ Failed to load UtMapping.csv: {ex.Message}");
                }
            }
            else
            {
                Log.Warning("⚠️ UtMapping.csv file not found in execution directory.");
            }

            return map;
        }
    }
}
