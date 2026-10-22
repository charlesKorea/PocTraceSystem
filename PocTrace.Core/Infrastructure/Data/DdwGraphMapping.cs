using System;
using System.Collections.Generic;
using PocTrace.Core.Enums;
using PocTrace.Core.Models;
using PocTrace.Core.Utils;

namespace PocTrace.Core.Infrastructure.Data
{
    public sealed class DdwLoadDiagnostics
    {
        public int ModelPocCount;
        public int PipeOwnedPocCount;
        public int ConnectedCount;
        public int UnconnectedCount;
        public int DanglingCount;
        public int PipesWithFourOrMore;
        public int MaxEndpoints;
        public int DuplicateConnectionOrders;
        public int UnknownNodesEmitted;
        public int UnknownVocabulary;
        public int ModuleChildrenSkipped;
        public int DeletedRowsSkipped;
        public int TakeoffCount;
        public int AttributeDuplicates;
        public List<string> UnknownSamples = new List<string>();
    }

    public sealed class DdwModelRow
    {
        public Guid InstanceId { get; set; }
        public string Name { get; set; }
        public string Tag { get; set; }
        public string ConstructionCode { get; set; }
        public string BimId { get; set; }
        public int? ModelType { get; set; }
        public int? CompositeYn { get; set; }
        public Guid? ParentInstanceId { get; set; }
        public string FamilyKey { get; set; }
        public int? DivisionType { get; set; }
        public string Sub1Name { get; set; }
        public string Sub2Name { get; set; }
        public int? Sub1DivisionType { get; set; }
        public long Pz { get; set; }
        public int OperationType { get; set; }
    }

    public sealed class DdwPocRow
    {
        public Guid InstanceId { get; set; }
        public Guid OwnerInstanceId { get; set; }
        public Guid? ConnectedPocId { get; set; }
        public int? OwnerType { get; set; }
        public int ConnectionOrder { get; set; }
        public int PocShape { get; set; }
        public double? Diameter { get; set; }
        public double? Width { get; set; }
        public double? TemplateDiameter { get; set; }
        public double? TemplateWidth { get; set; }
        public string InstanceUtilityName { get; set; }
        public string TemplateUtilityName { get; set; }
        public string InstanceUtilityGroupName { get; set; }
        public string TemplateUtilityGroupName { get; set; }
        public string PocTemplateName { get; set; }
        public string Memo { get; set; }
        public string Tag { get; set; }
        public string ConnectionTag { get; set; }
        public int OperationType { get; set; }
        public Guid? TargetInstanceId { get; set; }
        public int? TargetOwnerType { get; set; }
        public Guid? TargetOwnerInstanceId { get; set; }
    }

    public sealed class DdwPipeRow
    {
        public Guid InstanceId { get; set; }
        public int PipeType { get; set; }
        public double Length { get; set; }
        public string UtilityName { get; set; }
        public DateTime? OpTime { get; set; }
        public string UserName { get; set; }
        public string CompanyName { get; set; }
        public string Tag { get; set; }
        public int OperationType { get; set; }
    }

    public sealed class DdwAttributeRow
    {
        public Guid OwnerId { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public int OperationType { get; set; }
    }

    /// <summary>
    /// DDW 행을 표준 DTO로 정규화한다. SQL 예외로 프로파일을 바꾸지 않는다.
    /// </summary>
    public static class DdwGraphNormalizer
    {
        public const int ModelOwnerType = 768;
        public const int PipeOwnerType = 256;
        public const int TakeoffPipeType = 17301768;
        public const int SquarePocShape = 1312;
        public const string ElementIdNumberAttribute = "Element ID_Number";
        public const string ModuleInfoAttribute = "__MODULE_INFO";

