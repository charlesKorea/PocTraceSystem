using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;

namespace PocTrace.Core.Engine.DeletedPipe
{
    /// <summary>
    /// EQPID 목록의 삭제 설비·삭제 POC에서 삭제 배관만 따라 길이를 합산한다.
    /// 삭제 피팅은 통과하고, 살아 있는 객체·다른 설비·끊긴 끝에서 멈춘다.
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

                progress?.Report(dbName + " 삭제 설비 조회");
                var equipment = repo.LoadDeletedEquipment();
                session.RememberEquipment(equipment);
                progress?.Report(dbName + " 삭제 설비 " + equipment.Count.ToString("N0") + "건");

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
                    for (int e = 0; e < equipment.Count; e++)
                    {
                        if (!DeletedPipeClassifier.TagMatches(equipment[e].Tag, eqpId))
                            continue;
                        tagHit = true;
                        if (DeletedPipeClassifier.IsEquipment(DeletedPipeClassifier.ToNode(equipment[e])))
                            matched.Add(equipment[e]);
                    }

                    if (matched.Count == 0)
                    {
                        report.Unmatched.Add(new DeletedPipeUnmatchedRow
                        {
                            DbName = dbName,
                            EqpId = eqpId,
                            Reason = tagHit
                                ? "Tag는 있으나 설비 Subdivision이 아닙니다."
                                : "기준일 이후 삭제 설비가 없습니다."
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
                        EquipmentCount = matched.Count
                    };
                    var pipesOnEqp = new Dictionary<Guid, DeletedPipeDetailRow>();
                    int pocCount = 0;

                    for (int m = 0; m < matched.Count; m++)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        var equip = matched[m];
                        allEquipment.Add(equip.InstanceId);
                        var state = new WalkState(eqpId, equip);
                        var pocs = session.QualifyingPocs(equip.InstanceId, report.Cutover);
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
                    }

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
                        summary.Note = summary.PipeCount == 0 ? "삭제 배관 없음" : "OK";
                    report.Summaries.Add(summary);
                }

                MarkShared(report.Details);
                report.Summaries.Add(BuildTotal(dbName, allEquipment.Count, allPocs.Count, allPipes.Values));
            }

            return report;
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

        private static DeletedPipeSummaryRow BuildTotal(
            string dbName,
            int equipmentCount,
            int pocCount,
            ICollection<DeletedPipeDetailRow> distinctPipes)
        {
            var total = new DeletedPipeSummaryRow
            {
                DbName = dbName,
                EqpId = "(전체)",
                EquipmentCount = equipmentCount,
                PocCount = pocCount,
                PipeCount = distinctPipes.Count,
                Note = "배관 ID 기준 1회"
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
