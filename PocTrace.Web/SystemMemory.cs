using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace PocTrace.Web
{
    /// <summary>
    /// 상단에 보여 줄 메모리 정보. 캐시 파일 크기에 배수를 곱한 값은 "예상"이다.
    /// 실제 점유는 메모리 로드 후 현재 프로세스 값으로 바뀐다.
    /// </summary>
    internal static class SystemMemory
    {
        private const double MegaByte = 1024.0 * 1024.0;
        private const double BaseOverheadMb = 300;
        private const int EstimateCacheSeconds = 60;

        private static readonly object Gate = new object();
        private static readonly Dictionary<string, CachedEstimate> Estimates =
            new Dictionary<string, CachedEstimate>(StringComparer.OrdinalIgnoreCase);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private sealed class MemoryStatusEx
        {
            public uint dwLength = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx buffer);

        private sealed class CachedEstimate
        {
            public DateTime At;
            public double CacheMb;
        }

        public static bool TryReadPhysical(out double totalMb, out double availableMb)
        {
            totalMb = 0;
            availableMb = 0;
            try
            {
                var status = new MemoryStatusEx();
                if (!GlobalMemoryStatusEx(status))
                    return false;
                totalMb = status.ullTotalPhys / MegaByte;
                availableMb = status.ullAvailPhys / MegaByte;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static double ProcessPrivateMb()
        {
            try
            {
                using (Process current = Process.GetCurrentProcess())
                    return current.PrivateMemorySize64 / MegaByte;
            }
            catch
            {
                return 0;
            }
        }

        public static double CacheFolderMb(string directory)
        {
            if (string.IsNullOrEmpty(directory))
                return 0;

            lock (Gate)
            {
                CachedEstimate hit;
                if (Estimates.TryGetValue(directory, out hit)
                    && (DateTime.UtcNow - hit.At).TotalSeconds < EstimateCacheSeconds)
                    return hit.CacheMb;
            }

            double mb = 0;
            try
            {
                if (Directory.Exists(directory))
                {
                    long bytes = 0;
                    foreach (string file in Directory.GetFiles(directory))
                    {
                        try { bytes += new FileInfo(file).Length; }
                        catch { /* 한 파일을 못 읽어도 나머지로 추정 */ }
                    }
                    mb = bytes / MegaByte;
                }
            }
            catch
            {
                mb = 0;
            }

            lock (Gate)
            {
                Estimates[directory] = new CachedEstimate { At = DateTime.UtcNow, CacheMb = mb };
            }
            return mb;
        }

        public static double EstimateMb(double cacheMb, double factor)
        {
            if (cacheMb <= 0)
                return 0;
            return Math.Round(cacheMb * factor + BaseOverheadMb, 0);
        }
    }
}
