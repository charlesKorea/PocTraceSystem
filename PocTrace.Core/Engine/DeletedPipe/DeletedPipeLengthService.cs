using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;

namespace PocTrace.Core.Engine.DeletedPipe
{
    /// <summary>
    /// EQPID 목록의 철거 설비에서 배관 길이를 합산한다.
    /// OperationType 2(기준일 이후)는 삭제된 객체만 따라간다.
    /// OperationType 1이면서 건설코드에 Delete가 있으면 5D에 남은 설비로 보고,
    /// OperationType 2 미만인 배관 길이와 피팅(이름·QCode) 수량을 낸다.
    /// 다른 설비, 노즐, 캡에서 멈춘다. 노즐 너머 1차 훅업 배관은 넣지 않는다.
    /// 피팅 인스턴스 목록은 쓰지 않는다.
    /// </summary>
    public sealed class DeletedPipeLengthService
    {
        public DeletedPipeReport Run(
            string connectionString,
            string dbName,
            IReadOnlyList<string> eqpIds,
            DateTime cutover,
            IProgress<string> progress,
            CancellationToken cancellation)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("SourceDb 연결 문자열이 없습니다.");
            if (string.IsNullOrWhiteSpace(dbName))
                throw new ArgumentException("dbName is required", nameof(dbName));

            var report = new DeletedPipeReport
            {
                DbName = dbName,
                Cutover = cutover.Date
            };

            var requested = DistinctEqpIds(eqpIds);
            if (requested.Count == 0)
            {
                report.Warnings.Add("EQPID가 없습니다.");
                return report;
            }

            using (var connection = new SqlConnection(connectionString))
            {
                progress?.Report(dbName + " 연결");
                connection.Open();
                var repo = new DeletedPipeRepository(connection, dbName, report.Cutover, cancellation);
                var session = new DeletedPipeSession(repo);
                UseSnapshot(dbName, report.Cutover, repo, session, progress);

                progress?.Report(dbName + " 삭제 설비 조회");
                var equipment = repo.LoadDeletedEquipment();
                var pending = repo.LoadPendingDeleteEquipment();
                session.RememberEquipment(equipment);
                session.RememberEquipment(pending);
                progress?.Report(dbName + " 완전삭제 " + equipment.Count.ToString("N0")
                    + "건, 건설코드 Delete " + pending.Count.ToString("N0") + "건");

                var allPipes = new Dictionary<Guid, DeletedPipeDetailRow>();
                var allEquipment = new HashSet<Guid>();
                var allPocs = new HashSet<Guid>();

                for (int i = 0; i < requested.Count; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    string eqpId = requested[i];
                    progress?.Report(string.Format(
                        "{0} EQPID {1}/{2} {3}",
                        dbName, i + 1, requested.Count, eqpId));

                    var matched = new List<DeletedEquipmentRow>();
                    bool tagHit = false;
                    CollectMatches(equipment, eqpId, matched, ref tagHit);
                    int deletedCount = matched.Count;
                    CollectMatches(pending, eqpId, matched, ref tagHit);

                    if (matched.Count == 0)
                    {
                        report.Unmatched.Add(new DeletedPipeUnmatchedRow
                        {
                            DbName = dbName,
                            EqpId = eqpId,
                            Reason = tagHit
                                ? "Tag는 있으나 설비 Subdivision이 아닙니다."
                                : "기준일 이후 삭제 설비도, 건설코드 Delete 설비도 없습니다."
                        });
                        report.Summaries.Add(new DeletedPipeSummaryRow
                        {
                            DbName = dbName,
                            EqpId = eqpId,
                            Note = tagHit ? "설비 Subdivision 아님" : "삭제 설비 없음"
                        });
                        continue;
                    }

                    var summary = new DeletedPipeSummaryRow
                    {
                        DbName = dbName,
                        EqpId = eqpId,
                        EquipmentCount = matched.Count,
                        Kind = KindLabel(deletedCount, matched.Count - deletedCount)
                    };
                    var pipesOnEqp = new Dictionary<Guid, DeletedPipeDetailRow>();
                    var fittingsOnEqp = new Dictionary<Guid, DeletedNodeRow>();
                    int pocCount = 0;

                    for (int m = 0; m < matched.Count; m++)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        var equip = matched[m];
                        allEquipment.Add(equip.InstanceId);
                        var mode = DeletedPipeClassifier.IsPendingDelete(equip.OperationType, equip.ConstructionCode)
                            ? DeletedWalkMode.StillPresent
                            : DeletedWalkMode.DeletedSinceCutover;
                        var state = new WalkState(eqpId, equip, mode);
                        var pocs = session.QualifyingPocs(equip.InstanceId, report.Cutover, mode);
                        pocCount += pocs.Count;
                        for (int p = 0; p < pocs.Count; p++)
                        {
                            allPocs.Add(pocs[p].InstanceId);
                            session.WalkFromPoc(pocs[p], state, report.Cutover, cancellation);
                        }

                        if (state.HopLimited)
                        {
                            summary.Note = "탐색 상한 도달";
                            report.Warnings.Add(eqpId + " / " + equip.InstanceId + " 탐색 상한 도달");
                        }

                        foreach (var detail in state.Details)
                        {
                            if (!pipesOnEqp.ContainsKey(detail.PipeId))
                                pipesOnEqp.Add(detail.PipeId, detail);
                        }
                        for (int f = 0; f < state.Fittings.Count; f++)
                        {
                            var fitting = state.Fittings[f];
                            if (!fittingsOnEqp.ContainsKey(fitting.InstanceId))
                                fittingsOnEqp.Add(fitting.InstanceId, fitting);
                        }
                    }

                    summary.FittingCount = fittingsOnEqp.Count;
                    AddBom(report, dbName, eqpId, fittingsOnEqp.Values);

                    summary.PocCount = pocCount;
                    summary.PipeCount = pipesOnEqp.Count;
                    foreach (var detail in pipesOnEqp.Values)
                    {
                        detail.DbName = dbName;
                        summary.LengthMeters += detail.LengthMeters;
                        summary.LengthMillimeters += detail.LengthMillimeters;
                        report.Details.Add(detail);
                        if (!allPipes.ContainsKey(detail.PipeId))
                            allPipes.Add(detail.PipeId, detail);
                    }
                    if (string.IsNullOrEmpty(summary.Note))
                    {
                        summary.Note = summary.PipeCount == 0
                            ? (summary.Kind == "완전삭제" ? "삭제 배관 없음" : "연결 배관 없음")
                            : "OK";
                    }
                    report.Summaries.Add(summary);
                }

