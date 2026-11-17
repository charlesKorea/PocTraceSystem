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
        public const long FittingsPipeType = 17301504L;
        public const long TakeoffPipeType = 17301768L;
        public const long StandardPipeType = 17301760L;
        public const long ElbowPipeType = 17301761L;
        public const long TeePipeType = 17301762L;
        public const long CrossPipeType = 17301763L;
        public const long ReducerPipeType = 17301764L;
        public const long FlexiblePipeType = 17301765L;
        public const long EndcapPipeType = 17301766L;
        public const long FlangePipeType = 17301769L;
        public const long BendingPipeType = 17301771L;
        public const long StandardDuctType = 17303552L;
        public const long ElbowDuctType = 17303553L;
        public const long TeeDuctType = 17303554L;
        public const long EndcapDuctType = 17303558L;
        public const long ReducerDuctType = 17303566L;
        public const long TakeoffDuctType = 17403560L;
        public const long EquipmentsModelType = 50694048L;
        public const long EtcEquipmentModelType = 50594303L;
        public const long EquipmentModelType = 50594049L;
        public const long ConnectorModelType = 51380485L;
        public const long ValveModelType = 51380481L;
        public const long NozzleModelType = 50856202L;
        public const long ClampModelType = 51380484L;
        public const long FlangeModelType = 50856201L;
        public const long DamperModelType = 51382287L;
        public const long ElbowModelType = 50856193L;
        public const long ReducerModelType = 50857988L;
        public const long TakeoffModelType = 50856200L;
        public const long EndcapModelType = 50856198L;
        public const long TeeModelType = 50856194L;
        public const long EtcFittingModelType = 50856447L;
        public const long AccessoryModelType = 51380735L;
        public const long StructureBimModelType = 50462976L;
        public const long ArchitectureModelType = 50465023L;
        public const long BeamModelType = 50462981L;
        public const long StructureFloorModelType = 50462977L;
        public const long StructureWallModelType = 50462978L;
        public const long StructureColumnModelType = 50462980L;
        public const long LadderCableTrayType = 17302528L;

        public static readonly string[] TakeoffAttributeNames =
        {
            "tag",
            "ConnectionTag",
            "S5_LATERAL_NUMBER"
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
            if (type == FittingsPipeType)
                return "fittings";
            if (type == TakeoffPipeType || type == TakeoffDuctType)
                return "takeoff";
            if (type == StandardPipeType || type == StandardDuctType)
                return "standard";
            if (type == ElbowPipeType || type == ElbowDuctType)
                return "elbow";
            if (type == TeePipeType || type == TeeDuctType)
                return "tee";
            if (type == CrossPipeType)
                return "cross";
            if (type == ReducerPipeType || type == ReducerDuctType)
                return "reducer";
            if (type == FlexiblePipeType)
                return "flexible";
            if (type == EndcapPipeType || type == EndcapDuctType)
                return "endcap";
            if (type == FlangePipeType)
                return "flange";
            if (type == BendingPipeType)
                return "bending";
            if (type == LadderCableTrayType)
                return "ladder_cable_tray";
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
            if (type == EquipmentsModelType)
                return "equipments";
            if (type == EquipmentModelType)
                return "equipment";
            if (type == EtcEquipmentModelType)
                return "etc_equipment";
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
            if (type == TakeoffModelType)
                return "takeoff(pipe)";
            if (type == EndcapModelType)
                return "endcap(pipe)";
            if (type == TeeModelType)
                return "tee(pipe)";
            if (type == EtcFittingModelType)
                return "etc_fitting(pipe)";
            if (type == AccessoryModelType)
                return "accessory(etc)";
            if (type == StructureBimModelType)
                return "structure(bim)";
            if (type == ArchitectureModelType)
                return "architecture(etc)";
            if (type == BeamModelType)
                return "beam";
            if (type == StructureFloorModelType)
                return "structure(floor)";
            if (type == StructureWallModelType)
                return "structure(wall)";
            if (type == StructureColumnModelType)
                return "structure(column)";
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

        public static bool IsNozzleModel(TemplateRow tpl, long? instanceModelType)
        {
            if (instanceModelType.HasValue && instanceModelType.Value == NozzleModelType)
                return true;
            return tpl != null && tpl.ModelType.HasValue && tpl.ModelType.Value == NozzleModelType;
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
