using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using PocTrace.DdwTraceBench.Data;
using PocTrace.DdwTraceBench.Trace;

namespace PocTrace.DdwTraceBench.Export
{
    public static class TraceExcelWriter
    {
        public static void Write(string path, TraceResult result, IList<string> schemaNotes, string connectionInfo)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            var file = new FileInfo(path);
            if (file.Exists)
                file.Delete();

            using (var package = new ExcelPackage(file))
            {
                WriteSummary(package.Workbook.Worksheets.Add("요약"), result, schemaNotes, connectionInfo);
                WriteTagOwners(package.Workbook.Worksheets.Add("Tag조회"), result);
                WriteHops(package.Workbook.Worksheets.Add("추적"), result);
                WritePipes(package.Workbook.Worksheets.Add("배관"), result);
                WriteObjects(package.Workbook.Worksheets.Add("객체"), result);
                WritePocs(package.Workbook.Worksheets.Add("POC"), result);
                package.Save();
            }
        }

        private static void WriteSheet(ExcelWorksheet ws, string[] headers, IList<object[]> rows)
        {
            for (int c = 0; c < headers.Length; c++)
            {
                ws.Cells[1, c + 1].Value = headers[c];
                ws.Cells[1, c + 1].Style.Font.Bold = true;
            }
            for (int r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                for (int c = 0; c < row.Length; c++)
                {
                    object v = row[c];
                    if (v is DateTime)
                    {
                        ws.Cells[r + 2, c + 1].Value = (DateTime)v;
                        ws.Cells[r + 2, c + 1].Style.Numberformat.Format = "yyyy-mm-dd hh:mm:ss";
                    }
                    else
                    {
                        ws.Cells[r + 2, c + 1].Value = v;
                    }
                }
            }
            ws.View.FreezePanes(2, 1);
            if (ws.Dimension != null)
                ws.Cells[ws.Dimension.Address].AutoFitColumns(8, 60);
        }

        private static string Ddw(string id)
        {
            return Hex.ToDdwId(id);
        }

        private static void DivisionOf(TraceResult result, HopRecord hop, out string divisionId, out string sub1Id, out string sub2Id, out string division, out string sub1, out string sub2)
        {
            TemplateRow tpl = null;
            if (hop.ToPocOwnerTable != null && !string.Equals(hop.ToPocOwnerTable, "tb_pipeinstances", StringComparison.OrdinalIgnoreCase))
                tpl = TemplateFor(result, hop.ToPocOwnerId);
            if (tpl == null)
                tpl = TemplateFor(result, hop.FromPocOwnerId);
            divisionId = tpl == null ? null : tpl.DivisionId;
            sub1Id = tpl == null ? null : tpl.Subdivision1Id;
            sub2Id = tpl == null ? null : tpl.Subdivision2Id;
            division = tpl == null ? null : tpl.Division;
            sub1 = tpl == null ? null : tpl.Subdivision1;
            sub2 = tpl == null ? null : tpl.Subdivision2;
        }

        private static TemplateRow TemplateFor(TraceResult result, string ownerId)
        {
            if (result == null || Hex.IsEmpty(ownerId))
                return null;
            for (int i = 0; i < result.Objects.Count; i++)
            {
                ObjectRecord obj = result.Objects[i];
                TemplateRow tpl;
                if (string.Equals(obj.Id, ownerId, StringComparison.OrdinalIgnoreCase) && obj.TemplateId != null && result.Templates.TryGetValue(obj.TemplateId, out tpl))
                    return tpl;
            }
            for (int i = 0; i < result.TagOwners.Count; i++)
            {
                TagOwnerInfo tag = result.TagOwners[i];
                if (tag.Owner != null && string.Equals(tag.Owner.OwnerId, ownerId, StringComparison.OrdinalIgnoreCase))
                    return tag.Template;
            }
            return null;
        }

        private static string DivisionText(TraceResult result, string templateId, int which)
        {
            TemplateRow tpl = TemplateById(result, templateId);
            if (tpl == null)
                return null;
            if (which == 1)
                return tpl.Subdivision1;
            if (which == 2)
                return tpl.Subdivision2;
            return tpl.Division;
        }

