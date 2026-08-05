using CraneOptimizer.Application.Reports.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;
using CraneOptimizer.Application.Services;
using CraneOptimizer.Application.Services.Identity;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Services;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Application.Reports.Services;

public sealed class ElementReportDataProvider : IElementReportDataProvider
{
    private readonly IReportableElementResolver _resolver;
    private readonly IImportReadinessService _readinessService;

    public ElementReportDataProvider(
        IReportableElementResolver resolver,
        IImportReadinessService readinessService)
    {
        _resolver = resolver;
        _readinessService = readinessService;
    }

    public IReadOnlyList<ElementReportRowDto> BuildRows(
        string templateName,
        IEnumerable<PrecastElement> scopedElements)
    {
        var elementsList = scopedElements.ToList();
        var canonicalSnapshot = CanonicalLiftUnitSnapshotBuilder.BuildCurrentSnapshot(elementsList);
        var gaEquivalentCogByLiftUnitId = BuildGaEquivalentCogLookup(elementsList);
        var canonicalUnits = ApplyTemplateFilter(canonicalSnapshot.CanonicalLiftUnits, templateName);

        return canonicalUnits
            .Select((unit, index) => ToRow(unit, index + 1, ResolveGaEquivalentCog(unit, gaEquivalentCogByLiftUnitId)))
            .ToList();
    }

    private sealed class ElementReadinessInfo
    {
        public string ValidationStatus { get; init; } = "Valid";
        public string ValidationWarnings { get; init; } = "\u2014";
        public string BlockingReasons { get; init; } = "\u2014";
    }

