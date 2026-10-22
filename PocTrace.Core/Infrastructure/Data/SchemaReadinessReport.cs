using System;
using System.Collections.Generic;
using System.Linq;
using PocTrace.Core.Enums;
using PocTrace.Core.Infrastructure;
using PocTrace.Core.Models;

namespace PocTrace.Core.Infrastructure.Data
{
    public sealed class SchemaReadinessReport
    {
        public DateTime CreatedAt { get; set; }
        public SchemaKind ActiveSchema { get; set; }
        public List<DatabaseReadiness> Databases { get; set; }

        public SchemaReadinessReport()
        {
            CreatedAt = DateTime.Now;
            Databases = new List<DatabaseReadiness>();
        }
    }

    public sealed class DatabaseReadiness
    {
        public string DbName { get; set; }
        public string Verdict { get; set; }
        public string Headline { get; set; }
        public List<GateRow> Gates { get; set; }
        public List<QueryRow> Queries { get; set; }
        public List<FillRow> Fills { get; set; }
        public List<GapRow> Gaps { get; set; }
        public List<DistRow> Distribution { get; set; }

        public DatabaseReadiness()
        {
            Verdict = "실패";
            Headline = "";
            Gates = new List<GateRow>();
            Queries = new List<QueryRow>();
            Fills = new List<FillRow>();
            Gaps = new List<GapRow>();
            Distribution = new List<DistRow>();
        }
    }

    public sealed class GateRow
    {
        public string Group { get; set; }
        public string Name { get; set; }
        public bool Required { get; set; }
        public bool Passed { get; set; }
        public string Detail { get; set; }
    }

    public sealed class QueryRow
    {
        public string Name { get; set; }
        public bool Ok { get; set; }
        public long ElapsedMs { get; set; }
        public long RowCount { get; set; }
        public string Note { get; set; }
        public string Error { get; set; }

        public QueryRow()
        {
            RowCount = -1;
        }
    }

    public sealed class FillRow
    {
        public string Name { get; set; }
        public long Numerator { get; set; }
        public long Denominator { get; set; }
        public string PercentText { get; set; }
        public string Level { get; set; }
        public string Note { get; set; }
    }

    public sealed class GapRow
    {
        public string Category { get; set; }
        public string Key { get; set; }
        public string Detail { get; set; }
    }

    public sealed class DistRow
    {
        public string Group { get; set; }
        public string Key { get; set; }
        public long Count { get; set; }
    }

    public static class GraphFillMetrics
    {
        public const int GapCap = 2000;

        public static string FormatPercent(long numerator, long denominator)
        {
            if (denominator <= 0)
                return "—";
            return (100.0 * numerator / denominator).ToString("0.0") + "%";
        }

