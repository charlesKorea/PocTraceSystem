using System;
using System.Collections.Generic;
using System.Globalization;
using Serilog;

namespace PocTrace.Core.Infrastructure.Data
{
    /// <summary>
    /// DDW 타입 상수. 8자리이고 17로 시작하면 pipe_type, 5로 시작하면 model_type이다.
    /// 목록에 없는 값은 <see cref="DdwUnknownTypeLog"/>에 남긴다.
    /// </summary>
    public static class DdwTypeCodes
    {
        public const long FittingsPipe = 17301504L;
        public const long NormalPipe = 17301760L;
        public const long ElbowPipe = 17301761L;
        public const long TeePipe = 17301762L;
        public const long CrossPipe = 17301763L;
        public const long ReducerPipe = 17301764L;
        public const long FlexiblePipe = 17301765L;
        public const long EndcapPipe = 17301766L;
        public const long TakeoffPipe = 17301768L;
        public const long FlangePipe = 17301769L;
        public const long BendingPipe = 17301771L;
        public const long NormalDuct = 17303552L;
        public const long ElbowDuct = 17303553L;
        public const long TeeDuct = 17303554L;
        public const long EndcapDuct = 17303558L;
        public const long ReducerDuct = 17303566L;
        public const long TakeoffDuct = 17403560L;

        public const long Equipments = 50694048L;
        public const long Equipment = 50594049L;
        public const long EtcEquipment = 50594303L;
        public const long Connector = 51380485L;
        public const long Valve = 51380481L;
        public const long Nozzle = 50856202L;
        public const long Clamp = 51380484L;
        public const long Flange = 50856201L;
        public const long Damper = 51382287L;
        public const long Elbow = 50856193L;
        public const long Reducer = 50857988L;

        public static string PipeMeaning(long code)
        {
            if (code == FittingsPipe) return "fittings";
            if (code == NormalPipe) return "standard";
            if (code == ElbowPipe) return "elbow";
            if (code == TeePipe) return "tee";
            if (code == CrossPipe) return "cross";
            if (code == ReducerPipe) return "reducer";
            if (code == FlexiblePipe) return "flexible";
            if (code == EndcapPipe) return "endcap";
            if (code == TakeoffPipe) return "takeoff";
            if (code == FlangePipe) return "flange";
            if (code == BendingPipe) return "bending";
            if (code == NormalDuct) return "standard";
            if (code == ElbowDuct) return "elbow";
            if (code == TeeDuct) return "tee";
            if (code == EndcapDuct) return "endcap";
            if (code == ReducerDuct) return "reducer";
            if (code == TakeoffDuct) return "takeoff";
            return null;
        }

        public static string PipeFamily(long code)
        {
            if (PipeMeaning(code) == null)
                return null;
            string text = code.ToString(CultureInfo.InvariantCulture);
            if (text.StartsWith("173035", StringComparison.Ordinal) || code == TakeoffDuct)
                return "duct";
            return "pipe";
        }

        public static string ModelMeaning(long code)
        {
            if (code == Equipments) return "equipments";
            if (code == Equipment) return "equipment";
            if (code == EtcEquipment) return "etc_equipment";
            if (code == Connector) return "connector";
            if (code == Valve) return "valve";
            if (code == Nozzle) return "nozzle";
            if (code == Clamp) return "clamp";
            if (code == Flange) return "flange";
            if (code == Damper) return "damper";
            if (code == Elbow) return "elbow";
            if (code == Reducer) return "reducer";
            return null;
        }

        public static void ObservePipe(long code)
        {
            if (PipeMeaning(code) != null)
                return;
            DdwUnknownTypeLog.Add("pipe_type", code);
        }

        public static void ObserveModel(int? code)
        {
            if (!code.HasValue || ModelMeaning(code.Value) != null)
                return;
            DdwUnknownTypeLog.Add("model_type", code.Value);
        }
    }

    public sealed class DdwUnknownTypeHit
    {
        public string Kind;
        public long Code;
        public int Count;
    }

    /// <summary>
    /// 정의되지 않은 pipe_type, model_type. 로그에 한 번씩 남기고, 캐시 작성 뒤 엑셀로 뽑는다.
    /// </summary>
    public static class DdwUnknownTypeLog
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, DdwUnknownTypeHit> Hits =
            new Dictionary<string, DdwUnknownTypeHit>(StringComparer.OrdinalIgnoreCase);

        public static void Clear()
        {
            lock (Gate)
                Hits.Clear();
        }

        public static void Add(string kind, long code)
        {
            string key = kind + ":" + code.ToString(CultureInfo.InvariantCulture);
            bool first;
            lock (Gate)
            {
                DdwUnknownTypeHit hit;
                if (!Hits.TryGetValue(key, out hit))
                {
                    hit = new DdwUnknownTypeHit { Kind = kind, Code = code, Count = 0 };
                    Hits[key] = hit;
                    first = true;
                }
                else
                {
                    first = false;
                }
                hit.Count++;
            }
            if (first)
            {
                Log.Information(
                    "[DDW] 정의되지 않은 {Kind} {Code}. 엑셀 목록에 남긴다.",
                    kind,
                    code);
            }
        }

        public static List<DdwUnknownTypeHit> Snapshot()
        {
            lock (Gate)
            {
                var list = new List<DdwUnknownTypeHit>();
                foreach (var hit in Hits.Values)
                {
                    list.Add(new DdwUnknownTypeHit { Kind = hit.Kind, Code = hit.Code, Count = hit.Count });
                }
                list.Sort(delegate (DdwUnknownTypeHit a, DdwUnknownTypeHit b)
                {
                    int kind = string.Compare(a.Kind, b.Kind, StringComparison.OrdinalIgnoreCase);
                    if (kind != 0)
                        return kind;
                    return a.Code.CompareTo(b.Code);
                });
                return list;
            }
        }
    }
}
