using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services
{
    internal static class GaLiftUnitInputPreparation
    {
        public static GaLiftUnitCounts CountLiftUnits(
            IReadOnlyList<PrecastElement> allElements,
            IReadOnlyList<PrecastElement> scopedElements,
            IReadOnlyCollection<string>? targetElementTypes)
        {
            List<PrecastElement> expanded = ExpandAssemblyScope(allElements, scopedElements);
            HashSet<string> parentIds = GetEligibleAssemblyParentIds(expanded, targetElementTypes);
            IReadOnlyDictionary<string, string> assemblyParentIds = BuildAssemblyParentIdMap(expanded);
            int grouped = expanded.Count(element =>
                parentIds.Contains(element.ElementID)
                && TryPrepareAssemblyParent(element, expanded, out _, out _));
            var groupedMemberIds = expanded
                .Where(element => assemblyParentIds.TryGetValue(element.ElementID, out string? parentId)
                    && parentIds.Contains(parentId))
                .Select(element => element.ElementID)
                .ToHashSet(StringComparer.Ordinal);
            int standalone = scopedElements.Count(element =>
                !groupedMemberIds.Contains(element.ElementID)
                && element.Role != IfcElementRole.LiftAssembly
                && element.IncludeInGa
                && element.Role is not (IfcElementRole.NeedsReview or IfcElementRole.GroupingOnly or IfcElementRole.InternalAuxiliary or IfcElementRole.Excluded)
                && HasTrustedPositiveWeight(element)
                && IsValidPoint(element.ElementCOG)
                && !IfcReferenceGeometryPolicy.IsGridReference(element)
                && !IsReinforcement(element));
            int rebarExcluded = expanded.Count(IsReinforcement)
                + expanded.Count(element => element.Role == IfcElementRole.LiftAssembly
                    && !IsReinforcement(element)
                    && IsReinforcementAssembly(element, expanded));

            return new GaLiftUnitCounts(grouped, standalone, rebarExcluded);
        }

        public static List<PrecastElement> ExpandAssemblyScope(
            IReadOnlyList<PrecastElement> allElements,
            IReadOnlyList<PrecastElement> scopedElements)
        {
            var byId = BuildElementMap(allElements);
            var includedIds = scopedElements
                .Where(element => !string.IsNullOrWhiteSpace(element.ElementID))
                .Select(element => element.ElementID)
                .ToHashSet(StringComparer.Ordinal);

            foreach (PrecastElement element in scopedElements)
            {
                PrecastElement? parent = ResolveAssemblyParent(element, allElements, byId);
                if (parent == null)
                    continue;

                includedIds.Add(parent.ElementID);
                foreach (PrecastElement child in ResolveChildren(parent, allElements, byId))
                    includedIds.Add(child.ElementID);
            }

            return allElements
                .Where(element => !string.IsNullOrWhiteSpace(element.ElementID) && includedIds.Contains(element.ElementID))
                .ToList();
        }

        public static HashSet<string> GetEligibleAssemblyParentIds(
            IReadOnlyList<PrecastElement> elements,
            IReadOnlyCollection<string>? targetElementTypes)
        {
            HashSet<string>? targetTypes = targetElementTypes == null || targetElementTypes.Count == 0
                ? null
                : targetElementTypes
                    .Where(type => !string.IsNullOrWhiteSpace(type))
                    .Select(type => type.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var byId = BuildElementMap(elements);

            return elements
                .Where(parent => parent.Role == IfcElementRole.LiftAssembly)
                .Where(parent => !IsReinforcementAssembly(parent, elements, byId))
                .Where(parent => targetTypes == null || ResolveChildren(parent, elements, byId)
                    .Append(parent)
                    .Any(member => targetTypes.Contains(NormalizeElementType(member.ElementType))))
                .Select(parent => parent.ElementID)
                .ToHashSet(StringComparer.Ordinal);
        }

        public static bool TryPrepareAssemblyParent(
            PrecastElement parent,
            IReadOnlyList<PrecastElement> allElements,
            out PrecastElement prepared,
            out string failureReason)
        {
            var byId = BuildElementMap(allElements);
            List<PrecastElement> nonRebarChildren = ResolveChildren(parent, allElements, byId)
                .Where(child => !IsReinforcement(child))
                .ToList();
            bool hasParentWeight = HasTrustedPositiveWeight(parent);
            List<PrecastElement> validContributors = nonRebarChildren
                .Where(HasTrustedPositiveWeight)
                .ToList();
            double liftWeight = hasParentWeight
                ? parent.WeightTons!.Value
                : validContributors.Sum(child => child.WeightTons!.Value);

            if (IsReinforcementAssembly(parent, allElements, byId))
            {
                prepared = parent;
                failureReason = "Reinforcement assemblies cannot enter GA.";
                return false;
            }

            if (liftWeight <= 0)
            {
                prepared = parent;
                failureReason = "Assembly has neither a trusted parent weight nor valid non-rebar child weights.";
                return false;
            }

            PrecastElement? mainPart = nonRebarChildren.FirstOrDefault(child =>
                    string.Equals(child.ElementID, parent.MainPartGlobalId, StringComparison.Ordinal))
                ?? validContributors.OrderByDescending(child => child.WeightTons).FirstOrDefault()
                ?? nonRebarChildren.FirstOrDefault(child => child.IncludeInRender);
            DomainPoint3D? liftCog = FirstValidPoint(parent.AssemblyLiftCOG, mainPart?.ElementCOG, parent.ElementCOG);

            if (liftCog == null)
            {
                prepared = parent;
                failureReason = "Assembly and main member COG are missing or invalid.";
                return false;
            }

            prepared = parent.CreateCopy();
            prepared.IncludeInGa = true;
            prepared.Role = IfcElementRole.LiftAssembly;
            prepared.AssemblyLiftWeight = liftWeight;
            prepared.AssemblyLiftCOG = liftCog;
            prepared.ElementCOG = liftCog;
            prepared.COG_M = liftCog;
            prepared.PhysicalCOGAvailable = true;
            prepared.ChildGlobalIds = nonRebarChildren.Select(child => child.ElementID).Distinct(StringComparer.Ordinal).ToList();
            prepared.AssemblyMemberIds = nonRebarChildren
                .Where(IsVisibleMember)
                .Select(child => child.ElementID)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            failureReason = string.Empty;
            return true;
        }

        public static PrecastElement PrepareAssemblyMember(PrecastElement child)
        {
            PrecastElement prepared = child.CreateCopy();
            prepared.IncludeInGa = false;
            if (!HasTrustedPositiveWeight(prepared))
            {
                prepared.WeightTons = 0;
                prepared.ElementWT = 0;
            }

            prepared.PhysicalCOGAvailable = prepared.PhysicalCOGAvailable && IsValidPoint(prepared.ElementCOG);
            return prepared;
        }

        public static string? ResolveAssemblyParentId(
            PrecastElement element,
            IReadOnlyList<PrecastElement> allElements)
        {
            PrecastElement? parent = ResolveAssemblyParent(element, allElements, BuildElementMap(allElements));
            return parent?.ElementID;
        }

        public static IReadOnlyDictionary<string, string> BuildAssemblyParentIdMap(
            IReadOnlyList<PrecastElement> allElements)
        {
            var byId = BuildElementMap(allElements);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (PrecastElement parent in allElements.Where(element => element.Role == IfcElementRole.LiftAssembly))
            {
                result[parent.ElementID] = parent.ElementID;
                foreach (PrecastElement child in ResolveChildren(parent, allElements, byId))
                    result[child.ElementID] = parent.ElementID;
            }

            return result;
        }

        public static bool IsVisibleMember(PrecastElement element) =>
            element.IncludeInRender
            && element.ChildClassification != AssemblyChildClassification.IgnoredReference
            && !IfcReferenceGeometryPolicy.IsGridReference(element)
            && !IsReinforcement(element);

        public static bool IsReinforcementId(
            string elementId,
            IReadOnlyDictionary<string, PrecastElement> elementsById) =>
            elementsById.TryGetValue(elementId, out PrecastElement? element) && IsReinforcement(element);

        public static bool IsReinforcement(PrecastElement element)
        {
            string classification = string.Join(" ",
                element.RawIfcType,
                element.ResolvedCategory,
                element.ElementType,
                element.ElementName,
                element.ElementRevitId,
                element.ElementMark,
                element.AssemblyMark).ToUpperInvariant();

            return classification.Contains("REINFORC", StringComparison.Ordinal)
                || classification.Contains("REBAR", StringComparison.Ordinal)
                || classification.Contains("TENDON", StringComparison.Ordinal)
                || classification.Contains("STIRRUP", StringComparison.Ordinal)
                || classification.Contains("TIE-WIRE", StringComparison.Ordinal);
        }

        public static bool IsReinforcementAssembly(
            PrecastElement parent,
            IReadOnlyList<PrecastElement> allElements) =>
            IsReinforcementAssembly(parent, allElements, BuildElementMap(allElements));

        public static bool HasTrustedPositiveWeight(PrecastElement element) =>
            element.WeightTons is > 0
            && element.WeightTrustLevel is IfcWeightTrustLevel.ExplicitQuantity or IfcWeightTrustLevel.ExplicitProperty;

        private static bool IsReinforcementAssembly(
            PrecastElement parent,
            IReadOnlyList<PrecastElement> allElements,
            IReadOnlyDictionary<string, PrecastElement> byId)
        {
            if (IsReinforcement(parent))
                return true;

            List<PrecastElement> children = ResolveChildren(parent, allElements, byId);
            return children.Count > 0 && children.All(IsReinforcement);
        }

        private static PrecastElement? ResolveAssemblyParent(
            PrecastElement element,
            IReadOnlyList<PrecastElement> allElements,
            IReadOnlyDictionary<string, PrecastElement> byId)
        {
            if (element.Role == IfcElementRole.LiftAssembly)
                return element;

            if (!string.IsNullOrWhiteSpace(element.ParentGlobalId)
                && byId.TryGetValue(element.ParentGlobalId, out PrecastElement? directParent)
                && directParent.Role == IfcElementRole.LiftAssembly)
            {
                return directParent;
            }

            return allElements.FirstOrDefault(parent =>
                parent.Role == IfcElementRole.LiftAssembly
                && parent.ChildGlobalIds.Contains(element.ElementID, StringComparer.Ordinal));
        }

        private static List<PrecastElement> ResolveChildren(
            PrecastElement parent,
            IReadOnlyList<PrecastElement> allElements,
            IReadOnlyDictionary<string, PrecastElement> byId)
        {
            var children = allElements
                .Where(element => string.Equals(element.ParentGlobalId, parent.ElementID, StringComparison.Ordinal))
                .ToList();

            foreach (string childId in parent.ChildGlobalIds)
            {
                if (byId.TryGetValue(childId, out PrecastElement? child) && !children.Contains(child))
                    children.Add(child);
            }

            return children;
        }

        private static Dictionary<string, PrecastElement> BuildElementMap(IReadOnlyList<PrecastElement> elements) =>
            elements
                .Where(element => !string.IsNullOrWhiteSpace(element.ElementID))
                .GroupBy(element => element.ElementID, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        private static DomainPoint3D? FirstValidPoint(params DomainPoint3D?[] points) =>
            points.FirstOrDefault(IsValidPoint);

        private static bool IsValidPoint(DomainPoint3D? point) => point != null
            && double.IsFinite(point.X)
            && double.IsFinite(point.Y)
            && double.IsFinite(point.Z);

        private static string NormalizeElementType(string? elementType) =>
            string.IsNullOrWhiteSpace(elementType) ? "Unknown" : elementType.Trim();
    }

    internal readonly record struct GaLiftUnitCounts(
        int GroupedAssemblyCount,
        int StandaloneCount,
        int RebarExcludedCount)
    {
        public int FinalCandidateCount => GroupedAssemblyCount + StandaloneCount;
    }
}
