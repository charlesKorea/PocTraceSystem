using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using PocTrace.DdwTraceBench.Data;

namespace PocTrace.DdwTraceBench.Trace
{
    /// <summary>
    /// 설비 Tag 의 모든 POC 에서 connected_poc_id 를 따라 배관과 컴포넌트로 퍼져 나간다.
    /// 규칙: 지나온 POC 로는 되돌아가지 않는다. poc_template_id 가 0 인 POC 의 주인은 배관(tb_pipeinstances)이다.
    /// 다른 설비 또는 modeltemplates.bim_yn = 1 객체를 만나면 이름을 기록하고 그 가지를 멈춘다.
    /// 모든 조회는 version_sequence &lt; 1000000000 이다(IDdwData 가 보장한다).
    /// </summary>
    public sealed class DdwTracer
    {
        private const string TModels = "tb_modelinstances";
        private const string TPipes = "tb_pipeinstances";

        private readonly IDdwData _data;
        private readonly Action<string> _log;

        private readonly Dictionary<string, PocRow> _pocsById = new Dictionary<string, PocRow>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _pocsAbsent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<PocRow>> _pocsByOwner = new Dictionary<string, List<PocRow>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ModelRow> _models = new Dictionary<string, ModelRow>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _modelsAbsent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TemplateRow> _templates = new Dictionary<string, TemplateRow>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _templatesAbsent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PipeRow> _pipes = new Dictionary<string, PipeRow>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _pipesAbsent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PipeSizeRow> _sizes = new Dictionary<string, PipeSizeRow>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _sizesAbsent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PocTemplateRow> _pocTemplates = new Dictionary<string, PocTemplateRow>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _pocTemplatesAbsent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private sealed class Source
        {
            public PocRow Poc;
            public int Depth;
            public string OwnerId;
            public string OwnerTable;
        }

        public DdwTracer(IDdwData data, Action<string> log)
        {
            _data = data;
            _log = log ?? (s => { });
        }

        public TraceResult Run(TraceRunOptions options, CancellationToken ct)
        {
            var result = new TraceResult { Options = options, Started = DateTime.Now };
            string tag = (options.Tag ?? string.Empty).Trim();
            if (tag.Length == 0)
                throw new ArgumentException("Tag가 비어 있습니다.");

            _log("Tag 속성 조회: " + tag);
            var owners = _data.FindTagOwners(tag);
            if (owners.Count == 0)
            {
                result.Notes.Add("tb_attributes에서 tag 속성 행을 찾지 못했다(삭제 행 제외).");
                Finish(result);
                return result;
            }

            var ownerIds = owners.Select(o => o.OwnerId).Where(i => !Hex.IsEmpty(i)).Distinct().ToList();
            Ensure(_models, _modelsAbsent, ownerIds, _data.GetModels);
            Ensure(_templates, _templatesAbsent, _models.Values.Select(m => m.TemplateId), _data.GetTemplates);

            foreach (var owner in owners)
            {
                var info = new TagOwnerInfo { Owner = owner };
                ModelRow model;
                if (owner.OwnerId != null && _models.TryGetValue(owner.OwnerId, out model))
                {
                    info.Model = model;
                    TemplateRow tpl;
                    if (model.TemplateId != null && _templates.TryGetValue(model.TemplateId, out tpl))
                        info.Template = tpl;
                    Judge(options, model, info.Template, out info.ByModelType, out info.ByFamilyKey, out info.ByBimYnZero);
                    info.IsEquipment = info.ByModelType || info.ByFamilyKey || info.ByBimYnZero;
                    if (info.Template == null)
                        info.Note = "modeltemplates 행 없음";
                }
                else
                {
                    info.Note = "tb_modelinstances에 없음(모델이 아닌 객체의 tag이거나 삭제됨)";
                }
                result.TagOwners.Add(info);
            }

            foreach (var info in result.TagOwners)
            {
                ct.ThrowIfCancellationRequested();
                if (info.Model == null)
                    continue;
                if (!info.IsEquipment && !options.ForceStartWhenNotEquipment)
                {
                    info.Note = Join(info.Note, "설비 판정 조건을 하나도 만족하지 않아 추적하지 않았다");
                    continue;
                }
                if (result.TagOwners.Any(o => o != info && o.Traced && string.Equals(o.Owner.OwnerId, info.Owner.OwnerId, StringComparison.OrdinalIgnoreCase)))
                {
                    info.Note = Join(info.Note, "같은 owner의 tag 행이 이미 추적됨");
                    continue;
                }
                info.Traced = true;
                TraceEquipment(options, result, info, ct);
            }

            Finish(result);
            return result;
        }

