using System;
using System.Globalization;
using PocTrace.DdwTraceBench.Data;

namespace PocTrace.DdwTraceBench.Trace
{
    /// <summary>
    /// 시험 도구가 아는 코드. 뜻을 모르는 숫자는 숫자만 적는다.
    /// </summary>
    public static class TraceCodes
    {
        public const long TakeoffPipeType = 17301768L;
        public const long OrdinaryPipeType = 17301760L;
        public const long EquipmentModelType = 50594049L;
        public const long ConnectorPipeModelType = 51380485L;

        public static readonly string[] TakeoffAttributeNames =
        {
            "tag",
            "ConnectionTag",
            "connectiontag",
            "HOOKUPNUMBER",
            "HookupNumber",
            "Hookup Number",
            "hookup number",
            "HOOKUP NUMBER"
        };

        public static string PipeTypeText(long? type)
        {
            if (!type.HasValue)
                return null;
            string meaning = PipeTypeMeaning(type.Value);
            string number = type.Value.ToString(CultureInfo.InvariantCulture);
            return meaning == null ? number : number + " " + meaning;
        }

        public static string PipeTypeMeaning(long type)
        {
            if (type == TakeoffPipeType)
                return "Takeoff";
            if (type == OrdinaryPipeType)
                return "일반 배관";
            return null;
        }

        public static string ModelTypeText(long? type)
        {
            if (!type.HasValue)
                return null;
            string meaning = ModelTypeMeaning(type.Value);
            string number = type.Value.ToString(CultureInfo.InvariantCulture);
            return meaning == null ? number : number + " " + meaning;
        }

        public static string ModelTypeMeaning(long type)
        {
            if (type == EquipmentModelType)
                return "설비 후보";
            if (type == ConnectorPipeModelType)
                return "connector_pipe";
            return null;
        }

        public static bool IsDamper(TemplateRow tpl)
        {
            return Contains(tpl, "damper");
        }

        public static bool IsNozzle(TemplateRow tpl)
        {
            return Contains(tpl, "nozzle");
        }

        private static bool Contains(TemplateRow tpl, string token)
        {
            if (tpl == null)
                return false;
            return Has(tpl.DisplayName, token) || Has(tpl.FamilyKey, token);
        }

        private static bool Has(string text, string token)
        {
            return !string.IsNullOrEmpty(text) && text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
