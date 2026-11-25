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
            int shown = groups.Count == 0 ? 1 : groups.Count;
            const int width = 960;
            const int panelHeight = 360;
            int height = 16 + shown * (panelHeight + 12);
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
                    for (int i = 0; i < groups.Count; i++)
                    {
                        int top = 12 + i * (panelHeight + 12);
                        DrawIso(g, groups[i], 16, top, width - 32, panelHeight, titleFont, bodyFont, ink);
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
            sb.Append("<p class=\"hint\">드래그로 돌리고 휠로 확대합니다. 짧은 가닥과 겹치는 UT 글자는 생략합니다.</p>");
            sb.Append("<p>설비<select id=\"drawPick\"></select></p>");
            sb.Append("<canvas id=\"draw3d\" width=\"1400\" height=\"720\"></canvas>");
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
            float left = x + 36;
            float top = y + 36;
            float plotW = w - 72;
            float plotH = h - 64;
            float scale = Math.Min(plotW / spanX, plotH / spanY);

            float MapX(float px, float py) => left + (IsoX(px, py) - minX) * scale;
            float MapY(float px, float py, float pz) => top + (maxY - IsoY(px, py, pz)) * scale;

            using (var pen = new Pen(Color.FromArgb(12, 110, 104), 2f))
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
            using (var markFont = MakeFont(10f, true))
            {
                for (int i = 0; i < group.Markers.Count; i++)
                {
                    var m = group.Markers[i];
                    float mx = MapX(m.X, m.Y);
                    float my = MapY(m.X, m.Y, m.Z);
                    g.FillEllipse(mark, mx - 4, my - 4, 8, 8);
                    if (!string.IsNullOrEmpty(m.Status))
                        g.DrawString(m.Status, markFont, ink, mx + 6, my - 8);
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
                string carryUt = result.StartPocUtilityGroup ?? "";
                if (prev.HasValue)
                    group.Markers.Add(new SketchMark { X = prev.Value.X, Y = prev.Value.Y, Z = prev.Value.Z, Status = "EQP" });
                if (result.Steps == null)
                    continue;
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
                    string status = StatusOf(step, result, i == result.Steps.Count - 1);
                    if (!string.IsNullOrEmpty(status))
                        group.Markers.Add(new SketchMark { X = point.Value.X, Y = point.Value.Y, Z = point.Value.Z, Status = status });
                    prev = point;
                }
            }
            var list = new List<SketchGroup>();
            foreach (string key in order)
                list.Add(map[key]);
            return list;
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

        private static string StatusOf(TraceStep step, TraceResult result, bool last)
        {
            if (last && result != null)
            {
                string end = result.STATUS;
                if (end == "NOZ" || end == "TOF" || end == "CAP" || end == "EMP" || end == "EQP" || end == "ING")
                    return end;
            }
            string type = step.NodeType ?? "";
            if (type.IndexOf("Nozzle", StringComparison.OrdinalIgnoreCase) >= 0)
                return "NOZ";
            if (type.IndexOf("Takeoff", StringComparison.OrdinalIgnoreCase) >= 0)
                return "TOF";
            if (type.IndexOf("Fitting", StringComparison.OrdinalIgnoreCase) >= 0)
                return "FIT";
            if (type.IndexOf("Equipment", StringComparison.OrdinalIgnoreCase) >= 0)
                return "EQP";
            return "";
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
                    sb.Append(",\"b\":[").Append(Num(s.X2)).Append(',').Append(Num(s.Y2)).Append(',').Append(Num(s.Z2)).Append("]}");
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
            "function pack(){var pick=Number(sel.value);var src=pick<0?groups:(groups[pick]?[groups[pick]]:[]);var segs=[],marks=[];" +
            "src.forEach(function(g){(g.segs||[]).forEach(function(s){segs.push(s);});(g.marks||[]).forEach(function(m){marks.push(m);});});return {segs:segs,marks:marks};}" +
            "function bounds(items){var b=null;function add(p){if(!b){b={x0:p[0],x1:p[0],y0:p[1],y1:p[1],z0:p[2],z1:p[2]};return;}b.x0=Math.min(b.x0,p[0]);b.x1=Math.max(b.x1,p[0]);b.y0=Math.min(b.y0,p[1]);b.y1=Math.max(b.y1,p[1]);b.z0=Math.min(b.z0,p[2]);b.z1=Math.max(b.z1,p[2]);}" +
            "items.segs.forEach(function(s){add(s.a);add(s.b);});items.marks.forEach(function(m){add(m.p);});" +
            "if(!b)b={x0:0,x1:1,y0:0,y1:1,z0:0,z1:1};b.cx=(b.x0+b.x1)/2;b.cy=(b.y0+b.y1)/2;b.cz=(b.z0+b.z1)/2;b.span=Math.max(1,b.x1-b.x0,b.y1-b.y0,b.z1-b.z0);return b;}" +
            "function rot(p,b){var x=(p[0]-b.cx)/b.span,y=(p[1]-b.cy)/b.span,z=(p[2]-b.cz)/b.span;var c=Math.cos(yaw),s=Math.sin(yaw);var x1=x*c-y*s,y1=x*s+y*c;var cp=Math.cos(pitch),sp=Math.sin(pitch);return {x:x1,y:y1*cp-z*sp,z:y1*sp+z*cp};}" +
            "function draw(){var w=cv.width,h=cv.height;ctx.clearRect(0,0,w,h);ctx.fillStyle='#fff';ctx.fillRect(0,0,w,h);var items=pack();if(!items.segs.length&&!items.marks.length){ctx.fillStyle='#5e6563';ctx.font='16px sans-serif';ctx.fillText('좌표가 있는 배관이 없습니다.',24,40);return;}var b=bounds(items);var scale=Math.min(w,h)*0.72*zoom;var taken=[];" +
            "function scr(p){var r=rot(p,b);return {x:w/2+r.x*scale,y:h/2-r.y*scale,z:r.z};}" +
            "ctx.lineWidth=2;ctx.strokeStyle='#0c6e68';items.segs.forEach(function(s){var a=scr(s.a),c=scr(s.b);ctx.beginPath();ctx.moveTo(a.x,a.y);ctx.lineTo(c.x,c.y);ctx.stroke();var dx=c.x-a.x,dy=c.y-a.y,pix=Math.sqrt(dx*dx+dy*dy);if(!s.ut||pix<42)return;var size=Math.max(11,Math.min(18,pix/7));ctx.font=size+'px sans-serif';var tw=ctx.measureText(s.ut).width,th=size+4;var tx=(a.x+c.x)/2-tw/2,ty=(a.y+c.y)/2-th/2;for(var i=0;i<taken.length;i++){var t=taken[i];if(!(tx>t.x+t.w||t.x>tx+tw||ty>t.y+t.h||t.y>ty+th))return;}taken.push({x:tx,y:ty,w:tw,h:th});ctx.fillStyle='rgba(255,255,255,0.88)';ctx.fillRect(tx-2,ty-1,tw+4,th);ctx.fillStyle='#083e3b';ctx.fillText(s.ut,tx,ty+size);});" +
            "ctx.font='700 12px sans-serif';items.marks.forEach(function(m){var p=scr(m.p);ctx.fillStyle='#0a84ff';ctx.beginPath();ctx.arc(p.x,p.y,4,0,6.3);ctx.fill();if(m.s){ctx.fillStyle='#1c1c1e';ctx.fillText(m.s,p.x+7,p.y-4);}});}" +
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
        }

        private sealed class SketchSeg
        {
            public float X1, Y1, Z1, X2, Y2, Z2;
            public string Ut;
        }

        private sealed class SketchMark
        {
            public float X, Y, Z;
            public string Status;
        }
    }
}