        public static GraphLoadBundle Build(
            IEnumerable<DdwModelRow> models,
            IEnumerable<DdwPocRow> modelPocs,
            IEnumerable<DdwPocRow> pipePocs,
            IEnumerable<DdwPipeRow> pipes,
            IEnumerable<DdwAttributeRow> attributes)
        {
            var diag = new DdwLoadDiagnostics();
            var elementIds = new Dictionary<Guid, string>();
            var modules = new Dictionary<Guid, string>();
            AbsorbAttributes(attributes, elementIds, modules, diag);

            var pipeRows = new List<DdwPipeRow>();
            var pipeIds = new HashSet<Guid>();
            if (pipes != null)
            {
                foreach (var row in pipes)
                {
                    if (row == null)
                        continue;
                    if (row.OperationType >= 2)
                    {
                        diag.DeletedRowsSkipped++;
                        continue;
                    }
                    if (row.PipeType == TakeoffPipeType)
                        continue;
                    pipeRows.Add(row);
                    pipeIds.Add(row.InstanceId);
                }
            }

            var endpointsByPipe = new Dictionary<Guid, List<PipeEndpoint>>();
            if (pipePocs != null)
            {
                foreach (var row in pipePocs)
                {
                    if (row == null)
                        continue;
                    if (row.OperationType >= 2)
                    {
                        diag.DeletedRowsSkipped++;
                        continue;
                    }
                    if (row.OwnerType != PipeOwnerType)
                        continue;
                    diag.PipeOwnedPocCount++;
                    List<PipeEndpoint> list;
                    if (!endpointsByPipe.TryGetValue(row.OwnerInstanceId, out list))
                    {
                        list = new List<PipeEndpoint>();
                        endpointsByPipe[row.OwnerInstanceId] = list;
                    }
                    list.Add(ToEndpoint(row, diag));
                }
            }

            var pipeDtos = new List<PipeData>(pipeRows.Count);
            for (int i = 0; i < pipeRows.Count; i++)
            {
                var row = pipeRows[i];
                var pipe = new PipeData
                {
                    PipeId = row.InstanceId,
                    Length = NormalizeLength(row.Length),
                    UtilityName = row.UtilityName ?? "",
                    OpTime = row.OpTime,
                    UserName = row.UserName ?? "",
                    CompanyName = row.CompanyName ?? ""
                };
                List<PipeEndpoint> eps;
                if (!endpointsByPipe.TryGetValue(row.InstanceId, out eps))
                    eps = new List<PipeEndpoint>();
                pipe.ReplaceEndpoints(eps);
                NoteEndpointShape(pipe, diag);
                pipeDtos.Add(pipe);
            }

            var takeoffs = new List<TakeoffData>();
            if (pipes != null)
            {
                foreach (var row in pipes)
                {
                    if (row == null || row.OperationType >= 2 || row.PipeType != TakeoffPipeType)
                        continue;
                    List<PipeEndpoint> eps;
                    if (!endpointsByPipe.TryGetValue(row.InstanceId, out eps))
                        eps = new List<PipeEndpoint>();
                    var holder = new PipeData { PipeId = row.InstanceId };
                    holder.ReplaceEndpoints(eps);
                    NoteEndpointShape(holder, diag);
                    takeoffs.Add(new TakeoffData
                    {
                        TakeoffId = row.InstanceId,
                        Tag = row.Tag ?? "",
                        PipeId = ContinuationPipeId(holder.Endpoints, pipeIds)
                    });
                }
            }
            diag.TakeoffCount = takeoffs.Count;

            var pocDtos = new List<PocData>();
            if (modelPocs != null)
            {
                foreach (var row in modelPocs)
                {
                    if (row == null)
                        continue;
                    if (row.OperationType >= 2)
                    {
                        diag.DeletedRowsSkipped++;
                        continue;
                    }
                    if (row.OwnerType != ModelOwnerType)
                        continue;
                    diag.ModelPocCount++;
                    var resolved = Resolve(row, diag);
                    string module = LookupModule(modules, row.InstanceId, row.OwnerInstanceId);
                    string utility = FirstNonEmpty(row.InstanceUtilityName, row.TemplateUtilityName);
                    string group = !string.IsNullOrEmpty(row.InstanceUtilityName)
                        ? (row.InstanceUtilityGroupName ?? "")
                        : (row.TemplateUtilityGroupName ?? "");
                    pocDtos.Add(new PocData
                    {
                        InstanceId = row.InstanceId,
                        OwnerId = row.OwnerInstanceId,
                        Outlet = resolved,
                        PointID = Guid.Empty,
                        Tag = row.Tag ?? "",
                        ReferenceTag = row.ConnectionTag ?? "",
                        PocName = row.PocTemplateName ?? "",
                        Information = row.Memo ?? "",
                        Size = ResolveSize(row),
                        UtilityName = utility ?? "",
                        UtilityGroupName = group ?? "",
                        ModuleName = module ?? ""
                    });
                }
            }

            var nodeDtos = new List<NodeInfo>();
            if (models != null)
            {
                foreach (var row in models)
                {
                    if (row == null)
                        continue;
                    if (row.OperationType >= 2)
                    {
                        diag.DeletedRowsSkipped++;
                        continue;
                    }
                    string unresolved;
                    var type = Classify(row, out unresolved);
                    if (type == NodeType.Unknown)
                    {
                        diag.UnknownVocabulary++;
                        RememberSample(diag, unresolved);
                        if (IsModuleChild(row))
                            diag.ModuleChildrenSkipped++;
                        continue;
                    }

                    string bim = row.BimId;
                    if (string.IsNullOrEmpty(bim))
                    {
                        string fromAttr;
                        if (elementIds.TryGetValue(row.InstanceId, out fromAttr))
                            bim = fromAttr;
                    }

                    nodeDtos.Add(new NodeInfo
                    {
                        InstanceId = row.InstanceId,
                        InstanceNumericId = DotNetGuidHash.FromGuid(row.InstanceId),
                        Tag = row.Tag ?? "",
                        Name = row.Name ?? "",
                        Type = type,
                        Division = row.DivisionType ?? 0,
                        Sub1Name = row.Sub1Name ?? "",
                        Sub2Name = row.Sub2Name ?? "",
                        QCode = row.FamilyKey ?? "",
                        ConstructionCode = row.ConstructionCode,
                        BimId = bim,
                        PZ = row.Pz
                    });
                }
            }

            return new GraphLoadBundle
            {
                Nodes = nodeDtos,
                Pocs = pocDtos,
                Pipes = pipeDtos,
                Takeoffs = takeoffs,
                HookupNumbers = new List<DynamicPropertiesData>(),
                Diagnostics = diag
            };
        }

