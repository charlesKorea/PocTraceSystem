using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PocTrace.Demolition;

namespace PocTrace.Web
{
    /// <summary>
    /// 127.0.0.1 에서만 받는다. 브라우저와 이 프로그램은 같은 SVDI 세션 안에 있다.
    /// </summary>
    internal sealed class PortalServer
    {
        private readonly PortalService _portal;
        private readonly string _wwwroot;
        private HttpListener _listener;
        private string _prefix;

        public PortalServer(PortalService portal)
        {
            _portal = portal;
            _wwwroot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");
        }

        public string Start(string host, int preferredPort)
        {
            host = string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host.Trim();
            Exception last = null;
            for (int port = preferredPort; port < preferredPort + 20; port++)
            {
                var listener = new HttpListener();
                string prefix = "http://" + host + ":" + port + "/";
                listener.Prefixes.Add(prefix);
                try
                {
                    listener.Start();
                    _listener = listener;
                    _prefix = prefix;
                    return prefix;
                }
                catch (Exception ex)
                {
                    last = ex;
                    listener.Close();
                }
            }

            throw new InvalidOperationException(
                "웹 포트를 열지 못했습니다. " + preferredPort + "부터 20개를 시도했습니다. " + (last == null ? "" : last.Message),
                last);
        }

        public void RunUntilEnter()
        {
            var thread = new Thread(Loop) { IsBackground = true, Name = "PocTrace.Web" };
            thread.Start();
            Console.WriteLine("종료하려면 Enter.");
            Console.ReadLine();
            try { _listener.Stop(); } catch { /* shutting down */ }
        }