        private static string DivisionId(TraceResult result, string templateId, int which)
        {
            TemplateRow tpl = TemplateById(result, templateId);
            if (tpl == null)
                return null;
            if (which == 1)
                return tpl.Subdivision1Id;
            if (which == 2)
                return tpl.Subdivision2Id;
            return tpl.DivisionId;
        }

        private static TemplateRow TemplateById(TraceResult result, string templateId)
        {
            TemplateRow tpl;
            if (result == null || Hex.IsEmpty(templateId) || !result.Templates.TryGetValue(templateId, out tpl))
                return null;
            return tpl;
        }

        private static string N(long? v)
        {
            return v.HasValue ? v.Value.ToString(CultureInfo.InvariantCulture) : null;
        }

        private static string YesNo(bool b)
        {
            return b ? "Y" : "N";
        }

        private static void WriteSummary(ExcelWorksheet ws, TraceResult result, IList<string> schemaNotes, string connectionInfo)
        {
            var o = result.Options;
            var rows = new List<object[]>();
            rows.Add(new object[] { "Tag", o.Tag });
            rows.Add(new object[] { "시작 시각", result.Started.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) });
            rows.Add(new object[] { "종료 시각", result.Finished.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) });
            rows.Add(new object[] { "연결", connectionInfo });
            rows.Add(new object[] { "live 조건", "모든 테이블 version_sequence < 1000000000" });
            rows.Add(new object[] { "설비 판정", "model_type " + (o.EquipByModelType ? string.Join("/", o.EquipmentModelTypes) : "(끔)")
                + ", family_key=" + (o.EquipByFamilyKey ? o.EquipmentFamilyKey : "(끔)")
                + ", bim_yn=0 " + (o.EquipByBimYnZero ? "(켬)" : "(끔)") + ". 하나만 만족해도 설비" });
            rows.Add(new object[] { "종료 조건", "다른 설비, modeltemplates.bim_yn=" + o.EndBimYn.ToString(CultureInfo.InvariantCulture)
                + " 객체, 이름에 노즐이 있고 bim_yn=1인 객체, model_type 51382287 damper와 pipe_type 17301768 배관의 연결(Takeoff)" });
            rows.Add(new object[] { "pass-through model_type", string.Join("/", o.PassThroughModelTypes) + " (설비 판정에서 제외하고 통과)" });
            rows.Add(new object[] { "한도", "객체 " + o.MaxObjects.ToString(CultureInfo.InvariantCulture) + "개, 단계 " + o.MaxDepth.ToString(CultureInfo.InvariantCulture) });
            rows.Add(new object[] { "한도 도달", result.LimitHit ? "예" : "아니오" });
            rows.Add(new object[] { "tag 속성 owner 수", result.TagOwners.Count });
            rows.Add(new object[] { "추적한 설비 수", result.TagOwners.Count(t => t.Traced) });
            foreach (var n in schemaNotes ?? new List<string>())
                rows.Add(new object[] { "스키마", n });
            foreach (var n in result.Notes)
                rows.Add(new object[] { "비고", n });

            WriteSheet(ws, new[] { "항목", "값" }, rows);

            int start = rows.Count + 4;
            string[] headers = { "시작설비 id", "설비 이름", "배관 수", "length2 합(mm)", "length2 합(m)", "length1 합(mm)", "컴포넌트 통과", "종료 객체 수", "종료 객체 이름", "추적 단계 수(hop)", "비고" };
            for (int c = 0; c < headers.Length; c++)
            {
                ws.Cells[start, c + 1].Value = headers[c];
                ws.Cells[start, c + 1].Style.Font.Bold = true;
            }
            int r = start + 1;
            foreach (var t in result.TagOwners.Where(x => x.Traced))
            {
                string eq = t.Owner.OwnerId;
                var pipes = result.Pipes.Where(p => string.Equals(p.StartEquipmentId, eq, StringComparison.OrdinalIgnoreCase)).ToList();
                double sum2 = pipes.Sum(p => p.Pipe.Length2 ?? 0d);
                double sum1 = pipes.Sum(p => p.Pipe.Length1 ?? 0d);
                var objs = result.Objects.Where(x => string.Equals(x.StartEquipmentId, eq, StringComparison.OrdinalIgnoreCase)).ToList();
                var ends = objs.Where(x => x.Role != null && x.Role.StartsWith("종료", StringComparison.Ordinal)).ToList();
                int passed = objs.Count(x => x.Role != null && x.Role.StartsWith("통과", StringComparison.Ordinal));
                ws.Cells[r, 1].Value = eq;
                ws.Cells[r, 2].Value = t.Template == null ? null : t.Template.DisplayName;
                ws.Cells[r, 3].Value = pipes.Count;
                ws.Cells[r, 4].Value = Math.Round(sum2, 3);
                ws.Cells[r, 5].Value = Math.Round(sum2 / 1000d, 3);
                ws.Cells[r, 6].Value = Math.Round(sum1, 3);
                ws.Cells[r, 7].Value = passed;
                ws.Cells[r, 8].Value = ends.Count;
                ws.Cells[r, 9].Value = string.Join(", ", ends.Select(x => x.DisplayName ?? "(이름 없음)").Distinct());
                ws.Cells[r, 10].Value = result.Hops.Count(h => string.Equals(h.StartEquipmentId, eq, StringComparison.OrdinalIgnoreCase));
                ws.Cells[r, 11].Value = t.Note;
                r++;
            }

            WritePocLengths(ws, result, r + 2);
            ws.Column(1).Width = 40;
            ws.Column(2).Width = 60;
        }

        private static void WritePocLengths(ExcelWorksheet ws, TraceResult result, int startRow)
        {
            ws.Cells[startRow, 1].Value = "POC별 배관 길이 (length2)";
            ws.Cells[startRow, 1].Style.Font.Bold = true;
            string[] headers = { "시작설비 db id", "시작설비 ddw id", "설비 이름", "출발 POC db id", "출발 POC ddw id", "출발 POC 이름", "배관 수", "length2 합(mm)", "length2 합(m)" };
            int headerRow = startRow + 1;
            for (int c = 0; c < headers.Length; c++)
            {
                ws.Cells[headerRow, c + 1].Value = headers[c];
                ws.Cells[headerRow, c + 1].Style.Font.Bold = true;
            }

            var ordered = OrderHopsForReading(result);
            int row = headerRow + 1;
            string rootEq = null;
            string rootPoc = null;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int count = 0;
            double sum2 = 0;
            foreach (var item in ordered)
            {
                HopRecord hop = item.Hop;
                bool newRoot = hop.Depth == 0 || rootPoc == null;
                if (newRoot && rootPoc != null)
                {
                    WritePocLengthRow(ws, result, row, rootEq, rootPoc, count, sum2);
                    row++;
                    seen.Clear();
                    count = 0;
                    sum2 = 0;
                }
                if (newRoot)
                {
                    rootEq = hop.StartEquipmentId;
                    rootPoc = hop.FromPocId;
                }
                if (!string.Equals(hop.ToPocOwnerTable, "tb_pipeinstances", StringComparison.OrdinalIgnoreCase) || Hex.IsEmpty(hop.ToPocOwnerId))
                    continue;
                if (!seen.Add(hop.ToPocOwnerId))
                    continue;
                PipeRecord pipe = FindPipe(result, hop.ToPocOwnerId);
                count++;
                if (pipe != null && pipe.Pipe.Length2.HasValue)
                    sum2 += pipe.Pipe.Length2.Value;
            }
            if (rootPoc != null)
                WritePocLengthRow(ws, result, row, rootEq, rootPoc, count, sum2);
        }

        private static void WritePocLengthRow(ExcelWorksheet ws, TraceResult result, int row, string eqId, string pocId, int count, double sum2)
        {
            string eqName = null;
            foreach (var t in result.TagOwners)
            {
                if (t.Owner != null && string.Equals(t.Owner.OwnerId, eqId, StringComparison.OrdinalIgnoreCase) && t.Template != null)
                    eqName = t.Template.DisplayName;
            }
            ws.Cells[row, 1].Value = eqId;
            ws.Cells[row, 2].Value = Ddw(eqId);
            ws.Cells[row, 3].Value = eqName;
            ws.Cells[row, 4].Value = pocId;
            ws.Cells[row, 5].Value = Ddw(pocId);
            ws.Cells[row, 6].Value = PocTemplateName(result, pocId);
            ws.Cells[row, 7].Value = count;
            ws.Cells[row, 8].Value = Math.Round(sum2, 3);
            ws.Cells[row, 9].Value = Math.Round(sum2 / 1000d, 3);
        }

        private static PipeRecord FindPipe(TraceResult result, string pipeId)
        {
            for (int i = 0; i < result.Pipes.Count; i++)
            {
                if (string.Equals(result.Pipes[i].Pipe.Id, pipeId, StringComparison.OrdinalIgnoreCase))
                    return result.Pipes[i];
            }
            return null;
        }

        private static void WriteTagOwners(ExcelWorksheet ws, TraceResult result)
        {
            string[] h =
            {
                "owner db id", "owner ddw id", "owner_type", "attr version_sequence", "tb_modelinstances 있음", "model_type", "model_template db id", "model_template ddw id",
                "display_nm", "family_key", "bim_yn", "template model_type",
                "division db id", "subdivision_1 db id", "subdivision_2 db id",
                "division ddw id", "subdivision_1 ddw id", "subdivision_2 ddw id",
                "division", "subdivision_1", "subdivision_2", "model_type 판정", "family_key 판정", "bim_yn=0 판정", "설비", "추적함", "비고"
            };
            var rows = new List<object[]>();
            foreach (var t in result.TagOwners)
            {
                rows.Add(new object[]
                {
                    t.Owner.OwnerId, Ddw(t.Owner.OwnerId), N(t.Owner.OwnerType), N(t.Owner.VersionSequence), YesNo(t.Model != null),
                    t.Model == null ? null : TraceCodes.ModelTypeText(t.Model.ModelType), t.Model == null ? null : t.Model.TemplateId, t.Model == null ? null : Ddw(t.Model.TemplateId),
                    t.Template == null ? null : t.Template.DisplayName, t.Template == null ? null : t.Template.FamilyKey,
                    t.Template == null ? null : N(t.Template.BimYn),
                    t.Template == null ? null : TraceCodes.ModelTypeText(t.Template.ModelType),
                    t.Template == null ? null : t.Template.DivisionId, t.Template == null ? null : t.Template.Subdivision1Id, t.Template == null ? null : t.Template.Subdivision2Id,
                    t.Template == null ? null : Ddw(t.Template.DivisionId), t.Template == null ? null : Ddw(t.Template.Subdivision1Id), t.Template == null ? null : Ddw(t.Template.Subdivision2Id),
                    t.Template == null ? null : t.Template.Division, t.Template == null ? null : t.Template.Subdivision1, t.Template == null ? null : t.Template.Subdivision2,
                    YesNo(t.ByModelType), YesNo(t.ByFamilyKey), YesNo(t.ByBimYnZero), YesNo(t.IsEquipment), YesNo(t.Traced), t.Note
                });
            }
            WriteSheet(ws, h, rows);
        }

        private static readonly System.Drawing.Color PipeFill = System.Drawing.Color.FromArgb(91, 155, 213);
        private static readonly System.Drawing.Color ComponentFill = System.Drawing.Color.FromArgb(255, 192, 0);
        private static readonly System.Drawing.Color EquipmentFill = System.Drawing.Color.FromArgb(255, 199, 206);

        private static void WriteHops(ExcelWorksheet ws, TraceResult result)
        {
            string[] headers =
            {
                "순번", "단계",
                "시작설비 db id", "출발 POC db id", "출발 소유 db id", "connected poc db id", "도착 소유 db id", "속성 owner db id",
                "시작설비 ddw id", "출발 POC ddw id", "출발 소유 ddw id", "connected poc ddw id", "도착 소유 ddw id", "속성 owner ddw id",
                "출발 POC 이름", "출발 소유 이름", "도착 객체 이름",
                "종류", "동작",
                "Status", "pipe_type", "model_type",
                "division db id", "subdivision_1 db id", "subdivision_2 db id",
                "division ddw id", "subdivision_1 ddw id", "subdivision_2 ddw id",
                "division", "subdivision_1", "subdivision_2",
                "damper", "tag", "connectiontag", "hookup number",
                "지나온 객체", "비고(status)"
            };
            int ownerColorCol = 1;
            for (int c = 0; c < headers.Length; c++)
            {
                ws.Cells[1, c + 1].Value = headers[c];
                ws.Cells[1, c + 1].Style.Font.Bold = true;
                if (headers[c] == "출발 소유 db id")
                    ownerColorCol = c + 1;
            }

            var ordered = OrderHopsForReading(result);
            int row = 2;
            int seq = 1;
            foreach (var item in ordered)
            {
                var x = item.Hop;
                string divisionId;
                string sub1Id;
                string sub2Id;
                string division;
                string sub1;
                string sub2;
                DivisionOf(result, x, out divisionId, out sub1Id, out sub2Id, out division, out sub1, out sub2);
                object[] values =
                {
                    seq, x.Depth,
                    x.StartEquipmentId, x.FromPocId, x.FromPocOwnerId, x.ToPocId, x.ToPocOwnerId, x.AttributeOwnerId,
                    Ddw(x.StartEquipmentId), Ddw(x.FromPocId), Ddw(x.FromPocOwnerId), Ddw(x.ToPocId), Ddw(x.ToPocOwnerId), Ddw(x.AttributeOwnerId),
                    PocTemplateName(result, x.FromPocId), FromOwnerName(result, x), ArrivalOwnerName(result, x),
                    x.Kind, x.Action,
                    x.TraceStatus, x.PipeTypeText, x.ModelTypeText,
                    divisionId, sub1Id, sub2Id, Ddw(divisionId), Ddw(sub1Id), Ddw(sub2Id), division, sub1, sub2,
                    x.DamperName, x.TakeoffTag, x.TakeoffConnectionTag, x.TakeoffHookup,
                    item.PassedNames, x.Status
                };
                for (int c = 0; c < values.Length; c++)
                    ws.Cells[row, c + 1].Value = values[c];

                var fill = FromOwnerFill(result, x);
                if (fill.HasValue)
                {
                    var cell = ws.Cells[row, ownerColorCol];
                    cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(fill.Value);
                }
                row++;
                seq++;
            }

            ws.View.FreezePanes(2, 1);
            if (ws.Dimension != null)
                ws.Cells[ws.Dimension.Address].AutoFitColumns(8, 60);
        }

        private sealed class HopLine
        {
            public HopRecord Hop;
            public string PassedNames;
        }

        private static List<HopLine> OrderHopsForReading(TraceResult result)
        {
            IList<HopRecord> hops = result.Hops;
            var lines = new List<HopLine>();
            var groups = new List<List<HopRecord>>();
            var indexByStart = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < hops.Count; i++)
            {
                string key = hops[i].StartEquipmentId ?? "";
                int at;
                if (!indexByStart.TryGetValue(key, out at))
                {
                    at = groups.Count;
                    indexByStart[key] = at;
                    groups.Add(new List<HopRecord>());
                }
                groups[at].Add(hops[i]);
            }
            for (int g = 0; g < groups.Count; g++)
                AppendEquipmentChain(result, groups[g], lines);
            return lines;
        }

        private static void AppendEquipmentChain(TraceResult result, List<HopRecord> hops, List<HopLine> lines)
        {
            var children = new Dictionary<int, List<int>>();
            var enteredBy = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var roots = new List<int>();
            for (int i = 0; i < hops.Count; i++)
            {
                HopRecord hop = hops[i];
                int parent;
                if (hop.Depth == 0 || Hex.IsEmpty(hop.FromPocOwnerId) || !enteredBy.TryGetValue(hop.FromPocOwnerId, out parent))
                    roots.Add(i);
                else
                {
                    List<int> list;
                    if (!children.TryGetValue(parent, out list))
                    {
                        list = new List<int>();
                        children[parent] = list;
                    }
                    list.Add(i);
                }
                if (string.Equals(hop.Action, "계속", StringComparison.Ordinal) && !Hex.IsEmpty(hop.ToPocOwnerId) && !enteredBy.ContainsKey(hop.ToPocOwnerId))
                    enteredBy[hop.ToPocOwnerId] = i;
            }

            var ids = new List<string>();
            var names = new List<string>();
            for (int r = 0; r < roots.Count; r++)
                WalkChain(result, hops, children, roots[r], ids, names, lines);
        }

        private static void WalkChain(
            TraceResult result,
            List<HopRecord> hops,
            Dictionary<int, List<int>> children,
            int index,
            List<string> ids,
            List<string> names,
            List<HopLine> lines)
        {
            HopRecord hop = hops[index];
            bool pushed = PushOwner(ids, names, hop.FromPocOwnerId, FromOwnerName(result, hop));
            string passed = null;
            if (IsEnd(hop))
            {
                var pathIds = new List<string>(ids);
                var pathNames = new List<string>(names);
                PushOwner(pathIds, pathNames, hop.ToPocOwnerId, ArrivalOwnerName(result, hop));
                passed = string.Join(", ", pathNames);
            }
            lines.Add(new HopLine { Hop = hop, PassedNames = passed });

            List<int> next;
            if (children.TryGetValue(index, out next))
            {
                for (int i = 0; i < next.Count; i++)
                    WalkChain(result, hops, children, next[i], ids, names, lines);
            }
            if (pushed)
            {
                ids.RemoveAt(ids.Count - 1);
                names.RemoveAt(names.Count - 1);
            }
        }

        private static bool IsEnd(HopRecord hop)
        {
            return string.Equals(hop.Kind, "종료", StringComparison.Ordinal)
                || string.Equals(hop.Action, "종료", StringComparison.Ordinal);
        }

        private static bool PushOwner(List<string> ids, List<string> names, string id, string name)
        {
            if (Hex.IsEmpty(id) && string.IsNullOrEmpty(name))
                return false;
            if (ids.Count > 0 && string.Equals(ids[ids.Count - 1], id ?? "", StringComparison.OrdinalIgnoreCase))
                return false;
            ids.Add(id ?? "");
            names.Add(string.IsNullOrEmpty(name) ? "(이름 없음)" : name);
            return true;
        }

        private static string PocTemplateName(TraceResult result, string pocId)
        {
            if (result == null || Hex.IsEmpty(pocId))
                return null;
            PocRow poc;
            if (!result.Pocs.TryGetValue(pocId, out poc))
                return null;
            if (Hex.IsEmpty(poc.PocTemplateId))
                return "pipe";
            PocTemplateRow template;
            if (result.PocTemplates.TryGetValue(poc.PocTemplateId, out template) && !string.IsNullOrEmpty(template.Name))
                return template.Name;
            return null;
        }

        private static string FromOwnerName(TraceResult result, HopRecord hop)
        {
            if (IsPipeFrom(result, hop))
                return "pipe";
            return ModelName(result, hop.FromPocOwnerId);
        }

        private static string ArrivalOwnerName(TraceResult result, HopRecord hop)
        {
            if (Hex.IsEmpty(hop.ToPocOwnerId))
                return null;
            if (string.Equals(hop.ToPocOwnerTable, "tb_pipeinstances", StringComparison.OrdinalIgnoreCase) || Hex.IsEmpty(hop.ToPocTemplateId))
                return PipeArrivalName(result, hop.ToPocOwnerId);
            return ModelName(result, hop.ToPocOwnerId);
        }

        private static string PipeArrivalName(TraceResult result, string pipeId)
        {
            PipeRecord pipe = FindPipe(result, pipeId);
            if (pipe == null || !pipe.Pipe.PipeType.HasValue)
                return "pipe";
            string meaning = TraceCodes.PipeTypeMeaning(pipe.Pipe.PipeType.Value);
            return string.IsNullOrEmpty(meaning) ? "pipe" : meaning;
        }

        private static bool IsPipeFrom(TraceResult result, HopRecord hop)
        {
            if (string.Equals(hop.FromPocOwnerTable, "tb_pipeinstances", StringComparison.OrdinalIgnoreCase))
                return true;
            if (result == null || Hex.IsEmpty(hop.FromPocId))
                return false;
            PocRow poc;
            return result.Pocs.TryGetValue(hop.FromPocId, out poc) && Hex.IsEmpty(poc.PocTemplateId);
        }

        private static string ModelName(TraceResult result, string ownerId)
        {
            if (result == null || Hex.IsEmpty(ownerId))
                return null;
            for (int i = 0; i < result.Objects.Count; i++)
            {
                ObjectRecord obj = result.Objects[i];
                if (string.Equals(obj.Id, ownerId, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(obj.DisplayName))
                    return obj.DisplayName;
            }
            for (int i = 0; i < result.TagOwners.Count; i++)
            {
                TagOwnerInfo tag = result.TagOwners[i];
                if (tag.Owner != null && string.Equals(tag.Owner.OwnerId, ownerId, StringComparison.OrdinalIgnoreCase) && tag.Template != null && !string.IsNullOrEmpty(tag.Template.DisplayName))
                    return tag.Template.DisplayName;
            }
            return null;
        }

        private static System.Drawing.Color? FromOwnerFill(TraceResult result, HopRecord hop)
        {
            if (IsPipeFrom(result, hop))
                return PipeFill;
            if (IsEquipmentOrEnd(result, hop))
                return EquipmentFill;
            if (string.Equals(hop.FromPocOwnerTable, "tb_modelinstances", StringComparison.OrdinalIgnoreCase) || ModelName(result, hop.FromPocOwnerId) != null)
                return ComponentFill;
            return null;
        }

        private static bool IsEquipmentOrEnd(TraceResult result, HopRecord hop)
        {
            if (result == null || Hex.IsEmpty(hop.FromPocOwnerId))
                return false;
            for (int i = 0; i < result.TagOwners.Count; i++)
            {
                TagOwnerInfo tag = result.TagOwners[i];
                if (tag.Owner != null && string.Equals(tag.Owner.OwnerId, hop.FromPocOwnerId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            for (int i = 0; i < result.Objects.Count; i++)
            {
                ObjectRecord obj = result.Objects[i];
                if (!string.Equals(obj.Id, hop.FromPocOwnerId, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (obj.Role != null && (obj.Role.StartsWith("시작", StringComparison.Ordinal) || obj.Role.StartsWith("종료", StringComparison.Ordinal)))
                    return true;
                if (!obj.PassThrough && (obj.ByModelType || obj.ByFamilyKey || obj.ByBimYnZero))
                    return true;
            }
            return false;
        }

        private static void WritePipes(ExcelWorksheet ws, TraceResult result)
        {
            string[] h =
            {
                "시작설비 db id", "시작설비 ddw id", "단계", "도착 POC db id", "도착 POC ddw id", "배관 db id", "배관 ddw id", "pipe_type", "pipe_type 의미", "length2 (mm, 기준)", "length1 (예비)",
                "pipesize_id", "pipesize_nm", "last_update", "version_sequence"
            };
            var rows = new List<object[]>();
            foreach (var p in result.Pipes)
            {
                rows.Add(new object[]
                {
                    p.StartEquipmentId, Ddw(p.StartEquipmentId), p.Depth, p.ViaPocId, Ddw(p.ViaPocId), p.Pipe.Id, Ddw(p.Pipe.Id), N(p.Pipe.PipeType),
                    p.Pipe.PipeType.HasValue ? TraceCodes.PipeTypeMeaning(p.Pipe.PipeType.Value) : null,
                    p.Pipe.Length2, p.Pipe.Length1,
                    p.Pipe.PipeSizeId, p.SizeName, p.Pipe.LastUpdate, N(p.Pipe.VersionSequence)
                });
            }
            WriteSheet(ws, h, rows);
        }

        private static void WriteObjects(ExcelWorksheet ws, TraceResult result)
        {
            string[] h =
            {
                "시작설비 db id", "시작설비 ddw id", "단계", "객체 db id", "객체 ddw id", "table", "역할", "도착 POC db id", "도착 POC ddw id", "model_type", "template model_type", "model_template db id", "model_template ddw id", "display_nm", "family_key", "bim_yn",
                "division db id", "subdivision_1 db id", "subdivision_2 db id",
                "division ddw id", "subdivision_1 ddw id", "subdivision_2 ddw id",
                "division", "subdivision_1", "subdivision_2",
                "model_type 판정", "family_key 판정", "bim_yn=0 판정", "pass-through", "POC 수", "비고"
            };
            var rows = new List<object[]>();
            foreach (var x in result.Objects)
            {
                rows.Add(new object[]
                {
                    x.StartEquipmentId, Ddw(x.StartEquipmentId), x.Depth, x.Id, Ddw(x.Id), x.Table, x.Role, x.ArrivedPocId, Ddw(x.ArrivedPocId), TraceCodes.ModelTypeText(x.ModelType), TraceCodes.ModelTypeText(x.TemplateModelType), x.TemplateId, Ddw(x.TemplateId), x.DisplayName, x.FamilyKey, N(x.BimYn),
                    DivisionId(result, x.TemplateId, 0), DivisionId(result, x.TemplateId, 1), DivisionId(result, x.TemplateId, 2),
                    Ddw(DivisionId(result, x.TemplateId, 0)), Ddw(DivisionId(result, x.TemplateId, 1)), Ddw(DivisionId(result, x.TemplateId, 2)),
                    DivisionText(result, x.TemplateId, 0), DivisionText(result, x.TemplateId, 1), DivisionText(result, x.TemplateId, 2),
                    YesNo(x.ByModelType), YesNo(x.ByFamilyKey), YesNo(x.ByBimYnZero), YesNo(x.PassThrough), x.PocCount, x.Note
                });
            }
            WriteSheet(ws, h, rows);
        }

        private static void WritePocs(ExcelWorksheet ws, TraceResult result)
        {
            string[] h =
            {
                "POC db id", "소유 db id", "poc_template db id", "connected poc db id",
                "POC ddw id", "소유 ddw id", "poc_template ddw id", "connected poc ddw id",
                "소유 table", "poc_template_nm", "equipment_yn", "memo",
                "utility_id", "owner_type", "connection_order", "version_sequence"
            };
            var pipeIds = new HashSet<string>(result.Pipes.Select(p => p.Pipe.Id), StringComparer.OrdinalIgnoreCase);
            var objIds = new HashSet<string>(result.Objects.Select(o => o.Id), StringComparer.OrdinalIgnoreCase);
            var rows = new List<object[]>();
            foreach (var poc in result.Pocs.Values.OrderBy(p => p.OwnerId, StringComparer.Ordinal).ThenBy(p => p.ConnectionOrder ?? long.MaxValue))
            {
                string table = pipeIds.Contains(poc.OwnerId ?? string.Empty) ? "tb_pipeinstances" : (objIds.Contains(poc.OwnerId ?? string.Empty) ? "tb_modelinstances" : null);
                PocTemplateRow tpl = null;
                if (poc.PocTemplateId != null)
                    result.PocTemplates.TryGetValue(poc.PocTemplateId, out tpl);
                rows.Add(new object[]
                {
                    poc.Id, poc.OwnerId, poc.PocTemplateId, poc.ConnectedPocId,
                    Ddw(poc.Id), Ddw(poc.OwnerId), Ddw(poc.PocTemplateId), Ddw(poc.ConnectedPocId),
                    table, tpl == null ? null : tpl.Name, tpl == null ? null : N(tpl.EquipmentYn), tpl == null ? null : tpl.Memo,
                    poc.UtilityId, N(poc.OwnerType), N(poc.ConnectionOrder), N(poc.VersionSequence)
                });
            }
            WriteSheet(ws, h, rows);
        }
    }
}
