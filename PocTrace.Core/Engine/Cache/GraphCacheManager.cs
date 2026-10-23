using System;
using System.Collections.Generic;
using System.Linq;
using PocTrace.Core.Engine.ReverseTrace;
using PocTrace.Core.Infrastructure.Diagnostics;
using PocTrace.Core.Models;
using PocTrace.Core.Enums;
using Serilog;

namespace PocTrace.Core.Engine.Cache
{
    public class GraphCacheManager : IGraphCache
    {
        // [필드 선언]
        private Dictionary<Guid, NodeInfo> _nodes;
        private Dictionary<string, NodeInfo> _nodesByTag;
        private Dictionary<string, List<Guid>> _nodeInstanceIdsByTag;
        private Dictionary<string, NodeInfo> _nodesByBimId;
        private Dictionary<long, NodeInfo> _nodesByInstanceNumericId;
        private ReverseGraphIndex _reverseGraphIndex;

        private Dictionary<Guid, PocData> _pocs;
        private Dictionary<Guid, List<PocData>> _pocsByOwner; // OwnerID 검색용 인덱스

        private Dictionary<Guid, PipeData> _pipes;
        private Dictionary<Guid, TakeoffData> _takeoffs;
        private Dictionary<Guid, Guid> _outletToPipeId;
        private Dictionary<Guid, DynamicPropertiesData> _dynamicProperties;

        private static readonly Dictionary<string, Dictionary<string, Dictionary<string, string>>> EmptyUtilityMaps =
            new Dictionary<string, Dictionary<string, Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);

        public bool IsInitialized { get; private set; }

        /// <summary>같은 끝점 대상에 먼저 등록된 배관만 남긴 뒤 무시한 횟수.</summary>
        public int OutletIndexCollisions { get; private set; }

        // 멤버 변수
        public string DbName { get; private set; } = ""; // 초기값       

