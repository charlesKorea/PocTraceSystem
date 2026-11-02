using System;

namespace PocTrace.Web
{
    /// <summary>
    /// 캐시 로드 중 Core가 보내는 문구를 단계 번호와 진행률로 바꾼다.
    /// Core는 퍼센트를 주지 않으므로 단계마다 대략적인 비중을 정해 둔다.
    /// 같은 단계가 다시 와도 값은 줄지 않는다.
    /// </summary>
    internal static class CacheLoadStages
    {
        internal sealed class Stage
        {
            public string Key;
            public int Percent;
            public string Ko;
            public string Zh;
        }

        public static readonly Stage Open = new Stage
        {
            Key = "open", Percent = 4,
            Ko = "캐시 파일을 여는 중", Zh = "正在打开缓存文件"
        };

        public static readonly Stage UtMapping = new Stage
        {
            Key = "ut", Percent = 68,
            Ko = "UT 매핑을 읽는 중", Zh = "正在读取 UT 映射"
        };

        private static readonly Stage[] Table =
        {
            new Stage { Key = "legacy", Percent = 20, Ko = "이전 형식 캐시를 읽는 중", Zh = "正在读取旧格式缓存" },
            new Stage { Key = "nodes", Percent = 10, Ko = "Nodes 읽는 중", Zh = "正在读取 Nodes" },
            new Stage { Key = "pocs", Percent = 28, Ko = "POC 읽는 중", Zh = "正在读取 POC" },
            new Stage { Key = "pipes", Percent = 42, Ko = "Pipes 읽는 중", Zh = "正在读取 Pipes" },
            new Stage { Key = "takeoffs", Percent = 54, Ko = "Takeoffs 읽는 중", Zh = "正在读取 Takeoffs" },
            new Stage { Key = "dynprops", Percent = 61, Ko = "Hookup 속성 읽는 중", Zh = "正在读取 Hookup 属性" },
            new Stage { Key = "files", Percent = 66, Ko = "캐시 파일 읽기 완료", Zh = "缓存文件读取完成" },
            new Stage { Key = "init", Percent = 70, Ko = "그래프를 준비하는 중", Zh = "正在准备图结构" },
            new Stage { Key = "sizing", Percent = 72, Ko = "인덱스 크기를 계산하는 중", Zh = "正在计算索引大小" },
            new Stage { Key = "g-nodes", Percent = 76, Ko = "Node 인덱스를 만드는 중", Zh = "正在建立 Node 索引" },
            new Stage { Key = "g-pocs", Percent = 82, Ko = "POC 인덱스를 만드는 중", Zh = "正在建立 POC 索引" },
            new Stage { Key = "g-pipes", Percent = 86, Ko = "Pipe 인덱스를 만드는 중", Zh = "正在建立 Pipe 索引" },
            new Stage { Key = "g-outlets", Percent = 88, Ko = "Pipe 출구 인덱스를 만드는 중", Zh = "正在建立 Pipe 出口索引" },
            new Stage { Key = "g-takeoffs", Percent = 90, Ko = "Takeoff 인덱스를 만드는 중", Zh = "正在建立 Takeoff 索引" },
            new Stage { Key = "g-dyn", Percent = 92, Ko = "Hookup 인덱스를 만드는 중", Zh = "正在建立 Hookup 索引" },
            new Stage { Key = "reverse", Percent = 94, Ko = "역방향 인덱스를 만드는 중 (가장 오래 걸립니다)", Zh = "正在建立反向索引（耗时最长）" }
        };

        /// <summary>Core 문구가 어느 단계인지 찾는다. 모르는 문구면 null.</summary>
        public static Stage Match(string message)
        {
            if (string.IsNullOrEmpty(message))
                return null;

            if (Has(message, "Loading Local Cache")) return Open;
            if (Has(message, "레거시 v4")) return Find("legacy");
            if (Has(message, "캐시: nodes")) return Find("nodes");
            if (Has(message, "캐시: pocs")) return Find("pocs");
            if (Has(message, "캐시: pipes")) return Find("pipes");
            if (Has(message, "캐시: takeoffs")) return Find("takeoffs");
            if (Has(message, "캐시: dynprops")) return Find("dynprops");
            if (Has(message, "캐시 로드 완료")) return Find("files");

            if (Has(message, "Initializing Graph: reverse index")) return Find("reverse");
            if (Has(message, "Initializing Graph: sizing indexes")) return Find("sizing");
            if (Has(message, "Initializing Graph: pipe outlets")) return Find("g-outlets");
            if (Has(message, "Initializing Graph: dynamic properties")) return Find("g-dyn");
            if (Has(message, "Initializing Graph: takeoffs")) return Find("g-takeoffs");
            if (Has(message, "Initializing Graph: pipes")) return Find("g-pipes");
            if (Has(message, "Initializing Graph: pocs")) return Find("g-pocs");
            if (Has(message, "Initializing Graph: nodes")) return Find("g-nodes");
            if (Has(message, "Initializing Graph")) return Find("init");
            return null;
        }

        /// <summary>단계 비중(0~100)을 작업 전체 구간 lo~hi 로 옮긴다.</summary>
        public static int Scale(int stagePercent, int lo, int hi)
        {
            if (hi < lo)
                hi = lo;
            double ratio = Math.Max(0, Math.Min(100, stagePercent)) / 100.0;
            return lo + (int)Math.Round((hi - lo) * ratio);
        }

        private static bool Has(string text, string needle)
        {
            return text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Stage Find(string key)
        {
            foreach (Stage item in Table)
            {
                if (item.Key == key)
                    return item;
            }
            return null;
        }
    }
}
