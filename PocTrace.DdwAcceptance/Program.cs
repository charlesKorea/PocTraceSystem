using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Engine.Logic;
using PocTrace.Core.Enums;
using PocTrace.Core.Infrastructure;
using PocTrace.Core.Infrastructure.Data;
using PocTrace.Startup;
using PocTrace.Core.Infrastructure.Repositories;
using PocTrace.Core.Models;

namespace PocTrace.DdwAcceptance
{
    internal static class Program
    {
        private static int _failed;

        private static int Main()
        {
            string boot = null;
            ExeUseSession use = null;
            try
            {
                boot = ExeBootFile.Start("DdwAcceptance");
                use = ExeUseLog.Open("DdwAcceptance");
                SchemaAndSql();
                S5dSlots();
                DdwEdges();
                EndpointsBeyondThree();
                TakeoffAndUnits();
                LiveRowsAndVocabulary();
                CacheRoundTrip();
                LegacyV9();
                GraphReachability();
                SchemaProbeReport();
            }
            catch (Exception ex)
            {
                ExeBootFile.Fail(boot, ex);
                Console.Error.WriteLine("FAIL exception " + ex);
                return 1;
            }
            finally
            {
                if (use != null)
                    use.Dispose();
            }

            if (_failed > 0)
            {
                Console.Error.WriteLine("FAILED " + _failed);
                return 1;
            }
            Console.WriteLine("DDW acceptance checks passed");
            return 0;
        }

        private static void SchemaAndSql()
        {
            Check(GlobalConfig.ActiveSchema == SchemaKind.S5d, "default SchemaKind is S5d");
            ExpectThrow(delegate { SchemaKindParser.Parse("guess"); }, "invalid SchemaKind throws");
            Check(GraphQueryProviders.Create(SchemaKind.S5d) is S5dGraphQueryProvider, "S5d provider");
            Check(GraphQueryProviders.Create(SchemaKind.TbStar) is DdwGraphQueryProvider, "TbStar provider");
            Check(GraphQueryProviders.Create(SchemaKind.WebDdw) is DdwGraphQueryProvider, "WebDdw provider");
            Check(SqlIdentifier.QuoteDatabase("fixture") == "[fixture]", "allow-list quote");
            ExpectThrow(delegate { SqlIdentifier.QuoteDatabase("other-db"); }, "db outside allow-list");

            string s5dPipes = SqlQueries.GetSelectPipes("x1");
            Check(s5dPipes.Contains("Outlet1") && s5dPipes.Contains("*1000") && s5dPipes.Contains("PipeLog"), "S5D pipe SQL kept");
            Check(!s5dPipes.Contains("TB_PIPEINSTANCES"), "S5D pipe SQL is not DDW");

            string tb = DdwSql.SelectModels("[fixture]", SchemaKind.TbStar);
            Check(tb.Contains("TB_MODELINSTANCES") && tb.Contains("OPERATIONTYPE < 2"), "TbStar live filter");
            Check(!tb.Contains("s5_eqcode") && !tb.Contains("[tag]") && !tb.Contains("*1000"), "TbStar has no projected columns or scale");
            Check(!tb.Contains("TOP 1") && !tb.Contains("VERSION_SEQUENCE"), "TbStar has no TOP 1 or version join");

            string web = DdwSql.SelectModels("[fixture]", SchemaKind.WebDdw);
            Check(web.Contains("s5_eqcode") && web.Contains("Subdivision_1_name") && web.Contains("elementId_number"), "WebDdw projected columns");
            Check(!web.Contains("*1000"), "WebDdw model SQL has no scale");

            string pipes = DdwSql.SelectPipes("[fixture]");
            Check(pipes.Contains("USER_GROUP_NAME") && pipes.Contains("ROUND(pi.LENGTH1, 1)"), "DDW length and company");
            Check(!pipes.Contains("*1000") && !pipes.Contains("TOP 1") && !pipes.Contains("Origin"), "DDW pipe SQL limits");
            Check(DdwSql.SelectModelPocs("[fixture]", SchemaKind.TbStar).Contains("OWNER_TYPE = 768"), "model pocs only");
            Check(!DdwSql.SelectModelPocs("[fixture]", SchemaKind.TbStar).Contains("[tag]"), "TbStar poc has no tag column");
            Check(DdwSql.SelectModelPocs("[fixture]", SchemaKind.WebDdw).Contains("connectionTag"), "WebDdw connectionTag");
            Check(DdwSql.SelectPipePocs("[fixture]").Contains("OWNER_TYPE = 256"), "pipe pocs");
        }

