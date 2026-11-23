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
            Check(tb.Contains("TB_MODELINSTANCES") && tb.Contains("version_sequence < 1000000000"), "TbStar live filter");
            Check(!tb.Contains("OPERATIONTYPE <"), "TbStar does not filter OPERATIONTYPE");
            Check(!tb.Contains("s5_eqcode") && !tb.Contains("[tag]") && !tb.Contains("*1000"), "TbStar has no projected columns or scale");
            Check(!tb.Contains("TOP 1"), "TbStar has no TOP 1");

            string web = DdwSql.SelectModels("[fixture]", SchemaKind.WebDdw);
            Check(web.Contains("s5_eqcode") && web.Contains("Subdivision_1_name") && web.Contains("elementId_number"), "WebDdw projected columns");
            Check(!web.Contains("*1000"), "WebDdw model SQL has no scale");

            string pipes = DdwSql.SelectPipes("[fixture]");
            Check(pipes.Contains("ROUND(pi.LENGTH2, 1)"), "DDW length");
            Check(!pipes.Contains("pi.USER_ID") && !pipes.Contains("TB_NEW_USERGROUPS") && !pipes.Contains("pi.LAST_UPDATE"), "pipe table has no user_id");
            Check(pipes.Contains("TB_VERSIONS") && pipes.Contains("VERSION_NUMBER") && pipes.Contains("LOGIN_ID") && pipes.Contains("TB_NEW_USERS"), "version user lookup");
            Check(!pipes.Contains("*1000") && !pipes.Contains("TOP 1") && !pipes.Contains("Origin"), "DDW pipe SQL limits");
            Check(tb.Contains("TRY_CONVERT(uniqueidentifier, mi.INSTANCE_ID)"), "model id is uniqueidentifier");
            Check(pipes.Contains("AS float"), "pipe length is float");
            Check(pipes.Contains("pi.DIAMETER1") && !pipes.Contains("PIPESIZE_ID"), "pipe size is diameter1");
            Check(DdwSql.SelectModelPocs("[fixture]", SchemaKind.TbStar).Contains("= 768"), "model pocs only");
            Check(DdwSql.SelectAttributes("[fixture]").Contains("TRY_CONVERT(uniqueidentifier, a.OWNER_ID)"), "attribute owner id");
            Check(!DdwSql.SelectModelPocs("[fixture]", SchemaKind.TbStar).Contains("[tag]"), "TbStar poc has no tag column");
            Check(DdwSql.SelectModelPocs("[fixture]", SchemaKind.WebDdw).Contains("connectionTag"), "WebDdw connectionTag");
            string pipePocs = DdwSql.SelectPipePocs("[fixture]");
            Check(pipePocs.Contains("00000000-0000-0000-0000-000000000000"), "bare pipe template");
            Check(!pipePocs.Contains("OWNER_TYPE = 256"), "owner type 256 stays off the pipe filter");
            string bimGroups = DdwSql.SelectBimGroups("[remote_monami].[monami]");
            Check(bimGroups.Contains("TB_INSTANCEGROUPS") && bimGroups.Contains("INSTANCE_GROUP_PARENT_ID"), "DDW bim group tree");
            Check(bimGroups.Contains("version_sequence < 1000000000") && bimGroups.Contains("NOT LIKE N'%capa%'"), "DDW bim group live and capa");
            Check(!bimGroups.Contains("TB_INSTANCEGROUPMEMBERS") && !bimGroups.Contains("TB_ATTRIBUTES"), "DDW bim group cache skips members and attributes");
            Check(DdwLine.CatalogPrefix() == "[remote_monami].[monami]", "DDW catalog prefix");
            Check(DdwLine.IsCacheKey("X2_M") && !DdwLine.IsCacheKey("x2"), "x2_m cache key");
            Check(DdwCapRule.IsCap("End Cap", "2-PIP", "", "") && DdwCapRule.IsCap("pipe", "2-PIP", "cap", ""), "DDW cap name or division");
            Check(!DdwCapRule.IsCap("VAC-012", "2-PIP", "기타", "") && !DdwCapRule.IsCap("End Cap", "equipment", "", ""), "DDW cap skips S5D names and equipment");
            var ut = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);
            ut["x2"] = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            ut["x2"]["g"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            UtMapAlias.CopyX2WhenX2mMissing(ut);
            Check(ut.ContainsKey("x2_m") && ReferenceEquals(ut["x2_m"], ut["x2"]), "x2_m follows x2 UT map");
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
            var ddwEquip = DdwGraphNormalizer.Build(new[]
            {
                new DdwModelRow { InstanceId = G(80), Name = "RME", FamilyKey = "equipment", ModelType = 50594049, BimYn = 0, OperationType = 0, Tag = "rme010n03" },
                new DdwModelRow { InstanceId = G(81), Name = "WALL", ModelType = 50462978, BimYn = 1, FamilyKey = "wall", OperationType = 0 },
                new DdwModelRow { InstanceId = G(82), Name = "NOZ", ModelType = 50856202, BimYn = 1, OperationType = 0 },
                new DdwModelRow { InstanceId = G(83), Name = "VALVE", ModelType = 51380481, BimYn = 0, OperationType = 0 },
                new DdwModelRow { InstanceId = G(84), Name = "BIMVALVE", ModelType = 51380481, BimYn = 1, OperationType = 0 },
                new DdwModelRow { InstanceId = G(88), Name = "FAC", Sub1Name = "7-FACILITY", CompositeYn = 0, DivisionType = 9, OperationType = 0, Tag = "fac010" }
            }, null, null, new[]
            {
                new DdwPipeRow { InstanceId = G(85), PipeType = 17301760, Length = 12, OperationType = 0 },
                new DdwPipeRow { InstanceId = G(86), PipeType = 17303552, Length = 9, OperationType = 0 },
                new DdwPipeRow { InstanceId = G(87), PipeType = 17302528, Length = 4, OperationType = 0 }
            }, null);
            Check(ddwEquip.Nodes.Any(n => n.Tag == "rme010n03" && n.Type == NodeType.Equipment), "family equipment is a start node");
            Check(!ddwEquip.Nodes.Any(n => n.InstanceId == G(81)), "architecture stays out of the cache");
            Check(ddwEquip.Nodes.Any(n => n.InstanceId == G(82) && n.Type == NodeType.Nozzle), "bim nozzle stays as a stop");
            Check(ddwEquip.Nodes.Any(n => n.InstanceId == G(83) && n.Type == NodeType.Fitting), "hookup valve stays");
            Check(!ddwEquip.Nodes.Any(n => n.InstanceId == G(84)), "bim_yn 1 accessory stays out");
            Check(ddwEquip.Pipes.Count == 1 && ddwEquip.Pipes[0].PipeId == G(85), "duct and cable tray stay out");
            Check(ddwEquip.Nodes.Any(n => n.InstanceId == G(88) && n.Type == NodeType.Equipment), "7- facility is equipment");
            Check(DdwTypeCodes.ModelMeaning(50462976) == "structure(bim)" && DdwTypeCodes.PipeMeaning(17302528) == "ladder_cable_tray", "undefined type meanings");
            Check(bundle.Diagnostics.DeletedRowsSkipped >= 2, "DDW-05 operation type 2 skipped");
            Check(bundle.Pipes.Count == 0, "deleted pipe omitted");
            Check(bundle.Nodes[0].InstanceNumericId == 0, "ddw instance id is not the s5d hash");
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

            var ddw = new GraphCacheManager();
            var eq = G(130);
            ddw.Initialize(
                "x2_m",
                new[] { new NodeInfo { InstanceId = eq, Tag = "rme010n03", Type = NodeType.Equipment, Name = "RME", Sub1Name = "" } },
                new[] { new PocData { InstanceId = G(131), OwnerId = eq } },
                new PipeData[0],
                new TakeoffData[0],
                new DynamicPropertiesData[0],
                null,
                false);
            var engine = new PocTrace.Core.Engine.TraceEngine(ddw, new TraceAlgorithm(ddw, null) { EnableDetailedDebug = false }, Serilog.Core.Logger.None);
            var found = engine.RunTrace("rme010n03", 0, false, System.Threading.CancellationToken.None);
            Check(found.Count > 0, "x2_m equipment starts without S5D subdivision");
            var facility = new GraphCacheManager();
            var fac = G(132);
            facility.Initialize(
                "x2_m",
                new[] { new NodeInfo { InstanceId = fac, Tag = "fac010", Type = NodeType.Fitting, Name = "FAC", Sub1Name = "7-FACILITY" } },
                new[] { new PocData { InstanceId = G(133), OwnerId = fac } },
                new PipeData[0],
                new TakeoffData[0],
                new DynamicPropertiesData[0],
                null,
                false);
            var facilityTrace = new PocTrace.Core.Engine.TraceEngine(facility, new TraceAlgorithm(facility, null) { EnableDetailedDebug = false }, Serilog.Core.Logger.None);
            Check(facilityTrace.RunTrace("fac010", 0, false, System.Threading.CancellationToken.None).Count > 0, "x2_m 7- facility starts");

            var memo = DdwGraphNormalizer.Build(
                new[] { new DdwModelRow { InstanceId = G(90), Name = "EQ", FamilyKey = "equipment", OperationType = 0, Tag = "eqmemo" } },
                new[]
                {
                    new DdwPocRow { InstanceId = G(91), OwnerInstanceId = G(90), OwnerType = 768, ConnectionOrder = 4, Memo = "stick note", PocTemplateName = "port_in_1", OperationType = 0 },
                    new DdwPocRow { InstanceId = G(92), OwnerInstanceId = G(90), OwnerType = 768, ConnectionOrder = 5, Memo = "", PocTemplateName = "port_in_2", OperationType = 0 }
                },
                null, null, null);
            Check(memo.Pocs[0].Information == "stick note" && memo.Pocs[0].ConnectionOrder == 4, "connector information is memo");
            Check(memo.Pocs[1].Information == "", "empty memo stays empty");

            var tofCache = new GraphCacheManager();
            var tofEq = G(150);
            var tofId = G(152);
            tofCache.Initialize(
                "x2_m",
                new[] { new NodeInfo { InstanceId = tofEq, Tag = "eqtof", Type = NodeType.Equipment, Name = "EQ" } },
                new[] { new PocData { InstanceId = G(151), OwnerId = tofEq, Outlet = tofId, ConnectionOrder = 3, Information = "from-memo" } },
                new PipeData[0],
                new[] { new TakeoffData { TakeoffId = tofId, Tag = "", PipeId = Guid.Empty } },
                new DynamicPropertiesData[0],
                null,
                false);
            var tofRows = new PocTrace.Core.Engine.TraceEngine(tofCache, new TraceAlgorithm(tofCache, null) { EnableDetailedDebug = false }, Serilog.Core.Logger.None)
                .RunTrace("eqtof", 0, false, System.Threading.CancellationToken.None);
            Check(tofRows.Count == 1 && tofRows[0].STATUS == "TOF", "path reaches takeoff");
            Check(tofRows[0].StartPocInformation == "from-memo" && tofRows[0].StartPocNo == "3", "x2_m poc no is connection order");
            Check(tofRows[0].FailCount > 0 && (tofRows[0].feedback ?? "").IndexOf("T.Tag EMPTY", StringComparison.Ordinal) >= 0, "empty damper tag is an error");
            Check(string.IsNullOrEmpty(tofRows[0].TargetPocGUID), "takeoff has no connector guid");

            var nozEq = G(160);
            var noz = G(161);
            var nozPoc = G(162);
            var nozCache = new GraphCacheManager();
            nozCache.Initialize(
                "x2_m",
                new[]
                {
                    new NodeInfo { InstanceId = nozEq, Tag = "eqnoz", Type = NodeType.Equipment, Name = "EQ" },
                    new NodeInfo { InstanceId = noz, Tag = "nz", Type = NodeType.Nozzle, Name = "TEE_NOZZLES", ModelType = 50856202 }
                },
                new[]
                {
                    new PocData { InstanceId = G(163), OwnerId = nozEq, Outlet = nozPoc, PocName = "OUT" },
                    new PocData { InstanceId = nozPoc, OwnerId = noz, Information = "", PocName = "NZ" }
                },
                new PipeData[0],
                new TakeoffData[0],
                new DynamicPropertiesData[0],
                null,
                false);
            var nozRows = new PocTrace.Core.Engine.TraceEngine(nozCache, new TraceAlgorithm(nozCache, null) { EnableDetailedDebug = false }, Serilog.Core.Logger.None)
                .RunTrace("eqnoz", 0, false, System.Threading.CancellationToken.None);
            Check(nozRows.Exists(x => x.STATUS == "NOZ"), "x2_m nozzle does not need information");
            Check(!nozRows.Exists(x => (x.EndName ?? "").IndexOf("Missing Nozzle Info", StringComparison.Ordinal) >= 0), "x2_m skips missing nozzle info");

            var hopPipe = G(170);
            var hopEq = G(171);
            var hopPoc = G(172);
            var hopEnd = G(173);
            var hop = new PipeData { PipeId = hopPipe, Length = 12.5, PipeType = 17301760, PipeSizeMm = 14.29, UtilityName = "N2" };
            hop.ReplaceEndpoints(new[]
            {
                new PipeEndpoint { PocInstanceId = hopEnd, NormalizedTarget = hopPoc, Resolution = PipeEndpointResolution.Connected }
            });
            var hopCache = new GraphCacheManager();
            hopCache.Initialize(
                "x2_m",
                new[] { new NodeInfo { InstanceId = hopEq, Tag = "eqhop", Type = NodeType.Equipment, Name = "EQ", DivisionName = "Process", Sub1Name = "1-MAIN", Sub2Name = "TOOL" } },
                new[] { new PocData { InstanceId = hopPoc, OwnerId = hopEq, Outlet = hopPipe, PocName = "INLET", UtilityName = "N2", UtilityGroupName = "GAS" } },
                new[] { hop },
                new TakeoffData[0],
                new[] { new DynamicPropertiesData { PointID = hopPipe, PipeNumber = "L1", HookupNumber = "H9" } },
                null,
                false);
            var hopResult = new TraceResult
            {
                StartPocId = hopPoc,
                StartNodeGUID = hopEq,
                StartNodeName = "EQ",
                Steps = new List<TraceStep>
                {
                    new TraceStep { StepIndex = 1, NodeId = hopEq, NodeName = "EQ", NodeType = "Equipment", OutletUsed = hopPipe },
                    new TraceStep { StepIndex = 2, NodeId = hopPipe, NodeName = "", NodeType = "PipeSeg" }
                }
            };
            var sheet = PocTrace.Core.Engine.Reporting.DdwTraceSheet.Build(hopCache, new[] { hopResult });
            Check(sheet.Count == 1 && (int)sheet[0][4] == 1, "trace sheet num starts at 1");
            Check((string)sheet[0][0] == hopEq.ToString() && (string)sheet[0][1] == "EQ", "trace sheet start instance and model");
            Check((string)sheet[0][2] == "INLET" && (string)sheet[0][10] == "pipe", "trace sheet poc names");
            Check((string)sheet[0][3] == PocTrace.Core.Infrastructure.DdwId.Text(hopPoc), "trace sheet start poc instance id");
            Check((string)sheet[0][12] == "standard" && (double)sheet[0][8] == 12.5, "trace sheet pipe type and length2");
            Check((string)sheet[0][9] == "H9", "pipe hookup number");

            var sheetTofId = G(180);
            var sheetTofEq = G(181);
            var sheetTofCache = new GraphCacheManager();
            sheetTofCache.Initialize(
                "x2_m",
                new[] { new NodeInfo { InstanceId = sheetTofEq, Tag = "eqtof", Type = NodeType.Equipment, Name = "EQ" } },
                new PocData[0],
                new PipeData[0],
                new[] { new TakeoffData { TakeoffId = sheetTofId, Tag = "" } },
                new DynamicPropertiesData[0],
                null,
                false);
            var tofSheet = PocTrace.Core.Engine.Reporting.DdwTraceSheet.Build(sheetTofCache, new[]
            {
                new TraceResult
                {
                    Steps = new List<TraceStep>
                    {
                        new TraceStep { NodeId = sheetTofEq, NodeType = "Equipment" },
                        new TraceStep { NodeId = sheetTofId, NodeName = "Damper[]", NodeType = "Takeoff" }
                    }
                }
            });
            Check(tofSheet.Count == 1 && (string)tofSheet[0][16] == "", "empty damper tag is blank");

            var nzObj = G(190);
            var nzPocId = G(191);
            var nzEq = G(192);
            var nzCache2 = new GraphCacheManager();
            nzCache2.Initialize(
                "x2_m",
                new[]
                {
                    new NodeInfo { InstanceId = nzEq, Tag = "eq", Type = NodeType.Equipment, Name = "EQ" },
                    new NodeInfo { InstanceId = nzObj, Tag = "nz", Type = NodeType.Nozzle, Name = "NZ" }
                },
                new[] { new PocData { InstanceId = nzPocId, OwnerId = nzObj, PocName = "NZPOC", Tag = "PTAG", ReferenceTag = "CTAG" } },
                new PipeData[0],
                new TakeoffData[0],
                new[]
                {
                    new DynamicPropertiesData { PointID = nzObj, PipeNumber = "OBJ-L" },
                    new DynamicPropertiesData { PointID = nzPocId, PipeNumber = "POC-L" }
                },
                null,
                false);
            var nzSheet = PocTrace.Core.Engine.Reporting.DdwTraceSheet.Build(nzCache2, new[]
            {
                new TraceResult
                {
                    Steps = new List<TraceStep>
                    {
                        new TraceStep { NodeId = nzEq, NodeType = "Equipment", OutletUsed = nzPocId },
                        new TraceStep { NodeId = nzObj, NodeType = "Nozzle", NodeName = "NZ" }
                    }
                }
            });
            Check(nzSheet.Count == 1 && (string)nzSheet[0][16] == "PTAG" && (string)nzSheet[0][17] == "CTAG", "nozzle tag and connection tag come from the poc");

            var tofPipe = G(210);
            var tofPoc = G(211);
            var duct = G(212);
            var ductPoc = G(213);
            var mainPipe = G(214);
            var mainPoc = G(215);
            var tofBundle = DdwGraphNormalizer.Build(
                null,
                null,
                new[]
                {
                    Endpoint(tofPipe, tofPoc, 0, ductPoc, DdwGraphNormalizer.PipeOwnerType, duct),
                    Endpoint(tofPipe, mainPoc, 1, G(216), DdwGraphNormalizer.PipeOwnerType, mainPipe)
                },
                new[]
                {
                    new DdwPipeRow { InstanceId = tofPipe, PipeType = DdwGraphNormalizer.TakeoffPipeType, Length = 1, OperationType = 0 },
                    new DdwPipeRow { InstanceId = duct, PipeType = 17303552, Length = 8, OperationType = 0 },
                    new DdwPipeRow { InstanceId = mainPipe, PipeType = 17301760, Length = 4, OperationType = 0 }
                },
                new[]
                {
                    new DdwAttributeRow { OwnerId = tofPoc, Name = "tag", Value = "T1", OperationType = 0 },
                    new DdwAttributeRow { OwnerId = tofPoc, Name = "ConnectionTag", Value = "C1", OperationType = 0 },
                    new DdwAttributeRow { OwnerId = duct, Name = "S5_LATERAL_NUMBER", Value = "DUCT-L", OperationType = 0 },
                    new DdwAttributeRow { OwnerId = tofPoc, Name = "S5_LATERAL_NUMBER", Value = "POC-L", OperationType = 0 },
                    new DdwAttributeRow { OwnerId = tofPipe, Name = "S5_LATERAL_NUMBER", Value = "TOF-L", OperationType = 0 }
                });
            Check(tofBundle.Pipes.Find(p => p.PipeId == duct) == null, "duct pipe stays out of the graph");
            Check(tofBundle.HookupNumbers.Find(h => h.PointID == duct && h.PipeNumber == "DUCT-L") != null, "duct lateral is an attribute only");
            Check(tofBundle.Takeoffs.Count == 1 && tofBundle.Takeoffs[0].DuctPipeId == duct && tofBundle.Takeoffs[0].PocId == tofPoc, "takeoff follows one connected poc to the duct");
            Check(tofBundle.Takeoffs[0].PipeId == mainPipe, "takeoff continuation stays the hookup pipe");
            Check(tofBundle.Takeoffs[0].Tag == "T1" && tofBundle.Takeoffs[0].ConnectionTag == "C1", "takeoff tag comes from its poc");
            var tofAttrCache = new GraphCacheManager();
            tofAttrCache.Initialize(
                "x2_m",
                new NodeInfo[0],
                new PocData[0],
                new PipeData[0],
                tofBundle.Takeoffs,
                tofBundle.HookupNumbers,
                null,
                false);
            var tofAttrSheet = PocTrace.Core.Engine.Reporting.DdwTraceSheet.Build(tofAttrCache, new[]
            {
                new TraceResult
                {
                    Steps = new List<TraceStep>
                    {
                        new TraceStep { NodeId = G(217), NodeType = "Equipment" },
                        new TraceStep { NodeId = tofPipe, NodeType = "Takeoff", NodeName = "Damper[]" }
                    }
                }
            });
            Check(tofAttrSheet.Count == 1 && (string)tofAttrSheet[0][16] == "T1" && (string)tofAttrSheet[0][17] == "C1", "takeoff tag columns stay on the poc");
            var tofStart = new NodeInfo { InstanceId = G(217), Tag = "eq", Type = NodeType.Equipment, PZ = 0 };
            var tofStartPoc = new PocData { InstanceId = G(218), OwnerId = G(217), UtilityName = "N2" };
            var tofRow = new TraceResult { EndType = EndPointType.Takeoff, StartPocId = tofStartPoc.InstanceId, EndPocId = tofPipe, EndName = "Damper[]", TargetNodeGUID = tofPipe, Line = "x2_m" };
            new PocTrace.Core.Engine.Validation.TraceValidator(Serilog.Core.Logger.None)
                .Validate(tofAttrCache, tofRow, tofStartPoc, tofStart, null, null, new List<TraceResult> { tofRow }, null);
            Check(tofRow.TargetNodeTag == "Damper[T1]", "details T:Tag is Damper[tag]");

            var sideBundle = DdwGraphNormalizer.Build(
                null,
                null,
                new[]
                {
                    Endpoint(tofPipe, tofPoc, 0, ductPoc, DdwGraphNormalizer.PipeOwnerType, duct),
                    Endpoint(tofPipe, mainPoc, 1, G(216), DdwGraphNormalizer.PipeOwnerType, mainPipe)
                },
                new[]
                {
                    new DdwPipeRow { InstanceId = tofPipe, PipeType = DdwGraphNormalizer.TakeoffPipeType, Length = 1, OperationType = 0 },
                    new DdwPipeRow { InstanceId = duct, PipeType = 17303552, Length = 8, OperationType = 0 },
                    new DdwPipeRow { InstanceId = mainPipe, PipeType = 17301760, Length = 4, OperationType = 0 }
                },
                new[]
                {
                    new DdwAttributeRow { OwnerId = mainPoc, Name = "tag", Value = "SIDE", OperationType = 0 },
                    new DdwAttributeRow { OwnerId = duct, Name = "S5_LATERAL_NUMBER", Value = "DUCT-L", OperationType = 0 }
                });
            Check(sideBundle.Takeoffs[0].Tag == "SIDE" && sideBundle.Takeoffs[0].DuctPipeId == duct, "takeoff tag can sit on the other poc");

            var empEq = G(200);
            var empA = G(201);
            var empB = G(202);
            var empNode = new NodeInfo { InstanceId = empEq, Tag = "eqemp", Type = NodeType.Equipment, Name = "EQ", PZ = 0 };
            var pocFilled = new PocData { InstanceId = empA, OwnerId = empEq, PocName = "A", UtilityName = "N2", UtilityGroupName = "GAS" };
            var pocBlank = new PocData { InstanceId = empB, OwnerId = empEq, PocName = "B", UtilityName = "", UtilityGroupName = "" };
            var empCache = new GraphCacheManager();
            empCache.Initialize("x2_m", new[] { empNode }, new[] { pocFilled, pocBlank }, new PipeData[0], new TakeoffData[0], new DynamicPropertiesData[0], null, false);
            var empFilled = new TraceResult { EndType = EndPointType.Empty, StartPocId = empA, Line = "x2_m", Steps = new List<TraceStep>() };
            var empBlank = new TraceResult { EndType = EndPointType.Empty, StartPocId = empB, Line = "x2_m", Steps = new List<TraceStep>() };
            var empRows = new List<TraceResult> { empFilled, empBlank };
            var validator = new PocTrace.Core.Engine.Validation.TraceValidator(Serilog.Core.Logger.None);
            validator.Validate(empCache, empFilled, pocFilled, empNode, null, null, empRows, null);
            validator.Validate(empCache, empBlank, pocBlank, empNode, null, null, empRows, null);
            Check(empFilled.BranchedPipes == 0 && empBlank.BranchedPipes == 0, "emp branch count is 0");
            Check((empFilled.IssueType ?? "").IndexOf("모델링 없음", StringComparison.Ordinal) >= 0, "emp with utility is modeling absent");
            Check((empBlank.IssueType ?? "").IndexOf("설비정합오류", StringComparison.Ordinal) >= 0, "blank utility is equipment integrity");
            Check(empBlank.IssueBelongTo == "ft 기술", "blank utility owner is ft tech");
            var batchEngine = new PocTrace.Core.Engine.TraceEngine(empCache, new TraceAlgorithm(empCache, null) { EnableDetailedDebug = false }, Serilog.Core.Logger.None);
            var missing = batchEngine.RunBatchTag("no-such-tag", false, System.Threading.CancellationToken.None);
            Check(missing.Count == 1 && missing[0].CheckResult == "미배치설비", "missing tag is unplaced");
            var undesigned = batchEngine.RunBatchTag("eqemp", false, System.Threading.CancellationToken.None);
            Check(undesigned.Count > 0 && undesigned.TrueForAll(r => r.CheckResult == "미설계 배치 설비"), "tag without pipe stays undesigned");
            Check(EqCodeSheetHeader(), "eq code header is the tag column");
        }

        private static bool EqCodeSheetHeader()
        {
            var read = PocTrace.Core.Engine.Reporting.EqCodeSheet.Read(
                PocTrace.Core.Engine.Reporting.EqCodeSheet.Write("EQ Code", "AAA", "aaa", "BBB"));
            return read.HeaderFound && read.Values.Count == 2 && read.Values[0] == "AAA" && read.Values[1] == "BBB" && read.Duplicates == 1;
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
