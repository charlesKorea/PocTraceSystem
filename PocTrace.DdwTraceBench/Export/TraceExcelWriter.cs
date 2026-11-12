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
            rows.Add(new object[] { "종료 조건", "다른 설비 또는 modeltemplates.bim_yn=" + o.EndBimYn.ToString(CultureInfo.InvariantCulture) + " 객체" });
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
            ws.Column(1).Width = 40;
            ws.Column(2).Width = 60;
        }

        private static void WriteTagOwners(ExcelWorksheet ws, TraceResult result)
        {
            string[] h =
            {
                "owner_id (tb_attributes)", "owner_type", "attr version_sequence", "tb_modelinstances 있음", "model_type", "model_template_id",
                "display_nm", "family_key", "bim_yn", "model_type 판정", "family_key 판정", "bim_yn=0 판정", "설비", "추적함", "비고"
            };
            var rows = new List<object[]>();
            foreach (var t in result.TagOwners)
            {
                rows.Add(new object[]
                {
                    t.Owner.OwnerId, N(t.Owner.OwnerType), N(t.Owner.VersionSequence), YesNo(t.Model != null),
                    t.Model == null ? null : N(t.Model.ModelType), t.Model == null ? null : t.Model.TemplateId,
                    t.Template == null ? null : t.Template.DisplayName, t.Template == null ? null : t.Template.FamilyKey,
                    t.Template == null ? null : N(t.Template.BimYn),
                    YesNo(t.ByModelType), YesNo(t.ByFamilyKey), YesNo(t.ByBimYnZero), YesNo(t.IsEquipment), YesNo(t.Traced), t.Note
                });
            }
            WriteSheet(ws, h, rows);
        }

        private static void WriteHops(ExcelWorksheet ws, TraceResult result)
        {
            string[] h =
            {
                "순번", "단계", "시작설비 id", "출발 POC id (tb_pocinstances)", "출발 POC 소유 id", "출발 소유 table",
                "도착 POC id (tb_pocinstances.connected_poc_id)", "도착 POC poc_template_id", "도착 POC owner_type", "도착 POC 소유 id", "도착 소유 table",
                "종류", "동작", "비고(status)"
            };
            var rows = new List<object[]>();
            foreach (var x in result.Hops)
            {
                rows.Add(new object[]
                {
                    x.Seq, x.Depth, x.StartEquipmentId, x.FromPocId, x.FromPocOwnerId, x.FromPocOwnerTable,
                    x.ToPocId, x.ToPocTemplateId, N(x.ToPocOwnerType), x.ToPocOwnerId, x.ToPocOwnerTable,
                    x.Kind, x.Action, x.Status
                });
            }
            WriteSheet(ws, h, rows);
        }

        private static void WritePipes(ExcelWorksheet ws, TraceResult result)
        {
            string[] h =
            {
                "시작설비 id", "단계", "도착 POC id", "배관 id (tb_pipeinstances)", "pipe_type", "length2 (mm, 기준)", "length1 (예비)",
                "pipesize_id", "pipesize_nm", "last_update", "version_sequence"
            };
            var rows = new List<object[]>();
            foreach (var p in result.Pipes)
            {
                rows.Add(new object[]
                {
                    p.StartEquipmentId, p.Depth, p.ViaPocId, p.Pipe.Id, N(p.Pipe.PipeType), p.Pipe.Length2, p.Pipe.Length1,
                    p.Pipe.PipeSizeId, p.SizeName, p.Pipe.LastUpdate, N(p.Pipe.VersionSequence)
                });
            }
            WriteSheet(ws, h, rows);
        }

        private static void WriteObjects(ExcelWorksheet ws, TraceResult result)
        {
            string[] h =
            {
                "시작설비 id", "단계", "객체 id", "table", "역할", "도착 POC id", "model_type", "model_template_id", "display_nm", "family_key", "bim_yn",
                "model_type 판정", "family_key 판정", "bim_yn=0 판정", "pass-through", "POC 수", "비고"
            };
            var rows = new List<object[]>();
            foreach (var x in result.Objects)
            {
                rows.Add(new object[]
                {
                    x.StartEquipmentId, x.Depth, x.Id, x.Table, x.Role, x.ArrivedPocId, N(x.ModelType), x.TemplateId, x.DisplayName, x.FamilyKey, N(x.BimYn),
                    YesNo(x.ByModelType), YesNo(x.ByFamilyKey), YesNo(x.ByBimYnZero), YesNo(x.PassThrough), x.PocCount, x.Note
                });
            }
            WriteSheet(ws, h, rows);
        }

        private static void WritePocs(ExcelWorksheet ws, TraceResult result)
        {
            string[] h =
            {
                "POC id (tb_pocinstances)", "소유 id", "소유 table", "poc_template_id", "poc_template_nm", "equipment_yn", "memo",
                "utility_id", "connected_poc_id", "owner_type", "connection_order", "version_sequence"
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
                    poc.Id, poc.OwnerId, table, poc.PocTemplateId, tpl == null ? null : tpl.Name, tpl == null ? null : N(tpl.EquipmentYn), tpl == null ? null : tpl.Memo,
                    poc.UtilityId, poc.ConnectedPocId, N(poc.OwnerType), N(poc.ConnectionOrder), N(poc.VersionSequence)
                });
            }
            WriteSheet(ws, h, rows);
        }
    }
}