        public static double NormalizeLength(double length1)
        {
            if (length1 <= 0)
                return 0;
            return Math.Round(length1, 1, MidpointRounding.AwayFromZero);
        }

        public static long NormalizePz(double posz)
        {
            return (long)Math.Round(posz, 0, MidpointRounding.AwayFromZero);
        }

        public static string ParseModuleName(string attributeVal)
        {
            if (string.IsNullOrEmpty(attributeVal))
                return null;
            const string key = "ModuleName=";
            int i = attributeVal.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (i < 0)
                return null;
            var rest = attributeVal.Substring(i + key.Length);
            int cut = rest.IndexOfAny(new[] { ';', ',', '\n', '\r' });
            if (cut >= 0)
                rest = rest.Substring(0, cut);
            rest = rest.Trim();
            return rest.Length == 0 ? null : rest;
        }

        public static Guid ContinuationPipeId(IList<PipeEndpoint> endpoints, ISet<Guid> ordinaryPipeIds)
        {
            if (endpoints == null || ordinaryPipeIds == null)
                return Guid.Empty;
            for (int i = 0; i < endpoints.Count; i++)
            {
                var ep = endpoints[i];
                if (ep == null || ep.Resolution != PipeEndpointResolution.Connected || !ep.NormalizedTarget.HasValue)
                    continue;
                if (ordinaryPipeIds.Contains(ep.NormalizedTarget.Value))
                    return ep.NormalizedTarget.Value;
            }
            return Guid.Empty;
        }

