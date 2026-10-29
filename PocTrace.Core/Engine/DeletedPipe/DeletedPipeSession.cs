using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace PocTrace.Core.Engine.DeletedPipe
{
    internal sealed class WalkState
    {
        public WalkState(string eqpId, DeletedEquipmentRow equipment, DeletedWalkMode mode)
        {
            EqpId = eqpId;
            Equipment = equipment;
            Mode = mode;
        }

        public string EqpId { get; private set; }
        public DeletedEquipmentRow Equipment { get; private set; }
        public DeletedWalkMode Mode { get; private set; }
        public List<DeletedNodeRow> Fittings { get; private set; } = new List<DeletedNodeRow>();
        private readonly HashSet<Guid> _fittingIds = new HashSet<Guid>();
        public HashSet<Guid> Visited { get; private set; } = new HashSet<Guid>();
        public HashSet<Guid> Queued { get; private set; } = new HashSet<Guid>();
        public HashSet<Guid> Pipes { get; private set; } = new HashSet<Guid>();
        public Queue<Guid> Queue { get; private set; } = new Queue<Guid>();
        public List<DeletedPipeDetailRow> Details { get; private set; } = new List<DeletedPipeDetailRow>();
        public int Hops { get; set; }
        public bool HopLimited { get; set; }
        public DeletedPocRow StartPoc { get; set; }

        public void RememberFitting(DeletedNodeRow node)
        {
            if (Mode != DeletedWalkMode.StillPresent || !DeletedPipeClassifier.IsFitting(node))
                return;
            if (node.InstanceId == Guid.Empty || !_fittingIds.Add(node.InstanceId))
                return;
            Fittings.Add(node);
        }

        public void Enqueue(Guid id)
        {
            if (id == Guid.Empty || Visited.Contains(id) || !Queued.Add(id))
                return;
            Queue.Enqueue(id);
        }
    }

    /// <summary>
    /// 스냅샷이 있으면 메모리에서만 따라간다.
    /// 스냅샷이 없을 때만 만난 ID를 조회하고, 같은 ID는 다시 읽지 않는다.
    /// </summary>
    internal sealed class DeletedPipeSession
    {
        private readonly DeletedPipeRepository _repository;
        private readonly Dictionary<Guid, DeletedPipeRow> _pipesById = new Dictionary<Guid, DeletedPipeRow>();
        private readonly Dictionary<Guid, List<Guid>> _pipesByOutlet = new Dictionary<Guid, List<Guid>>();
        private readonly HashSet<Guid> _pipeProbes = new HashSet<Guid>();
        private readonly Dictionary<Guid, DeletedPocRow> _pocsById = new Dictionary<Guid, DeletedPocRow>();
        private readonly Dictionary<Guid, List<Guid>> _pocsByOutlet = new Dictionary<Guid, List<Guid>>();
        private readonly Dictionary<Guid, List<DeletedPocRow>> _pocsByOwner = new Dictionary<Guid, List<DeletedPocRow>>();
        private readonly HashSet<Guid> _pocProbes = new HashSet<Guid>();
        private readonly HashSet<Guid> _ownerProbes = new HashSet<Guid>();
        private readonly Dictionary<Guid, DeletedNodeRow> _nodes = new Dictionary<Guid, DeletedNodeRow>();
        private readonly HashSet<Guid> _nodeProbes = new HashSet<Guid>();
        private readonly Dictionary<Guid, DeletedTakeoffRow> _takeoffs = new Dictionary<Guid, DeletedTakeoffRow>();
        private readonly HashSet<Guid> _takeoffProbes = new HashSet<Guid>();
        private bool _snapshotReady;

        public DeletedPipeSession(DeletedPipeRepository repository)
        {
            _repository = repository;
        }

        public void RememberEquipment(IList<DeletedEquipmentRow> equipment)
        {
            if (equipment == null)
                return;
            for (int i = 0; i < equipment.Count; i++)
            {
                var node = DeletedPipeClassifier.ToNode(equipment[i]);
                if (node == null || node.InstanceId == Guid.Empty)
                    continue;
                _nodes[node.InstanceId] = node;
                _nodeProbes.Add(node.InstanceId);
            }
        }

        public void UseSnapshot(DeletedPipeSnapshot snapshot)
        {
            if (snapshot == null)
                return;
            MergePipes(snapshot.Pipes);
            MergePocs(snapshot.Pocs);
            MergeNodes(snapshot.Nodes);
            MergeTakeoffs(snapshot.Takeoffs);
            _snapshotReady = true;
        }

        public List<DeletedPocRow> QualifyingPocs(Guid ownerId, DateTime cutover, DeletedWalkMode mode)
        {
            EnsureOwners(new[] { ownerId });
            List<DeletedPocRow> owned;
            if (!_pocsByOwner.TryGetValue(ownerId, out owned))
                return new List<DeletedPocRow>();

            var list = new List<DeletedPocRow>();
            for (int i = 0; i < owned.Count; i++)
            {
                if (DeletedPipeClassifier.Accepts(mode, owned[i].OperationType, owned[i].OpTime, cutover))
                    list.Add(owned[i]);
            }
            return list;
        }

        public void WalkFromPoc(DeletedPocRow startPoc, WalkState state, DateTime cutover, CancellationToken cancellation)
        {
            if (startPoc == null || state == null)
                return;

            state.StartPoc = startPoc;
            state.Visited.Add(startPoc.InstanceId);
            var seeds = SeedIds(startPoc);
            Prefetch(seeds, cancellation);
            var pipes = PipesTouching(seeds);
            for (int i = 0; i < pipes.Count; i++)
                EnterPipe(pipes[i], startPoc.InstanceId, state, cutover);

            Drain(state, cutover, cancellation);
        }

        private void Drain(WalkState state, DateTime cutover, CancellationToken cancellation)
        {
            while (state.Queue.Count > 0)
            {
                cancellation.ThrowIfCancellationRequested();
                if (state.Hops >= DeletedPipeClassifier.MaxHops)
                {
                    state.HopLimited = true;
                    return;
                }

                var wave = new List<Guid>();
                while (state.Queue.Count > 0 && wave.Count < 50)
                {
                    Guid id = state.Queue.Dequeue();
                    state.Queued.Remove(id);
                    if (id == Guid.Empty || !state.Visited.Add(id))
                        continue;
                    state.Hops++;
                    wave.Add(id);
                    if (state.Hops >= DeletedPipeClassifier.MaxHops)
                    {
                        state.HopLimited = true;
                        break;
                    }
                }

                if (wave.Count == 0)
                    continue;

                Prefetch(wave, cancellation);
                for (int i = 0; i < wave.Count; i++)
                    HandleFrontier(wave[i], state, cutover, cancellation);
            }
        }

        private void HandleFrontier(Guid id, WalkState state, DateTime cutover, CancellationToken cancellation)
        {
            bool matched = false;

            bool holdPipes = false;
            DeletedTakeoffRow takeoff;
            if (_takeoffs.TryGetValue(id, out takeoff))
            {
                matched = true;
                if (DeletedPipeClassifier.ContinuesPastTakeoff(state.StartPoc))
                    HandleTakeoff(takeoff, id, state, cutover, cancellation);
                else
                    holdPipes = true;
            }

            var pocs = PocsTouching(new[] { id });
            if (pocs.Count > 0)
            {
                matched = true;
                for (int i = 0; i < pocs.Count; i++)
                    HandlePoc(pocs[i], state, cutover, cancellation);
            }

            var pipes = PipesTouching(new[] { id });
            if (!holdPipes && pipes.Count > 0)
            {
                matched = true;
                for (int i = 0; i < pipes.Count; i++)
                    EnterPipe(pipes[i], id, state, cutover);
            }

            if (matched)
                return;

            DeletedNodeRow node;
            if (_nodes.TryGetValue(id, out node))
                HandleNode(node, state, cutover, cancellation);
        }

        private void HandleTakeoff(
            DeletedTakeoffRow takeoff,
            Guid entryId,
            WalkState state,
            DateTime cutover,
            CancellationToken cancellation)
        {
            if (takeoff == null)
                return;
            if (state.Mode == DeletedWalkMode.StillPresent)
            {
                if (takeoff.OperationType >= 2)
                    return;
            }
            else if (takeoff.OperationType != 2)
            {
                return;
            }

            var ids = new List<Guid>();
            if (takeoff.PipeId.HasValue && takeoff.PipeId.Value != Guid.Empty)
                ids.Add(takeoff.PipeId.Value);
            if (takeoff.TakeoffId != Guid.Empty)
                ids.Add(takeoff.TakeoffId);
            Prefetch(ids, cancellation);
            var pipes = PipesTouching(ids);
            for (int i = 0; i < pipes.Count; i++)
                EnterPipe(pipes[i], entryId, state, cutover);
        }

        private void HandlePoc(DeletedPocRow poc, WalkState state, DateTime cutover, CancellationToken cancellation)
        {
            if (poc == null)
                return;
            if (!DeletedPipeClassifier.Accepts(state.Mode, poc.OperationType, poc.OpTime, cutover))
                return;
            if (poc.OwnerId == state.Equipment.InstanceId)
                return;

            EnsureNodes(new[] { poc.OwnerId }, cancellation);
            DeletedNodeRow owner;
            if (!_nodes.TryGetValue(poc.OwnerId, out owner))
                return;
            if (DeletedPipeClassifier.IsEquipment(owner))
                return;
            if (!DeletedPipeClassifier.Accepts(state.Mode, owner.OperationType, owner.OpTime, cutover))
                return;
            if (DeletedPipeClassifier.IsNozzleEnd(owner, poc))
                return;
            state.RememberFitting(owner);
            if (DeletedPipeClassifier.IsCapEnd(owner))
                return;

            EnsureOwners(new[] { owner.InstanceId });
            List<DeletedPocRow> siblings;
            if (!_pocsByOwner.TryGetValue(owner.InstanceId, out siblings))
                return;

            for (int i = 0; i < siblings.Count; i++)
            {
                var sibling = siblings[i];
                if (sibling.InstanceId == poc.InstanceId)
                    continue;
                if (!DeletedPipeClassifier.Accepts(state.Mode, sibling.OperationType, sibling.OpTime, cutover))
                    continue;

                var seeds = SeedIds(sibling);
                Prefetch(seeds, cancellation);
                var pipes = PipesTouching(seeds);
                for (int p = 0; p < pipes.Count; p++)
                    EnterPipe(pipes[p], sibling.InstanceId, state, cutover);
                if (sibling.Outlet.HasValue)
                    state.Enqueue(sibling.Outlet.Value);
            }
        }

        private void HandleNode(DeletedNodeRow node, WalkState state, DateTime cutover, CancellationToken cancellation)
        {
            if (node == null || node.InstanceId == state.Equipment.InstanceId)
                return;
            if (DeletedPipeClassifier.IsEquipment(node))
                return;
            if (!DeletedPipeClassifier.Accepts(state.Mode, node.OperationType, node.OpTime, cutover))
                return;
            if (DeletedPipeClassifier.IsNozzleEnd(node, null))
                return;
            state.RememberFitting(node);
            if (DeletedPipeClassifier.IsCapEnd(node))
                return;

            EnsureOwners(new[] { node.InstanceId });
            List<DeletedPocRow> pocs;
            if (!_pocsByOwner.TryGetValue(node.InstanceId, out pocs))
                return;

            for (int i = 0; i < pocs.Count; i++)
            {
                if (!DeletedPipeClassifier.Accepts(state.Mode, pocs[i].OperationType, pocs[i].OpTime, cutover))
                    continue;
                var seeds = SeedIds(pocs[i]);
                Prefetch(seeds, cancellation);
                var pipes = PipesTouching(seeds);
                for (int p = 0; p < pipes.Count; p++)
                    EnterPipe(pipes[p], pocs[i].InstanceId, state, cutover);
                if (pocs[i].Outlet.HasValue)
                    state.Enqueue(pocs[i].Outlet.Value);
            }
        }

        private void EnterPipe(DeletedPipeRow pipe, Guid entryId, WalkState state, DateTime cutover)
        {
            if (pipe == null)
                return;
            if (!DeletedPipeClassifier.Accepts(state.Mode, pipe.OperationType, pipe.OpTime, cutover))
                return;
            if (!state.Pipes.Add(pipe.PipeId))
                return;

            var detail = new DeletedPipeDetailRow
            {
                DbName = null,
                EqpId = state.EqpId,
                EquipmentInstanceId = state.Equipment.InstanceId,
                EquipmentInstanceName = state.Equipment.Name ?? string.Empty,
                EquipmentTag = state.Equipment.Tag,
                EquipmentDeletedAt = state.Equipment.OpTime,
                StartPocId = state.StartPoc == null ? Guid.Empty : state.StartPoc.InstanceId,
                PipeId = pipe.PipeId,
                UtilityName = pipe.UtilityName ?? string.Empty,
                LengthMeters = pipe.Length <= 0 ? 0 : pipe.Length,
                LengthMillimeters = DeletedPipeClassifier.Millimeters(pipe.Length),
                PipeDeletedAt = pipe.OpTime,
                Order = state.Details.Count + 1,
                UserName = pipe.UserName ?? string.Empty,
                CompanyName = pipe.CompanyName ?? string.Empty
            };
            state.Details.Add(detail);

            foreach (var outlet in Outlets(pipe))
            {
                if (outlet == Guid.Empty || outlet == entryId)
                    continue;
                state.Enqueue(outlet);
            }
        }

        private void Prefetch(IList<Guid> ids, CancellationToken cancellation)
        {
            if (_snapshotReady)
                return;
            cancellation.ThrowIfCancellationRequested();
            var clean = Clean(ids);
            var pipeMissing = Missing(clean, _pipeProbes);
            if (pipeMissing.Count > 0)
                MergePipes(_repository.LoadPipesTouching(pipeMissing));

            var pocMissing = Missing(clean, _pocProbes);
            if (pocMissing.Count > 0)
                MergePocs(_repository.LoadPocsTouching(pocMissing));

            var takeoffMissing = Missing(clean, _takeoffProbes);
            if (takeoffMissing.Count > 0)
                MergeTakeoffs(_repository.LoadTakeoffs(takeoffMissing));

            var nodeMissing = Missing(clean, _nodeProbes);
            if (nodeMissing.Count > 0)
                MergeNodes(_repository.LoadNodes(nodeMissing));

            var owners = new List<Guid>();
            for (int i = 0; i < clean.Count; i++)
            {
                var pocs = PocsTouching(new[] { clean[i] });
                for (int p = 0; p < pocs.Count; p++)
                {
                    if (pocs[p].OwnerId != Guid.Empty && !_nodeProbes.Contains(pocs[p].OwnerId))
                        owners.Add(pocs[p].OwnerId);
                }
            }
            EnsureNodes(owners, cancellation);
        }

        private void EnsureNodes(IList<Guid> ids, CancellationToken cancellation)
        {
            if (_snapshotReady)
                return;
            cancellation.ThrowIfCancellationRequested();
            var missing = Missing(Clean(ids), _nodeProbes);
            if (missing.Count > 0)
                MergeNodes(_repository.LoadNodes(missing));
        }

        private void EnsureOwners(IList<Guid> ownerIds)
        {
            if (_snapshotReady)
                return;
            var missing = Missing(Clean(ownerIds), _ownerProbes);
            if (missing.Count == 0)
                return;
            MergePocs(_repository.LoadPocsByOwner(missing));
        }

        private List<DeletedPipeRow> PipesTouching(IList<Guid> ids)
        {
            var result = new List<DeletedPipeRow>();
            var seen = new HashSet<Guid>();
            var clean = Clean(ids);
            for (int i = 0; i < clean.Count; i++)
            {
                Guid id = clean[i];
                DeletedPipeRow byId;
                if (_pipesById.TryGetValue(id, out byId) && seen.Add(byId.PipeId))
                    result.Add(byId);

                List<Guid> linked;
                if (!_pipesByOutlet.TryGetValue(id, out linked))
                    continue;
                for (int p = 0; p < linked.Count; p++)
                {
                    DeletedPipeRow pipe;
                    if (_pipesById.TryGetValue(linked[p], out pipe) && seen.Add(pipe.PipeId))
                        result.Add(pipe);
                }
            }
            return result;
        }

        private List<DeletedPocRow> PocsTouching(IList<Guid> ids)
        {
            var result = new List<DeletedPocRow>();
            var seen = new HashSet<Guid>();
            var clean = Clean(ids);
            for (int i = 0; i < clean.Count; i++)
            {
                Guid id = clean[i];
                DeletedPocRow byId;
                if (_pocsById.TryGetValue(id, out byId) && seen.Add(byId.InstanceId))
                    result.Add(byId);

                List<Guid> linked;
                if (!_pocsByOutlet.TryGetValue(id, out linked))
                    continue;
                for (int p = 0; p < linked.Count; p++)
                {
                    DeletedPocRow poc;
                    if (_pocsById.TryGetValue(linked[p], out poc) && seen.Add(poc.InstanceId))
                        result.Add(poc);
                }
            }
            return result;
        }

        private void MergePipes(IList<DeletedPipeRow> rows)
        {
            if (rows == null)
                return;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || row.PipeId == Guid.Empty)
                    continue;
                _pipesById[row.PipeId] = row;
                Index(_pipesByOutlet, row.PipeId, row.PipeId);
                Index(_pipesByOutlet, row.Outlet1, row.PipeId);
                Index(_pipesByOutlet, row.Outlet2, row.PipeId);
                Index(_pipesByOutlet, row.Outlet3, row.PipeId);
            }
        }

        private void MergePocs(IList<DeletedPocRow> rows)
        {
            if (rows == null)
                return;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || row.InstanceId == Guid.Empty)
                    continue;
                _pocsById[row.InstanceId] = row;
                _pocProbes.Add(row.InstanceId);
                Index(_pocsByOutlet, row.InstanceId, row.InstanceId);
                Index(_pocsByOutlet, row.Outlet, row.InstanceId);

                List<DeletedPocRow> owned;
                if (!_pocsByOwner.TryGetValue(row.OwnerId, out owned))
                {
                    owned = new List<DeletedPocRow>();
                    _pocsByOwner.Add(row.OwnerId, owned);
                }
                bool replaced = false;
                for (int j = 0; j < owned.Count; j++)
                {
                    if (owned[j].InstanceId == row.InstanceId)
                    {
                        owned[j] = row;
                        replaced = true;
                        break;
                    }
                }
                if (!replaced)
                    owned.Add(row);
            }
        }

        private void MergeNodes(IList<DeletedNodeRow> rows)
        {
            if (rows == null)
                return;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] == null || rows[i].InstanceId == Guid.Empty)
                    continue;
                _nodes[rows[i].InstanceId] = rows[i];
                _nodeProbes.Add(rows[i].InstanceId);
            }
        }

        private void MergeTakeoffs(IList<DeletedTakeoffRow> rows)
        {
            if (rows == null)
                return;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] == null || rows[i].TakeoffId == Guid.Empty)
                    continue;
                _takeoffs[rows[i].TakeoffId] = rows[i];
                _takeoffProbes.Add(rows[i].TakeoffId);
            }
        }

        private static void Index(Dictionary<Guid, List<Guid>> index, Guid? key, Guid value)
        {
            if (!key.HasValue || key.Value == Guid.Empty)
                return;
            Index(index, key.Value, value);
        }

        private static void Index(Dictionary<Guid, List<Guid>> index, Guid key, Guid value)
        {
            if (key == Guid.Empty)
                return;
            List<Guid> list;
            if (!index.TryGetValue(key, out list))
            {
                list = new List<Guid>();
                index.Add(key, list);
            }
            if (!list.Contains(value))
                list.Add(value);
        }

        private static List<Guid> SeedIds(DeletedPocRow poc)
        {
            var list = new List<Guid>();
            if (poc == null)
                return list;
            if (poc.InstanceId != Guid.Empty)
                list.Add(poc.InstanceId);
            if (poc.Outlet.HasValue && poc.Outlet.Value != Guid.Empty)
                list.Add(poc.Outlet.Value);
            return list;
        }

        private static IEnumerable<Guid> Outlets(DeletedPipeRow pipe)
        {
            if (pipe.Outlet1.HasValue)
                yield return pipe.Outlet1.Value;
            if (pipe.Outlet2.HasValue)
                yield return pipe.Outlet2.Value;
            if (pipe.Outlet3.HasValue)
                yield return pipe.Outlet3.Value;
        }

        private static List<Guid> Missing(IList<Guid> ids, HashSet<Guid> probed)
        {
            var missing = new List<Guid>();
            for (int i = 0; i < ids.Count; i++)
            {
                if (probed.Add(ids[i]))
                    missing.Add(ids[i]);
            }
            return missing;
        }

        private static List<Guid> Clean(IList<Guid> ids)
        {
            var list = new List<Guid>();
            if (ids == null)
                return list;
            var seen = new HashSet<Guid>();
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] != Guid.Empty && seen.Add(ids[i]))
                    list.Add(ids[i]);
            }
            return list;
        }
    }
}
