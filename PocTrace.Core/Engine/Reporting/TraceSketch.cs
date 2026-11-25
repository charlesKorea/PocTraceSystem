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

        public static void WriteHtml(string path, IList<EquipmentTraceSummary> summaries, string line, IList<TraceResult> details, IGraphCache cache)
        {
            var groups = BuildGroups(details, cache);
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html lang=\"ko\"><head><meta charset=\"utf-8\" />");
            sb.Append("<title>Summary</title><style>");
            sb.Append("body{font-family:'Malgun Gothic',sans-serif;background:#f6f4ef;color:#1c1c1e;margin:24px;}");
            sb.Append("table{border-collapse:collapse;background:#fff;margin:0 0 28px;}");
            sb.Append("th,td{border:1px solid #e4e0d8;padding:6px 10px;font-size:14px;}");
            sb.Append("th{background:#e7f3f1;} canvas{width:100%;height:auto;background:#fff;border:1px solid #e4e0d8;}");
            sb.Append(".draw-layout{display:flex;gap:16px;align-items:flex-start;} .draw-layout canvas{flex:1;min-width:0;}");
            sb.Append(".draw-side{width:280px;flex:none;background:#fff;border:1px solid #e4e0d8;}");
            sb.Append(".draw-side h2{font-size:15px;margin:10px 12px;} .draw-side td.st{color:#0a84ff;font-weight:700;}");
            sb.Append("select{font:inherit;margin-left:8px;} p.hint{color:#5e6563;}");
            sb.Append("</style></head><body><h1>Summary</h1>");
            sb.Append("<table><thead><tr>");
            string[] headers = {
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
                    sb.Append("<tr>");
                    Cell(sb, line);
                    Cell(sb, s.EquipmentTag);
                    Cell(sb, s.CheckResult);
                    Cell(sb, s.CountERR.ToString(CultureInfo.InvariantCulture));
                    Cell(sb, s.EquipmentCount.ToString(CultureInfo.InvariantCulture));
                    Cell(sb, s.TotalPocCount.ToString(CultureInfo.InvariantCulture));
                    Cell(sb, s.DerivedBranchCount.ToString(CultureInfo.InvariantCulture));
                    Cell(sb, s.TotalHopCount.ToString(CultureInfo.InvariantCulture));
                    Cell(sb, Math.Round(s.TotalLength, 1).ToString("0.0", CultureInfo.InvariantCulture));
                    Cell(sb, s.LatestOpTime);
                    Cell(sb, s.LatestUserName);
                    Cell(sb, s.LatestCompanyName);
                    sb.Append("</tr>");
                }
            }
            sb.Append("</tbody></table><h1>Drawing</h1>");
            sb.Append("<p class=\"hint\">드래그로 돌리고 휠로 확대합니다. 같은 UT는 같은 색이고, 이름은 연결된 가닥 중 가장 긴 곳에 한 번만 적습니다.</p>");
            sb.Append("<p>설비<select id=\"drawPick\"></select></p>");
            sb.Append("<div class=\"draw-layout\"><canvas id=\"draw3d\" width=\"1100\" height=\"720\"></canvas>");
            sb.Append("<div class=\"draw-side\"><h2>POC Status</h2><table><thead><tr><th>POC</th><th>Status</th></tr></thead><tbody id=\"drawStatusBody\"></tbody></table></div></div>");
            sb.Append("<script>");
            sb.Append("var DRAW=");
            AppendScene(sb, groups);
            sb.Append(";");
            sb.Append(ViewerScript);
            sb.Append("</script></body></html>");
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
                    using (var pen = new Pen(LineColor(s.Color), 2f))
                        g.DrawLine(pen, x1, y1, x2, y2);
                    float pix = (float)Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
                    if (pix < 36f || string.IsNullOrEmpty(s.Ut))
                        continue;
                    float size = pix < 90f ? 9f : (pix < 160f ? 11f : 13f);
                    using (var font = MakeFont(size, false))
                    {
                        var sizePx = g.MeasureString(s.Ut, font);
                        float tx = (x1 + x2) / 2f - sizePx.Width / 2f;
                        float ty = (y1 + y2) / 2f - sizePx.Height / 2f;
                        var box = new RectangleF(tx, ty, sizePx.Width, sizePx.Height);
                        if (Hits(taken, box))
                            continue;
                        taken.Add(box);
                        g.FillRectangle(plate, box);
                        g.DrawString(s.Ut, font, labelBrush, tx, ty);
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
                    g.DrawString(m.Status, markFont, end ? statusInk : ink, mx + dx, my + dy);
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
                if (prev.HasValue)
                    AddNameOnce(group, prev.Value, ObjectName(result));
                AddStatusRow(group, result);
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
                        Point3? point = Locate(cache, step.NodeId);
                        if (!point.HasValue)
                            continue;
                        if (prev.HasValue && segments < MaxSegments && Apart(prev.Value, point.Value))
                        {
                            group.Segments.Add(new SketchSeg
                            {
                                X1 = prev.Value.X, Y1 = prev.Value.Y, Z1 = prev.Value.Z,
                                X2 = point.Value.X, Y2 = point.Value.Y, Z2 = point.Value.Z,
                                Ut = Short(carryUt)
                            });
                            segments++;
                        }
                        prev = point;
                        last = point;
                    }
                }
                string end = result.STATUS;
                if (IsEndCode(end) && last.HasValue)
                    AddMark(group, last.Value, end);
            }
            var list = new List<SketchGroup>();
            foreach (string key in order)
            {
                FinishSketch(map[key]);
                list.Add(map[key]);
            }
            return list;
        }

        private static void AddStatusRow(SketchGroup group, TraceResult result)
        {
            string poc = result.StartPocName;
            if (string.IsNullOrEmpty(poc))
                poc = result.StartPocNo;
            if (string.IsNullOrEmpty(poc))
                poc = Short(result.StartPocId.ToString());
            string status = result.STATUS ?? "";
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
                string key = string.IsNullOrEmpty(segs[i].Ut) ? "" : segs[i].Ut;
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

        private static string ObjectName(TraceResult result)
        {
            if (result == null)
                return "";
            if (!string.IsNullOrEmpty(result.StartNodeName))
                return Short(result.StartNodeName);
            return Short(result.StartEquipmentTag);
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
            if (string.IsNullOrEmpty(text))
                return;
            for (int i = 0; i < group.Markers.Count; i++)
            {
                var mark = group.Markers[i];
                if (mark.Status == text
                    && Math.Abs(mark.X - point.X) < 0.05f
                    && Math.Abs(mark.Y - point.Y) < 0.05f
                    && Math.Abs(mark.Z - point.Z) < 0.05f)
                    return;
            }
            group.Markers.Add(new SketchMark { X = point.X, Y = point.Y, Z = point.Z, Status = text });
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

        private static void Cell(StringBuilder sb, string value)
        {
            sb.Append("<td>").Append(Esc(value)).Append("</td>");
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
            "(function(){var cv=document.getElementById('draw3d');if(!cv)return;var ctx=cv.getContext('2d');var yaw=0.9,pitch=0.6,zoom=1,drag=null;" +
            "var sel=document.getElementById('drawPick');var groups=(DRAW&&DRAW.groups)||[];" +
            "function opt(v,t){var o=document.createElement('option');o.value=v;o.textContent=t;sel.appendChild(o);}" +
            "opt('-1','전체');for(var i=0;i<groups.length;i++)opt(String(i),groups[i].title||(''+i));" +
            "function pack(){var pick=Number(sel.value);var src=pick<0?groups:(groups[pick]?[groups[pick]]:[]);var segs=[],marks=[],rows=[];" +
            "src.forEach(function(g){(g.segs||[]).forEach(function(s){segs.push(s);});(g.marks||[]).forEach(function(m){marks.push(m);});(g.rows||[]).forEach(function(r){rows.push(r);});});return {segs:segs,marks:marks,rows:rows};}" +
            "function bounds(items){var b=null;function add(p){if(!b){b={x0:p[0],x1:p[0],y0:p[1],y1:p[1],z0:p[2],z1:p[2]};return;}b.x0=Math.min(b.x0,p[0]);b.x1=Math.max(b.x1,p[0]);b.y0=Math.min(b.y0,p[1]);b.y1=Math.max(b.y1,p[1]);b.z0=Math.min(b.z0,p[2]);b.z1=Math.max(b.z1,p[2]);}" +
            "items.segs.forEach(function(s){add(s.a);add(s.b);});items.marks.forEach(function(m){add(m.p);});" +
            "if(!b)b={x0:0,x1:1,y0:0,y1:1,z0:0,z1:1};b.cx=(b.x0+b.x1)/2;b.cy=(b.y0+b.y1)/2;b.cz=(b.z0+b.z1)/2;b.span=Math.max(1,b.x1-b.x0,b.y1-b.y0,b.z1-b.z0);return b;}" +
            "function rot(p,b){var x=(p[0]-b.cx)/b.span,y=(p[1]-b.cy)/b.span,z=(p[2]-b.cz)/b.span;var c=Math.cos(yaw),s=Math.sin(yaw);var x1=x*c-y*s,y1=x*s+y*c;var cp=Math.cos(pitch),sp=Math.sin(pitch);return {x:x1,y:y1*cp-z*sp,z:y1*sp+z*cp};}" +
            "function endCode(s){return s==='NOZ'||s==='TOF'||s==='CAP'||s==='EMP'||s==='ING';}" +
            "function draw(){var w=cv.width,h=cv.height;ctx.clearRect(0,0,w,h);ctx.fillStyle='#fff';ctx.fillRect(0,0,w,h);var items=pack();var body=document.getElementById('drawStatusBody');if(body){body.innerHTML='';(items.rows||[]).forEach(function(r){var tr=document.createElement('tr');var a=document.createElement('td');a.textContent=r.poc||'';var c=document.createElement('td');c.className='st';c.textContent=r.st||'';tr.appendChild(a);tr.appendChild(c);body.appendChild(tr);});}if(!items.segs.length&&!items.marks.length){ctx.fillStyle='#5e6563';ctx.font='16px sans-serif';ctx.fillText('좌표가 있는 배관이 없습니다.',24,40);return;}var b=bounds(items);var scale=Math.min(w,h)*0.72*zoom;var taken=[];" +
            "function scr(p){var r=rot(p,b);return {x:w/2+r.x*scale,y:h/2-r.y*scale,z:r.z};}" +
            "ctx.lineWidth=2;items.segs.forEach(function(s){var a=scr(s.a),c=scr(s.b);ctx.strokeStyle=s.c||'#0c6e68';ctx.beginPath();ctx.moveTo(a.x,a.y);ctx.lineTo(c.x,c.y);ctx.stroke();var dx=c.x-a.x,dy=c.y-a.y,pix=Math.sqrt(dx*dx+dy*dy);if(!s.ut||pix<42)return;var size=Math.max(11,Math.min(18,pix/7));ctx.font=size+'px sans-serif';var tw=ctx.measureText(s.ut).width,th=size+4;var tx=(a.x+c.x)/2-tw/2,ty=(a.y+c.y)/2-th/2;for(var i=0;i<taken.length;i++){var t=taken[i];if(!(tx>t.x+t.w||t.x>tx+tw||ty>t.y+t.h||t.y>ty+th))return;}taken.push({x:tx,y:ty,w:tw,h:th});ctx.fillStyle='rgba(255,255,255,0.88)';ctx.fillRect(tx-2,ty-1,tw+4,th);ctx.fillStyle='#083e3b';ctx.fillText(s.ut,tx,ty+size);});" +
            "ctx.font='700 12px sans-serif';items.marks.forEach(function(m){var p=scr(m.p);ctx.fillStyle='#0a84ff';ctx.beginPath();ctx.arc(p.x,p.y,4,0,6.3);ctx.fill();if(!m.s)return;var dy=0;if(m.s==='EMP'){for(var i=0;i<items.marks.length;i++){var o=items.marks[i];if(!o.s||endCode(o.s))continue;if(Math.abs(o.p[0]-m.p[0])<0.05&&Math.abs(o.p[1]-m.p[1])<0.05&&Math.abs(o.p[2]-m.p[2])<0.05){dy=18;break;}}}ctx.fillStyle=endCode(m.s)?'#0a84ff':'#1c1c1e';ctx.fillText(m.s,p.x+8,p.y-4+dy);});}" +
            "sel.onchange=draw;cv.onmousedown=function(e){drag={x:e.clientX,y:e.clientY,yaw:yaw,pitch:pitch};};window.addEventListener('mouseup',function(){drag=null;});" +
            "window.addEventListener('mousemove',function(e){if(!drag)return;yaw=drag.yaw+(e.clientX-drag.x)*0.01;pitch=Math.max(-1.2,Math.min(1.2,drag.pitch+(e.clientY-drag.y)*0.01));draw();});" +
            "cv.addEventListener('wheel',function(e){e.preventDefault();zoom=Math.max(0.4,Math.min(4,zoom*(e.deltaY>0?0.9:1.1)));draw();},{passive:false});draw();})();";

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
            public string Color;
        }

        private sealed class SketchMark
        {
            public float X, Y, Z;
            public string Status;
        }
    }
}