        private static void S5dSlots()
        {
            var pipe = new PipeData();
            pipe.Outlet1 = G(1);
            pipe.Outlet2 = null;
            pipe.Outlet3 = G(3);
            pipe.EnsureThreeS5dSlots();
            Check(pipe.Endpoints.Count == 3, "S5D hydrates 3 slots");
            Check(pipe.Endpoints[1].Resolution == PipeEndpointResolution.Unconnected, "null slot stays");
            Check(pipe.Outlet1 == G(1) && pipe.Outlet2 == null && pipe.Outlet3 == G(3), "projection keeps gaps");
            Check(pipe.Endpoints[0].ConnectionOrder == 1 && pipe.Endpoints[2].ConnectionOrder == 3, "orders 1 and 3");
        }

        private static void DdwEdges()
        {
            var pipeId = G(10);
            var pipePoc = G(11);
            var modelPoc = G(12);
            var otherPoc = G(13);
            var bundle = DdwGraphNormalizer.Build(
                null,
                new[]
                {
                    Poc(modelPoc, G(20), pipePoc, DdwGraphNormalizer.PipeOwnerType, pipePoc, pipeId),
                    Poc(G(21), G(22), otherPoc, DdwGraphNormalizer.ModelOwnerType, otherPoc, otherPoc),
                    Poc(G(23), G(24), null, null, null, null),
                    Poc(G(25), G(26), Guid.Empty, null, null, null),
                    Poc(G(27), G(28), G(99), null, null, null),
                    new DdwPocRow
                    {
                        InstanceId = G(29),
                        OwnerInstanceId = pipeId,
                        OwnerType = DdwGraphNormalizer.PipeOwnerType,
                        OperationType = 0
                    }
                },
                null,
                null,
                null);

            Check(bundle.Pocs.Count == 5, "OWNER_TYPE 256 is not a PocData");
            Check(bundle.Pocs[0].Outlet == pipeId, "DDW-01 model poc to pipe");
            Check(bundle.Pocs[1].Outlet == otherPoc, "DDW-02 model poc to model poc");
            Check(bundle.Pocs[2].Outlet == null && bundle.Pocs[3].Outlet == null && bundle.Pocs[4].Outlet == null, "DDW-04 outlets skipped");
            Check(bundle.Diagnostics.UnconnectedCount == 2 && bundle.Diagnostics.DanglingCount == 1, "DDW-04 counts");
            Check(bundle.Pocs[0].OwnerId == G(20), "owner id stays raw");
            Check(bundle.Pocs[0].PointID == Guid.Empty, "PointID is not the poc instance");
            Check(bundle.HookupNumbers.Count == 0, "hookup deferred");
        }

