using System;

namespace PocTrace.Core.Infrastructure
{
    /// <summary>
    /// 그래프 원천. 쿼리 실패로 다른 값을 고르지 않는다.
    /// TbStar = 측정된 TB_* (tag 등 projected 컬럼 없음).
    /// WebDdw = tag, s5_eqcode, construction_code, elementId_number,
    /// Subdivision_1_name, Subdivision_2_name, poc tag, connectionTag 가 있는 적재본.
    /// </summary>
    public enum SchemaKind
    {
        S5d = 0,
        TbStar = 1,
        WebDdw = 2
    }

    public static class SchemaKindParser
    {
        public static bool TryParse(string text, out SchemaKind kind)
        {
            kind = SchemaKind.S5d;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            if (string.Equals(text.Trim(), "S5d", StringComparison.OrdinalIgnoreCase))
            {
                kind = SchemaKind.S5d;
                return true;
            }
            if (string.Equals(text.Trim(), "TbStar", StringComparison.OrdinalIgnoreCase))
            {
                kind = SchemaKind.TbStar;
                return true;
            }
            if (string.Equals(text.Trim(), "WebDdw", StringComparison.OrdinalIgnoreCase))
            {
                kind = SchemaKind.WebDdw;
                return true;
            }
            return false;
        }

        public static SchemaKind Parse(string text)
        {
            SchemaKind kind;
            if (!TryParse(text, out kind))
                throw new InvalidOperationException(
                    "SchemaKind '" + (text ?? "") + "' is not S5d, TbStar, or WebDdw.");
            return kind;
        }
    }
}