        public static void Append(DatabaseReadiness report, GraphLoadBundle bundle, SchemaKind kind)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));
            if (bundle == null)
            {
                report.Fills.Add(Info("그래프 채움", 0, 0, "조회 결과가 없어 비율을 계산하지 않았습니다."));
                return;
            }

            var nodes = bundle.Nodes ?? new List<NodeInfo>();
            var pocs = bundle.Pocs ?? new List<PocData>();
            var pipes = bundle.Pipes ?? new List<PipeData>();
            var takeoffs = bundle.Takeoffs ?? new List<TakeoffData>();
            var diag = bundle.Diagnostics;

            int equipment = 0;
            int taggedEquipment = 0;
            var typeCounts = new Dictionary<string, int>();
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                    continue;
                string typeName = node.Type.ToString();
                int current;
                typeCounts.TryGetValue(typeName, out current);
                typeCounts[typeName] = current + 1;
                if (node.Type == NodeType.Equipment)
                {
                    equipment++;
                    if (!string.IsNullOrWhiteSpace(node.Tag))
                        taggedEquipment++;
                }
            }
            foreach (var pair in typeCounts.OrderBy(p => p.Key))
            {
                report.Distribution.Add(new DistRow
                {
                    Group = "노드 유형",
                    Key = pair.Key,
                    Count = pair.Value
                });
            }

            int pocWithOutlet = 0;
            for (int i = 0; i < pocs.Count; i++)
            {
                var poc = pocs[i];
                if (poc != null && poc.Outlet.HasValue && poc.Outlet.Value != Guid.Empty)
                    pocWithOutlet++;
            }

            int pipesWithEdge = 0;
            int dangling = diag != null ? diag.DanglingCount : 0;
            int gapListed = 0;
            var histogram = new Dictionary<int, int>();
            for (int i = 0; i < pipes.Count; i++)
            {
                var pipe = pipes[i];
                if (pipe == null)
                    continue;
                int edges = 0;
                int localDangling = 0;
                if (pipe.Endpoints != null)
                {
                    for (int e = 0; e < pipe.Endpoints.Count; e++)
                    {
                        var ep = pipe.Endpoints[e];
                        if (ep == null)
                            continue;
                        if (ep.Resolution == PipeEndpointResolution.Dangling)
                        {
                            localDangling++;
                            if (gapListed < GapCap)
                            {
                                report.Gaps.Add(new GapRow
                                {
                                    Category = "Dangling 끝점",
                                    Key = pipe.PipeId.ToString(),
                                    Detail = "ConnectionOrder " + ep.ConnectionOrder
                                });
                                gapListed++;
                            }
                        }
                        if (ep.Resolution == PipeEndpointResolution.Connected
                            && ep.NormalizedTarget.HasValue
                            && ep.NormalizedTarget.Value != Guid.Empty)
                            edges++;
                    }
                }
                if (diag == null)
                    dangling += localDangling;
                int bucket = edges > 10 ? 11 : edges;
                int bucketCount;
                histogram.TryGetValue(bucket, out bucketCount);
                histogram[bucket] = bucketCount + 1;
                if (edges > 0)
                    pipesWithEdge++;
                else if (gapListed < GapCap)
                {
                    report.Gaps.Add(new GapRow
                    {
                        Category = "배관 연결끝점 없음",
                        Key = pipe.PipeId.ToString(),
                        Detail = "유효 NormalizedTarget 0건"
                    });
                    gapListed++;
                }
            }
            foreach (var pair in histogram.OrderBy(p => p.Key))
            {
                report.Distribution.Add(new DistRow
                {
                    Group = "배관당 유효 끝점",
                    Key = pair.Key >= 11 ? "11+" : pair.Key.ToString(),
                    Count = pair.Value
                });
            }

            int takeoffReady = 0;
            for (int i = 0; i < takeoffs.Count; i++)
            {
                var takeoff = takeoffs[i];
                if (takeoff == null)
                    continue;
                if (takeoff.PipeId != Guid.Empty)
                    takeoffReady++;
                else if (gapListed < GapCap)
                {
                    report.Gaps.Add(new GapRow
                    {
                        Category = "Takeoff PipeId 없음",
                        Key = takeoff.TakeoffId.ToString(),
                        Detail = "continuation 배관을 찾지 못함"
                    });
                    gapListed++;
                }
            }

            if (diag != null)
            {
                for (int i = 0; i < diag.UnknownSamples.Count && gapListed < GapCap; i++)
                {
                    report.Gaps.Add(new GapRow
                    {
                        Category = "미분류 어휘",
                        Key = diag.UnknownSamples[i],
                        Detail = "Equipment로 세지 않음"
                    });
                    gapListed++;
                }
            }

            report.Fills.Add(CountRow("노드", nodes.Count, "활성 프로파일이 메모리에 올린 노드"));
            report.Fills.Add(Rate("Equipment Tag", taggedEquipment, equipment, TagLevel(kind, equipment, taggedEquipment), TagNote(kind)));
            report.Fills.Add(Rate("POC Outlet", pocWithOutlet, pocs.Count, pocs.Count == 0 ? "정보" : "정보", "Outlet이 비어 있는 POC는 미연결일 수 있습니다."));
            string pipeLevel = pipes.Count == 0 ? "주의" : (pipesWithEdge == 0 ? "주의" : "통과");
            report.Fills.Add(Rate("배관 연결끝점", pipesWithEdge, pipes.Count, pipeLevel, pipes.Count == 0 ? "배관 행이 없습니다." : "유효 끝점이 1개 이상인 배관"));
            string tofLevel = takeoffs.Count == 0 ? "정보" : (takeoffReady == takeoffs.Count ? "통과" : "주의");
            report.Fills.Add(Rate("Takeoff PipeId", takeoffReady, takeoffs.Count, tofLevel, "PIPE_TYPE=17301768 continuation"));
            report.Fills.Add(CountRow("dangling", dangling, dangling > 0 ? "간선은 생략하고 로드는 계속합니다." : "dangling 없음"));
            report.Fills[report.Fills.Count - 1].Level = dangling > 0 ? "주의" : "통과";
            int unknown = diag != null ? diag.UnknownVocabulary : 0;
            report.Fills.Add(CountRow("미분류 어휘", unknown, "표본은 부족 시트"));
            report.Fills[report.Fills.Count - 1].Level = unknown > 0 ? "주의" : "통과";
            if (diag != null)
            {
                report.Fills.Add(CountRow("4끝점 이상 배관", diag.PipesWithFourOrMore, "최대 " + diag.MaxEndpoints));
                report.Fills.Add(CountRow("순번 중복", diag.DuplicateConnectionOrders, "CONNECTION_ORDER 중복 끝점"));
                report.Fills.Add(CountRow("모듈 자식 생략", diag.ModuleChildrenSkipped, "부모 경유. 독립 Equipment가 아님"));
                report.Fills.Add(CountRow("삭제 행 제외", diag.DeletedRowsSkipped, "OPERATIONTYPE >= 2"));
            }
            report.Fills.Add(Info("공유 캐시", 0, 0, "이 점검은 GraphCache를 쓰지 않습니다."));
        }

        public static void ApplyVerdict(DatabaseReadiness report, SchemaKind active)
        {
            int requiredFail = 0;
            string firstFail = null;
            int ddwPass = 0;
            int ddwAll = 0;
            int webPass = 0;
            int webAll = 0;
            for (int i = 0; i < report.Gates.Count; i++)
            {
                var gate = report.Gates[i];
                if (gate.Group == "DDW 테이블")
                {
                    ddwAll++;
                    if (gate.Passed)
                        ddwPass++;
                }
                if (gate.Group == "WebDdw 컬럼")
                {
                    webAll++;
                    if (gate.Passed)
                        webPass++;
                }
                if (gate.Required && !gate.Passed)
                {
                    requiredFail++;
                    if (firstFail == null)
                        firstFail = gate.Group + " " + gate.Name + " — " + gate.Detail;
                }
            }

            bool loadFailed = false;
            string loadError = null;
            for (int i = 0; i < report.Queries.Count; i++)
            {
                var query = report.Queries[i];
                if (query.Name == "그래프 로드" && !query.Ok)
                {
                    loadFailed = true;
                    loadError = query.Error;
                }
            }

            bool fillWarn = false;
            for (int i = 0; i < report.Fills.Count; i++)
            {
                if (report.Fills[i].Level == "주의")
                    fillWarn = true;
            }

            if (requiredFail > 0)
            {
                report.Verdict = "실패";
                report.Headline = "필수 항목 " + requiredFail + "건 실패. " + (firstFail ?? "");
                if (loadFailed && !string.IsNullOrEmpty(loadError) && loadError.IndexOf("필수 게이트가 실패", StringComparison.Ordinal) < 0)
                    report.Headline += " 그래프 로드: " + loadError;
            }
            else if (loadFailed)
            {
                report.Verdict = "실패";
                report.Headline = "그래프 로드 실패. " + (loadError ?? "");
            }
            else if (fillWarn || (active == SchemaKind.S5d && ddwPass < ddwAll) || (active != SchemaKind.WebDdw && webAll > 0 && webPass < webAll))
            {
                report.Verdict = "주의";
                if (active == SchemaKind.S5d && ddwPass < ddwAll)
                    report.Headline = "활성 S5d 조회는 끝났습니다. DDW 테이블 " + ddwPass + "/" + ddwAll + ". WebDdw 컬럼 " + webPass + "/" + webAll + ". 현장 DDW가 아직 없거나 일부만 있습니다.";
                else if (active == SchemaKind.TbStar && webPass < webAll)
                    report.Headline = "TbStar 조회는 끝났습니다. WebDdw 컬럼은 " + webPass + "/" + webAll + "이라 그 프로파일로는 바꿀 수 없습니다.";
                else
                    report.Headline = "조회는 끝났고 채움 비율에 주의가 있습니다. 부족 시트를 보세요. DDW 테이블 " + ddwPass + "/" + ddwAll + ".";
            }
            else
            {
                report.Verdict = "통과";
                report.Headline = "필수 게이트와 활성 프로파일 조회가 끝났습니다. DDW 테이블 " + ddwPass + "/" + ddwAll + ". 공유 캐시는 쓰지 않았습니다.";
            }

            report.Fills.Insert(0, new FillRow
            {
                Name = "DDW 테이블",
                Numerator = ddwPass,
                Denominator = ddwAll,
                PercentText = FormatPercent(ddwPass, ddwAll),
                Level = ddwPass == ddwAll ? "통과" : "주의",
                Note = "INFORMATION_SCHEMA. 데이터 쿼리 전에 확인"
            });
            report.Fills.Insert(1, new FillRow
            {
                Name = "WebDdw 컬럼",
                Numerator = webPass,
                Denominator = webAll,
                PercentText = FormatPercent(webPass, webAll),
                Level = active == SchemaKind.WebDdw && webPass < webAll ? "실패" : (webPass == webAll ? "통과" : "정보"),
                Note = "없으면 WebDdw SQL을 실행하지 않습니다."
            });
        }

        private static string TagLevel(SchemaKind kind, int equipment, int tagged)
        {
            if (equipment == 0)
                return "정보";
            if (kind == SchemaKind.TbStar)
                return "정보";
            if (tagged == 0)
                return "주의";
            return "통과";
        }

        private static string TagNote(SchemaKind kind)
        {
            if (kind == SchemaKind.TbStar)
                return "TbStar에는 Tag 컬럼이 없습니다. 0%여도 실패가 아닙니다.";
            if (kind == SchemaKind.WebDdw)
                return "BIM_YN 규칙으로 채운 Tag";
            return "S5D ObjectInstanceLog.Tag";
        }

        private static FillRow Rate(string name, long numerator, long denominator, string level, string note)
        {
            return new FillRow
            {
                Name = name,
                Numerator = numerator,
                Denominator = denominator,
                PercentText = FormatPercent(numerator, denominator),
                Level = level,
                Note = note
            };
        }

        private static FillRow CountRow(string name, long count, string note)
        {
            return new FillRow
            {
                Name = name,
                Numerator = count,
                Denominator = count > 0 ? count : 0,
                PercentText = count.ToString("N0"),
                Level = "정보",
                Note = note
            };
        }

        private static FillRow Info(string name, long numerator, long denominator, string note)
        {
            return new FillRow
            {
                Name = name,
                Numerator = numerator,
                Denominator = denominator,
                PercentText = "—",
                Level = "정보",
                Note = note
            };
        }
    }
}
