using System;
using System.Diagnostics;
using System.IO;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text;

namespace PocTrace.Core.Infrastructure.Diagnostics
{
    /// <summary>
    /// 캐시 로드/그래프 초기화 진단 로그. Serilog가 OOM 중에 할당에 실패해도
    /// 직전 단계가 디스크에 남도록 WriteThrough로 한 줄씩 쓴다.
    /// </summary>
    public static class MemoryLoadLog
    {
        private static readonly object Gate = new object();
        private static readonly Process Current = Process.GetCurrentProcess();
        private static readonly byte[] FallbackLine = Encoding.ASCII.GetBytes(
            "memory-log format failed (out of memory)\r\n");

        private static FileStream _stream;
        private static string _path;
        private static StringBuilder _builder;
        private static int _memoryStatusLength;

        public static string FilePath
        {
            get { return _path; }
        }

        public static void Initialize(string logDirectory)
        {
            try
            {
                lock (Gate)
                {
                    if (_stream != null)
                        return;

                    if (string.IsNullOrWhiteSpace(logDirectory))
                        logDirectory = PocTraceLocalPaths.LogsDirectory("App");

                    Directory.CreateDirectory(logDirectory);
                    _path = Path.Combine(logDirectory, "memory-load.log");
                    RotateIfHuge(_path);
                    _builder = new StringBuilder(4096);
                    _stream = OpenStream(_path);
                }

                TryWritePointerNextToExe();
                Stage("MemoryLog.Ready", "path=" + _path);
            }
            catch
            {
                // 진단 로그 실패가 앱 시작을 막으면 안 된다.
            }
        }

        public static void Stage(string stage, string detail = null)
        {
            try
            {
                lock (Gate)
                {
                    EnsureOpen();
                    Emit(FormatLine(stage, detail));
                }
            }
            catch
            {
            }
        }