        private void Loop()
        {
            while (_listener != null && _listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = _listener.GetContext();
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        Handle(context);
                    }
                    catch (Exception ex)
                    {
                        WriteRaw(context, 500, "{\"error\":" + Quote(ex.Message) + "}");
                    }
                });
            }
        }

        private void Handle(HttpListenerContext context)
        {
            try
            {
                string path = context.Request.Url.AbsolutePath.TrimEnd('/');
                if (path.Length == 0)
                    path = "/";
                string method = context.Request.HttpMethod.ToUpperInvariant();

                if (method == "GET" && (path == "/" || path == "/index.html"))
                {
                    WriteFile(context, Path.Combine(_wwwroot, "index.html"), "text/html; charset=utf-8");
                    return;
                }

                if (method == "GET" && path == "/history.html")
                {
                    WriteFile(context, Path.Combine(_wwwroot, "history.html"), "text/html; charset=utf-8");
                    return;
                }

                if (method == "GET" && path == "/api/license")
                {
                    WriteJson(context, 200, _portal.GetLicenseView());
                    return;
                }

                if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
                {
                    if (!_portal.HasLicense)
                    {
                        WriteJson(context, 403, Err("라이선스가 없습니다.", "没有许可证。"));
                        return;
                    }

                    bool extraTool = path.StartsWith("/api/demolition", StringComparison.OrdinalIgnoreCase)
                        || path.StartsWith("/api/nozzle-traces", StringComparison.OrdinalIgnoreCase);
                    if (extraTool && !_portal.CanUseExtraTools)
                    {
                        WriteJson(context, 403, Err(
                            "철거검수와 노즐추적은 2차 권한에서 사용할 수 없습니다.",
                            "拆除检验和喷嘴追踪不能在二次权限下使用。"));
                        return;
                    }
                }

                if (method == "GET" && path == "/api/lines")
                {
                    WriteJson(context, 200, _portal.GetLines());
                    return;
                }

                if (method == "GET" && path == "/api/prefs")
                {
                    WriteJson(context, 200, _portal.GetPrefs());
                    return;
                }

                if (method == "POST" && path == "/api/prefs")
                {
                    var body = ReadJson(context.Request);
                    _portal.SavePrefs(Value(body, "lang"), Value(body, "theme"));
                    WriteJson(context, 200, _portal.GetPrefs());
                    return;
                }

                if (method == "GET" && path == "/api/history")
                {
                    WriteJson(context, 200, _portal.GetHistory());
                    return;
                }

                if (method == "POST" && path == "/api/cache")
                {
                    var body = ReadJson(context.Request);
                    string id = _portal.StartCacheLoad(Value(body, "db"));
                    WriteJson(context, 202, new { jobId = id });
                    return;
                }

                if (method == "POST" && path == "/api/traces")
                {
                    var body = ReadJson(context.Request);
                    string id = _portal.StartTrace(Value(body, "db"), Value(body, "tag"));
                    WriteJson(context, 202, new { jobId = id });
                    return;
                }

                if (method == "POST" && path == "/api/batches")
                {
                    var body = ReadJson(context.Request);
                    string id = _portal.StartBatch(Value(body, "db"), Value(body, "text"));
                    WriteJson(context, 202, new { jobId = id });
                    return;
                }

                if (method == "POST" && path == "/api/demolition/preview")
                {
                    object draft;
                    if (IsMultipart(context.Request))
                    {
                        string uploadName;
                        byte[] upload = ReadUpload(context.Request, out uploadName);
                        draft = _portal.PreviewDemolitionUpload(upload, uploadName);
                    }
                    else
                    {
                        draft = _portal.PreviewDemolitionText(Value(ReadJson(context.Request), "text"));
                    }
                    WriteJson(context, 200, draft);
                    return;
                }

                if (method == "POST" && path == "/api/demolition")
                {
                    var body = ReadJson(context.Request);
                    string id = _portal.StartDemolition(Value(body, "draftId"), Value(body, "text"));
                    WriteJson(context, 202, new { jobId = id });
                    return;
                }

                if (method == "POST" && path == "/api/nozzle-traces")
                {
                    var body = ReadJson(context.Request);
                    string id = _portal.StartNozzle(Value(body, "db"), Value(body, "keyKind"), Value(body, "text"));
                    WriteJson(context, 202, new { jobId = id });
                    return;
                }

                if (method == "GET" && path == "/api/jobs")
                {
                    WriteJson(context, 200, _portal.ListJobs());
                    return;
                }

                if (path.StartsWith("/api/jobs/", StringComparison.OrdinalIgnoreCase))
                {
                    string rest = path.Substring("/api/jobs/".Length);
                    bool excel = rest.EndsWith("/excel", StringComparison.OrdinalIgnoreCase);
                    bool html = rest.EndsWith("/html", StringComparison.OrdinalIgnoreCase);
                    bool cancel = rest.EndsWith("/cancel", StringComparison.OrdinalIgnoreCase);
                    string id = rest;
                    if (excel) id = rest.Substring(0, rest.Length - "/excel".Length);
                    else if (html) id = rest.Substring(0, rest.Length - "/html".Length);
                    else if (cancel) id = rest.Substring(0, rest.Length - "/cancel".Length);

                    if (method == "POST" && cancel)
                    {
                        if (_portal.FindJob(id) == null)
                        {
                            WriteJson(context, 404, Err("작업을 찾을 수 없습니다.", "找不到作业。"));
                            return;
                        }
                        _portal.CancelJob(id);
                        WriteJson(context, 200, new { ok = true });
                        return;
                    }

                    if (method != "GET")
                    {
                        WriteJson(context, 404, Err("없는 주소입니다.", "没有这个地址。"));
                        return;
                    }

                    JobRecord job = _portal.FindJob(id);
                    if (job == null)
                    {
                        WriteJson(context, 404, Err("작업을 찾을 수 없습니다.", "找不到作业。"));
                        return;
                    }

                    if (excel)
                    {
                        if (string.IsNullOrEmpty(job.ExcelPath) || !File.Exists(job.ExcelPath))
                        {
                            WriteJson(context, 404, Err("Excel 파일이 아직 없습니다.", "Excel 文件还不存在。"));
                            return;
                        }

                        WriteFile(context, job.ExcelPath, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            Path.GetFileName(job.ExcelPath));
                        return;
                    }

                    if (html)
                    {
                        if (string.IsNullOrEmpty(job.HtmlPath) || !File.Exists(job.HtmlPath))
                        {
                            WriteJson(context, 404, Err("HTML 요약이 아직 없습니다.", "HTML 摘要还不存在。"));
                            return;
                        }

                        WriteFile(context, job.HtmlPath, "text/html; charset=utf-8", Path.GetFileName(job.HtmlPath));
                        return;
                    }

                    object view = _portal.ClientView(id);
                    if (view == null)
                    {
                        WriteJson(context, 404, Err("작업을 찾을 수 없습니다.", "找不到作业。"));
                        return;
                    }
                    WriteJson(context, 200, view);
                    return;
                }

                WriteJson(context, 404, Err("없는 주소입니다.", "没有这个地址。"));
            }
            catch (PortalInputException ex)
            {
                WriteJson(context, 400, new { error = ex.Message });
            }
            catch (PortalBusyException ex)
            {
                WriteJson(context, 409, new { error = ex.Message });
            }
            catch (Exception ex)
            {
                WriteRaw(context, 500, "{\"error\":" + Quote(ex.Message) + "}");
            }
        }

        private static object Err(string korean, string chinese)
        {
            return new { error = DemolitionUiText.Kz(korean, chinese) };
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ") + "\"";
        }

        private static bool IsMultipart(HttpListenerRequest request)
        {
            string contentType = request.ContentType ?? "";
            return contentType.IndexOf("multipart/form-data", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static byte[] ReadUpload(HttpListenerRequest request, out string fileName)
        {
            const int maxBytes = 8 * 1024 * 1024;
            if (request.ContentLength64 > maxBytes)
                throw new PortalInputException("파일은 8MB까지입니다.");

            byte[] body = ReadBodyLimited(request, maxBytes);
            return ExtractMultipartFile(body, request.ContentType, out fileName);
        }

        private static byte[] ReadBodyLimited(HttpListenerRequest request, int maxBytes)
        {
            using (var stream = new MemoryStream())
            {
                var buffer = new byte[8192];
                int total = 0;
                int read;
                while ((read = request.InputStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    total += read;
                    if (total > maxBytes)
                        throw new PortalInputException("파일은 8MB까지입니다.");
                    stream.Write(buffer, 0, read);
                }
                return stream.ToArray();
            }
        }

        private static byte[] ExtractMultipartFile(byte[] body, string contentType, out string fileName)
        {
            fileName = "upload.xlsx";
            var match = Regex.Match(contentType ?? "", "boundary=\"?([^\";]+)\"?", RegexOptions.IgnoreCase);
            if (!match.Success)
                throw new PortalInputException("업로드 형식을 읽지 못했습니다.");

            string boundary = match.Groups[1].Value.Trim();
            byte[] start = Encoding.UTF8.GetBytes("--" + boundary);
            int pos = IndexOf(body, start, 0);
            if (pos < 0)
                throw new PortalInputException("업로드 형식을 읽지 못했습니다.");

            int headerStart = pos + start.Length;
            if (headerStart + 1 < body.Length && body[headerStart] == (byte)'\r' && body[headerStart + 1] == (byte)'\n')
                headerStart += 2;

            byte[] headerSep = Encoding.UTF8.GetBytes("\r\n\r\n");
            int headerEnd = IndexOf(body, headerSep, headerStart);
            if (headerEnd < 0)
                throw new PortalInputException("업로드 형식을 읽지 못했습니다.");

            string headers = Encoding.UTF8.GetString(body, headerStart, headerEnd - headerStart);
            var name = Regex.Match(headers, "filename=\"([^\"]*)\"", RegexOptions.IgnoreCase);
            if (!name.Success)
                name = Regex.Match(headers, "filename\\*=UTF-8''([^\\r\\n;]+)", RegexOptions.IgnoreCase);
            if (name.Success && name.Groups[1].Value.Length > 0)
                fileName = Path.GetFileName(Uri.UnescapeDataString(name.Groups[1].Value.Trim()));

            int dataStart = headerEnd + headerSep.Length;
            byte[] next = Encoding.UTF8.GetBytes("\r\n--" + boundary);
            int dataEnd = IndexOf(body, next, dataStart);
            if (dataEnd < 0)
                dataEnd = body.Length;
            int length = dataEnd - dataStart;
            if (length <= 0)
                throw new PortalInputException("파일이 비어 있습니다.");

            var file = new byte[length];
            Buffer.BlockCopy(body, dataStart, file, 0, length);
            return file;
        }

        private static int IndexOf(byte[] buffer, byte[] pattern, int start)
        {
            if (buffer == null || pattern == null || pattern.Length == 0 || start < 0)
                return -1;
            int last = buffer.Length - pattern.Length;
            for (int i = start; i <= last; i++)
            {
                int j = 0;
                for (; j < pattern.Length; j++)
                {
                    if (buffer[i + j] != pattern[j])
                        break;
                }
                if (j == pattern.Length)
                    return i;
            }
            return -1;
        }

        private static JObject ReadJson(HttpListenerRequest request)
        {
            if (!request.HasEntityBody)
                return new JObject();
            using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
            {
                string text = reader.ReadToEnd();
                if (string.IsNullOrWhiteSpace(text))
                    return new JObject();
                return JObject.Parse(text);
            }
        }

        private static string Value(JObject body, string name)
        {
            var token = body[name];
            return token == null ? "" : token.ToString();
        }

        private static void WriteRaw(HttpListenerContext context, int status, string json)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json ?? "{}");
                context.Response.StatusCode = status;
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                context.Response.OutputStream.Close();
            }
            catch
            {
                try { context.Response.Abort(); } catch { /* client already gone */ }
            }
        }

        private static void WriteJson(HttpListenerContext context, int status, object payload)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload));
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.OutputStream.Close();
        }

        private static void WriteFile(HttpListenerContext context, string path, string contentType, string downloadName = null)
        {
            if (!File.Exists(path))
            {
                WriteJson(context, 404, Err("파일을 찾을 수 없습니다.", "找不到文件。"));
                return;
            }

            byte[] bytes = File.ReadAllBytes(path);
            context.Response.StatusCode = 200;
            context.Response.ContentType = contentType;
            context.Response.ContentLength64 = bytes.Length;
            if (!string.IsNullOrEmpty(downloadName))
                context.Response.AddHeader("Content-Disposition", "attachment; filename=\"" + downloadName + "\"");
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.OutputStream.Close();
        }
    }
}