        // [Fix] 인터페이스 멤버 구현 (public getter 필수)
        // [Type Update]
        public Dictionary<string, Dictionary<string, Dictionary<string, string>>> UtilityMaps { get; private set; }
            = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);

        // [수정] Initialize 메서드 (누락된 로직 복원 완료)
        public void Initialize(
			string dbName,
            IEnumerable<NodeInfo> nodes,
            IEnumerable<PocData> pocs,
            IEnumerable<PipeData> pipes,
            IEnumerable<TakeoffData> takeoffs,
            IEnumerable<DynamicPropertiesData> dynamicProperties,
            Dictionary<string, Dictionary<string, Dictionary<string, string>>> utMaps,
            bool buildReverseGraphIndex = true,
            IProgress<string> progress = null)
        {
            ReleaseMemory();
            try
            {
            Mark(progress, "Initialize.Start", "Initializing Graph...");
            Log.Information("🔵 [Cache] Initialize Started.");

            var nodeList = Materialize(nodes);
            var pocList = Materialize(pocs);
            var pipeList = Materialize(pipes);
            var takeoffList = Materialize(takeoffs);
            var dynList = Materialize(dynamicProperties);

            int nodeCapacity = Math.Max(16, nodeList.Count);
            int pocCapacity = Math.Max(16, pocList.Count);
            int pipeCapacity = Math.Max(16, pipeList.Count);
            int takeoffCapacity = Math.Max(16, takeoffList.Count);
            int dynCapacity = Math.Max(16, dynList.Count);

            Mark(progress, "Initialize.Materialized", "Initializing Graph: sizing indexes",
                "db=" + (dbName ?? "")
                + " nodes=" + nodeList.Count
                + " pocs=" + pocList.Count
                + " pipes=" + pipeList.Count
                + " takeoffs=" + takeoffList.Count
                + " dyn=" + dynList.Count
                + " reverse=" + buildReverseGraphIndex);

            // 연속된 큰 배열을 잡기 전에 LOH를 한 번 압축한다.
            MemoryLoadLog.CompactForLargeAlloc("Initialize.BeforeDictionaries");

            // 1. Dictionaries 초기화 (capacity hint → Resize/OOM 완화)
            Mark(progress, "Initialize.Dict.Nodes", "Initializing Graph: nodes", "cap=" + nodeCapacity);
            _nodes = new Dictionary<Guid, NodeInfo>(nodeCapacity);
            Mark(progress, "Initialize.Dict.NodesByTag", "Initializing Graph: nodes", "cap=" + nodeCapacity);
            _nodesByTag = new Dictionary<string, NodeInfo>(nodeCapacity, StringComparer.OrdinalIgnoreCase);
            Mark(progress, "Initialize.Dict.NodeIdsByTag", "Initializing Graph: nodes", "cap=" + nodeCapacity);
            _nodeInstanceIdsByTag = new Dictionary<string, List<Guid>>(nodeCapacity, StringComparer.OrdinalIgnoreCase);
            Mark(progress, "Initialize.Dict.NodesByBimId", "Initializing Graph: nodes", "cap=" + nodeCapacity);
            _nodesByBimId = new Dictionary<string, NodeInfo>(nodeCapacity, StringComparer.OrdinalIgnoreCase);
            Mark(progress, "Initialize.Dict.NodesByNumericId", "Initializing Graph: nodes", "cap=" + nodeCapacity);
            _nodesByInstanceNumericId = new Dictionary<long, NodeInfo>(nodeCapacity);

            this.DbName = dbName;
            this.UtilityMaps = utMaps ?? new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();

            int duplicateCount = 0;
            int damperUpdateCount = 0;

            // 2. Nodes 로딩 및 인덱싱 (Damper 우선순위 로직 포함)
            foreach (var node in nodeList)
            {
                // [Logic A] 신규 노드 추가
                if (!_nodes.ContainsKey(node.InstanceId))
                {
                    _nodes.Add(node.InstanceId, node);

                    // [복구] Tag 인덱스 추가
                    if (!string.IsNullOrEmpty(node.Tag))
                    {
                        if (!_nodesByTag.ContainsKey(node.Tag))
                            _nodesByTag.Add(node.Tag, node);
                        IndexNodeTag(node.Tag, node.InstanceId);
                    }

                    IndexNodeLookupKeys(node);
                }
                // [Logic B] 중복 발생 시 (Damper 우선순위 처리)
                else
                {
                    var existing = _nodes[node.InstanceId];

                    // [중요 수정] 방어 로직 강화
                    // 1. 기존 것이 Damper라면, 절대로 덮어쓰지 않는다. (Nozzle이 와도 무시)
                    if (existing.Type == NodeType.Damper)
                    {
                        // Damper 유지. 아무것도 하지 않음.
                        continue;
                    }

                    // 2. 기존 것이 Damper가 아니고, 새 것이 Damper라면 -> 덮어쓴다 (승격)
                    if (existing.Type != NodeType.Damper && node.Type == NodeType.Damper)
                    {
                        // 메인 저장소 업데이트
                        _nodes[node.InstanceId] = node;

                        // [복구] Tag 인덱스도 업데이트
                        if (!string.IsNullOrEmpty(node.Tag))
                        {
                            _nodesByTag[node.Tag] = node;
                            IndexNodeTag(node.Tag, node.InstanceId);
                        }

                        IndexNodeLookupKeys(node);

                        damperUpdateCount++;
                        Log.Debug("   -> [Update] Replaced Node '{Tag}' with Damper Type.", node.Tag);
                    }
                    duplicateCount++;
                }
            }
            Log.Information("   -> Nodes Loaded: {Count:N0} (Updates: {Upd}, Duplicates Ignored: {Dup})",
                _nodes.Count, damperUpdateCount, duplicateCount);

            // 3. POCs 초기화 — 단일 패스 (ToDictionary+GroupBy 이중 적재 방지)
            try
            {
                Mark(progress, "Initialize.Dict.Pocs", "Initializing Graph: pocs", "cap=" + pocCapacity);
                _pocs = new Dictionary<Guid, PocData>(pocCapacity);
                _pocsByOwner = new Dictionary<Guid, List<PocData>>(Math.Max(16, pocCapacity / 4));
                foreach (var poc in pocList)
                {
                    _pocs[poc.InstanceId] = poc;
                    List<PocData> ownerList;
                    if (!_pocsByOwner.TryGetValue(poc.OwnerId, out ownerList))
                    {
                        ownerList = new List<PocData>();
                        _pocsByOwner[poc.OwnerId] = ownerList;
                    }
                    ownerList.Add(poc);
                }

                Log.Information("   -> POCs Loaded: {Count:N0}. PocGroups(Owners): {Groups:N0}", _pocs.Count, _pocsByOwner.Count);
            }
            catch (Exception ex)
            {
                MemoryLoadLog.Fail("Initialize.Pocs", ex);
                Log.Error(ex, "🔥 Error building POC Dictionary. Check for duplicate POC InstanceIDs!");
                throw;
            }

            // 4. Pipes 초기화 — capacity hint + outlet 인덱스 단일 패스
            Mark(progress, "Initialize.Dict.Pipes", "Initializing Graph: pipes", "cap=" + pipeCapacity);
            _pipes = new Dictionary<Guid, PipeData>(pipeCapacity);
            foreach (var pipe in pipeList)
                _pipes[pipe.PipeId] = pipe;

            int outletCollisions;
            Mark(progress, "Initialize.OutletIndex", "Initializing Graph: pipe outlets", "pipes=" + pipeList.Count);
            _outletToPipeId = BuildOutletToPipeIndex(pipeList, out outletCollisions);
            OutletIndexCollisions = outletCollisions;
            Log.Information(
                "   -> Pipes Loaded: {Count:N0}, OutletIndex: {Outlets:N0}, OutletCollisions: {Collisions:N0}",
                _pipes.Count, _outletToPipeId.Count, outletCollisions);

            // 5. Takeoffs 초기화
            Mark(progress, "Initialize.Dict.Takeoffs", "Initializing Graph: takeoffs", "cap=" + takeoffCapacity);
            _takeoffs = new Dictionary<Guid, TakeoffData>(takeoffCapacity);
            foreach (var takeoff in takeoffList)
                _takeoffs[takeoff.TakeoffId] = takeoff;
            Log.Information("   -> Takeoffs Loaded: {Count:N0}", _takeoffs.Count);

            // 6. dynamicProperties 초기화 (중복 PointID 처리)
            Mark(progress, "Initialize.Dict.DynProps", "Initializing Graph: dynamic properties", "cap=" + dynCapacity);
            _dynamicProperties = new Dictionary<Guid, DynamicPropertiesData>(dynCapacity);
            foreach (var dp in dynList)
            {
                if (!_dynamicProperties.ContainsKey(dp.PointID))
                    _dynamicProperties[dp.PointID] = dp;
            }
            Log.Information("   -> Dynamic Properties Loaded: {Count:N0}", _dynamicProperties.Count);

            if (buildReverseGraphIndex)
            {
                Mark(progress, "Initialize.ReverseIndex", "Initializing Graph: reverse index");
                _reverseGraphIndex = ReverseGraphIndex.Build(this);
                Log.Information("   -> ReverseGraphIndex built targets={Targets}", _reverseGraphIndex.TargetCount);
                MemoryLoadLog.Stage("Initialize.ReverseIndex.Done", "targets=" + _reverseGraphIndex.TargetCount);
            }
            else
            {
                _reverseGraphIndex = null;
                Log.Information("   -> ReverseGraphIndex skipped (buildReverseGraphIndex=false)");
                MemoryLoadLog.Stage("Initialize.ReverseIndex.Skipped");
            }

            IsInitialized = true;
            MemoryLoadLog.Stage("Initialize.Complete", "db=" + (dbName ?? ""));
            Log.Information("✅ [Cache] Initialization Complete.");
            }
            catch (Exception ex)
            {
                // 실패한 큰 배열을 먼저 놓고 압축한 뒤 예외를 남긴다.
                // 그렇지 않으면 대화상자와 로그 한 줄조차 할당하지 못한다.
                ReleaseMemory();
                try { MemoryLoadLog.CompactForLargeAlloc("Initialize.Failed"); }
                catch { }
                MemoryLoadLog.Fail("Initialize", ex);
                try { Log.Error(ex, "[Cache] Initialize failed."); }
                catch { /* Serilog 할당이 OOM이면 디스크 단계 로그만 남긴다. */ }
                throw;
            }
        }

        private static void Mark(IProgress<string> progress, string stage, string label, string detail = null)
        {
            MemoryLoadLog.Stage(stage, detail);
            try { Log.Information("[Cache] {Stage} {Detail}", stage, detail ?? string.Empty); }
            catch { }
            try { progress?.Report(label); }
            catch { }
        }

        /// <summary>DB 라인 처리 후 Admin이 호출 — x2 로드 전 x1 메모리 해제.</summary>
        public void ReleaseMemory()
        {
            _nodes = null;
            _nodesByTag = null;
            _nodeInstanceIdsByTag = null;
            _nodesByBimId = null;
            _nodesByInstanceNumericId = null;
            _pocs = null;
            _pocsByOwner = null;
            _pipes = null;
            _takeoffs = null;
            _outletToPipeId = null;
            _dynamicProperties = null;
            _reverseGraphIndex = null;
            UtilityMaps = EmptyUtilityMaps;
            IsInitialized = false;
            DbName = string.Empty;
        }

        private static List<T> Materialize<T>(IEnumerable<T> source)
        {
            if (source == null)
                return new List<T>();
            var list = source as List<T>;
            return list ?? source.ToList();
        }
        // Thread-Safe Read Operations (Dictionary 읽기는 Thread-Safe함)
        public bool TryGetNode(Guid instanceId, out NodeInfo node) => _nodes.TryGetValue(instanceId, out node);

        // [인터페이스 구현: 태그로 노드 찾기]
        public bool TryGetNodeByTag(string tag, out NodeInfo node)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                node = null;
                return false;
            }
            // 인덱스에서 조회 (O(1))
            return _nodesByTag.TryGetValue(tag, out node);
        }

        public bool TryGetPoc(Guid instanceId, out PocData poc) => _pocs.TryGetValue(instanceId, out poc);

        public bool TryGetPipe(Guid pipeId, out PipeData pipe) => _pipes.TryGetValue(pipeId, out pipe);

        public IEnumerable<PocData> GetPocsByOwner(Guid ownerId)
        {
            //return _pocsByOwner.Contains(ownerId) ? _pocsByOwner[ownerId] : Enumerable.Empty<PocData>();
            if (_pocsByOwner.TryGetValue(ownerId, out var list))
            {
                return list;
            }
            return Enumerable.Empty<PocData>();
        }

        public NodeType GetNodeType(Guid instanceId)
        {
            if (_nodes.TryGetValue(instanceId, out var node))
                return node.Type;
            return NodeType.Unknown; // Default fallback
        }

        // [인터페이스 구현 추가]
        public string GetNodeName(Guid nodeId)
        {
            if (_nodes.TryGetValue(nodeId, out var node))
            {
                // NodeInfo가 null일 경우 대비
                return node?.Name ?? "Unknown";
            }
            return "Unknown";
        }

        public string GetNodeTag(Guid nodeId)
        {
            if (_nodes.TryGetValue(nodeId, out var node))
            {
                return node?.Tag ?? "Unknown";
            }
            return "Unknown";
        }

        // [Fix] GetPipe 구현
        public PipeData GetPipe(Guid pipeId)
        {
            _pipes.TryGetValue(pipeId, out var pipe);
            return pipe;
        }

        public bool TryGetTakeoff(Guid takeoffId, out TakeoffData takeoff)
        {
            return _takeoffs.TryGetValue(takeoffId, out takeoff);
        }

        public bool TryFindPipeIdByOutlet(Guid outletId, out Guid pipeId)
        {
            if (_outletToPipeId != null && _outletToPipeId.TryGetValue(outletId, out pipeId))
                return true;

            pipeId = Guid.Empty;
            return false;
        }

        private static Dictionary<Guid, Guid> BuildOutletToPipeIndex(IList<PipeData> pipes, out int collisions)
        {
            collisions = 0;
            if (pipes == null || pipes.Count == 0)
                return new Dictionary<Guid, Guid>(16);

            int slots = 0;
            for (int i = 0; i < pipes.Count; i++)
            {
                var pipe = pipes[i];
                if (pipe != null && pipe.Endpoints != null)
                    slots += pipe.Endpoints.Count;
            }
            int capacity = slots < 16 ? 16 : slots;
            var index = new Dictionary<Guid, Guid>(capacity);

            for (int i = 0; i < pipes.Count; i++)
            {
                var pipe = pipes[i];
                if (pipe == null) continue;
                foreach (var target in PipeEndpointTraversal.ConnectedTargets(pipe))
                    RegisterOutlet(index, target, pipe.PipeId, ref collisions);
            }

            return index;
        }

        private static void RegisterOutlet(Dictionary<Guid, Guid> index, Guid outlet, Guid pipeId, ref int collisions)
        {
            if (outlet == Guid.Empty)
                return;

            if (!index.ContainsKey(outlet))
                index.Add(outlet, pipeId);
            else
                collisions++;
        }

        // [신규 추가] FindStartPocs 구현
        public IEnumerable<PocData> FindStartPocs(string equipTag, bool includePartialMatch = false)
        {
            if (string.IsNullOrWhiteSpace(equipTag))
                return Enumerable.Empty<PocData>();

            string tag = equipTag.Trim();
            var nodeIds = new HashSet<Guid>();

            // 1) 정확 일치 (기존 동작 보존)
            if (_nodeInstanceIdsByTag.TryGetValue(tag, out var exactIds))
            {
                foreach (var id in exactIds)
                    nodeIds.Add(id);
            }

            // 2) [VOC] 부분 매칭(Viewer 전용): 설비 Tag를 ','로 분리한 토큰 중 하나가 입력 tag와
            //    정확히 일치하는 설비도 대상에 포함. 예) 입력 "dama01" → 스크러버 "dama01,dama02"도 포함.
            //    (Admin 스케줄러는 중복 검수 방지를 위해 includePartialMatch=false로 미적용)
            if (includePartialMatch)
            {
                foreach (var kv in _nodeInstanceIdsByTag)
                {
                    if (kv.Key.IndexOf(',') < 0)
                        continue; // 단일 태그는 (1)에서 이미 처리됨

                    bool tokenMatched = kv.Key
                        .Split(',')
                        .Any(t => string.Equals(t.Trim(), tag, StringComparison.OrdinalIgnoreCase));

                    if (tokenMatched)
                    {
                        foreach (var id in kv.Value)
                            nodeIds.Add(id);
                    }
                }
            }

            var result = new List<PocData>();
            foreach (var nodeId in nodeIds)
            {
                if (_pocsByOwner.TryGetValue(nodeId, out var ownerPocs))
                    result.AddRange(ownerPocs);
            }

            return result;
        }

        private void IndexNodeTag(string tag, Guid instanceId)
        {
            if (!_nodeInstanceIdsByTag.TryGetValue(tag, out var ids))
            {
                ids = new List<Guid>();
                _nodeInstanceIdsByTag[tag] = ids;
            }

            if (!ids.Contains(instanceId))
                ids.Add(instanceId);
        }

        private void IndexNodeLookupKeys(NodeInfo node)
        {
            if (node == null) return;

            if (!string.IsNullOrWhiteSpace(node.BimId))
            {
                string bimKey = node.BimId.Trim();
                if (!_nodesByBimId.ContainsKey(bimKey))
                    _nodesByBimId[bimKey] = node;
            }

            if (node.InstanceNumericId != 0)
            {
                if (!_nodesByInstanceNumericId.ContainsKey(node.InstanceNumericId))
                    _nodesByInstanceNumericId[node.InstanceNumericId] = node;
            }
        }

        public bool TryGetNodeByBimId(string bimId, out NodeInfo node)
        {
            node = null;
            if (string.IsNullOrWhiteSpace(bimId))
                return false;
            return _nodesByBimId.TryGetValue(bimId.Trim(), out node);
        }

        public bool TryGetNodeByInstanceNumericId(long instanceNumericId, out NodeInfo node)
        {
            node = null;
            if (instanceNumericId == 0)
                return false;
            return _nodesByInstanceNumericId.TryGetValue(instanceNumericId, out node);
        }

        public ReverseGraphIndex GetReverseGraphIndex()
        {
            if (_reverseGraphIndex == null && IsInitialized)
            {
                MemoryLoadLog.Stage("ReverseGraphIndex.Build");
                try
                {
                    _reverseGraphIndex = ReverseGraphIndex.Build(this);
                }
                catch (Exception ex)
                {
                    try { MemoryLoadLog.CompactForLargeAlloc("ReverseGraphIndex.Failed"); }
                    catch { }
                    MemoryLoadLog.Fail("ReverseGraphIndex.Build", ex);
                    throw;
                }

                MemoryLoadLog.Stage("ReverseGraphIndex.Done", "targets=" + _reverseGraphIndex.TargetCount);
                try { Log.Information("[Cache] ReverseGraphIndex built (lazy) targets={Targets}", _reverseGraphIndex.TargetCount); }
                catch { }
            }
            return _reverseGraphIndex;
        }

        /// <summary>
        /// 읽기 클라이언트용. Initialize(..., buildReverseGraphIndex: false) 이후,
        /// 호출자가 원본 리스트 참조를 놓은 다음 호출한다. 캐시 파일 형식은 바꾸지 않는다.
        /// </summary>
        public void BuildReverseIndexAfterInputsReleased(IProgress<string> progress = null)
        {
            if (!IsInitialized)
                return;

            MemoryLoadLog.CompactForLargeAlloc("Graph.BeforeReverseIndex");
            try { progress?.Report("Initializing Graph: reverse index"); }
            catch { }
            GetReverseGraphIndex();
        }

        // [New] 구현
        public NodeInfo GetNode(Guid nodeId)
        {
            _nodes.TryGetValue(nodeId, out var node);
            return node;
        }

        public IEnumerable<PocData> EnumeratePocs()
        {
            if (_pocs == null || _pocs.Count == 0)
                return Enumerable.Empty<PocData>();
            return _pocs.Values;
        }

        public IEnumerable<TakeoffData> EnumerateTakeoffs()
        {
            if (_takeoffs == null || _takeoffs.Count == 0)
                return Enumerable.Empty<TakeoffData>();
            return _takeoffs.Values;
        }

        public IEnumerable<PipeData> EnumeratePipes()
        {
            if (_pipes == null || _pipes.Count == 0)
                return Enumerable.Empty<PipeData>();
            return _pipes.Values;
        }

        public IEnumerable<NodeInfo> EnumerateNodes()
        {
            if (_nodes == null || _nodes.Count == 0)
                return Enumerable.Empty<NodeInfo>();
            return _nodes.Values;
        }

        public PocData GetPoc(Guid pocId)
        {
            _pocs.TryGetValue(pocId, out var poc);
            return poc;
        }

        public string GetSubdivision1(Guid nodeId)
        {
            // _nodes 딕셔너리에서 nodeId로 검색 후 Subdivision1 반환
            if (_nodes != null && _nodes.TryGetValue(nodeId, out var node))
            {
                return node.Sub1Name;
            }
            return null; // 못 찾으면 null 반환
        }
        public string GetSubdivision2(Guid nodeId)
        {
            // _nodes 딕셔너리에서 nodeId로 검색 후 Subdivision2 반환
            if (_nodes != null && _nodes.TryGetValue(nodeId, out var node))
            {
                return node.Sub2Name;
            }
            return null; // 못 찾으면 null 반환
        }
        public string GetQCode(Guid nodeId)
        {
            // _nodes 딕셔너리에서 nodeId로 검색 후 Subdivision2 반환
            if (_nodes != null && _nodes.TryGetValue(nodeId, out var node))
            {
                return node.QCode;
            }
            return null; // 못 찾으면 null 반환
        }
        public string GetDivision(Guid nodeId)
        {
            var ret = string.Empty;
            // _nodes 딕셔너리에서 nodeId로 검색 후 Subdivision2 반환
            if (_nodes != null && _nodes.TryGetValue(nodeId, out var node))
            {
                switch (node.Division) 
                {
                    case 0: ret = "Equipment"; break;
                    case 1: ret = "PlumbingComponent"; break; //Nozzle
                    case 2: ret = "MEP"; break; //Others
                    case 3: ret = "MEP"; break; //Fitting
                    case 4: ret = "MEP"; break; //Damper
                }
            }
            return ret; // 못 찾으면 null 반환
        }
                public string GetHookupNumber(Guid id)
        {
            var ret = string.Empty;
            // _nodes 딕셔너리에서 nodeId로 검색 후 Subdivision2 반환
            if (_dynamicProperties != null && _dynamicProperties.TryGetValue(id, out var huNumber))
            {
                return huNumber.HookupNumber; // 못 찾으면 null 반환
            }
            return ""; // 못 찾으면 null 반환
        }

        // [신규] DynamicPropertiesData 조회 (PocNo=PocID, ConnectorUse=POC InstanceId)
        public DynamicPropertiesData GetDynamicProperties(Guid ownerId)
        {
            if (_dynamicProperties != null && _dynamicProperties.TryGetValue(ownerId, out var dp))
            {
                return dp;
            }
            return null;
        }
    }
}