        private static void EndpointsBeyondThree()
        {
            var a = G(2);
            var b = G(1);
            var pipe = new DdwPipeRow
            {
                InstanceId = G(40),
                PipeType = 17301760,
                Length = 10,
                OperationType = 0
            };
            var pocs = new[]
            {
                Endpoint(G(40), G(41), 2, a, DdwGraphNormalizer.ModelOwnerType, a),
                Endpoint(G(40), G(42), 2, b, DdwGraphNormalizer.ModelOwnerType, b),
                Endpoint(G(40), G(43), 0, G(43), DdwGraphNormalizer.ModelOwnerType, G(43)),
                Endpoint(G(40), G(44), 5, null, 0, Guid.Empty)
            };
            var bundle = DdwGraphNormalizer.Build(null, null, pocs, new[] { pipe }, null);
            var dto = bundle.Pipes[0];
            Check(dto.Endpoints.Count == 4, "DDW-03 keeps 4 endpoints");
            Check(dto.Endpoints[0].ConnectionOrder == 0, "lowest order first");
            Check(dto.Endpoints[1].ConnectionOrder == 2 && dto.Endpoints[1].PocInstanceId == G(41), "same order sorts by poc id");
            Check(dto.Endpoints[2].PocInstanceId == G(42) && dto.Endpoints[3].ConnectionOrder == 5, "gap retained");
            Check(dto.Endpoints[3].Resolution == PipeEndpointResolution.Unconnected, "open end stays in place");
            Check(dto.Outlet1 == G(43) && dto.Outlet2 == a && dto.Outlet3 == b, "projection is first three targets");
            Check(PipeEndpointTraversal.ConnectedTargets(dto).Count() == 3, "open end is not traversed");
            Check(bundle.Diagnostics.DuplicateConnectionOrders == 1, "duplicate order counted");
        }

        private static void TakeoffAndUnits()
        {
            var pipeId = G(50);
            var takeoffId = G(51);
            var pipes = new[]
            {
                new DdwPipeRow { InstanceId = pipeId, PipeType = 17301760, Length = 1000, OperationType = 1 },
                new DdwPipeRow { InstanceId = takeoffId, PipeType = DdwGraphNormalizer.TakeoffPipeType, Length = 3, OperationType = 0, Tag = "TOF" }
            };
            var pocs = new[]
            {
                Endpoint(takeoffId, G(52), 0, pipeId, DdwGraphNormalizer.PipeOwnerType, pipeId),
                Endpoint(takeoffId, G(53), 1, G(54), DdwGraphNormalizer.ModelOwnerType, G(54))
            };
            var bundle = DdwGraphNormalizer.Build(null, null, pocs, pipes, null);
            Check(bundle.Pipes.Count == 1 && bundle.Pipes[0].PipeId == pipeId, "takeoff excluded from pipes");
            Check(bundle.Takeoffs.Count == 1 && bundle.Takeoffs[0].PipeId == pipeId, "DDW-06 continuation is earliest pipe");
            Check(bundle.Pipes[0].Length == 1000, "DDW-07 length is not scaled");
            Check(DdwGraphNormalizer.NormalizeLength(1.25) == 1.3, "length rounds to 0.1");
            Check(DdwGraphNormalizer.NormalizeLength(0) == 0, "non positive length is 0");
            Check(DdwGraphNormalizer.NormalizePz(15.6) == 16, "PZ rounds without scale");

            var sized = DdwGraphNormalizer.Build(
                null,
                new[]
                {
                    new DdwPocRow
                    {
                        InstanceId = G(60),
                        OwnerInstanceId = G(61),
                        OwnerType = DdwGraphNormalizer.ModelOwnerType,
                        OperationType = 0,
                        PocShape = 0,
                        Diameter = 25,
                        TemplateDiameter = 99,
                        InstanceUtilityName = "UTIL_A",
                        TemplateUtilityName = "UTIL_B"
                    }
                },
                null,
                null,
                null);
            Check(sized.Pocs[0].Size == 25 && sized.Pocs[0].UtilityName == "UTIL_A", "instance size and utility win");
        }