        private void Finish(TraceResult result)
        {
            Ensure(_pocTemplates, _pocTemplatesAbsent, _pocsById.Values.Select(p => p.PocTemplateId), _data.GetPocTemplates);
            result.Finished = DateTime.Now;
            foreach (var kv in _pocsById)
                result.Pocs[kv.Key] = kv.Value;
            foreach (var kv in _pocTemplates)
                result.PocTemplates[kv.Key] = kv.Value;
            foreach (var w in _data.Warnings.Distinct())
                result.Notes.Add(w);
        }

        private static void Judge(TraceRunOptions o, ModelRow model, TemplateRow tpl, out bool byModelType, out bool byFamilyKey, out bool byBimYnZero)
        {
            byModelType = o.EquipByModelType && model != null && model.ModelType.HasValue && o.EquipmentModelTypes.Contains(model.ModelType.Value);
            byFamilyKey = o.EquipByFamilyKey && tpl != null && !string.IsNullOrEmpty(tpl.FamilyKey)
                && string.Equals(tpl.FamilyKey.Trim(), o.EquipmentFamilyKey, StringComparison.OrdinalIgnoreCase);
            byBimYnZero = o.EquipByBimYnZero && tpl != null && tpl.BimYn.HasValue && tpl.BimYn.Value == 0;
        }

