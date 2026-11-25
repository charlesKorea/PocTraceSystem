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
    /// 설비 삽입점 기준의 평면 스케치. 배관 중심선이 아니라, 좌표가 있는 객체를 직선으로 잇는다.
    /// HTML에는 Summary와 이 그림만 넣는다.
    /// </summary>
    public static class TraceSketch
    {
        private const int MaxEquipment = 24;
        private const int MaxPoints = 400;

        public static byte[] Render(IList<TraceResult> details, IGraphCache cache)
        {
            var panels = BuildPanels(details, cache);
            int shown = Math.Min(panels.Count, MaxEquipment);
            if (shown == 0)
                shown = 1;
            const int width = 960;
            const int panelHeight = 320;
            int height = 16 + shown * (panelHeight + 12);
            using (var bmp = new Bitmap(width, height))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.FromArgb(246, 244, 239));
                using (var titleFont = MakeFont(13f, true))
                using (var bodyFont = MakeFont(12f, false))
                using (var ink = new SolidBrush(Color.FromArgb(28, 28, 30)))
                using (var muted = new SolidBrush(Color.FromArgb(94, 101, 99)))
                {
                    if (panels.Count == 0)
                    {
                        g.DrawString("좌표가 있는 배관이 없습니다.", bodyFont, muted, 24, 24);
                    }
                    for (int i = 0; i < shown && i < panels.Count; i++)
                    {
                        int top = 12 + i * (panelHeight + 12);
                        DrawPanel(g, panels[i], 16, top, width - 32, panelHeight, titleFont, bodyFont, ink, muted);
                    }
                    if (panels.Count > MaxEquipment)
                    {
                        g.DrawString("그림은 설비 " + MaxEquipment + "개까지입니다. 나머지는 Summary에만 있습니다.",
                            bodyFont, muted, 24, height - 28);
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

        public static void WriteHtml(string path, IList<EquipmentTraceSummary> summaries, string line, byte[] png)
        {
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html lang=\"ko\"><head><meta charset=\"utf-8\" />");
            sb.Append("<title>Summary</title><style>");
            sb.Append("body{font-family:'Malgun Gothic',sans-serif;background:#f6f4ef;color:#1c1c1e;margin:24px;}");
            sb.Append("table{border-collapse:collapse;background:#fff;margin:0 0 28px;}");
            sb.Append("th,td{border:1px solid #e4e0d8;padding:6px 10px;font-size:14px;}");
            sb.Append("th{background:#e7f3f1;} img{max-width:100%;height:auto;background:#fff;border:1px solid #e4e0d8;}");
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
            if (png != null && png.Length > 0)
            {
                sb.Append("<img alt=\"Drawing\" src=\"data:image/png;base64,");
                sb.Append(Convert.ToBase64String(png));
                sb.Append("\" />");
            }
            else
            {
                sb.Append("<p>좌표가 있는 배관이 없습니다.</p>");
            }
            sb.Append("</body></html>");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
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

        private static void DrawPanel(
            Graphics g, SketchPanel panel, int x, int y, int w, int h,
            Font titleFont, Font bodyFont, Brush ink, Brush muted)
        {
            using (var back = new SolidBrush(Color.White))
            using (var penBox = new Pen(Color.FromArgb(228, 224, 216)))
            {
                g.FillRectangle(back, x, y, w, h);
                g.DrawRectangle(penBox, x, y, w - 1, h - 1);
            }
            g.DrawString(panel.Title, titleFont, ink, x + 12, y + 8);
            if (panel.Paths.Count == 0)
            {
                g.DrawString("좌표가 없습니다. 캐시를 다시 저장한 뒤에 그릴 수 있습니다.", bodyFont, muted, x + 12, y + 40);
                return;
            }

            float minX = 0, minY = 0, maxX = 0, maxY = 0;
            bool any = false;
            for (int p = 0; p < panel.Paths.Count; p++)
            {
                var path = panel.Paths[p];
                for (int i = 0; i < path.Count; i++)
                {
                    if (!any)
                    {
                        minX = maxX = path[i].X;
                        minY = maxY = path[i].Y;
                        any = true;
                    }
                    else
                    {
                        if (path[i].X < minX) minX = path[i].X;
                        if (path[i].X > maxX) maxX = path[i].X;
                        if (path[i].Y < minY) minY = path[i].Y;
                        if (path[i].Y > maxY) maxY = path[i].Y;
                    }
                }
            }
            float spanX = Math.Max(1f, maxX - minX);
            float spanY = Math.Max(1f, maxY - minY);
            float left = x + 24;
            float top = y + 40;
            float plotW = w - 48;
            float plotH = h - 64;
            float scale = Math.Min(plotW / spanX, plotH / spanY);

            Color[] colors =
            {
                Color.FromArgb(12, 110, 104),
                Color.FromArgb(10, 132, 255),
                Color.FromArgb(180, 83, 9),
                Color.FromArgb(139, 92, 246)
            };
            for (int p = 0; p < panel.Paths.Count; p++)
            {
                var path = panel.Paths[p];
                if (path.Count == 0)
                    continue;
                using (var pen = new Pen(colors[p % colors.Length], 2f))
                {
                    for (int i = 1; i < path.Count; i++)
                    {
                        float x1 = left + (path[i - 1].X - minX) * scale;
                        float y1 = top + (maxY - path[i - 1].Y) * scale;
                        float x2 = left + (path[i].X - minX) * scale;
                        float y2 = top + (maxY - path[i].Y) * scale;
                        g.DrawLine(pen, x1, y1, x2, y2);
                    }
                }
                float ex = left + (path[0].X - minX) * scale;
                float ey = top + (maxY - path[0].Y) * scale;
                using (var box = new Pen(Color.FromArgb(8, 62, 59), 2f))
                    g.DrawRectangle(box, ex - 7, ey - 7, 14, 14);
            }
        }

        private static List<SketchPanel> BuildPanels(IList<TraceResult> details, IGraphCache cache)
        {
            var order = new List<string>();
            var map = new Dictionary<string, SketchPanel>(StringComparer.OrdinalIgnoreCase);
            if (details == null)
                return new List<SketchPanel>();
            foreach (var result in details)
            {
                if (result == null)
                    continue;
                string key = string.IsNullOrEmpty(result.StartEquipmentTag)
                    ? result.StartNodeGUID.ToString()
                    : result.StartEquipmentTag;
                SketchPanel panel;
                if (!map.TryGetValue(key, out panel))
                {
                    panel = new SketchPanel { Title = key };
                    map[key] = panel;
                    order.Add(key);
                }
                var path = new List<PointF>();
                AddPoint(path, Locate(cache, result.StartNodeGUID));
                if (result.Steps != null)
                {
                    foreach (var step in result.Steps)
                    {
                        if (step == null)
                            continue;
                        if (path.Count >= MaxPoints)
                            break;
                        AddPoint(path, Locate(cache, step.NodeId));
                    }
                }
                if (path.Count > 0)
                    panel.Paths.Add(path);
            }
            var list = new List<SketchPanel>();
            foreach (string key in order)
                list.Add(map[key]);
            return list;
        }

        private static void AddPoint(List<PointF> path, PointF? point)
        {
            if (!point.HasValue)
                return;
            if (path.Count > 0)
            {
                var last = path[path.Count - 1];
                if (Math.Abs(last.X - point.Value.X) < 0.05f && Math.Abs(last.Y - point.Value.Y) < 0.05f)
                    return;
            }
            path.Add(point.Value);
        }

        private static PointF? Locate(IGraphCache cache, Guid id)
        {
            if (cache == null || id == Guid.Empty)
                return null;
            NodeInfo node;
            if (!cache.TryGetNode(id, out node) || node == null)
                return null;
            if (Math.Abs(node.PosX) < 0.05 && Math.Abs(node.PosY) < 0.05 && Math.Abs(node.PosZ) < 0.05)
                return null;
            return new PointF((float)node.PosX, (float)node.PosY);
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

        private sealed class SketchPanel
        {
            public string Title;
            public List<List<PointF>> Paths = new List<List<PointF>>();
        }
    }
}