                MarkShared(report.Details);
                report.Summaries.Add(BuildTotal(dbName, allEquipment.Count, allPocs.Count, allPipes.Values, report));
            }

            return report;
        }

        private static void UseSnapshot(
            string dbName,
            DateTime cutover,
            DeletedPipeRepository repo,
            DeletedPipeSession session,
            IProgress<string> progress)
        {
            string path = DeletedPipeSnapshotStore.FilePath(dbName, cutover);
            var snapshot = DeletedPipeSnapshotStore.TryLoad(dbName, cutover);
            if (snapshot == null)
            {
                progress?.Report(dbName + " 배관 로그를 한 번 읽어 스냅샷을 만듭니다");
                snapshot = repo.LoadSnapshot(dbName, cutover, progress);
                if (DeletedPipeSnapshotStore.TrySave(dbName, snapshot))
                    progress?.Report(dbName + " 스냅샷 저장 " + path);
                else
                    progress?.Report(dbName + " 스냅샷 파일을 쓰지 못했습니다. 이번 실행은 메모리에서만 탐색합니다. " + path);
            }
            else
            {
                progress?.Report(dbName + " 스냅샷 캐시 " + path
                    + " 작성 " + snapshot.BuiltAt.ToString("yyyy-MM-dd HH:mm"));
            }

            session.UseSnapshot(snapshot);
            progress?.Report(string.Format(
                "{0} 스냅샷 배관 {1:N0} POC {2:N0} 노드 {3:N0} 테이크오프 {4:N0}",
                dbName,
                snapshot.Pipes == null ? 0 : snapshot.Pipes.Count,
                snapshot.Pocs == null ? 0 : snapshot.Pocs.Count,
                snapshot.Nodes == null ? 0 : snapshot.Nodes.Count,
                snapshot.Takeoffs == null ? 0 : snapshot.Takeoffs.Count));
        }

        private static void MarkShared(List<DeletedPipeDetailRow> details)
        {
            var counts = new Dictionary<Guid, HashSet<string>>();
            for (int i = 0; i < details.Count; i++)
            {
                var row = details[i];
                HashSet<string> eqps;
                if (!counts.TryGetValue(row.PipeId, out eqps))
                {
                    eqps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    counts.Add(row.PipeId, eqps);
                }
                eqps.Add(row.EqpId ?? string.Empty);
            }

            for (int i = 0; i < details.Count; i++)
            {
                HashSet<string> eqps;
                if (counts.TryGetValue(details[i].PipeId, out eqps))
                    details[i].Shared = eqps.Count > 1;
            }
        }

        private static void CollectMatches(
            IList<DeletedEquipmentRow> source,
            string eqpId,
            List<DeletedEquipmentRow> matched,
            ref bool tagHit)
        {
            if (source == null)
                return;
            for (int e = 0; e < source.Count; e++)
            {
                if (!DeletedPipeClassifier.TagMatches(source[e].Tag, eqpId))
                    continue;
                tagHit = true;
                if (!DeletedPipeClassifier.IsEquipment(DeletedPipeClassifier.ToNode(source[e])))
                    continue;
                bool already = false;
                for (int m = 0; m < matched.Count; m++)
                {
                    if (matched[m].InstanceId == source[e].InstanceId)
                    {
                        already = true;
                        break;
                    }
                }
                if (!already)
                    matched.Add(source[e]);
            }
        }

        private static string KindLabel(int deletedCount, int pendingCount)
        {
            if (deletedCount > 0 && pendingCount > 0)
                return "혼합";
            if (pendingCount > 0)
                return "건설코드Delete";
            return "완전삭제";
        }

        private static void AddBom(
            DeletedPipeReport report,
            string dbName,
            string eqpId,
            ICollection<DeletedNodeRow> fittings)
        {
            var groups = new Dictionary<string, DeletedFittingBomRow>(StringComparer.OrdinalIgnoreCase);
            foreach (var fitting in fittings)
            {
                string name = DeletedPipeClassifier.FittingLabel(fitting);
                string qcode = fitting.QCode ?? string.Empty;
                string key = name + "\n" + qcode;
                DeletedFittingBomRow row;
                if (!groups.TryGetValue(key, out row))
                {
                    row = new DeletedFittingBomRow
                    {
                        DbName = dbName,
                        EqpId = eqpId,
                        FittingName = name,
                        QCode = qcode
                    };
                    groups.Add(key, row);
                }
                row.Count++;
            }

            var ordered = new List<DeletedFittingBomRow>(groups.Values);
            ordered.Sort(delegate (DeletedFittingBomRow a, DeletedFittingBomRow b)
            {
                int byCount = b.Count.CompareTo(a.Count);
                if (byCount != 0)
                    return byCount;
                return string.Compare(a.FittingName, b.FittingName, StringComparison.OrdinalIgnoreCase);
            });
            report.Bom.AddRange(ordered);
        }

        private static DeletedPipeSummaryRow BuildTotal(
            string dbName,
            int equipmentCount,
            int pocCount,
            ICollection<DeletedPipeDetailRow> distinctPipes,
            DeletedPipeReport report)
        {
            int fittingCount = 0;
            for (int i = 0; i < report.Summaries.Count; i++)
                fittingCount += report.Summaries[i].FittingCount;

            var total = new DeletedPipeSummaryRow
            {
                DbName = dbName,
                EqpId = "(전체)",
                EquipmentCount = equipmentCount,
                PocCount = pocCount,
                PipeCount = distinctPipes.Count,
                FittingCount = fittingCount,
                Kind = string.Empty,
                Note = "배관 ID 기준 1회, 피팅은 EQPID별 합"
            };
            foreach (var pipe in distinctPipes)
            {
                total.LengthMeters += pipe.LengthMeters;
                total.LengthMillimeters += pipe.LengthMillimeters;
            }
            return total;
        }

        private static List<string> DistinctEqpIds(IReadOnlyList<string> eqpIds)
        {
            var list = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (eqpIds == null)
                return list;
            for (int i = 0; i < eqpIds.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(eqpIds[i]))
                    continue;
                string id = eqpIds[i].Trim();
                if (seen.Add(id))
                    list.Add(id);
            }
            return list;
        }
    }
}