    private static IReadOnlyDictionary<string, ElementReadinessInfo> BuildElementReadinessLookup(
        ImportReadinessSummary readinessSummary) =>
        readinessSummary.ElementDiagnostics
            .Where(diagnostic => !string.IsNullOrWhiteSpace(diagnostic.ElementId))
            .GroupBy(diagnostic => diagnostic.ElementId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var diagnostic = group.First();
                    var blockingReasons = JoinMessages(diagnostic.BlockingReasons);
                    return new ElementReadinessInfo
                    {
                        ValidationStatus = diagnostic.ReadinessStatus,
                        ValidationWarnings = JoinMessages(diagnostic.WarningMessages),
                        BlockingReasons = diagnostic.IsReadyForGa ? "\u2014" : blockingReasons
                    };
                },
                StringComparer.OrdinalIgnoreCase);
    private static IReadOnlyList<PrecastElement> ApplyTemplateFilter(
        IReadOnlyList<PrecastElement> elements,
        string templateName)
    {
        if (!templateName.Equals(ReportTemplateCatalog.MissingEstimatedWeight, StringComparison.OrdinalIgnoreCase))
            return elements;

        return elements
            .Where(element =>
            {
                var mapping = ReportWeightMapper.Map(element);
                return mapping.WeightVerified == "No"
                    || mapping.WeightStatus is "Missing" or "Estimated" or "Needs Review";
            })
            .ToList();
    }

    private static IReadOnlyList<CanonicalLiftUnitSnapshotDto> ApplyTemplateFilter(
        IReadOnlyList<CanonicalLiftUnitSnapshotDto> units,
        string templateName)
    {
        if (!templateName.Equals(ReportTemplateCatalog.MissingEstimatedWeight, StringComparison.OrdinalIgnoreCase))
            return units;

        return units
            .Where(unit => !string.Equals(unit.WeightTrustLevel, IfcWeightTrustLevel.ExplicitQuantity.ToString(), StringComparison.Ordinal)
                && !string.Equals(unit.WeightTrustLevel, IfcWeightTrustLevel.ExplicitProperty.ToString(), StringComparison.Ordinal))
            .ToList();
    }

    private static ElementReportRowDto ToRow(
        CanonicalLiftUnitSnapshotDto unit,
        int number,
        GaPointSnapshotDto? gaEquivalentCog)
    {
        var (bottomElevation, topElevation) = ResolveCanonicalElevationRange(unit);
        var hasTrustedWeight = IsTrustedWeight(unit.WeightTrustLevel) && unit.LiftWeightTon is > 0;
        var blockingReasons = hasTrustedWeight
            ? JoinMessages(unit.BlockingReasons)
            : "No Trusted Weight";
        var includedInAnalysis = unit.ImportIncludeInGaFlag && hasTrustedWeight;

        return new ElementReportRowDto
        {
            No = number,
        ElementId = unit.LiftUnitId,
        ElementMark = FirstNonEmpty(unit.CanonicalMark, unit.ElementMark, unit.LiftUnitId),
        ElementName = FirstNonEmpty(unit.ElementName, "\u2014"),
        ElementType = FirstNonEmpty(unit.ElementType, "\u2014"),
        CanonicalMark = FirstNonEmpty(unit.CanonicalMark, unit.ElementMark, unit.LiftUnitId),
        MarkSource = unit.MarkSource ?? string.Empty,
        MarkConfidence = unit.MarkConfidence ?? "None",
        IsExplicitMark = unit.IsExplicitMark,
        SourceRevitId = FirstNonEmpty(unit.SourceElementGlobalId, unit.LiftUnitId),
        StoreyLevel = FirstNonEmpty(unit.StoreyOrElevation, "\u2014"),
        Material = FirstNonEmpty(unit.RawMaterialValue, unit.Material, "\u2014"),
        MaterialType = FirstNonEmpty(unit.MaterialType, "\u2014"),
        MaterialGrade = FirstNonEmpty(unit.MaterialGrade, "\u2014"),
        RawMaterialValue = unit.RawMaterialValue ?? string.Empty,
        MaterialSource = unit.MaterialSource,
        Length = unit.LengthM,
        Width = unit.WidthM,
        Height = unit.HeightM,
        VolumeM3 = unit.VolumeM3,
        VolumeSource = string.IsNullOrWhiteSpace(unit.DimensionSource) ? "Missing" : unit.DimensionSource,
        CogX = gaEquivalentCog?.X ?? ResolvePhysicalCog(unit)?.X,
        CogY = gaEquivalentCog?.Y ?? ResolvePhysicalCog(unit)?.Y,
        CogZ = gaEquivalentCog?.Z ?? ResolvePhysicalCog(unit)?.Z,
        AssemblyMemberCount = unit.AssemblyMemberCount,
        TopLevelElevation = topElevation,
        BottomLevelElevation = bottomElevation,
        WeightTons = unit.LiftWeightTon,
        WeightSource = unit.WeightSource ?? string.Empty,
        WeightVerified = hasTrustedWeight ? "Yes" : "No",
        WeightStatus = hasTrustedWeight ? "Verified" : "Missing",
        WeightWarning = "-",
        IncludedInCraneOptimization = includedInAnalysis ? "Yes" : "No",
        IncludedInAnalysis = includedInAnalysis ? "Yes" : "No",
        AnalysisExclusionReason = includedInAnalysis ? "\u2014" : ResolveAnalysisExclusionReason(unit.BlockingReasons, blockingReasons),
        ValidationStatus = hasTrustedWeight ? FirstNonEmpty(unit.ValidationStatus, "Valid") : "Invalid",
        ValidationWarnings = JoinMessages(unit.ValidationWarnings),
        WeightTrustLevel = unit.WeightTrustLevel ?? string.Empty,
            BlockingReasons = blockingReasons
        };
    }

    private static IReadOnlyDictionary<string, GaPointSnapshotDto> BuildGaEquivalentCogLookup(
        IReadOnlyList<PrecastElement> sourceElements)
    {
        if (sourceElements.Count == 0)
            return new Dictionary<string, GaPointSnapshotDto>(StringComparer.Ordinal);

        var clonedSceneElements = sourceElements
            .Where(element => element != null)
            .Select(CloneForReportRollup)
            .ToList();

        var preparedElements = PrepareGaInputElementsForReport(clonedSceneElements);
        return BuildFinalGaLiftUnitCogLookup(preparedElements);
    }

    private static IReadOnlyList<PrecastElement> PrepareGaInputElementsForReport(
        IReadOnlyList<PrecastElement> sceneElements)
    {
        var filteredElements = sceneElements.ToList();
        var allElementsMap = BuildElementMap(sceneElements);
        var eligibleAssemblyParentIds = GetEligibleAssemblyParentIds(filteredElements);
        var assemblyParentIds = BuildAssemblyParentIdMap(filteredElements);
        var prepared = new List<PrecastElement>();

        foreach (var element in filteredElements)
        {
            if (IsReinforcement(element)
                || element.Role == IfcElementRole.LiftAssembly && IsReinforcementAssembly(element, filteredElements))
            {
                continue;
            }

            if (IfcReferenceGeometryPolicy.IsGridReference(element))
                continue;

            if (element.Role == IfcElementRole.LiftAssembly)
            {
                if (!eligibleAssemblyParentIds.Contains(element.ElementID))
                    continue;

                if (TryPrepareAssemblyParent(element, filteredElements, out var preparedParent))
                    prepared.Add(preparedParent);

                continue;
            }

            var assemblyParentId = assemblyParentIds.TryGetValue(element.ElementID, out var resolvedParentId)
                ? resolvedParentId
                : null;
            if (assemblyParentId != null && eligibleAssemblyParentIds.Contains(assemblyParentId))
            {
                prepared.Add(PrepareAssemblyMember(element));
                continue;
            }

            if (!element.IncludeInGa
                || element.Role is IfcElementRole.NeedsReview or IfcElementRole.GroupingOnly or IfcElementRole.InternalAuxiliary or IfcElementRole.Excluded
                || !HasTrustedPositiveWeight(element)
                || !IsValidPoint(element.ElementCOG)
                || IsChildOfTrustedAssemblyTarget(element, allElementsMap))
            {
                continue;
            }

            prepared.Add(element.CreateCopy());
        }

        return prepared;
    }

    private static IReadOnlyDictionary<string, GaPointSnapshotDto> BuildFinalGaLiftUnitCogLookup(
        IReadOnlyList<PrecastElement> allElements)
    {
        var allById = allElements
            .Where(element => !string.IsNullOrWhiteSpace(element.ElementID))
            .ToDictionary(element => element.ElementID, StringComparer.Ordinal);
        var gaEnabledElements = allElements
            .Where(element => element.IncludeInGa && !IfcReferenceGeometryPolicy.IsGridReference(element))
            .ToList();
        var result = new Dictionary<string, GaPointSnapshotDto>(StringComparer.Ordinal);
        var groupedMemberIds = new HashSet<string>(StringComparer.Ordinal);

        var parentAssemblies = allElements
            .Where(element => element.Role == IfcElementRole.LiftAssembly && element.IncludeInGa)
            .ToList();

        foreach (var parent in parentAssemblies)
        {
            var children = allElements
                .Where(element => string.Equals(element.ParentGlobalId, parent.ElementID, StringComparison.Ordinal))
                .ToList();

            foreach (var childId in parent.ChildGlobalIds)
            {
                if (allById.TryGetValue(childId, out var child) && !children.Contains(child))
                    children.Add(child);
            }

            if (children.Count == 0 && !parent.AssemblyLiftWeight.HasValue)
                continue;

            if (children.Count > 0)
                AssemblyRollupService.ReconstructAssemblyClassifications(children);

            var liftUnit = AssemblyRollupService.BuildAssemblyLiftUnit(parent, children, out _);
            AddCogAlias(result, liftUnit.ElementID, liftUnit.ElementCOG);
            AddCogAlias(result, parent.ElementID, liftUnit.ElementCOG);
            AddCogAlias(result, parent.MainPartGlobalId, liftUnit.ElementCOG);

            groupedMemberIds.Add(parent.ElementID);
            foreach (var child in children)
                groupedMemberIds.Add(child.ElementID);
        }

        foreach (var element in gaEnabledElements)
        {
            if (groupedMemberIds.Contains(element.ElementID))
                continue;

            AddCogAlias(result, element.ElementID, element.ElementCOG);
        }

        return result;
    }

    private static bool IsChildOfTrustedAssemblyTarget(
        PrecastElement element,
        IReadOnlyDictionary<string, PrecastElement> allElementsMap)
    {
        if (string.IsNullOrWhiteSpace(element.ParentGlobalId)
            || !allElementsMap.TryGetValue(element.ParentGlobalId, out var parent))
        {
            return false;
        }

        return parent.IncludeInGa
            && parent.Role is IfcElementRole.LiftAssembly or IfcElementRole.LiftableElement
            && HasTrustedPositiveWeight(parent);
    }

    private static bool TryPrepareAssemblyParent(
        PrecastElement parent,
        IReadOnlyList<PrecastElement> allElements,
        out PrecastElement prepared)
    {
        var byId = BuildElementMap(allElements);
        var nonRebarChildren = ResolveChildren(parent, allElements, byId)
            .Where(child => !IsReinforcement(child))
            .ToList();
        var validContributors = nonRebarChildren
            .Where(HasTrustedPositiveWeight)
            .ToList();
        var liftWeight = HasTrustedPositiveWeight(parent)
            ? parent.WeightTons!.Value
            : validContributors.Sum(child => child.WeightTons!.Value);

        if (IsReinforcementAssembly(parent, allElements, byId) || liftWeight <= 0)
        {
            prepared = parent;
            return false;
        }

        var mainPart = nonRebarChildren.FirstOrDefault(child =>
                string.Equals(child.ElementID, parent.MainPartGlobalId, StringComparison.Ordinal))
            ?? validContributors.OrderByDescending(child => child.WeightTons).FirstOrDefault()
            ?? nonRebarChildren.FirstOrDefault(child => child.IncludeInRender);
        var canonicalCog = CanonicalLiftUnitSnapshotBuilder.ResolveCanonicalCog(parent, allElements);
        var liftCog = canonicalCog.PhysicalCogTrustedForGa
            ? new Domain.Common.DomainPoint3D(
                canonicalCog.PhysicalCogProjectM.X,
                canonicalCog.PhysicalCogProjectM.Y,
                canonicalCog.PhysicalCogProjectM.Z)
            : FirstValidPoint(parent.AssemblyLiftCOG, mainPart?.ElementCOG, parent.ElementCOG);

        if (liftCog == null)
        {
            prepared = parent;
            return false;
        }

        prepared = parent.CreateCopy();
        prepared.IncludeInGa = true;
        prepared.Role = IfcElementRole.LiftAssembly;
        prepared.AssemblyLiftWeight = liftWeight;
        prepared.AssemblyLiftCOG = liftCog;
        prepared.ElementCOG = liftCog;
        prepared.COG_M = liftCog;
        if (mainPart != null)
        {
            prepared.ElementLength = FirstPositive(mainPart.ElementLength, mainPart.BBoxLength, mainPart.BoundingBox_M.X);
            prepared.ElementWidth = FirstPositive(mainPart.ElementWidth, mainPart.BBoxWidth, mainPart.BoundingBox_M.Y);
            prepared.ElementHeight = FirstPositive(mainPart.ElementHeight, mainPart.BBoxHeight, mainPart.BoundingBox_M.Z);
        }

        prepared.PhysicalCOGAvailable = true;
        prepared.ChildGlobalIds = nonRebarChildren.Select(child => child.ElementID).Distinct(StringComparer.Ordinal).ToList();
        prepared.AssemblyMemberIds = nonRebarChildren
            .Where(IsVisibleMember)
            .Select(child => child.ElementID)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return true;
    }

    private static PrecastElement PrepareAssemblyMember(PrecastElement child)
    {
        var prepared = child.CreateCopy();
        prepared.IncludeInGa = false;
        if (!HasTrustedPositiveWeight(prepared))
        {
            prepared.WeightTons = 0;
            prepared.ElementWT = 0;
        }

        prepared.PhysicalCOGAvailable = prepared.PhysicalCOGAvailable && IsValidPoint(prepared.ElementCOG);
        return prepared;
    }

    private static HashSet<string> GetEligibleAssemblyParentIds(IReadOnlyList<PrecastElement> elements) =>
        elements
            .Where(parent => parent.Role == IfcElementRole.LiftAssembly)
            .Where(parent => !IsReinforcementAssembly(parent, elements))
            .Select(parent => parent.ElementID)
            .ToHashSet(StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, string> BuildAssemblyParentIdMap(IReadOnlyList<PrecastElement> allElements)
    {
        var byId = BuildElementMap(allElements);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var parent in allElements.Where(element => element.Role == IfcElementRole.LiftAssembly))
        {
            result[parent.ElementID] = parent.ElementID;
            foreach (var child in ResolveChildren(parent, allElements, byId))
                result[child.ElementID] = parent.ElementID;
        }

        return result;
    }

    private static List<PrecastElement> ResolveChildren(
        PrecastElement parent,
        IReadOnlyList<PrecastElement> allElements,
        IReadOnlyDictionary<string, PrecastElement> byId)
    {
        var children = allElements
            .Where(element => string.Equals(element.ParentGlobalId, parent.ElementID, StringComparison.Ordinal))
            .ToList();

        foreach (var childId in parent.ChildGlobalIds)
        {
            if (byId.TryGetValue(childId, out var child) && !children.Contains(child))
                children.Add(child);
        }

        return children;
    }

    private static Dictionary<string, PrecastElement> BuildElementMap(IReadOnlyList<PrecastElement> elements) =>
        elements
            .Where(element => !string.IsNullOrWhiteSpace(element.ElementID))
            .GroupBy(element => element.ElementID, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    private static bool IsVisibleMember(PrecastElement element) =>
        element.IncludeInRender
        && element.ChildClassification != AssemblyChildClassification.IgnoredReference
        && !IfcReferenceGeometryPolicy.IsGridReference(element)
        && !IsReinforcement(element);

    private static bool IsReinforcementAssembly(
        PrecastElement parent,
        IReadOnlyList<PrecastElement> allElements) =>
        IsReinforcementAssembly(parent, allElements, BuildElementMap(allElements));

    private static bool IsReinforcementAssembly(
        PrecastElement parent,
        IReadOnlyList<PrecastElement> allElements,
        IReadOnlyDictionary<string, PrecastElement> byId)
    {
        if (IsReinforcement(parent))
            return true;

        var children = ResolveChildren(parent, allElements, byId);
        return children.Count > 0 && children.All(IsReinforcement);
    }

    private static bool IsReinforcement(PrecastElement element)
    {
        var classification = string.Join(" ",
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

    private static bool HasTrustedPositiveWeight(PrecastElement element) =>
        element.WeightTons is > 0
        && CraneOptimizer.Domain.Policy.IfcWeightTrustPolicy.IsGaApproved(element.WeightTrustLevel);

    private static Domain.Common.DomainPoint3D? FirstValidPoint(params Domain.Common.DomainPoint3D?[] points) =>
        points.FirstOrDefault(IsValidPoint);

    private static bool IsValidPoint(Domain.Common.DomainPoint3D? point) =>
        point != null
        && double.IsFinite(point.X)
        && double.IsFinite(point.Y)
        && double.IsFinite(point.Z);

    private static PrecastElement CloneForReportRollup(PrecastElement source)
    {
        var clone = source.CreateCopy();
        clone.AssemblyMemberIds = source.AssemblyMemberIds.ToList();
        clone.ChildGlobalIds = source.ChildGlobalIds.ToList();
        clone.VisibleGeometryOwnerGlobalIds = source.VisibleGeometryOwnerGlobalIds.ToList();
        clone.HiddenInternalGeometryOwnerGlobalIds = source.HiddenInternalGeometryOwnerGlobalIds.ToList();
        clone.ExtractionWarnings = source.ExtractionWarnings.ToList();
        clone.ValidationWarnings = source.ValidationWarnings.ToList();
        clone.AssemblyLiftCOG = source.AssemblyLiftCOG == null
            ? null
            : new Domain.Common.DomainPoint3D(source.AssemblyLiftCOG.X, source.AssemblyLiftCOG.Y, source.AssemblyLiftCOG.Z);
        return clone;
    }

    private static void AddCogAlias(
        IDictionary<string, GaPointSnapshotDto> target,
        string? liftUnitId,
        Domain.Common.DomainPoint3D? cog)
    {
        if (string.IsNullOrWhiteSpace(liftUnitId)
            || cog == null
            || !double.IsFinite(cog.X)
            || !double.IsFinite(cog.Y)
            || !double.IsFinite(cog.Z))
        {
            return;
        }

        target[liftUnitId] = new GaPointSnapshotDto
        {
            X = cog.X,
            Y = cog.Y,
            Z = cog.Z,
            Frame = "Project",
            Unit = "m"
        };
    }

    private static GaPointSnapshotDto? ResolveGaEquivalentCog(
        CanonicalLiftUnitSnapshotDto unit,
        IReadOnlyDictionary<string, GaPointSnapshotDto> gaEquivalentCogByLiftUnitId)
    {
        foreach (var candidate in new[]
        {
            unit.LiftUnitId,
            unit.SourceElementGlobalId,
            unit.MainPartGlobalId,
            unit.ParentAssemblyGlobalId
        })
        {
            if (!string.IsNullOrWhiteSpace(candidate)
                && gaEquivalentCogByLiftUnitId.TryGetValue(candidate, out var cog))
            {
                return cog;
            }
        }

        return null;
    }

    private static (string Bottom, string Top) ResolveCanonicalElevationRange(CanonicalLiftUnitSnapshotDto unit)
    {
        var cog = ResolvePhysicalCog(unit);
        if (cog == null || unit.HeightM is not > 0)
            return ("\u2014", "\u2014");

        var halfHeight = unit.HeightM.Value / 2d;
        return ($"{cog.Z - halfHeight:0.00} m", $"{cog.Z + halfHeight:0.00} m");
    }

    private static GaPointSnapshotDto? ResolvePhysicalCog(CanonicalLiftUnitSnapshotDto unit)
    {
        var point = unit.Cog.PhysicalCogProjectM;
        if (double.IsFinite(point.X)
            && double.IsFinite(point.Y)
            && double.IsFinite(point.Z)
            && (Math.Abs(point.X) > double.Epsilon || Math.Abs(point.Y) > double.Epsilon || Math.Abs(point.Z) > double.Epsilon))
        {
            return point;
        }

        return unit.LiftCogLocal;
    }

    private static ElementReportRowDto ToRow(
        PrecastElement element,
        int number,
        IReadOnlyDictionary<double, string> levelLabels,
        IReadOnlyDictionary<string, ElementReadinessInfo> readinessLookup,
        IReadOnlyList<PrecastElement> allElements)
    {
        var mapping = ReportWeightMapper.Map(element);
        var identity = CanonicalMarkResolver.Resolve(element);

        var geometry = ElementBindingResolver.ResolveGeometry(element, allElements, preferAssemblyMainPart: true);
        var length = geometry.LengthM;
        var width = geometry.WidthM;
        var height = geometry.HeightM;

        var (volume, volumeSource) = ResolveReportVolume(element, length, width, height);
        var (bottomElevation, topElevation) = ResolveElevationRange(geometry);
        var material = CanonicalMaterialResolver.Resolve(element.ElementMaterial);

        string blockingReasons = "Element ID is missing.";
        string valStatus = "Blocked";
        string valWarnings = "\u2014";

        if (!string.IsNullOrEmpty(element.ElementID) && readinessLookup.TryGetValue(element.ElementID, out var readInfo))
        {
            blockingReasons = readInfo.BlockingReasons;
            valStatus = readInfo.ValidationStatus;
            valWarnings = readInfo.ValidationWarnings;
        }

        return new ElementReportRowDto
        {
            No = number,
            ElementId = element.ElementID,
            ElementMark = identity.DisplayLabel,
            ElementName = ResolveElementName(element),
            ElementType = FirstNonEmpty(element.ElementType, "\u2014"),
            CanonicalMark = identity.DisplayLabel,
            MarkSource = identity.MarkSource ?? string.Empty,
            MarkConfidence = identity.MarkConfidence,
            IsExplicitMark = identity.IsExplicitMark,
            SourceRevitId = FirstNonEmpty(element.ElementRevitId, element.ElementID, "\u2014"),
            StoreyLevel = ResolveStoreyLevel(element, levelLabels, allElements),
            Material = string.IsNullOrWhiteSpace(element.ElementMaterial) ? "\u2014" : element.ElementMaterial,
            MaterialType = material.MaterialType,
            MaterialGrade = material.MaterialGrade,
            RawMaterialValue = material.RawMaterialValue,
            MaterialSource = material.MaterialSource,
            Length = length,
            Width = width,
            Height = height,
            VolumeM3 = volume,
            VolumeSource = volumeSource,
            CogX = geometry.CenterOfGravityM?.X,
            CogY = geometry.CenterOfGravityM?.Y,
            CogZ = geometry.CenterOfGravityM?.Z,
            AssemblyMemberCount = ResolveAssemblyMemberCount(element),
            TopLevelElevation = topElevation,
            BottomLevelElevation = bottomElevation,
            WeightTons = ReportWeightMapper.ResolveWeight(element),
            WeightSource = string.IsNullOrWhiteSpace(element.WeightSource) ? "Missing" : element.WeightSource,
            WeightVerified = mapping.WeightVerified,
            WeightStatus = mapping.WeightStatus,
            WeightWarning = mapping.WarningMessage,
            IncludedInCraneOptimization = element.IncludeInGa ? "Yes" : "No",
            IncludedInAnalysis = element.IncludeInGa ? "Yes" : "No",
            AnalysisExclusionReason = element.IncludeInGa ? "\u2014" : ResolveAnalysisExclusionReason([], blockingReasons),

            // New validation properties
            ValidationStatus = valStatus,
            ValidationWarnings = valWarnings,
            WeightTrustLevel = element.WeightTrustLevel.ToString(),
            BlockingReasons = blockingReasons
        };
    }

    private static int ResolveAssemblyMemberCount(PrecastElement element)
    {
        if (element.AssemblyMemberIds.Count > 0)
            return element.AssemblyMemberIds.Distinct(StringComparer.Ordinal).Count();

        if (element.ChildGlobalIds.Count > 0)
            return element.ChildGlobalIds.Distinct(StringComparer.Ordinal).Count();

        return 1;
    }



    private static string JoinMessages(IReadOnlyList<string> messages) =>
        messages.Count == 0
            ? "\u2014"
            : string.Join("; ", messages.Where(message => !string.IsNullOrWhiteSpace(message)).Distinct(StringComparer.OrdinalIgnoreCase));

    private static string ResolveAnalysisExclusionReason(
        IReadOnlyList<string> canonicalBlockingReasons,
        string fallbackReason)
    {
        var reason = JoinMessages(canonicalBlockingReasons);
        if (!IsPlaceholder(reason))
            return reason;

        return IsPlaceholder(fallbackReason) ? "\u2014" : fallbackReason;
    }
    private static string ResolveElementName(PrecastElement element)
    {
        var name = element.ElementName?.Trim();
        return IsPlaceholder(name) ? "\u2014" : name!;
    }

    private static bool EqualsAny(string value, params string?[] candidates) =>
        candidates.Any(candidate =>
            !string.IsNullOrWhiteSpace(candidate)
            && value.Equals(candidate.Trim(), StringComparison.OrdinalIgnoreCase));

    private static bool IsPlaceholder(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || value.Equals("-", StringComparison.OrdinalIgnoreCase)
        || value.Equals("\u2014", StringComparison.OrdinalIgnoreCase)
        || value.Equals("N/A", StringComparison.OrdinalIgnoreCase)
        || value.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
        || value.Equals("null", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeGenericTypeName(string value)
    {
        var text = value.Trim();
        if (text.Any(char.IsDigit))
            return false;

        var normalized = text.Replace("_", "-", StringComparison.Ordinal).ToUpperInvariant();
        return normalized.Contains("FOOTING", StringComparison.Ordinal)
            || normalized.Contains("FOUNDATION", StringComparison.Ordinal)
            || normalized.Contains("SLAB", StringComparison.Ordinal)
            || normalized.Contains("WALL", StringComparison.Ordinal)
            || normalized.Contains("COLUMN", StringComparison.Ordinal)
            || normalized.Contains("BEAM", StringComparison.Ordinal)
            || normalized.Contains("STAIR", StringComparison.Ordinal)
            || normalized.Contains("PILE", StringComparison.Ordinal);
    }

    private static IReadOnlyDictionary<double, string> BuildLevelLabels(
        IReadOnlyList<PrecastElement> elements,
        IReadOnlyList<PrecastElement> allElements)
    {
        return elements
            .Select(element => GetBaseElevationMeters(element, allElements))
            .Where(elevation => elevation.HasValue)
            .Select(elevation => elevation!.Value)
            .Distinct()
            .OrderBy(z => z)
            .Select((z, index) => new
            {
                Elevation = z,
                Label = $"Level {index + 1}"
            })
            .ToDictionary(x => x.Elevation, x => x.Label);
    }

    private static string ResolveStoreyLevel(
        PrecastElement element,
        IReadOnlyDictionary<double, string> levelLabels,
        IReadOnlyList<PrecastElement> allElements)
    {
        var baseElevation = GetBaseElevationMeters(element, allElements);
        if (baseElevation.HasValue && levelLabels.TryGetValue(baseElevation.Value, out var label))
            return label;

        return "\u2014";
    }

    private static double? GetBaseElevationMeters(
        PrecastElement element,
        IReadOnlyList<PrecastElement> allElements)
    {
        ElementGeometryBinding geometry = ElementBindingResolver.ResolveGeometry(element, allElements, preferAssemblyMainPart: true);
        if (geometry.HeightM is not > 0 || geometry.CenterOfGravityM == null)
            return null;

        return Math.Round(geometry.CenterOfGravityM.Z - geometry.HeightM.Value / 2d, 0);
    }

    private static (double? Volume, string Source) ResolveReportVolume(
        PrecastElement element,
        double? length,
        double? width,
        double? height)
    {
        var explicitVolume = FirstPositive(element.VolumeM3, element.ElementVolume);
        if (explicitVolume is > 0)
            return (explicitVolume, "Explicit");

        if (length is > 0 && width is > 0 && height is > 0)
            return (length.Value * width.Value * height.Value, "Calculated from dimensions");

        return (null, "Missing");
    }

    private static (string Bottom, string Top) ResolveElevationRange(ElementGeometryBinding geometry)
    {
        if (geometry.CenterOfGravityM == null || geometry.HeightM is not > 0)
            return ("\u2014", "\u2014");

        var halfHeight = geometry.HeightM.Value / 2d;
        return ($"{geometry.CenterOfGravityM.Z - halfHeight:0.00} m", $"{geometry.CenterOfGravityM.Z + halfHeight:0.00} m");
    }



    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static double? FirstPositive(params double?[] values) =>
        values.FirstOrDefault(value => value is > 0);

    private static bool IsTrustedWeight(string? weightTrustLevel) =>
        string.Equals(weightTrustLevel, IfcWeightTrustLevel.ExplicitQuantity.ToString(), StringComparison.Ordinal)
        || string.Equals(weightTrustLevel, IfcWeightTrustLevel.ExplicitProperty.ToString(), StringComparison.Ordinal);
}