        public static void Fail(string source, Exception ex)
        {
            try
            {
                string typeName = ex == null ? "null" : ex.GetType().FullName;
                string message = ex == null ? string.Empty : ex.Message;
                Stage("FAIL." + (source ?? "unknown"), "type=" + typeName + " message=" + OneLine(message));

                string stack = null;
                try
                {
                    if (ex != null)
                        stack = ex.ToString();
                }
                catch
                {
                    stack = null;
                }

                if (string.IsNullOrEmpty(stack))
                    return;

                lock (Gate)
                {
                    EnsureOpen();
                    Emit(stack);
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// 큰 배열을 만들기 직전에 LOH를 한 번 압축한다.
        /// 32비트 프로세스에서 워킹셋은 남아 있는데 연속 주소가 없어 OOM이 나는 경우를 줄인다.
        /// </summary>
        public static void CompactForLargeAlloc(string stage)
        {
            Stage(stage + ".BeforeGc");
            try
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
            }
            catch (Exception ex)
            {
                Stage(stage + ".GcFailed", ex.GetType().Name);
            }

            Stage(stage + ".AfterGc");
        }

        /// <summary>
        /// count가 파일 길이보다 크면 손상된 헤더다. new List(count)는 그때 OOM만 던지고 로그가 없다.
        /// </summary>
        public static void EnsureRecordCount(string section, int count, long fileLengthBytes)
        {
            Stage(section + ".Count", "count=" + count + " fileBytes=" + fileLengthBytes);
            if (count < 0)
                throw new InvalidDataException(section + " count is negative (" + count + ").");

            if (fileLengthBytes >= 0 && (long)count > fileLengthBytes)
            {
                throw new InvalidDataException(
                    section + " count " + count + " is larger than the file (" + fileLengthBytes + " bytes).");
            }
        }

        private static void EnsureOpen()
        {
            if (_stream != null)
                return;

            string dir = PocTraceLocalPaths.LogsDirectory("App");
            _path = Path.Combine(dir, "memory-load.log");
            _builder = _builder ?? new StringBuilder(4096);
            _stream = OpenStream(_path);
        }

        private static FileStream OpenStream(string path)
        {
            return new FileStream(
                path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite,
                4096,
                FileOptions.WriteThrough);
        }

        private static void RotateIfHuge(string path)
        {
            if (!File.Exists(path))
                return;

            var info = new FileInfo(path);
            if (info.Length < 5L * 1024L * 1024L)
                return;

            string oldPath = path + ".old";
            if (File.Exists(oldPath))
                File.Delete(oldPath);
            File.Move(path, oldPath);
        }

        private static void TryWritePointerNextToExe()
        {
            try
            {
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(_path))
                    return;
                File.WriteAllText(Path.Combine(dir, "memory-load.path.txt"), _path);
            }
            catch
            {
            }
        }

        private static string FormatLine(string stage, string detail)
        {
            if (_builder == null)
                _builder = new StringBuilder(4096);

            _builder.Clear();
            _builder.Append(DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffK"));
            _builder.Append(" stage=").Append(stage ?? string.Empty);
            AppendProcess(_builder);
            AppendMachine(_builder);
            if (!string.IsNullOrEmpty(detail))
            {
                _builder.Append(" | ");
                _builder.Append(OneLine(detail));
            }

            return _builder.ToString();
        }

        private static void AppendProcess(StringBuilder sb)
        {
            sb.Append(" procBits=").Append(Environment.Is64BitProcess ? 64 : 32);
            sb.Append(" os64=").Append(Environment.Is64BitOperatingSystem ? 1 : 0);
            try
            {
                Current.Refresh();
                sb.Append(" wsMB=").Append(Current.WorkingSet64 / (1024L * 1024L));
                sb.Append(" privateMB=").Append(Current.PrivateMemorySize64 / (1024L * 1024L));
                sb.Append(" virtualMB=").Append(Current.VirtualMemorySize64 / (1024L * 1024L));
            }
            catch
            {
                sb.Append(" wsMB=? privateMB=? virtualMB=?");
            }

            try
            {
                sb.Append(" gcMB=").Append(GC.GetTotalMemory(false) / (1024L * 1024L));
            }
            catch
            {
                sb.Append(" gcMB=?");
            }
        }

        private static void AppendMachine(StringBuilder sb)
        {
            MEMORYSTATUSEX status;
            if (!TryMemoryStatus(out status))
            {
                sb.Append(" physLoadPct=? availPhysMB=? availPageMB=? availVirtualMB=? totalVirtualMB=?");
                return;
            }

            sb.Append(" physLoadPct=").Append(status.dwMemoryLoad);
            sb.Append(" availPhysMB=").Append(status.ullAvailPhys / (1024UL * 1024UL));
            sb.Append(" availPageMB=").Append(status.ullAvailPageFile / (1024UL * 1024UL));
            sb.Append(" availVirtualMB=").Append(status.ullAvailVirtual / (1024UL * 1024UL));
            sb.Append(" totalVirtualMB=").Append(status.ullTotalVirtual / (1024UL * 1024UL));
        }

        private static bool TryMemoryStatus(out MEMORYSTATUSEX status)
        {
            status = new MEMORYSTATUSEX();
            try
            {
                if (_memoryStatusLength == 0)
                    _memoryStatusLength = Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                status.dwLength = (uint)_memoryStatusLength;
                return GlobalMemoryStatusEx(ref status);
            }
            catch
            {
                return false;
            }
        }

        private static void Emit(string text)
        {
            if (_stream == null || text == null)
                return;

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                _stream.Write(bytes, 0, bytes.Length);
                if (bytes.Length == 0 || bytes[bytes.Length - 1] != (byte)'\n')
                {
                    _stream.WriteByte((byte)'\r');
                    _stream.WriteByte((byte)'\n');
                }

                _stream.Flush(true);
            }
            catch (OutOfMemoryException)
            {
                try
                {
                    _stream.Write(FallbackLine, 0, FallbackLine.Length);
                    _stream.Flush(true);
                }
                catch
                {
                }
            }
        }

        private static string OneLine(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text.Replace("\r", " ").Replace("\n", " ");
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
    }
}
