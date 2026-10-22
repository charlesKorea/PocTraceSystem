using System;
using System.IO;
using System.Text;

namespace PocTrace.Core.Infrastructure
{
    /// <summary>
    /// Text I/O: always write UTF-8. Read UTF-8 when valid, otherwise CP949
    /// so existing Korean Windows Default-encoded files still load.
    /// </summary>
    public static class TextFileEncoding
    {
        public static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public static Encoding Cp949
        {
            get
            {
                try
                {
                    return Encoding.GetEncoding(949);
                }
                catch (ArgumentException)
                {
                    return Encoding.Default;
                }
            }
        }

        public static string ReadAllText(string path)
        {
            return Decode(File.ReadAllBytes(path));
        }

        public static string[] ReadAllLines(string path)
        {
            var text = ReadAllText(path);
            if (string.IsNullOrEmpty(text))
                return new string[0];

            var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            if (normalized.EndsWith("\n", StringComparison.Ordinal))
                normalized = normalized.Substring(0, normalized.Length - 1);
            return normalized.Split('\n');
        }

        public static void WriteAllText(string path, string contents)
        {
            File.WriteAllText(path, contents ?? string.Empty, Utf8NoBom);
        }

        public static string Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return Utf8NoBom.GetString(bytes, 3, bytes.Length - 3);

            return LooksLikeUtf8(bytes)
                ? Utf8NoBom.GetString(bytes)
                : Cp949.GetString(bytes);
        }

        private static bool LooksLikeUtf8(byte[] bytes)
        {
            int i = 0;
            while (i < bytes.Length)
            {
                byte b = bytes[i];
                if (b <= 0x7F)
                {
                    i++;
                    continue;
                }

                int extra;
                if ((b & 0xE0) == 0xC0) extra = 1;
                else if ((b & 0xF0) == 0xE0) extra = 2;
                else if ((b & 0xF8) == 0xF0) extra = 3;
                else return false;

                if (i + extra >= bytes.Length)
                    return false;
                for (int j = 1; j <= extra; j++)
                {
                    if ((bytes[i + j] & 0xC0) != 0x80)
                        return false;
                }

                i += extra + 1;
            }

            return true;
        }
    }
}