        private static void AbsorbAttributes(
            IEnumerable<DdwAttributeRow> attributes,
            Dictionary<Guid, string> elementIds,
            Dictionary<Guid, string> modules,
            DdwLoadDiagnostics diag)
        {
            if (attributes == null)
                return;
            foreach (var row in attributes)
            {
                if (row == null)
                    continue;
                if (row.OperationType >= 2)
                {
                    diag.DeletedRowsSkipped++;
                    continue;
                }
                if (string.Equals(row.Name, ElementIdNumberAttribute, StringComparison.OrdinalIgnoreCase))
                {
                    if (elementIds.ContainsKey(row.OwnerId))
                        diag.AttributeDuplicates++;
                    else if (!string.IsNullOrEmpty(row.Value))
                        elementIds[row.OwnerId] = row.Value;
                }
                else if (string.Equals(row.Name, ModuleInfoAttribute, StringComparison.OrdinalIgnoreCase))
                {
                    var module = ParseModuleName(row.Value);
                    if (module == null)
                        continue;
                    if (modules.ContainsKey(row.OwnerId))
                        diag.AttributeDuplicates++;
                    else
                        modules[row.OwnerId] = module;
                }
            }
        }

        private static string LookupModule(Dictionary<Guid, string> modules, Guid pocId, Guid ownerId)
        {
            string value;
            if (modules.TryGetValue(pocId, out value))
                return value;
            if (modules.TryGetValue(ownerId, out value))
                return value;
            return null;
        }

        private static PipeEndpoint ToEndpoint(DdwPocRow row, DdwLoadDiagnostics diag)
        {
            Guid? target;
            var resolution = ResolveRaw(row, out target, diag);
            return new PipeEndpoint
            {
                PocInstanceId = row.InstanceId,
                ConnectionOrder = row.ConnectionOrder,
                NormalizedTarget = target,
                Resolution = resolution
            };
        }

        private static Guid? Resolve(DdwPocRow row, DdwLoadDiagnostics diag)
        {
            Guid? target;
            var resolution = ResolveRaw(row, out target, diag);
            if (resolution == PipeEndpointResolution.Connected)
                return target;
            return null;
        }

        private static PipeEndpointResolution ResolveRaw(DdwPocRow row, out Guid? target, DdwLoadDiagnostics diag)
        {
            target = null;
            if (!row.ConnectedPocId.HasValue || row.ConnectedPocId.Value == Guid.Empty)
            {
                diag.UnconnectedCount++;
                return PipeEndpointResolution.Unconnected;
            }
            if (!row.TargetInstanceId.HasValue || row.TargetInstanceId.Value == Guid.Empty)
            {
                diag.DanglingCount++;
                return PipeEndpointResolution.Dangling;
            }
            if (row.TargetOwnerType == ModelOwnerType)
            {
                target = row.TargetInstanceId;
                diag.ConnectedCount++;
                return PipeEndpointResolution.Connected;
            }
            if (row.TargetOwnerType == PipeOwnerType)
            {
                if (!row.TargetOwnerInstanceId.HasValue || row.TargetOwnerInstanceId.Value == Guid.Empty)
                {
                    diag.DanglingCount++;
                    return PipeEndpointResolution.Dangling;
                }
                target = row.TargetOwnerInstanceId;
                diag.ConnectedCount++;
                return PipeEndpointResolution.Connected;
            }
            diag.DanglingCount++;
            return PipeEndpointResolution.Dangling;
        }

        private static void NoteEndpointShape(PipeData pipe, DdwLoadDiagnostics diag)
        {
            int n = pipe.Endpoints == null ? 0 : pipe.Endpoints.Count;
            if (n > diag.MaxEndpoints)
                diag.MaxEndpoints = n;
            if (n >= 4)
                diag.PipesWithFourOrMore++;
            var seen = new HashSet<int>();
            for (int i = 0; i < n; i++)
            {
                int order = pipe.Endpoints[i].ConnectionOrder;
                if (!seen.Add(order))
                    diag.DuplicateConnectionOrders++;
            }
        }

        private static double ResolveSize(DdwPocRow row)
        {
            bool square = row.PocShape == SquarePocShape;
            double? instance = square ? row.Width : row.Diameter;
            if (instance.HasValue && instance.Value != 0)
                return instance.Value;
            double? template = square ? row.TemplateWidth : row.TemplateDiameter;
            if (template.HasValue)
                return template.Value;
            return 0;
        }