        private void TraceEquipment(TraceRunOptions o, TraceResult result, TagOwnerInfo start, CancellationToken ct)
        {
            string eqId = start.Owner.OwnerId;
            _log("설비 추적 시작: " + Hex.Short(eqId));

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var startPocIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var expandedOwners = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { eqId };

            result.Objects.Add(new ObjectRecord
            {
                StartEquipmentId = eqId,
                Depth = 0,
                Id = eqId,
                Table = TModels,
                Role = "시작 설비",
                ModelType = start.Model.ModelType,
                TemplateId = start.Model.TemplateId,
                DisplayName = start.Template == null ? null : start.Template.DisplayName,
                FamilyKey = start.Template == null ? null : start.Template.FamilyKey,
                BimYn = start.Template == null ? (long?)null : start.Template.BimYn,
                ByModelType = start.ByModelType,
                ByFamilyKey = start.ByFamilyKey,
                ByBimYnZero = start.ByBimYnZero
            });

            var startPocs = PocsOf(new[] { eqId })[eqId];
            var startObj = result.Objects[result.Objects.Count - 1];
            startObj.PocCount = startPocs.Count;
            if (startPocs.Count == 0)
            {
                start.Note = Join(start.Note, "설비 owner_instance_id로 live POC를 찾지 못했다(poc 없음)");
                return;
            }

            var frontier = new List<Source>();
            foreach (var p in startPocs)
            {
                visited.Add(p.Id);
                startPocIds.Add(p.Id);
                frontier.Add(new Source { Poc = p, Depth = 0, OwnerId = eqId, OwnerTable = TModels });
            }

            int seq = result.Hops.Count;
            int depth = 0;
            int objectCount = 0;
            while (frontier.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                if (depth > o.MaxDepth)
                {
                    result.LimitHit = true;
                    result.Notes.Add("단계 한도 " + o.MaxDepth.ToString(CultureInfo.InvariantCulture) + "에서 멈춤: 설비 " + Hex.Short(eqId));
                    break;
                }

                _log("단계 " + depth.ToString(CultureInfo.InvariantCulture) + ": 출발 POC " + frontier.Count.ToString(CultureInfo.InvariantCulture) + "개");

                var qIds = frontier.Select(s => s.Poc.ConnectedPocId).Where(i => !Hex.IsEmpty(i) && !visited.Contains(i)).Distinct().ToList();
                Ensure(_pocsById, _pocsAbsent, qIds, _data.GetPocsByIds);

                var zeroOwners = new List<string>();
                var otherOwners = new List<string>();
                foreach (var q in qIds)
                {
                    PocRow row;
                    if (!_pocsById.TryGetValue(q, out row) || Hex.IsEmpty(row.OwnerId))
                        continue;
                    if (Hex.IsEmpty(row.PocTemplateId))
                        zeroOwners.Add(row.OwnerId);
                    else
                        otherOwners.Add(row.OwnerId);
                }

                Ensure(_pipes, _pipesAbsent, zeroOwners, _data.GetPipes);
                Ensure(_models, _modelsAbsent, otherOwners, _data.GetModels);
                Ensure(_models, _modelsAbsent, zeroOwners.Where(i => !_pipes.ContainsKey(i)), _data.GetModels);
                Ensure(_pipes, _pipesAbsent, otherOwners.Where(i => !_models.ContainsKey(i)), _data.GetPipes);
                Ensure(_templates, _templatesAbsent, _models.Values.Select(m => m.TemplateId), _data.GetTemplates);
                Ensure(_sizes, _sizesAbsent, _pipes.Values.Select(p => p.PipeSizeId), _data.GetPipeSizes);

                var next = new List<Source>();
                var expandOrder = new List<Source>();
                var expandRecords = new Dictionary<string, ObjectRecord>(StringComparer.OrdinalIgnoreCase);

                foreach (var s in frontier)
                {
                    var hop = new HopRecord
                    {
                        Seq = ++seq,
                        Depth = depth,
                        StartEquipmentId = eqId,
                        FromPocId = s.Poc.Id,
                        FromPocOwnerId = s.OwnerId,
                        FromPocOwnerTable = s.OwnerTable,
                        ToPocId = s.Poc.ConnectedPocId
                    };
                    result.Hops.Add(hop);

                    string qid = s.Poc.ConnectedPocId;
                    if (Hex.IsEmpty(qid))
                    {
                        hop.Kind = "미연결";
                        hop.Action = "끝";
                        hop.Status = "connected_poc_id 없음(null 또는 0): 미연결 끝점";
                        continue;
                    }
                    if (visited.Contains(qid))
                    {
                        hop.Kind = "방문";
                        hop.Action = "건너뜀";
                        hop.Status = startPocIds.Contains(qid) ? "원 설비의 POC로 돌아옴(이미 추적 대상)" : "이미 방문한 POC";
                        continue;
                    }

                    PocRow qPoc;
                    if (!_pocsById.TryGetValue(qid, out qPoc))
                    {
                        visited.Add(qid);
                        hop.Kind = "dangling";
                        hop.Action = "끝";
                        hop.Status = "상대 POC 행 없음(삭제되었거나 tb_pocinstances에 없음)";
                        continue;
                    }

                    visited.Add(qid);
                    hop.ToPocTemplateId = qPoc.PocTemplateId;
                    hop.ToPocOwnerType = qPoc.OwnerType;
                    hop.ToPocOwnerId = qPoc.OwnerId;
                    string asym = string.Equals(qPoc.ConnectedPocId, s.Poc.Id, StringComparison.OrdinalIgnoreCase)
                        ? string.Empty
                        : " [비대칭: 상대 POC의 connected_poc_id가 출발 POC가 아님]";

                    if (Hex.IsEmpty(qPoc.OwnerId))
                    {
                        hop.Kind = "owner 없음";
                        hop.Action = "끝";
                        hop.Status = "상대 POC의 owner_instance_id 없음" + asym;
                        continue;
                    }

                    if (string.Equals(qPoc.OwnerId, eqId, StringComparison.OrdinalIgnoreCase))
                    {
                        hop.ToPocOwnerTable = TModels;
                        hop.Kind = "원 설비";
                        hop.Action = "끝";
                        hop.Status = "원 설비로 복귀" + asym;
                        continue;
                    }

                    if (expandedOwners.Contains(qPoc.OwnerId))
                    {
                        hop.ToPocOwnerTable = _pipes.ContainsKey(qPoc.OwnerId) ? TPipes : (_models.ContainsKey(qPoc.OwnerId) ? TModels : null);
                        hop.Kind = "방문";
                        hop.Action = "건너뜀";
                        hop.Status = "이미 방문한 객체의 다른 POC로 도착(루프)" + asym;
                        continue;
                    }

                    bool zero = Hex.IsEmpty(qPoc.PocTemplateId);
                    PipeRow pipe = null;
                    ModelRow model = null;
                    if (zero)
                    {
                        _pipes.TryGetValue(qPoc.OwnerId, out pipe);
                        if (pipe == null)
                            _models.TryGetValue(qPoc.OwnerId, out model);
                    }
                    else
                    {
                        _models.TryGetValue(qPoc.OwnerId, out model);
                        if (model == null)
                            _pipes.TryGetValue(qPoc.OwnerId, out pipe);
                    }

                    if (pipe != null)
                    {
                        expandedOwners.Add(pipe.Id);
                        hop.ToPocOwnerTable = TPipes;
                        hop.Kind = "배관";
                        hop.Action = "계속";
                        hop.Status = (zero ? "poc_template_id=0, tb_pipeinstances에서 찾음" : "poc_template_id가 0이 아니지만 tb_pipeinstances에서 찾음") + asym;
                        string sizeName = null;
                        PipeSizeRow sz;
                        if (pipe.PipeSizeId != null && _sizes.TryGetValue(pipe.PipeSizeId, out sz))
                            sizeName = sz.Name;
                        else if (pipe.PipeSizeId != null)
                            hop.Status += " [pipesize 행 없음]";
                        result.Pipes.Add(new PipeRecord { StartEquipmentId = eqId, Depth = depth, ViaPocId = qid, Pipe = pipe, SizeName = sizeName });
                        objectCount++;
                        var src = new Source { Depth = depth + 1, OwnerId = pipe.Id, OwnerTable = TPipes };
                        expandOrder.Add(src);
                        continue;
                    }

                    if (model != null)
                    {
                        hop.ToPocOwnerTable = TModels;
                        TemplateRow tpl = null;
                        if (model.TemplateId != null)
                            _templates.TryGetValue(model.TemplateId, out tpl);
                        bool byMt, byFk, byBim0;
                        Judge(o, model, tpl, out byMt, out byFk, out byBim0);
                        bool pass = model.ModelType.HasValue && o.PassThroughModelTypes.Contains(model.ModelType.Value);
                        bool isEquip = !pass && (byMt || byFk || byBim0);
                        bool isEnd = tpl != null && tpl.BimYn.HasValue && tpl.BimYn.Value == o.EndBimYn;

                        var rec = new ObjectRecord
                        {
                            StartEquipmentId = eqId,
                            Depth = depth,
                            Id = model.Id,
                            Table = TModels,
                            ArrivedPocId = qid,
                            ModelType = model.ModelType,
                            TemplateId = model.TemplateId,
                            DisplayName = tpl == null ? null : tpl.DisplayName,
                            FamilyKey = tpl == null ? null : tpl.FamilyKey,
                            BimYn = tpl == null ? (long?)null : tpl.BimYn,
                            ByModelType = byMt,
                            ByFamilyKey = byFk,
                            ByBimYnZero = byBim0,
                            PassThrough = pass
                        };
                        result.Objects.Add(rec);
                        expandedOwners.Add(model.Id);
                        objectCount++;

                        if (isEnd)
                        {
                            rec.Role = isEquip ? "종료(bim_yn=" + o.EndBimYn.ToString(CultureInfo.InvariantCulture) + ", 설비 판정도 만족)" : "종료(bim_yn=" + o.EndBimYn.ToString(CultureInfo.InvariantCulture) + ")";
                            hop.Kind = "종료";
                            hop.Action = "종료";
                            hop.Status = "bim_yn=" + o.EndBimYn.ToString(CultureInfo.InvariantCulture) + " 객체: " + (rec.DisplayName ?? "(이름 없음)") + asym;
                        }
                        else if (isEquip)
                        {
                            rec.Role = "종료(다른 설비)";
                            hop.Kind = "종료";
                            hop.Action = "종료";
                            hop.Status = "다른 설비: " + (rec.DisplayName ?? "(이름 없음)") + asym;
                        }
                        else
                        {
                            rec.Role = pass ? "통과(connector 등 pass-through)" : "통과";
                            hop.Kind = "컴포넌트";
                            hop.Action = "계속";
                            hop.Status = (tpl == null ? "modeltemplates 행 없음, bim_yn 판정 불가. " : string.Empty) + (rec.DisplayName ?? "") + asym;
                            var src = new Source { Depth = depth + 1, OwnerId = model.Id, OwnerTable = TModels };
                            expandOrder.Add(src);
                            expandRecords[model.Id] = rec;
                        }
                        continue;
                    }

                    expandedOwners.Add(qPoc.OwnerId);
                    hop.Kind = "owner 없음";
                    hop.Action = "끝";
                    hop.Status = "상대 POC의 owner를 tb_pipeinstances, tb_modelinstances 어디에서도 찾지 못함" + asym;
                }

                if (objectCount > o.MaxObjects)
                {
                    result.LimitHit = true;
                    result.Notes.Add("객체 한도 " + o.MaxObjects.ToString(CultureInfo.InvariantCulture) + "를 넘어 멈춤: 설비 " + Hex.Short(eqId));
                    break;
                }

                if (expandOrder.Count > 0)
                {
                    var pocsByOwner = PocsOf(expandOrder.Select(e => e.OwnerId).ToList());
                    foreach (var e in expandOrder)
                    {
                        List<PocRow> pocs = pocsByOwner[e.OwnerId];
                        ObjectRecord rec;
                        if (expandRecords.TryGetValue(e.OwnerId, out rec))
                            rec.PocCount = pocs.Count;
                        foreach (var p in pocs)
                        {
                            if (visited.Contains(p.Id))
                                continue;
                            visited.Add(p.Id);
                            next.Add(new Source { Poc = p, Depth = depth + 1, OwnerId = e.OwnerId, OwnerTable = e.OwnerTable });
                        }
                    }
                }

                frontier = next;
                depth++;
            }
        }