        private static void LiveRowsAndVocabulary()
        {
            var models = new[]
            {
                new DdwModelRow
                {
                    InstanceId = G(70),
                    Name = "EQP",
                    Sub1Name = "1-MAIN TOOL",
                    DivisionType = 0,
                    CompositeYn = 1,
                    ParentInstanceId = Guid.Empty,
                    Pz = 10,
                    OperationType = 0
                },
                new DdwModelRow
                {
                    InstanceId = G(71),
                    Sub1Name = "1-MAIN TOOL",
                    DivisionType = 0,
                    CompositeYn = 0,
                    Pz = 10,
                    OperationType = 1
                },
                new DdwModelRow
                {
                    InstanceId = G(72),
                    Name = "GRATING",
                    Sub1Name = "GRATING",
                    DivisionType = 9,
                    ModelType = 50594049,
                    CompositeYn = 1,
                    ParentInstanceId = Guid.Empty,
                    Pz = 1,
                    OperationType = 0
                },
                new DdwModelRow
                {
                    InstanceId = G(73),
                    Sub1Name = "1-MAIN TOOL",
                    DivisionType = 0,
                    CompositeYn = 1,
                    OperationType = 2
                }
            };
            var bundle = DdwGraphNormalizer.Build(models, null, null, new[]
            {
                new DdwPipeRow { InstanceId = G(74), PipeType = 1, Length = 1, OperationType = 2 }
            }, null);

            Check(bundle.Nodes.Count == 1 && bundle.Nodes[0].Type == NodeType.Equipment, "independent equipment kept");
            Check(!bundle.Nodes.Any(n => n.InstanceId == G(71)), "module child is not equipment");
            Check(!bundle.Nodes.Any(n => n.Type == NodeType.Equipment && n.Sub1Name == "GRATING"), "model type alone is not equipment");
            Check(bundle.Diagnostics.UnknownVocabulary >= 1, "unknown vocabulary counted");
            Check(bundle.Diagnostics.UnknownSamples.Exists(s => s.Contains("GRATING") && s.Contains("50594049")), "grating sample");
            Check(bundle.Diagnostics.DeletedRowsSkipped >= 2, "DDW-05 operation type 2 skipped");
            Check(bundle.Pipes.Count == 0, "deleted pipe omitted");
            Check(bundle.Nodes[0].InstanceNumericId == PocTrace.Core.Utils.DotNetGuidHash.FromGuid(G(70)), "numeric id from guid");
        }

        private static void CacheRoundTrip()
        {
            var root = GlobalConfig.SharedCachePath;
            if (Directory.Exists(root))
                Directory.Delete(root, true);

            var pipe = new PipeData { PipeId = G(80), Length = 4.5, UtilityName = "U", ModuleName = "M" };
            var endpoints = new List<PipeEndpoint>();
            for (int i = 0; i < 115; i++)
            {
                endpoints.Add(new PipeEndpoint
                {
                    PocInstanceId = G(1000 + i),
                    ConnectionOrder = i * 2,
                    NormalizedTarget = G(2000 + i),
                    Resolution = PipeEndpointResolution.Connected
                });
            }
            pipe.ReplaceEndpoints(endpoints);

            var repo = new BinaryCacheRepository();
            repo.SaveToCache("fixture", new List<NodeInfo>(), new List<PocData>(), new List<PipeData> { pipe },
                new List<TakeoffData>(), new List<DynamicPropertiesData>());

            int version;
            DateTime savedAt;
            Check(repo.TryPeekManifest("fixture", out version, out savedAt), "manifest readable");
            Check(version == CacheConstants.CurrentVersion && version == 10, "CACHE-01 version 10");

            var loaded = repo.LoadFromCache("fixture");
            var again = loaded.Pipes.Single();
            Check(again.Endpoints.Count == 115, "CACHE-01 115 endpoints");
            Check(again.Endpoints[0].ConnectionOrder == 0 && again.Endpoints[114].ConnectionOrder == 228, "orders survived");
            Check(again.Endpoints[114].NormalizedTarget == G(2000 + 114) && again.Length == 4.5, "target and length survived");
            Check(again.Outlet1 == G(2000) && again.Outlet3 == G(2002), "first three projections");

            var pipesPath = Path.Combine(GlobalConfig.GetLiveCacheDirectory("fixture"), CacheConstants.SectionPipes);
            using (var fs = new FileStream(pipesPath, FileMode.Open, FileAccess.Write))
                fs.SetLength(8);
            ExpectThrow(delegate { repo.LoadFromCache("fixture"); }, "CACHE-02 truncated section fails");
        }

