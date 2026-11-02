using System;
using System.Text.RegularExpressions;

namespace PocTrace.Web
{
    /// <summary>
    /// 공용 기록 폴더(Y 드라이브) 경로가 어느 화면에도 나가지 않게 응답 문자열에서 가린다.
    /// </summary>
    internal static class PathMask
    {
        public const string Replacement = "[Shared]";

        private static readonly Regex YDrive = new Regex(
            @"(?<![A-Za-z0-9])[Yy]:(?:\\\\|\\|/)[^\s""',;)\]]*",
            RegexOptions.Compiled);

        private static readonly Regex Tail = new Regex(
            @"\[Shared\](?:\\\\|\\|/)[^\s""',;)\]]*",
            RegexOptions.Compiled);

        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            string root = null;
            try { root = SharedHistoryStore.RootPath; }
            catch { }

            if (!string.IsNullOrWhiteSpace(root))
            {
                root = root.TrimEnd('\\', '/');
                text = ReplaceAll(text, root);
                text = ReplaceAll(text, root.Replace("\\", "\\\\"));
                text = ReplaceAll(text, root.Replace('\\', '/'));
            }
            text = YDrive.Replace(text, Replacement);
            return Tail.Replace(text, Replacement);
        }

        private static string ReplaceAll(string text, string find)
        {
            if (string.IsNullOrEmpty(find))
                return text;
            int index = text.IndexOf(find, StringComparison.OrdinalIgnoreCase);
            while (index >= 0)
            {
                text = text.Substring(0, index) + Replacement + text.Substring(index + find.Length);
                index = text.IndexOf(find, index + Replacement.Length, StringComparison.OrdinalIgnoreCase);
            }
            return text;
        }
    }
}