        private static string FirstNonEmpty(string first, string second)
        {
            if (!string.IsNullOrEmpty(first))
                return first;
            return second ?? "";
        }

        public static NodeType Classify(DdwModelRow row, out string unresolved)
        {
            unresolved = null;
            if (row == null)
            {
                unresolved = "null";
                return NodeType.Unknown;
            }
            string sub1 = row.Sub1Name ?? "";
            string sub2 = row.Sub2Name ?? "";
            if (NameIs(sub1, "Damper"))
                return NodeType.Damper;
            if (IsNozzle(sub1, sub2, row.Sub1DivisionType))
                return NodeType.Nozzle;
            if (IsEquipment(row))
                return NodeType.Equipment;
            if (IsFitting(row))
                return NodeType.Fitting;
            unresolved = sub1 + "|" + sub2 + "|" + (row.ModelType.HasValue ? row.ModelType.Value.ToString() : "");
            return NodeType.Unknown;
        }

        private static bool IsNozzle(string sub1, string sub2, int? sub1Division)
        {
            if (!NameIs(sub1, "TAKE-OFF"))
                return false;
            if (sub1Division.HasValue && sub1Division.Value == 0)
                return false;
            if (!sub1Division.HasValue)
                return false;
            if (NameIs(sub1, "Damper"))
                return false;
            return Contains(sub2, "Nozzle") || Contains(sub2, "valvebox");
        }

        private static bool IsEquipment(DdwModelRow row)
        {
            if ((row.DivisionType ?? -1) != 0)
                return false;
            if (string.IsNullOrEmpty(row.Sub1Name))
                return false;
            if (IsExcludedEquipmentSub(row.Sub1Name))
                return false;
            if (!string.IsNullOrEmpty(row.ConstructionCode)
                && row.ConstructionCode.IndexOf("Delete", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            if (row.Pz >= 100000)
                return false;
            if (!row.CompositeYn.HasValue || row.CompositeYn.Value == 0)
                return false;
            if (!IsNullGuid(row.ParentInstanceId))
                return false;
            return true;
        }

        private static bool IsFitting(DdwModelRow row)
        {
            int division = row.DivisionType ?? -1;
            if (division != 0 && division != 3)
                return false;
            if (string.IsNullOrEmpty(row.Sub1Name))
                return false;
            if (IsExcludedFittingSub(row.Sub1Name))
                return false;
            if (Contains(row.Name, "Nozzle"))
                return false;
            return true;
        }

        private static bool IsExcludedEquipmentSub(string sub1)
        {
            return NameIs(sub1, "BIM")
                || NameIs(sub1, "3-template")
                || NameIs(sub1, "5-electrical");
        }

        private static bool IsExcludedFittingSub(string sub1)
        {
            return NameIs(sub1, "Damper")
                || NameIs(sub1, "1-MAIN TOOL")
                || NameIs(sub1, "2-SUB TOOL")
                || NameIs(sub1, "3-TEMPLATE")
                || NameIs(sub1, "4-PCS")
                || NameIs(sub1, "5-ELECTRICAL")
                || NameIs(sub1, "6-GCS-S.GAS")
                || NameIs(sub1, "6-GCS-CHEMICAL");
        }

        private static bool IsModuleChild(DdwModelRow row)
        {
            if (!row.CompositeYn.HasValue || row.CompositeYn.Value == 0)
                return true;
            return !IsNullGuid(row.ParentInstanceId);
        }

        private static bool IsNullGuid(Guid? value)
        {
            return !value.HasValue || value.Value == Guid.Empty;
        }

        private static bool NameIs(string value, string expected)
        {
            return string.Equals(value ?? "", expected, StringComparison.OrdinalIgnoreCase);
        }

        private static bool Contains(string value, string part)
        {
            return !string.IsNullOrEmpty(value)
                && value.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void RememberSample(DdwLoadDiagnostics diag, string sample)
        {
            if (string.IsNullOrEmpty(sample) || diag.UnknownSamples.Count >= 32)
                return;
            if (!diag.UnknownSamples.Contains(sample))
                diag.UnknownSamples.Add(sample);
        }
    }
}
