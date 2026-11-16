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
        public const long StandardPipeType = 17301760L;
        public const long BendingPipeType = 17301771L;
        public const long EquipmentModelType = 50594049L;
        public const long ConnectorModelType = 51380485L;
        public const long ValveModelType = 51380481L;
        public const long NozzleModelType = 50856202L;
        public const long ClampModelType = 51380484L;
        public const long FlangeModelType = 50856201L;
        public const long DamperModelType = 51382287L;
        public const long ElbowModelType = 50856193L;
        public const long ReducerModelType = 50857988L;

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
            if (type == StandardPipeType)
                return "standard";
            if (type == BendingPipeType)
                return "bending";
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
            if (type == ConnectorModelType)
                return "connector";
            if (type == ValveModelType)
                return "valve";
            if (type == NozzleModelType)
                return "nozzle";
            if (type == ClampModelType)
                return "clamp";
            if (type == FlangeModelType)
                return "flange";
            if (type == DamperModelType)
                return "damper";
            if (type == ElbowModelType)
                return "elbow";
            if (type == ReducerModelType)
                return "reducer";
            return null;
        }

        public static bool IsDamper(TemplateRow tpl, long? instanceModelType)
        {
            if (IsDamperModel(tpl, instanceModelType))
                return true;
            if (IsNozzle(tpl))
                return false;
            return Has(tpl == null ? null : tpl.DisplayName, "damper") || Has(tpl == null ? null : tpl.FamilyKey, "damper");
        }

        public static bool IsNozzle(TemplateRow tpl)
        {
            return tpl != null && tpl.BimYn.HasValue && tpl.BimYn.Value == 1 && Has(tpl.DisplayName, "노즐");
        }

        private static bool IsDamperModel(TemplateRow tpl, long? instanceModelType)
        {
            if (instanceModelType.HasValue && instanceModelType.Value == DamperModelType)
                return true;
            return tpl != null && tpl.ModelType.HasValue && tpl.ModelType.Value == DamperModelType;
        }

        private static bool Has(string text, string token)
        {
            return !string.IsNullOrEmpty(text) && text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