        private Dictionary<string, List<PocRow>> PocsOf(IList<string> ownerIds)
        {
            var need = ownerIds.Where(i => !Hex.IsEmpty(i) && !_pocsByOwner.ContainsKey(i)).Distinct().ToList();
            if (need.Count > 0)
            {
                var rows = _data.GetPocsByOwners(need);
                foreach (var id in need)
                    _pocsByOwner[id] = new List<PocRow>();
                foreach (var r in rows)
                {
                    List<PocRow> list;
                    if (r.OwnerId != null && _pocsByOwner.TryGetValue(r.OwnerId, out list))
                    {
                        list.Add(r);
                        if (!_pocsById.ContainsKey(r.Id))
                            _pocsById[r.Id] = r;
                    }
                }
                foreach (var id in need)
                {
                    _pocsByOwner[id] = _pocsByOwner[id].OrderBy(p => p.ConnectionOrder ?? long.MaxValue).ThenBy(p => p.Id, StringComparer.Ordinal).ToList();
                }
            }

            var map = new Dictionary<string, List<PocRow>>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in ownerIds)
            {
                List<PocRow> list;
                map[id] = _pocsByOwner.TryGetValue(id, out list) ? list : new List<PocRow>();
            }
            return map;
        }

        private static void Ensure<T>(IDictionary<string, T> cache, ISet<string> absent, IEnumerable<string> ids, Func<IList<string>, IDictionary<string, T>> load)
        {
            var need = ids.Where(i => !Hex.IsEmpty(i) && !cache.ContainsKey(i) && !absent.Contains(i)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (need.Count == 0)
                return;
            var got = load(need);
            foreach (var id in need)
            {
                T v;
                if (got.TryGetValue(id, out v))
                    cache[id] = v;
                else
                    absent.Add(id);
            }
        }

        private static string Join(string a, string b)
        {
            return string.IsNullOrEmpty(a) ? b : a + "; " + b;
        }
    }
}
