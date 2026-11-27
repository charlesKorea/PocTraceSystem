using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using OfficeOpenXml;
using OfficeOpenXml.Drawing;
using PocTrace.Core.Engine.Cache;
using PocTrace.Core.Infrastructure.Data;
using PocTrace.Core.Models;

namespace PocTrace.Core.Engine.Reporting
{
    /// <summary>
    /// 삽입점 3차원 스케치. 배관 중심선이 아니다.
    /// 엑셀 Drawing은 등각 그림이고, HTML은 같은 장면을 마우스로 돌린다.
    /// </summary>
    public static class TraceSketch
    {
        private const int MaxEquipment = 24;
        private const int MaxSegments = 400;

        public static byte[] Render(IList<TraceResult> details, IGraphCache cache)
        {
            var groups = BuildGroups(details, cache);
            const int width = 1200;
            int height = 16;
            for (int i = 0; i < groups.Count; i++)
                height += PanelHeight(groups[i]) + 12;
            if (groups.Count == 0)
                height = 16 + 360;
            using (var bmp = new Bitmap(width, height))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.FromArgb(246, 244, 239));
                using (var titleFont = MakeFont(13f, true))
                using (var bodyFont = MakeFont(11f, false))
                using (var ink = new SolidBrush(Color.FromArgb(28, 28, 30)))
                using (var muted = new SolidBrush(Color.FromArgb(94, 101, 99)))
                {
                    if (groups.Count == 0)
                        g.DrawString("좌표가 있는 배관이 없습니다. 캐시를 다시 저장한 뒤에 그릴 수 있습니다.", bodyFont, muted, 24, 24);
                    int top = 12;
                    for (int i = 0; i < groups.Count; i++)
                    {
                        int panel = PanelHeight(groups[i]);
                        DrawIso(g, groups[i], 16, top, width - 32, panel, titleFont, bodyFont, ink);
                        top += panel + 12;
                    }
                }
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    return ms.ToArray();
                }
            }
        }

        public static void AddSheet(ExcelPackage package, byte[] png)
        {
            var ws = package.Workbook.Worksheets.Add("Drawing");
            ws.Cells["A1"].Value = "Drawing";
            ws.Cells["A1"].Style.Font.Bold = true;
            if (png == null || png.Length == 0)
                return;
            var picture = ws.Drawings.AddPicture("sketch", new MemoryStream(png), ePictureType.Png);
            picture.SetPosition(2, 0, 0, 0);
        }

        public static void WriteHtml(string path, IList<EquipmentTraceSummary> summaries, string line, IList<TraceResult> details, IGraphCache cache, bool drawing)
        {
            bool ddw = false;
            if (summaries != null)
            {
                for (int i = 0; i < summaries.Count; i++)
                {
                    if (summaries[i] != null && summaries[i].DdwColumns)
                    {
                        ddw = true;
                        break;
                    }
                }
            }
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html lang=\"ko\"><head><meta charset=\"utf-8\" />");
            sb.Append("<title>Summary</title><style>");
            sb.Append("body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI','Malgun Gothic',sans-serif;background:#f4f4f6;color:#1c1c1e;margin:28px;}");
            sb.Append("table{border-collapse:collapse;background:#fff;margin:0 0 28px;}");
            sb.Append("th,td{border:1px solid #e4e0d8;padding:6px 10px;font-size:14px;}");
            sb.Append("th{background:#e7f3f1;} td.fail{color:#d92d20;font-weight:700;}");
            sb.Append("canvas{width:100%;height:auto;background:#fff;border-radius:18px;box-shadow:0 12px 40px rgba(28,28,30,.08);}");
            sb.Append(".draw-layout{display:flex;gap:18px;align-items:flex-start;}");
            sb.Append(".draw-stage{position:relative;flex:1;min-width:0;}");
            sb.Append(".draw-side{width:280px;flex:none;background:#fff;border-radius:18px;box-shadow:0 12px 40px rgba(28,28,30,.06);}");
            sb.Append(".draw-side h2{font-size:13px;letter-spacing:.04em;margin:14px 16px 8px;color:#6e6e73;}");
            sb.Append(".draw-side table{margin:0;width:100%;} .draw-side td,.draw-side th{border:0;border-top:1px solid #ececef;}");
            sb.Append(".st-ok{color:#128a3e;font-weight:700;} .st-bad{color:#d92d20;font-weight:700;} .st-emp{color:#1c1c1e;font-weight:700;}");
            sb.Append("p.hint{color:#6e6e73;font-size:14px;line-height:1.5;}");
            sb.Append(".draw-label{font-size:12px;font-weight:650;letter-spacing:.02em;color:#6e6e73;margin:0 0 8px;}");
            sb.Append(".draw-note{min-height:1.2em;color:#6e6e73;font-size:13px;margin:0 0 12px;}");
            sb.Append(".draw-picks,.draw-legend,.view-grid{display:flex;flex-wrap:wrap;gap:8px;margin:0 0 14px;}");
            sb.Append(".draw-picks label{display:inline-flex;align-items:center;gap:6px;border:1px solid #e4e4e8;background:#fff;border-radius:999px;padding:6px 12px;box-shadow:0 1px 2px rgba(28,28,30,.04);}");
            sb.Append(".draw-legend button,.draw-picks button,.view-grid button{font:inherit;border:1px solid #e4e4e8;background:#fff;border-radius:999px;padding:6px 12px;cursor:pointer;}");
            sb.Append(".draw-legend button.on,.draw-picks button.on,.view-grid button.on{background:#0c6e68;border-color:#0c6e68;color:#fff;font-weight:650;}");
            sb.Append("#drawStatusBody tr{cursor:pointer;} #drawStatusBody tr.on td{background:#e7f3f1;}");
            sb.Append(".viewdock{position:absolute;top:14px;right:14px;width:168px;padding:12px;border-radius:18px;background:rgba(255,255,255,.88);box-shadow:0 10px 30px rgba(28,28,30,.12);}");
            sb.Append(".cube-scene{width:96px;height:96px;margin:0 auto 8px;perspective:480px;}");
            sb.Append(".cube{width:56px;height:56px;margin:20px auto;position:relative;transform-style:preserve-3d;}");
            sb.Append(".cube .face{position:absolute;width:56px;height:56px;padding:0;border:1px solid rgba(12,110,104,.45);background:rgba(255,255,255,.94);color:#083e3b;font:650 11px/56px -apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;cursor:pointer;}");
            sb.Append(".cube .front{transform:translateZ(28px);} .cube .back{transform:rotateY(180deg) translateZ(28px);}");
            sb.Append(".cube .right{transform:rotateY(90deg) translateZ(28px);} .cube .left{transform:rotateY(-90deg) translateZ(28px);}");
            sb.Append(".cube .top{transform:rotateX(90deg) translateZ(28px);} .cube .bottom{transform:rotateX(-90deg) translateZ(28px);}");
            sb.Append("</style></head><body><h1>Summary</h1>");
            sb.Append("<table><thead><tr>");
            string[] headers = ddw
                ? new[]
                {
                    "Line", "大分类", "小分类", "건설CODE", "EQ Code", "设计社", "设备搬入日", "出图需求日",
                    "Result", "Issues", "eqp count", "Pocs", "BreakDown", "BOM(Hop)", "Length(m)",
                    "Date", "Worker", "Company", "비고"
                }
                : new[]
                {
                    "Line", "EQ Code", "Result", "Issues", "eqp count", "Pocs",
                    "BreakDown", "BOM(Hop)", "Length(m)", "Date", "Worker", "Company"
                };
            for (int i = 0; i < headers.Length; i++)
                sb.Append("<th>").Append(Esc(headers[i])).Append("</th>");
            sb.Append("</tr></thead><tbody>");
            if (summaries != null)
            {
                foreach (var s in summaries)
                {
                    if (s == null || string.Equals(s.Placement, "미설계 배치 설비", StringComparison.Ordinal))
                        continue;
                    bool fail = string.Equals(s.CheckResult, "FAIL", StringComparison.OrdinalIgnoreCase);
                    sb.Append("<tr>");
                    Cell(sb, line);
                    if (ddw)
                    {
                        Cell(sb, s.MajorClass);
                        Cell(sb, s.MinorClass);
                        Cell(sb, s.UploadConstructionCode);
                    }
                    Cell(sb, s.EquipmentTag);
                    if (ddw)
                    {
                        Cell(sb, s.DesignCompany);
                        Cell(sb, s.MoveInDate);
                        Cell(sb, s.DrawingNeedDate);
                    }
                    Cell(sb, s.CheckResult, fail);
                    Cell(sb, s.CountERR.ToString(CultureInfo.InvariantCulture), fail);
                    Cell(sb, s.EquipmentCount.ToString(CultureInfo.InvariantCulture));
                    Cell(sb, s.TotalPocCount.ToString(CultureInfo.InvariantCulture));
                    Cell(sb, s.DerivedBranchCount.ToString(CultureInfo.InvariantCulture));
                    Cell(sb, s.TotalHopCount.ToString(CultureInfo.InvariantCulture));
                    Cell(sb, Math.Round(s.TotalLength, 1).ToString("0.0", CultureInfo.InvariantCulture));
                    Cell(sb, s.LatestOpTime);
                    Cell(sb, s.LatestUserName);
                    Cell(sb, s.LatestCompanyName);
                    if (ddw)
                        Cell(sb, s.Remark);
                    sb.Append("</tr>");
                }
            }
            sb.Append("</tbody></table>");
            if (drawing)
            {
                var groups = BuildGroups(details, cache);
                sb.Append("<h1>Drawing</h1>");
                sb.Append("<p class=\"hint\">드래그로 돌리고 휠로 확대합니다. 설비는 처음 1대만 켜지고 최대 3대입니다. UT와 POC로 배관을 좁힙니다. 오른쪽 위 육면체는 Top, Bottom, Front, Back, Left, Right입니다. 점 옆은 피팅 이름, 선 옆 숫자는 그 배관 길이입니다. 관경이 바뀌면 Ø로 표시합니다. TOF 옆은 Tag, NOZ 옆은 Connection Tag입니다.</p>");
                sb.Append("<div class=\"draw-label\">설비 · 최대 3대</div>");
                sb.Append("<div id=\"drawEquip\" class=\"draw-picks\"></div>");
                sb.Append("<p class=\"draw-note\" id=\"drawNote\">처음에는 첫 설비만 보입니다.</p>");
                sb.Append("<div class=\"draw-label\">UT</div>");
                sb.Append("<div id=\"drawUt\" class=\"draw-legend\"></div>");
                sb.Append("<div class=\"draw-layout\"><div class=\"draw-stage\"><canvas id=\"draw3d\" width=\"1100\" height=\"720\"></canvas>");
                sb.Append("<div class=\"viewdock\" id=\"viewDock\"><div class=\"cube-scene\"><div class=\"cube\" id=\"viewCube\">");
                sb.Append("<button type=\"button\" class=\"face front\" data-view=\"front\">Front</button>");
                sb.Append("<button type=\"button\" class=\"face back\" data-view=\"back\">Back</button>");
                sb.Append("<button type=\"button\" class=\"face right\" data-view=\"right\">Right</button>");
                sb.Append("<button type=\"button\" class=\"face left\" data-view=\"left\">Left</button>");
                sb.Append("<button type=\"button\" class=\"face top\" data-view=\"top\">Top</button>");
                sb.Append("<button type=\"button\" class=\"face bottom\" data-view=\"bottom\">Bottom</button>");
                sb.Append("</div></div><div class=\"view-grid\">");
                sb.Append("<button type=\"button\" data-view=\"top\">Top</button><button type=\"button\" data-view=\"bottom\">Bottom</button>");
                sb.Append("<button type=\"button\" data-view=\"front\">Front</button><button type=\"button\" data-view=\"back\">Back</button>");
                sb.Append("<button type=\"button\" data-view=\"left\">Left</button><button type=\"button\" data-view=\"right\">Right</button>");
                sb.Append("</div></div></div>");
                sb.Append("<div class=\"draw-side\"><h2>POC STATUS</h2><table><thead><tr><th>POC</th><th>Status</th></tr></thead><tbody id=\"drawStatusBody\"></tbody></table></div></div>");
                sb.Append("<script>");
                sb.Append("var DRAW=");
                AppendScene(sb, groups);
                sb.Append(";");
                sb.Append(ViewerScript);
                sb.Append("</script>");
            }
            sb.Append("</body></html>");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        private static void DrawIso(Graphics g, SketchGroup group, int x, int y, int w, int h, Font titleFont, Font bodyFont, Brush ink)
        {
            using (var back = new SolidBrush(Color.White))
            using (var penBox = new Pen(Color.FromArgb(228, 224, 216)))
            {
                g.FillRectangle(back, x, y, w, h);
                g.DrawRectangle(penBox, x, y, w - 1, h - 1);
            }
            g.DrawString(group.Title, titleFont, ink, x + 12, y + 8);
            if (group.Segments.Count == 0 && group.Markers.Count == 0)
            {
                using (var muted = new SolidBrush(Color.FromArgb(94, 101, 99)))
                    g.DrawString("좌표가 없습니다. 캐시를 다시 저장한 뒤에 그릴 수 있습니다.", bodyFont, muted, x + 12, y + 40);
                DrawStatusTable(g, group, x + w - 228f, y + 28, 220f, bodyFont, ink);
                return;
            }

            var projected = new List<float[]>();
            void Consider(float px, float py, float pz)
            {
                float ix = IsoX(px, py);
                float iy = IsoY(px, py, pz);
                projected.Add(new[] { ix, iy });
            }
            for (int i = 0; i < group.Segments.Count; i++)
            {
                var s = group.Segments[i];
                Consider(s.X1, s.Y1, s.Z1);
                Consider(s.X2, s.Y2, s.Z2);
            }
            for (int i = 0; i < group.Markers.Count; i++)
            {
                var m = group.Markers[i];
                Consider(m.X, m.Y, m.Z);
            }
            float minX = projected[0][0], maxX = minX, minY = projected[0][1], maxY = minY;
            for (int i = 1; i < projected.Count; i++)
            {
                if (projected[i][0] < minX) minX = projected[i][0];
                if (projected[i][0] > maxX) maxX = projected[i][0];
                if (projected[i][1] < minY) minY = projected[i][1];
                if (projected[i][1] > maxY) maxY = projected[i][1];
            }
            float spanX = Math.Max(1f, maxX - minX);
            float spanY = Math.Max(1f, maxY - minY);
            const float tableW = 228f;
            float left = x + 28;
            float top = y + 36;
            float plotW = Math.Max(40f, w - tableW - 40f);
            float plotH = h - 64;
            float scale = Math.Min(plotW / spanX, plotH / spanY);

            float MapX(float px, float py) => left + (IsoX(px, py) - minX) * scale;
            float MapY(float px, float py, float pz) => top + (maxY - IsoY(px, py, pz)) * scale;

            using (var labelBrush = new SolidBrush(Color.FromArgb(8, 62, 59)))
            using (var plate = new SolidBrush(Color.FromArgb(230, 255, 255, 255)))
            {
                var taken = new List<RectangleF>();
                for (int i = 0; i < group.Segments.Count; i++)
                {
                    var s = group.Segments[i];
                    float x1 = MapX(s.X1, s.Y1);
                    float y1 = MapY(s.X1, s.Y1, s.Z1);
                    float x2 = MapX(s.X2, s.Y2);
                    float y2 = MapY(s.X2, s.Y2, s.Z2);
                    using (var pen = new Pen(LineColor(s.Color), StrokeWidth(s.SizeMm)))
                        g.DrawLine(pen, x1, y1, x2, y2);
                    if (!string.IsNullOrEmpty(s.SizeChange))
                    {
                        using (var font = MakeFont(8f, true))
                        using (var amber = new SolidBrush(Color.FromArgb(180, 83, 9)))
                            g.DrawString(s.SizeChange, font, amber, x1 + 6f, y1 - 14f);
                    }
                    float pix = (float)Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
                    if (pix >= 36f && !string.IsNullOrEmpty(s.Ut))
                    {
                        float size = pix < 90f ? 9f : (pix < 160f ? 11f : 13f);
                        using (var font = MakeFont(size, false))
                        {
                            var sizePx = g.MeasureString(s.Ut, font);
                            float tx = (x1 + x2) / 2f - sizePx.Width / 2f;
                            float ty = (y1 + y2) / 2f - sizePx.Height / 2f;
                            var box = new RectangleF(tx, ty, sizePx.Width, sizePx.Height);
                            if (!Hits(taken, box))
                            {
                                taken.Add(box);
                                g.FillRectangle(plate, box);
                                g.DrawString(s.Ut, font, labelBrush, tx, ty);
                            }
                        }
                    }
                    if (pix >= 48f && !string.IsNullOrEmpty(s.Len))
                    {
                        using (var lenFont = MakeFont(8f, false))
                        using (var muted = new SolidBrush(Color.FromArgb(110, 110, 115)))
                        {
                            var lenPx = g.MeasureString(s.Len, lenFont);
                            g.DrawString(s.Len, lenFont, muted, (x1 + x2) / 2f - lenPx.Width / 2f, (y1 + y2) / 2f + 10f);
                        }
                    }
                }
            }

            using (var mark = new SolidBrush(Color.FromArgb(10, 132, 255)))
            using (var statusInk = new SolidBrush(Color.FromArgb(10, 132, 255)))
            using (var markFont = MakeFont(10f, true))
            {
                for (int i = 0; i < group.Markers.Count; i++)
                {
                    var m = group.Markers[i];
                    float mx = MapX(m.X, m.Y);
                    float my = MapY(m.X, m.Y, m.Z);
                    g.FillEllipse(mark, mx - 4, my - 4, 8, 8);
                    if (string.IsNullOrEmpty(m.Status))
                        continue;
                    bool end = IsEndCode(m.Status);
                    float dx = 8f;
                    float dy = -8f;
                    if (m.Status == "EMP" && NearObjectName(group, m))
                        dy = 16f;
                    g.DrawString(string.IsNullOrEmpty(m.Label) ? m.Status : m.Label, markFont, end ? statusInk : ink, mx + dx, my + dy);
                }
            }
            DrawStatusTable(g, group, x + w - tableW, y + 28, tableW - 8, bodyFont, ink);
        }

        private static int PanelHeight(SketchGroup group)
        {
            int rows = group == null || group.Rows == null ? 0 : group.Rows.Count;
            return Math.Max(360, 72 + rows * 16);
        }

        private static bool NearObjectName(SketchGroup group, SketchMark mark)
        {
            for (int i = 0; i < group.Markers.Count; i++)
            {
                var other = group.Markers[i];
                if (other == mark || IsEndCode(other.Status))
                    continue;
                if (Math.Abs(other.X - mark.X) < 0.05f
                    && Math.Abs(other.Y - mark.Y) < 0.05f
                    && Math.Abs(other.Z - mark.Z) < 0.05f)
                    return true;
            }
            return false;
        }

        private static void DrawStatusTable(Graphics g, SketchGroup group, float x, float y, float w, Font font, Brush ink)
        {
            g.DrawString("POC", font, ink, x, y);
            g.DrawString("Status", font, ink, x + w * 0.62f, y);
            if (group.Rows == null)
                return;
            using (var blue = new SolidBrush(Color.FromArgb(10, 132, 255)))
            {
                for (int i = 0; i < group.Rows.Count; i++)
                {
                    float rowY = y + 18f + i * 16f;
                    var row = group.Rows[i];
                    g.DrawString(row.Poc ?? "", font, ink, x, rowY);
                    g.DrawString(row.Status ?? "", font, blue, x + w * 0.62f, rowY);
                }
            }
        }

        private static bool Hits(List<RectangleF> taken, RectangleF box)
        {
            for (int i = 0; i < taken.Count; i++)
            {
                if (taken[i].IntersectsWith(box))
                    return true;
            }
            return false;
        }

        private static float IsoX(float x, float y)
        {
            return (x - y) * 0.8660254f;
        }

        private static float IsoY(float x, float y, float z)
        {
            return (x + y) * 0.5f - z;
        }

        private static List<SketchGroup> BuildGroups(IList<TraceResult> details, IGraphCache cache)
        {
            var order = new List<string>();
            var map = new Dictionary<string, SketchGroup>(StringComparer.OrdinalIgnoreCase);
            int segments = 0;
            if (details == null)
                return new List<SketchGroup>();
            foreach (var result in details)
            {
                if (result == null || IsSkipped(result))
                    continue;
                string key = string.IsNullOrEmpty(result.StartEquipmentTag)
                    ? result.StartNodeGUID.ToString()
                    : result.StartEquipmentTag;
                SketchGroup group;
                if (!map.TryGetValue(key, out group))
                {
                    if (map.Count >= MaxEquipment)
                        continue;
                    group = new SketchGroup { Title = key };
                    map[key] = group;
                    order.Add(key);
                }
                Point3? prev = Locate(cache, result.StartNodeGUID);
                Point3? last = prev;
                string carryUt = result.StartPocUtilityGroup ?? "";
                string pocName = PocName(result);
                if (prev.HasValue)
                    AddNameOnce(group, prev.Value, StartCaption(result));
                AddStatusRow(group, pocName, result.STATUS ?? "");
                double lastSizeMm = 0;
                if (result.Steps != null)
                {
                    for (int i = 0; i < result.Steps.Count; i++)
                    {
                        var step = result.Steps[i];
                        if (step == null)
                            continue;
                        string ut = PipeUt(cache, result, step);
                        if (!string.IsNullOrEmpty(ut))
                            carryUt = ut;
                        double sizeMm = PipeSizeOf(cache, step);
                        string lengthText = PipeLengthText(cache, step);
                        string sizeChange = "";
                        if (sizeMm > 0 && lastSizeMm > 0 && Math.Abs(sizeMm - lastSizeMm) >= 0.5)
                            sizeChange = SizeText(lastSizeMm) + "→" + SizeText(sizeMm);
                        if (sizeMm > 0)
                            lastSizeMm = sizeMm;
                        Point3? point = Locate(cache, step.NodeId);
                        if (!point.HasValue)
                            continue;
                        if (IsFittingStep(step))
                        {
                            string fittingName = FittingLabel(step);
                            if (fittingName.Length > 0)
                                AddMark(group, point.Value, "FIT", fittingName);
                        }
                        if (prev.HasValue && segments < MaxSegments && Apart(prev.Value, point.Value))
                        {
                            string segmentUt = Short(carryUt);
                            group.Segments.Add(new SketchSeg
                            {
                                X1 = prev.Value.X, Y1 = prev.Value.Y, Z1 = prev.Value.Z,
                                X2 = point.Value.X, Y2 = point.Value.Y, Z2 = point.Value.Z,
                                Ut = segmentUt,
                                UtKey = segmentUt,
                                Poc = pocName,
                                Len = lengthText,
                                SizeMm = sizeMm,
                                SizeChange = sizeChange
                            });
                            segments++;
                        }
                        prev = point;
                        last = point;
                    }
                }
                string end = result.STATUS ?? "";
                if (IsEndCode(end) && last.HasValue)
                    AddMark(group, last.Value, end, EndCaption(result));
            }
            var list = new List<SketchGroup>();
            foreach (string key in order)
            {
                FinishSketch(map[key]);
                list.Add(map[key]);
            }
            return list;
        }

        private static string PocName(TraceResult result)
        {
            string poc = result.StartPocName;
            if (string.IsNullOrEmpty(poc))
                poc = result.StartPocNo;
            if (string.IsNullOrEmpty(poc))
                poc = Short(result.StartPocId.ToString());
            return poc ?? "";
        }

        private static string EndCaption(TraceResult result)
        {
            string status = result.STATUS ?? "";
            string extra = "";
            if (status == "TOF")
                extra = result.TargetNodeTag ?? "";
            else if (status == "NOZ")
                extra = result.TargetPocConnectionTag ?? "";
            extra = extra.Trim();
            if (extra.Length == 0)
                return status;
            if (extra.Length > 40)
                extra = extra.Substring(0, 40);
            return status + " " + extra;
        }

        private static void AddStatusRow(SketchGroup group, string poc, string status)
        {
            for (int i = 0; i < group.Rows.Count; i++)
            {
                if (string.Equals(group.Rows[i].Poc, poc, StringComparison.Ordinal)
                    && string.Equals(group.Rows[i].Status, status, StringComparison.Ordinal))
                    return;
            }
            group.Rows.Add(new SketchRow { Poc = poc, Status = status });
        }

        private static void FinishSketch(SketchGroup group)
        {
            var segs = group.Segments;
            int n = segs.Count;
            if (n == 0)
                return;
            var parent = new int[n];
            for (int i = 0; i < n; i++)
                parent[i] = i;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    if (!SameText(segs[i].Ut, segs[j].Ut) || !Touches(segs[i], segs[j]))
                        continue;
                    int a = FindRoot(parent, i);
                    int b = FindRoot(parent, j);
                    if (a != b)
                        parent[a] = b;
                }
            }
            var best = new int[n];
            var bestLen = new float[n];
            for (int i = 0; i < n; i++)
            {
                best[i] = -1;
                bestLen[i] = -1f;
            }
            for (int i = 0; i < n; i++)
            {
                int root = FindRoot(parent, i);
                float len = SegLen(segs[i]);
                if (len > bestLen[root])
                {
                    bestLen[root] = len;
                    best[root] = i;
                }
            }
            var colorOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int next = 0;
            for (int i = 0; i < n; i++)
            {
                string key = string.IsNullOrEmpty(segs[i].UtKey) ? (segs[i].Ut ?? "") : segs[i].UtKey;
                string color;
                if (!colorOf.TryGetValue(key, out color))
                {
                    color = LineColors[next % LineColors.Length];
                    next++;
                    colorOf[key] = color;
                }
                segs[i].Color = color;
                int root = FindRoot(parent, i);
                if (best[root] != i)
                    segs[i].Ut = "";
            }
        }

        private static int FindRoot(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        private static bool SameText(string a, string b)
        {
            return string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);
        }

        private static bool Touches(SketchSeg a, SketchSeg b)
        {
            return Near(a.X1, a.Y1, a.Z1, b.X1, b.Y1, b.Z1)
                || Near(a.X1, a.Y1, a.Z1, b.X2, b.Y2, b.Z2)
                || Near(a.X2, a.Y2, a.Z2, b.X1, b.Y1, b.Z1)
                || Near(a.X2, a.Y2, a.Z2, b.X2, b.Y2, b.Z2);
        }

        private static bool Near(float ax, float ay, float az, float bx, float by, float bz)
        {
            return Math.Abs(ax - bx) < 0.05f && Math.Abs(ay - by) < 0.05f && Math.Abs(az - bz) < 0.05f;
        }

        private static float SegLen(SketchSeg s)
        {
            float dx = s.X2 - s.X1;
            float dy = s.Y2 - s.Y1;
            float dz = s.Z2 - s.Z1;
            return dx * dx + dy * dy + dz * dz;
        }

        private static bool IsSkipped(TraceResult result)
        {
            return string.Equals(result.ResultLabel, "미배치설비", StringComparison.Ordinal)
                || string.Equals(result.ResultLabel, "미설계 배치 설비", StringComparison.Ordinal);
        }

        private static bool Apart(Point3 a, Point3 b)
        {
            return Math.Abs(a.X - b.X) > 0.05f || Math.Abs(a.Y - b.Y) > 0.05f || Math.Abs(a.Z - b.Z) > 0.05f;
        }

        private static string PipeUt(IGraphCache cache, TraceResult result, TraceStep step)
        {
            if (step.NodeType == null || step.NodeType.IndexOf("Pipe", StringComparison.OrdinalIgnoreCase) < 0)
                return "";
            PipeData pipe;
            if (cache != null && cache.TryGetPipe(step.NodeId, out pipe) && pipe != null && !string.IsNullOrEmpty(pipe.UtilityName))
                return pipe.UtilityName;
            if (!string.IsNullOrEmpty(step.NodeName))
                return step.NodeName;
            return result.StartPocUtilityGroup ?? "";
        }

        private static readonly string[] LineColors =
        {
            "#0c6e68", "#0a84ff", "#b45309", "#7c3aed", "#db2777",
            "#059669", "#d97706", "#2563eb", "#be123c", "#0f766e",
            "#9333ea", "#ea580c", "#0369a1", "#65a30d", "#c026d3"
        };

        private static Color LineColor(string hex)
        {
            try
            {
                if (!string.IsNullOrEmpty(hex))
                    return ColorTranslator.FromHtml(hex);
            }
            catch (Exception)
            {
            }
            return Color.FromArgb(12, 110, 104);
        }

        private static string StartCaption(TraceResult result)
        {
            if (result == null)
                return "";
            string name = "";
            if (!string.IsNullOrEmpty(result.StartNodeName))
                name = result.StartNodeName.Trim();
            else if (!string.IsNullOrEmpty(result.StartEquipmentTag))
                name = result.StartEquipmentTag.Trim();
            string sub1 = (result.StartNodeSubDivision1 ?? "").Trim();
            string sub2 = (result.StartNodeSubDivision2 ?? "").Trim();
            string text = name;
            if (sub1.Length > 0 || sub2.Length > 0)
            {
                string sub = sub1;
                if (sub2.Length > 0)
                    sub = sub.Length == 0 ? sub2 : sub + " / " + sub2;
                text = text.Length == 0 ? sub : text + "  " + sub;
            }
            if (text.Length > 60)
                text = text.Substring(0, 60);
            return text;
        }

        private static bool IsPipeStep(TraceStep step)
        {
            return step != null && step.NodeType != null
                && step.NodeType.IndexOf("Pipe", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsFittingStep(TraceStep step)
        {
            return step != null && string.Equals(step.NodeType, "Fitting", StringComparison.OrdinalIgnoreCase);
        }

        private static string FittingLabel(TraceStep step)
        {
            string name = step == null ? "" : (step.NodeName ?? "").Trim();
            if (name.Length > 28)
                name = name.Substring(0, 28);
            return name;
        }

        private static double PipeSizeOf(IGraphCache cache, TraceStep step)
        {
            PipeData pipe;
            if (!IsPipeStep(step) || cache == null || !cache.TryGetPipe(step.NodeId, out pipe) || pipe == null)
                return 0;
            return pipe.PipeSizeMm > 0 ? pipe.PipeSizeMm : 0;
        }

        private static string PipeLengthText(IGraphCache cache, TraceStep step)
        {
            PipeData pipe;
            if (!IsPipeStep(step) || cache == null || !cache.TryGetPipe(step.NodeId, out pipe) || pipe == null || pipe.Length <= 0)
                return "";
            bool millimeters = cache.DbName == null || !DdwLine.IsCacheKey(cache.DbName) || pipe.Length >= 100;
            double meters = millimeters ? pipe.Length / 1000.0 : pipe.Length;
            return meters.ToString("0.00", CultureInfo.InvariantCulture) + " m";
        }

        private static string SizeText(double millimeters)
        {
            return "Ø" + millimeters.ToString("0.#", CultureInfo.InvariantCulture);
        }

        private static float StrokeWidth(double millimeters)
        {
            if (millimeters >= 100)
                return 4.5f;
            if (millimeters >= 50)
                return 3.2f;
            if (millimeters > 0)
                return 2.2f;
            return 2f;
        }

        private static bool IsEndCode(string status)
        {
            return status == "NOZ" || status == "TOF" || status == "CAP" || status == "EMP" || status == "ING";
        }

        private static void AddNameOnce(SketchGroup group, Point3 point, string name)
        {
            if (string.IsNullOrEmpty(name))
                return;
            for (int i = 0; i < group.Markers.Count; i++)
            {
                if (!IsEndCode(group.Markers[i].Status))
                    return;
            }
            AddMark(group, point, name);
        }

        private static void AddMark(SketchGroup group, Point3 point, string text)
        {
            AddMark(group, point, text, text);
        }

        private static void AddMark(SketchGroup group, Point3 point, string status, string label)
        {
            if (string.IsNullOrEmpty(status) && string.IsNullOrEmpty(label))
                return;
            for (int i = 0; i < group.Markers.Count; i++)
            {
                var mark = group.Markers[i];
                if (mark.Status == status
                    && mark.Label == label
                    && Math.Abs(mark.X - point.X) < 0.05f
                    && Math.Abs(mark.Y - point.Y) < 0.05f
                    && Math.Abs(mark.Z - point.Z) < 0.05f)
                    return;
            }
            group.Markers.Add(new SketchMark
            {
                X = point.X,
                Y = point.Y,
                Z = point.Z,
                Status = status ?? "",
                Label = string.IsNullOrEmpty(label) ? (status ?? "") : label
            });
        }

        private static string Short(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            string text = value.Trim();
            return text.Length > 16 ? text.Substring(0, 16) : text;
        }

        private static Point3? Locate(IGraphCache cache, Guid id)
        {
            if (cache == null || id == Guid.Empty)
                return null;
            NodeInfo node;
            if (!cache.TryGetNode(id, out node) || node == null)
                return null;
            if (Math.Abs(node.PosX) < 0.05 && Math.Abs(node.PosY) < 0.05 && Math.Abs(node.PosZ) < 0.05)
                return null;
            return new Point3((float)node.PosX, (float)node.PosY, (float)node.PosZ);
        }

        private static void AppendScene(StringBuilder sb, List<SketchGroup> groups)
        {
            sb.Append("{\"groups\":[");
            for (int g = 0; g < groups.Count; g++)
            {
                if (g > 0)
                    sb.Append(',');
                var group = groups[g];
                sb.Append("{\"title\":").Append(Json(group.Title)).Append(",\"segs\":[");
                for (int i = 0; i < group.Segments.Count; i++)
                {
                    if (i > 0)
                        sb.Append(',');
                    var s = group.Segments[i];
                    sb.Append("{\"ut\":").Append(Json(s.Ut));
                    sb.Append(",\"uk\":").Append(Json(s.UtKey));
                    sb.Append(",\"poc\":").Append(Json(s.Poc));
                    sb.Append(",\"len\":").Append(Json(s.Len));
                    sb.Append(",\"sz\":").Append(s.SizeMm.ToString("0.##", CultureInfo.InvariantCulture));
                    sb.Append(",\"ch\":").Append(Json(s.SizeChange));
                    sb.Append(",\"a\":[").Append(Num(s.X1)).Append(',').Append(Num(s.Y1)).Append(',').Append(Num(s.Z1)).Append(']');
                    sb.Append(",\"b\":[").Append(Num(s.X2)).Append(',').Append(Num(s.Y2)).Append(',').Append(Num(s.Z2)).Append(']');
                    sb.Append(",\"c\":").Append(Json(s.Color)).Append('}');
                }
                sb.Append("],\"rows\":[");
                for (int i = 0; i < group.Rows.Count; i++)
                {
                    if (i > 0)
                        sb.Append(',');
                    var row = group.Rows[i];
                    sb.Append("{\"poc\":").Append(Json(row.Poc));
                    sb.Append(",\"st\":").Append(Json(row.Status)).Append('}');
                }
                sb.Append("],\"marks\":[");
                for (int i = 0; i < group.Markers.Count; i++)
                {
                    if (i > 0)
                        sb.Append(',');
                    var m = group.Markers[i];
                    sb.Append("{\"s\":").Append(Json(m.Status));
                    sb.Append(",\"l\":").Append(Json(string.IsNullOrEmpty(m.Label) ? m.Status : m.Label));
                    sb.Append(",\"p\":[").Append(Num(m.X)).Append(',').Append(Num(m.Y)).Append(',').Append(Num(m.Z)).Append("]}");
                }
                sb.Append("]}");
            }
            sb.Append("]}");
        }

        private static string Num(float value)
        {
            return value.ToString("0.0", CultureInfo.InvariantCulture);
        }

        private static string Json(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static void Cell(StringBuilder sb, string value, bool fail = false)
        {
            sb.Append(fail ? "<td class=\"fail\">" : "<td>").Append(Esc(value)).Append("</td>");
        }

        private static string Esc(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        private static Font MakeFont(float size, bool bold)
        {
            try
            {
                return new Font("Malgun Gothic", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
            }
            catch (ArgumentException)
            {
                return new Font(FontFamily.GenericSansSerif, size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
            }
        }

        private const string ViewerScript =
            "(function(){" +
            "  var cv=document.getElementById('draw3d');" +
            "  if(!cv)return;" +
            "  var ctx=cv.getContext('2d');" +
            "  var yaw=0.9,pitch=0.6,zoom=1,drag=null;" +
            "  var groups=(DRAW&&DRAW.groups)||[];" +
            "  var utPick='';" +
            "  var pocPick='';" +
            "  var maxEquip=3;" +
            "  var equip=document.getElementById('drawEquip');" +
            "  var legend=document.getElementById('drawUt');" +
            "  var note=document.getElementById('drawNote');" +
            "  var dock=document.getElementById('viewDock');" +
            "  var cube=document.getElementById('viewCube');" +
            "  var views={" +
            "    top:{yaw:0,pitch:0}," +
            "    bottom:{yaw:0,pitch:Math.PI}," +
            "    front:{yaw:0,pitch:-Math.PI/2}," +
            "    back:{yaw:Math.PI,pitch:-Math.PI/2}," +
            "    left:{yaw:Math.PI/2,pitch:-Math.PI/2}," +
            "    right:{yaw:-Math.PI/2,pitch:-Math.PI/2}" +
            "  };" +
            "  function btn(parent,text,on,click){" +
            "    var b=document.createElement('button');" +
            "    b.type='button';" +
            "    b.textContent=text;" +
            "    b.className=on?'on':'';" +
            "    b.onclick=click;" +
            "    parent.appendChild(b);" +
            "    return b;" +
            "  }" +
            "  function boxes(){return equip?equip.querySelectorAll('input'):[];}" +
            "  function checkedCount(){" +
            "    var n=0;" +
            "    var list=boxes();" +
            "    for(var i=0;i<list.length;i++) if(list[i].checked) n++;" +
            "    return n;" +
            "  }" +
            "  function groupsOn(){" +
            "    var src=[];" +
            "    var list=boxes();" +
            "    for(var i=0;i<list.length;i++){" +
            "      if(list[i].checked&&groups[Number(list[i].value)]) src.push(groups[Number(list[i].value)]);" +
            "    }" +
            "    return src;" +
            "  }" +
            "  function say(text){ if(note) note.textContent=text; }" +
            "  if(equip){" +
            "    btn(equip,'전체 선택',false,function(){" +
            "      var list=boxes();" +
            "      for(var i=0;i<list.length;i++) list[i].checked=i<maxEquip;" +
            "      pocPick='';" +
            "      say(list.length>maxEquip?'앞 3대만 켰습니다. 한 번에 3대까지입니다.':'선택한 설비를 모두 켰습니다.');" +
            "      renderLegend();" +
            "      draw();" +
            "    });" +
            "    btn(equip,'전체 해제',false,function(){" +
            "      var list=boxes();" +
            "      for(var i=0;i<list.length;i++) list[i].checked=false;" +
            "      pocPick='';" +
            "      say('설비 선택이 없습니다.');" +
            "      renderLegend();" +
            "      draw();" +
            "    });" +
            "    for(var gi=0;gi<groups.length;gi++){" +
            "      (function(i){" +
            "        var label=document.createElement('label');" +
            "        var box=document.createElement('input');" +
            "        box.type='checkbox';" +
            "        box.value=String(i);" +
            "        box.checked=i===0;" +
            "        box.onchange=function(){" +
            "          if(box.checked&&checkedCount()>maxEquip){" +
            "            box.checked=false;" +
            "            say('한 번에 3대까지 고를 수 있습니다.');" +
            "            return;" +
            "          }" +
            "          pocPick='';" +
            "          say(checkedCount()===0?'설비 선택이 없습니다.':'최대 3대까지 함께 볼 수 있습니다.');" +
            "          renderLegend();" +
            "          draw();" +
            "        };" +
            "        label.appendChild(box);" +
            "        label.appendChild(document.createTextNode(groups[i].title||(''+i)));" +
            "        equip.appendChild(label);" +
            "      })(gi);" +
            "    }" +
            "  }" +
            "  function utKey(s){return ((s&&(s.uk||s.ut))||'').toLowerCase();}" +
            "  function renderLegend(){" +
            "    if(!legend)return;" +
            "    legend.innerHTML='';" +
            "    btn(legend,'전체 UT',!utPick,function(){utPick='';renderLegend();draw();});" +
            "    var seen={};" +
            "    groupsOn().forEach(function(g){" +
            "      (g.segs||[]).forEach(function(s){" +
            "        var raw=(s&&(s.uk||s.ut))||'';" +
            "        var k=raw.toLowerCase();" +
            "        if(!k||seen[k])return;" +
            "        seen[k]={text:raw,color:s.c||'#0c6e68'};" +
            "      });" +
            "    });" +
            "    Object.keys(seen).sort().forEach(function(k){" +
            "      var b=btn(legend,seen[k].text,utPick===k,function(){" +
            "        utPick=utPick===k?'':k;" +
            "        renderLegend();" +
            "        draw();" +
            "      });" +
            "      b.style.borderColor=seen[k].color;" +
            "    });" +
            "  }" +
            "  function near(p,q){" +
            "    return Math.abs(p[0]-q[0])<0.05&&Math.abs(p[1]-q[1])<0.05&&Math.abs(p[2]-q[2])<0.05;" +
            "  }" +
            "  function pack(){" +
            "    var segs=[],marks=[],rows=[];" +
            "    groupsOn().forEach(function(g){" +
            "      (g.segs||[]).forEach(function(s){segs.push(s);});" +
            "      (g.marks||[]).forEach(function(m){marks.push(m);});" +
            "      (g.rows||[]).forEach(function(r){rows.push(r);});" +
            "    });" +
            "    var shown=segs.filter(function(s){" +
            "      if(utPick&&utKey(s)!==utPick)return false;" +
            "      if(pocPick&&s.poc!==pocPick)return false;" +
            "      return true;" +
            "    });" +
            "    var shownMarks=marks;" +
            "    if(utPick||pocPick){" +
            "      shownMarks=marks.filter(function(m){" +
            "        for(var i=0;i<shown.length;i++){" +
            "          if(near(m.p,shown[i].a)||near(m.p,shown[i].b))return true;" +
            "        }" +
            "        return false;" +
            "      });" +
            "    }" +
            "    return {segs:shown,marks:shownMarks,rows:rows};" +
            "  }" +
            "  function bounds(items){" +
            "    var b=null;" +
            "    function add(p){" +
            "      if(!b){b={x0:p[0],x1:p[0],y0:p[1],y1:p[1],z0:p[2],z1:p[2]};return;}" +
            "      b.x0=Math.min(b.x0,p[0]);b.x1=Math.max(b.x1,p[0]);" +
            "      b.y0=Math.min(b.y0,p[1]);b.y1=Math.max(b.y1,p[1]);" +
            "      b.z0=Math.min(b.z0,p[2]);b.z1=Math.max(b.z1,p[2]);" +
            "    }" +
            "    items.segs.forEach(function(s){add(s.a);add(s.b);});" +
            "    items.marks.forEach(function(m){add(m.p);});" +
            "    if(!b)b={x0:0,x1:1,y0:0,y1:1,z0:0,z1:1};" +
            "    b.cx=(b.x0+b.x1)/2;b.cy=(b.y0+b.y1)/2;b.cz=(b.z0+b.z1)/2;" +
            "    b.span=Math.max(1,b.x1-b.x0,b.y1-b.y0,b.z1-b.z0);" +
            "    return b;" +
            "  }" +
            "  function rot(p,b){" +
            "    var x=(p[0]-b.cx)/b.span,y=(p[1]-b.cy)/b.span,z=(p[2]-b.cz)/b.span;" +
            "    var c=Math.cos(yaw),s=Math.sin(yaw);" +
            "    var x1=x*c-y*s,y1=x*s+y*c;" +
            "    var cp=Math.cos(pitch),sp=Math.sin(pitch);" +
            "    return {x:x1,y:y1*cp-z*sp,z:y1*sp+z*cp};" +
            "  }" +
            "  function endCode(s){return s==='NOZ'||s==='TOF'||s==='CAP'||s==='EMP'||s==='ING'||s==='EQP'||s==='ERR';}" +
            "  function statusColor(s){if(s==='NOZ'||s==='TOF'||s==='CAP'||s==='EQP')return '#128a3e';if(s==='ING'||s==='ERR')return '#d92d20';if(s==='EMP')return '#1c1c1e';return '';}" +
            "  function statusClass(s){if(s==='NOZ'||s==='TOF'||s==='CAP'||s==='EQP')return 'st-ok';if(s==='ING'||s==='ERR')return 'st-bad';if(s==='EMP')return 'st-emp';return '';}" +
            "  function strokeWidth(s){" +
            "    var sz=Number(s&&s.sz)||0;" +
            "    if(sz>=100)return 5.5;" +
            "    if(sz>=50)return 3.6;" +
            "    if(sz>0)return 2.4;" +
            "    return 2.15;" +
            "  }" +
            "  function angNear(a,b){" +
            "    var d=Math.abs(a-b)%(Math.PI*2);" +
            "    if(d>Math.PI)d=Math.PI*2-d;" +
            "    return d<0.08;" +
            "  }" +
            "  function activeView(){" +
            "    var names=['top','bottom','front','back','left','right'];" +
            "    for(var i=0;i<names.length;i++){" +
            "      var v=views[names[i]];" +
            "      if(angNear(yaw,v.yaw)&&angNear(pitch,v.pitch))return names[i];" +
            "    }" +
            "    return '';" +
            "  }" +
            "  function spinCube(){" +
            "    if(!cube)return;" +
            "    var yd=yaw*180/Math.PI, pd=pitch*180/Math.PI;" +
            "    cube.style.transform='rotateX('+(-pd)+'deg) rotateZ('+(-yd)+'deg)';" +
            "    if(!dock)return;" +
            "    var name=activeView();" +
            "    var buttons=dock.querySelectorAll('[data-view]');" +
            "    for(var i=0;i<buttons.length;i++){" +
            "      if(buttons[i].classList) buttons[i].classList.toggle('on', buttons[i].getAttribute('data-view')===name);" +
            "    }" +
            "  }" +
            "  function draw(){" +
            "    spinCube();" +
            "    var w=cv.width,h=cv.height;" +
            "    ctx.clearRect(0,0,w,h);" +
            "    ctx.fillStyle='#fff';" +
            "    ctx.fillRect(0,0,w,h);" +
            "    var items=pack();" +
            "    var body=document.getElementById('drawStatusBody');" +
            "    if(body){" +
            "      body.innerHTML='';" +
            "      (items.rows||[]).forEach(function(r){" +
            "        var tr=document.createElement('tr');" +
            "        if(pocPick&&r.poc===pocPick) tr.className='on';" +
            "        tr.onclick=function(){pocPick=pocPick===r.poc?'':r.poc;draw();};" +
            "        var a=document.createElement('td');" +
            "        a.textContent=r.poc||'';" +
            "        var c=document.createElement('td');" +
            "        c.className=statusClass(r.st||'');" +
            "        c.textContent=r.st||'';" +
            "        tr.appendChild(a);" +
            "        tr.appendChild(c);" +
            "        body.appendChild(tr);" +
            "      });" +
            "    }" +
            "    if(!items.segs.length&&!items.marks.length){" +
            "      ctx.fillStyle='#6e6e73';" +
            "      ctx.font='16px sans-serif';" +
            "      ctx.fillText('표시할 배관이 없습니다.',24,40);" +
            "      return;" +
            "    }" +
            "    var b=bounds(items);" +
            "    var scale=Math.min(w,h)*0.72*zoom;" +
            "    var taken=[];" +
            "    function scr(p){var r=rot(p,b);return {x:w/2+r.x*scale,y:h/2-r.y*scale,z:r.z};}" +
            "    items.segs.forEach(function(s){" +
            "      var a=scr(s.a),c=scr(s.b);" +
            "      ctx.strokeStyle=s.c||'#0c6e68';" +
            "      ctx.lineWidth=strokeWidth(s);" +
            "      ctx.beginPath();ctx.moveTo(a.x,a.y);ctx.lineTo(c.x,c.y);ctx.stroke();" +
            "      if(s.ch){" +
            "        ctx.fillStyle='#b45309';" +
            "        ctx.beginPath();ctx.arc(a.x,a.y,5,0,6.3);ctx.fill();" +
            "        ctx.font='700 12px sans-serif';" +
            "        ctx.fillText(s.ch,a.x+8,a.y-8);" +
            "      }" +
            "      var dx=c.x-a.x,dy=c.y-a.y,pix=Math.sqrt(dx*dx+dy*dy);" +
            "      if(pix<28)return;" +
            "      if(s.ut){" +
            "        var size=Math.max(11,Math.min(18,pix/7));" +
            "        ctx.font=size+'px sans-serif';" +
            "        var tw=ctx.measureText(s.ut).width,th=size+4;" +
            "        var tx=(a.x+c.x)/2-tw/2,ty=(a.y+c.y)/2-th/2-8;" +
            "        var hit=false;" +
            "        for(var i=0;i<taken.length;i++){" +
            "          var t=taken[i];" +
            "          if(!(tx>t.x+t.w||t.x>tx+tw||ty>t.y+t.h||t.y>ty+th)){hit=true;break;}" +
            "        }" +
            "        if(!hit){" +
            "          taken.push({x:tx,y:ty,w:tw,h:th});" +
            "          ctx.fillStyle='rgba(255,255,255,0.9)';" +
            "          ctx.fillRect(tx-3,ty-1,tw+6,th);" +
            "          ctx.fillStyle='#083e3b';" +
            "          ctx.fillText(s.ut,tx,ty+size);" +
            "        }" +
            "      }" +
            "      if(!s.len)return;" +
            "      ctx.font='600 11px sans-serif';" +
            "      var lw=ctx.measureText(s.len).width;" +
            "      var lx=(a.x+c.x)/2-lw/2, ly=(a.y+c.y)/2+6;" +
            "      ctx.fillStyle='#6e6e73';" +
            "      ctx.fillText(s.len,lx,ly+11);" +
            "    });" +
            "    ctx.font='600 12px sans-serif';" +
            "    items.marks.forEach(function(m){" +
            "      var p=scr(m.p);" +
            "      if(m.s==='FIT'){" +
            "        ctx.fillStyle='#fff';" +
            "        ctx.strokeStyle='#0c6e68';" +
            "        ctx.lineWidth=1.6;" +
            "        ctx.beginPath();ctx.arc(p.x,p.y,3.5,0,6.3);ctx.fill();ctx.stroke();" +
            "        if(!m.l)return;" +
            "        ctx.fillStyle='#083e3b';" +
            "        ctx.fillText(m.l,p.x+8,p.y+4);" +
            "        return;" +
            "      }" +
            "      var sc=statusColor(m.s);" +
            "      ctx.fillStyle=sc||'#1c1c1e';" +
            "      ctx.beginPath();ctx.arc(p.x,p.y,4,0,6.3);ctx.fill();" +
            "      var label=m.l||m.s;" +
            "      if(!label)return;" +
            "      var dy=0;" +
            "      if(m.s==='EMP'){" +
            "        for(var i=0;i<items.marks.length;i++){" +
            "          var o=items.marks[i];" +
            "          if(!o.s||endCode(o.s)||o.s==='FIT')continue;" +
            "          if(Math.abs(o.p[0]-m.p[0])<0.05&&Math.abs(o.p[1]-m.p[1])<0.05&&Math.abs(o.p[2]-m.p[2])<0.05){dy=18;break;}" +
            "        }" +
            "      }" +
            "      ctx.fillStyle=sc||'#1c1c1e';" +
            "      ctx.fillText(label,p.x+8,p.y-4+dy);" +
            "    });" +
            "  }" +
            "  renderLegend();" +
            "  if(dock){" +
            "    dock.onclick=function(e){" +
            "      var t=e.target;" +
            "      while(t&&t!==dock&&!(t.getAttribute&&t.getAttribute('data-view'))) t=t.parentNode;" +
            "      if(!t||!t.getAttribute)return;" +
            "      var name=t.getAttribute('data-view');" +
            "      if(!views[name])return;" +
            "      yaw=views[name].yaw;" +
            "      pitch=views[name].pitch;" +
            "      draw();" +
            "    };" +
            "  }" +
            "  cv.onmousedown=function(e){drag={x:e.clientX,y:e.clientY,yaw:yaw,pitch:pitch};};" +
            "  window.addEventListener('mouseup',function(){drag=null;});" +
            "  window.addEventListener('mousemove',function(e){" +
            "    if(!drag)return;" +
            "    yaw=drag.yaw+(e.clientX-drag.x)*0.01;" +
            "    pitch=Math.max(-Math.PI/2,Math.min(Math.PI/2,drag.pitch+(e.clientY-drag.y)*0.01));" +
            "    draw();" +
            "  });" +
            "  cv.addEventListener('wheel',function(e){" +
            "    e.preventDefault();" +
            "    zoom=Math.max(0.4,Math.min(4,zoom*(e.deltaY>0?0.9:1.1)));" +
            "    draw();" +
            "  },{passive:false});" +
            "  draw();" +
            "})();";

        private struct Point3
        {
            public Point3(float x, float y, float z)
            {
                X = x;
                Y = y;
                Z = z;
            }
            public float X;
            public float Y;
            public float Z;
        }

        private sealed class SketchGroup
        {
            public string Title;
            public List<SketchSeg> Segments = new List<SketchSeg>();
            public List<SketchMark> Markers = new List<SketchMark>();
            public List<SketchRow> Rows = new List<SketchRow>();
        }

        private sealed class SketchRow
        {
            public string Poc;
            public string Status;
        }

        private sealed class SketchSeg
        {
            public float X1, Y1, Z1, X2, Y2, Z2;
            public string Ut;
            public string UtKey;
            public string Poc;
            public string Len;
            public double SizeMm;
            public string SizeChange;
            public string Color;
        }

        private sealed class SketchMark
        {
            public float X, Y, Z;
            public string Status;
            public string Label;
        }
    }
}