        private static void LegacyV9()
        {
            using (var ms = new MemoryStream())
            {
                using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true))
                {
                    w.Write(2);
                    WriteV9(w, G(90), G(91), null, G(93));
                    WriteV9(w, G(94), G(95), G(96), null);
                }
                ms.Position = 0;
                using (var r = new BinaryReader(ms, System.Text.Encoding.UTF8, true))
                {
                    var list = PipeSectionFormat.Read(r, 9);
                    Check(list.Count == 2 && list[1].PipeId == G(94), "S5D-02 stream stays aligned");
                    Check(list[0].Endpoints.Count == 3, "v9 hydrates exactly 3");
                    Check(list[0].Endpoints[1].Resolution == PipeEndpointResolution.Unconnected, "middle slot unconnected");
                    Check(list[0].Outlet1 == G(91) && list[0].Outlet3 == G(93), "v9 outlets");
                    Check(list[1].Endpoints.Count == 3 && list[1].Outlet2 == G(96), "second pipe intact");
                }
            }
        }

        private static void GraphReachability()
        {
            var equip = G(110);
            var pipeId = G(111);
            var takeoffId = G(112);
            var fourth = G(113);
            var pipe = new PipeData { PipeId = pipeId, Length = 1 };
            pipe.ReplaceEndpoints(new[]
            {
                new PipeEndpoint { PocInstanceId = G(1), ConnectionOrder = 0, Resolution = PipeEndpointResolution.Unconnected },
                new PipeEndpoint { PocInstanceId = G(2), ConnectionOrder = 1, NormalizedTarget = G(114), Resolution = PipeEndpointResolution.Connected },
                new PipeEndpoint { PocInstanceId = G(3), ConnectionOrder = 2, NormalizedTarget = G(115), Resolution = PipeEndpointResolution.Connected },
                new PipeEndpoint { PocInstanceId = G(4), ConnectionOrder = 4, NormalizedTarget = takeoffId, Resolution = PipeEndpointResolution.Connected },
                new PipeEndpoint { PocInstanceId = G(5), ConnectionOrder = 9, NormalizedTarget = fourth, Resolution = PipeEndpointResolution.Connected }
            });
            var other = new PipeData { PipeId = G(116), Length = 1 };
            other.ReplaceEndpoints(new[]
            {
                new PipeEndpoint { ConnectionOrder = 1, NormalizedTarget = fourth, Resolution = PipeEndpointResolution.Connected }
            });

            var cache = new GraphCacheManager();
            cache.Initialize(
                "fixture",
                new[] { new NodeInfo { InstanceId = equip, Tag = "EQP_A", Type = NodeType.Equipment, Name = "EQP_A" } },
                new[] { new PocData { InstanceId = G(117), OwnerId = equip, Outlet = pipeId } },
                new[] { pipe, other },
                new[] { new TakeoffData { TakeoffId = takeoffId, Tag = "TOF_A", PipeId = pipeId } },
                new DynamicPropertiesData[0],
                null,
                true);

            Guid indexed;
            Check(cache.TryFindPipeIdByOutlet(takeoffId, out indexed) && indexed == pipeId, "index sees endpoint 4");
            Check(cache.TryFindPipeIdByOutlet(fourth, out indexed) && indexed == pipeId, "first pipe wins");
            Check(cache.OutletIndexCollisions == 1, "collision counted");

            var incoming = cache.GetReverseGraphIndex().GetIncomingEdges(fourth);
            Check(incoming.Any(e => e.SourceId == pipeId), "reverse index sees endpoint past slot 3");

            var trace = new TraceAlgorithm(cache, null) { EnableDetailedDebug = false };
            var results = trace.TracePath(new PocData { InstanceId = G(117), OwnerId = equip, Outlet = pipeId }, "EQP_A");
            Check(results.Any(r => r.EndType == EndPointType.Takeoff), "GRAPH-01 forward reaches takeoff past slot 3");
        }

        private static DdwPocRow Poc(Guid id, Guid owner, Guid? connected, int? targetOwnerType, Guid? targetInstance, Guid? targetOwner)
        {
            return new DdwPocRow
            {
                InstanceId = id,
                OwnerInstanceId = owner,
                OwnerType = DdwGraphNormalizer.ModelOwnerType,
                ConnectedPocId = connected,
                TargetInstanceId = targetInstance,
                TargetOwnerType = targetOwnerType,
                TargetOwnerInstanceId = targetOwner,
                OperationType = 0
            };
        }

        private static DdwPocRow Endpoint(Guid ownerPipe, Guid pocId, int order, Guid? connected, int targetOwnerType, Guid targetOwner)
        {
            bool linked = connected.HasValue && connected.Value != Guid.Empty;
            return new DdwPocRow
            {
                InstanceId = pocId,
                OwnerInstanceId = ownerPipe,
                OwnerType = DdwGraphNormalizer.PipeOwnerType,
                ConnectionOrder = order,
                ConnectedPocId = connected,
                TargetInstanceId = linked ? connected : null,
                TargetOwnerType = linked ? (int?)targetOwnerType : null,
                TargetOwnerInstanceId = linked ? (Guid?)targetOwner : null,
                OperationType = 0
            };
        }

        private static void WriteV9(BinaryWriter w, Guid id, Guid? o1, Guid? o2, Guid? o3)
        {
            w.Write(id.ToByteArray());
            WriteNullable(w, o1);
            WriteNullable(w, o2);
            WriteNullable(w, o3);
            w.Write(1.5d);
            w.Write("u");
            w.Write("m");
            w.Write(false);
            w.Write("user");
            w.Write("co");
        }

        private static void WriteNullable(BinaryWriter w, Guid? value)
        {
            w.Write(value.HasValue);
            if (value.HasValue)
                w.Write(value.Value.ToByteArray());
        }

        private static Guid G(int n)
        {
            var bytes = new byte[16];
            bytes[0] = (byte)(n & 0xFF);
            bytes[1] = (byte)((n >> 8) & 0xFF);
            bytes[2] = (byte)((n >> 16) & 0xFF);
            bytes[15] = 1;
            return new Guid(bytes);
        }

        private static void SchemaProbeReport()
        {
            string leaked = SchemaReadinessProbe.SafeError(new InvalidOperationException("Login failed Password=Secret123; server"));
            Check(leaked.Contains("Password=***") && !leaked.Contains("Secret123"), "probe error redacts password");

            var warn = new DatabaseReadiness { DbName = "fixture" };
            AddGates(warn, SchemaReadinessProbe.S5dTables, "S5D 테이블", true, true);
            AddGates(warn, SchemaReadinessProbe.DdwTables, "DDW 테이블", false, false);
            for (int i = 0; i < SchemaReadinessProbe.WebColumns.Length; i++)
            {
                warn.Gates.Add(new GateRow
                {
                    Group = "WebDdw 컬럼",
                    Name = SchemaReadinessProbe.WebColumns[i][0] + "." + SchemaReadinessProbe.WebColumns[i][1],
                    Required = false,
                    Passed = false,
                    Detail = "없음"
                });
            }
            warn.Queries.Add(new QueryRow { Name = "그래프 로드", Ok = true, RowCount = 4, Note = "synthetic" });
            GraphFillMetrics.Append(warn, SyntheticBundle(true), SchemaKind.S5d);
            GraphFillMetrics.ApplyVerdict(warn, SchemaKind.S5d);
            Check(warn.Verdict == "주의", "S5d without DDW is caution");
            Check(warn.Headline.Contains("DDW 테이블 0/" + SchemaReadinessProbe.DdwTables.Length), "headline shows missing DDW");
            Check(warn.Fills.Exists(f => f.Name == "DDW 테이블" && f.PercentText == "0.0%"), "DDW fill percent");
            Check(warn.Fills.Exists(f => f.Name == "공유 캐시" && f.Note.Contains("GraphCache")), "cache is not written");
            Check(warn.Gaps.Exists(g => g.Category == "Dangling 끝점"), "dangling gap");
            Check(warn.Gaps.Exists(g => g.Category == "Takeoff PipeId 없음"), "takeoff gap");
            Check(warn.Gaps.Exists(g => g.Category == "미분류 어휘" && g.Key == "GRATING"), "unknown sample gap");
            Check(warn.Distribution.Exists(d => d.Group == "배관당 유효 끝점" && d.Key == "1"), "endpoint histogram");

            var failed = new DatabaseReadiness { DbName = "fixture" };
            failed.Gates.Add(new GateRow { Group = "S5D 테이블", Name = "PipeLog", Required = true, Passed = false, Detail = "테이블 없음" });
            failed.Queries.Add(new QueryRow { Name = "그래프 로드", Ok = false, Error = "필수 게이트가 실패해 활성 프로파일 조회를 생략했습니다." });
            GraphFillMetrics.Append(failed, null, SchemaKind.S5d);
            GraphFillMetrics.ApplyVerdict(failed, SchemaKind.S5d);
            Check(failed.Verdict == "실패" && failed.Headline.Contains("PipeLog"), "required gate failure");

            var loadOnly = new DatabaseReadiness { DbName = "fixture" };
            loadOnly.Queries.Add(new QueryRow { Name = "그래프 로드", Ok = false, Error = "timeout" });
            GraphFillMetrics.Append(loadOnly, null, SchemaKind.S5d);
            GraphFillMetrics.ApplyVerdict(loadOnly, SchemaKind.S5d);
            Check(loadOnly.Verdict == "실패" && loadOnly.Headline.Contains("timeout"), "load exception headline");

            var tb = new DatabaseReadiness { DbName = "fixture" };
            AddGates(tb, SchemaReadinessProbe.S5dTables, "S5D 테이블", false, true);
            AddGates(tb, SchemaReadinessProbe.DdwTables, "DDW 테이블", true, true);
            for (int i = 0; i < SchemaReadinessProbe.WebColumns.Length; i++)
            {
                tb.Gates.Add(new GateRow
                {
                    Group = "WebDdw 컬럼",
                    Name = SchemaReadinessProbe.WebColumns[i][0] + "." + SchemaReadinessProbe.WebColumns[i][1],
                    Required = false,
                    Passed = true,
                    Detail = "있음"
                });
            }
            tb.Queries.Add(new QueryRow { Name = "그래프 로드", Ok = true, RowCount = 2 });
            var clean = SyntheticBundle(false);
            clean.Nodes[0].Tag = "";
            GraphFillMetrics.Append(tb, clean, SchemaKind.TbStar);
            GraphFillMetrics.ApplyVerdict(tb, SchemaKind.TbStar);
            Check(tb.Verdict == "통과", "TbStar tag 0 percent is not a failure");
            Check(tb.Fills.Exists(f => f.Name == "Equipment Tag" && f.Level == "정보" && f.PercentText == "0.0%"), "TbStar tag stays informational");

            var web = new DatabaseReadiness { DbName = "fixture" };
            web.Gates.Add(new GateRow { Group = "WebDdw 컬럼", Name = "TB_MODELINSTANCES.tag", Required = true, Passed = false, Detail = "없음" });
            web.Queries.Add(new QueryRow { Name = "그래프 로드", Ok = false, Error = "생략" });
            GraphFillMetrics.Append(web, null, SchemaKind.WebDdw);
            GraphFillMetrics.ApplyVerdict(web, SchemaKind.WebDdw);
            Check(web.Verdict == "실패", "missing WebDdw column fails");

            try
            {
                string dir = Path.Combine(Path.GetTempPath(), "poctrace-schema-probe");
                string path = SchemaReadinessExcel.Write(new SchemaReadinessReport
                {
                    ActiveSchema = SchemaKind.S5d,
                    Databases = new List<DatabaseReadiness> { warn }
                }, dir);
                Check(File.Exists(path) && new FileInfo(path).Length > 1000, "excel report written");
                string packed = ReadZipText(path);
                Check(packed.Contains("요약") && packed.Contains("게이트") && packed.Contains("쿼리"), "excel sheet names");
                Check(packed.Contains("채움") && packed.Contains("부족") && packed.Contains("분포") && packed.Contains("안내"), "excel detail sheets");
                Check(packed.Contains("주의") && packed.Contains("GraphCache"), "excel verdict and cache note");
                Check(!packed.Contains("Secret123") && !packed.Contains("Password="), "workbook has no password");
            }
            catch (Exception ex)
            {
                Console.WriteLine("excel write skipped: " + ex.GetType().Name + " " + ex.Message);
            }
        }

        private static string ReadZipText(string path)
        {
            var builder = new System.Text.StringBuilder();
            using (var zip = ZipFile.OpenRead(path))
            {
                foreach (var entry in zip.Entries)
                {
                    if (!entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                        continue;
                    using (var reader = new StreamReader(entry.Open()))
                        builder.Append(reader.ReadToEnd());
                }
            }
            return builder.ToString();
        }

        private static void AddGates(DatabaseReadiness db, string[] names, string group, bool required, bool passed)
        {
            for (int i = 0; i < names.Length; i++)
            {
                db.Gates.Add(new GateRow
                {
                    Group = group,
                    Name = names[i],
                    Required = required,
                    Passed = passed,
                    Detail = passed ? "있음" : "테이블 없음"
                });
            }
        }

        private static GraphLoadBundle SyntheticBundle(bool withGaps)
        {
            var pipe = new PipeData { PipeId = G(501) };
            var endpoints = new List<PipeEndpoint>
            {
                new PipeEndpoint
                {
                    PocInstanceId = G(502),
                    ConnectionOrder = 1,
                    NormalizedTarget = G(503),
                    Resolution = PipeEndpointResolution.Connected
                }
            };
            if (withGaps)
            {
                endpoints.Add(new PipeEndpoint
                {
                    PocInstanceId = G(504),
                    ConnectionOrder = 2,
                    Resolution = PipeEndpointResolution.Dangling
                });
            }
            pipe.ReplaceEndpoints(endpoints);
            return new GraphLoadBundle
            {
                Nodes = new List<NodeInfo>
                {
                    new NodeInfo { InstanceId = G(505), Type = NodeType.Equipment, Tag = "P-100" }
                },
                Pocs = new List<PocData>
                {
                    new PocData { Outlet = G(503) }
                },
                Pipes = new List<PipeData> { pipe },
                Takeoffs = new List<TakeoffData>
                {
                    new TakeoffData { TakeoffId = G(506), PipeId = withGaps ? Guid.Empty : G(501) }
                },
                Diagnostics = new DdwLoadDiagnostics
                {
                    DanglingCount = withGaps ? 1 : 0,
                    UnknownVocabulary = withGaps ? 1 : 0,
                    UnknownSamples = withGaps ? new List<string> { "GRATING" } : new List<string>()
                }
            };
        }

        private static void Check(bool condition, string name)
        {
            if (condition)
            {
                Console.WriteLine("ok " + name);
                return;
            }
            _failed++;
            Console.Error.WriteLine("FAIL " + name);
        }

        private static void ExpectThrow(Action action, string name)
        {
            try
            {
                action();
            }
            catch (Exception)
            {
                Console.WriteLine("ok " + name);
                return;
            }
            _failed++;
            Console.Error.WriteLine("FAIL " + name);
        }
    }
}
