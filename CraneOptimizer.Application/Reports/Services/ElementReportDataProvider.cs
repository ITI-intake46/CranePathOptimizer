using CraneOptimizer.Application.Reports.DTOs;
using CraneOptimizer.Application.Services;
using CraneOptimizer.Application.Services.Identity;
using CraneOptimizer.Domain.Diagnostics;
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
        var reportable = _resolver.ResolveReportableElements(elementsList);
        var filtered = ApplyTemplateFilter(reportable, templateName);
        var levelLabels = BuildLevelLabels(filtered);

        var readinessSummary = _readinessService.BuildSummary(filtered, isCheckerEnabled: true);
        var lookup = BuildElementReadinessLookup(filtered, readinessSummary);

        return filtered
            .Select((element, index) => ToRow(element, index + 1, levelLabels, lookup))
            .ToList();
    }

    private sealed class ElementReadinessInfo
    {
        public string ValidationStatus { get; init; } = "Valid";
        public string ValidationWarnings { get; init; } = "\u2014";
        public string ReadyForGa { get; init; } = "No";
        public string MissingReadinessReason { get; init; } = "\u2014";
    }

    private static IReadOnlyDictionary<string, ElementReadinessInfo> BuildElementReadinessLookup(
        IReadOnlyList<PrecastElement> elements,
        ImportReadinessSummary readinessSummary)
    {
        var allElementsMap = new Dictionary<string, PrecastElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var el in elements)
        {
            if (!string.IsNullOrEmpty(el.ElementID) && !allElementsMap.ContainsKey(el.ElementID))
            {
                allElementsMap[el.ElementID] = el;
            }
        }

        var lookup = new Dictionary<string, ElementReadinessInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var el in elements)
        {
            if (string.IsNullOrEmpty(el.ElementID))
                continue;

            var blockingReasons = new List<string>();

            if (!el.IncludeInGa)
            {
                blockingReasons.Add("Element is not included in crane optimization.");
            }

            if (el.Role == IfcElementRole.NeedsReview ||
                el.Role == IfcElementRole.GroupingOnly ||
                el.Role == IfcElementRole.InternalAuxiliary ||
                el.Role == IfcElementRole.Excluded)
            {
                blockingReasons.Add($"Invalid element role for GA: {el.Role}.");
            }

            if (!el.WeightTons.HasValue || el.WeightTons.Value <= 0)
            {
                blockingReasons.Add("Weight is missing or invalid (<= 0).");
            }
            else
            {
                var hasExplicitTrustedWeight =
                    el.WeightTrustLevel == IfcWeightTrustLevel.ExplicitQuantity ||
                    el.WeightTrustLevel == IfcWeightTrustLevel.ExplicitProperty;

                if (!hasExplicitTrustedWeight)
                {
                    blockingReasons.Add($"Weight status is {ReportWeightMapper.Map(el).WeightStatus} (Trust Level: {el.WeightTrustLevel}).");
                }
            }

            // Parent-child double count risk check
            if (!string.IsNullOrWhiteSpace(el.ParentGlobalId) && allElementsMap.TryGetValue(el.ParentGlobalId, out var parentElement))
            {
                if (parentElement.IncludeInGa &&
                    (parentElement.Role == IfcElementRole.LiftAssembly ||
                     parentElement.Role == IfcElementRole.LiftableElement))
                {
                    var parentWeightTrusted = parentElement.WeightTrustLevel == IfcWeightTrustLevel.ExplicitQuantity ||
                                              parentElement.WeightTrustLevel == IfcWeightTrustLevel.ExplicitProperty;
                    if (parentWeightTrusted)
                    {
                        blockingReasons.Add("Parent-child double-counting risk: child component is enabled alongside its parent assembly.");
                    }
                }
            }

            // COG check
            if (el.ElementCOG == null ||
                (Math.Abs(el.ElementCOG.X) < 0.0001 &&
                 Math.Abs(el.ElementCOG.Y) < 0.0001 &&
                 Math.Abs(el.ElementCOG.Z) < 0.0001) ||
                double.IsNaN(el.ElementCOG.X) || double.IsInfinity(el.ElementCOG.X) ||
                double.IsNaN(el.ElementCOG.Y) || double.IsInfinity(el.ElementCOG.Y) ||
                double.IsNaN(el.ElementCOG.Z) || double.IsInfinity(el.ElementCOG.Z))
            {
                blockingReasons.Add("Center of Gravity (COG) is missing, zero, or invalid.");
            }

            var warningMessages = new List<string>();
            if (readinessSummary.Messages != null)
            {
                foreach (var msg in readinessSummary.Messages)
                {
                    if (msg.AffectedElementIds != null && msg.AffectedElementIds.Contains(el.ElementID, StringComparer.OrdinalIgnoreCase))
                    {
                        warningMessages.Add(msg.Message);
                    }
                }
            }

            var isBlocked = blockingReasons.Count > 0;
            var valStatus = isBlocked ? "Blocked" : (warningMessages.Count > 0 ? "Warning" : "Ready");
            var readyForGa = isBlocked ? "No" : "Yes";
            var missingReason = isBlocked ? string.Join("; ", blockingReasons) : "\u2014";
            var warnings = warningMessages.Count > 0 ? string.Join("; ", warningMessages.Distinct(StringComparer.OrdinalIgnoreCase)) : "\u2014";

            lookup[el.ElementID] = new ElementReadinessInfo
            {
                ValidationStatus = valStatus,
                ValidationWarnings = warnings,
                ReadyForGa = readyForGa,
                MissingReadinessReason = missingReason
            };
        }

        return lookup;
    }

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

    private static ElementReportRowDto ToRow(
        PrecastElement element,
        int number,
        IReadOnlyDictionary<double, string> levelLabels,
        IReadOnlyDictionary<string, ElementReadinessInfo> readinessLookup)
    {
        var mapping = ReportWeightMapper.Map(element);
        var identity = CanonicalMarkResolver.Resolve(element);

        var length = FirstPositive(element.ElementLength, element.BBoxLength, element.BoundingBox_M?.X);
        var width = FirstPositive(element.ElementWidth, element.BBoxWidth, element.BoundingBox_M?.Y);
        var height = FirstPositive(element.ElementHeight, element.BBoxHeight, element.BoundingBox_M?.Z);

        var (volume, volumeSource) = ResolveReportVolume(element, length, width, height);
        var (bottomElevation, topElevation) = ResolveElevationRange(element, height);

        string readyForGa = "No";
        string missingReason = "Element ID is missing.";
        string valStatus = "Blocked";
        string valWarnings = "\u2014";

        if (!string.IsNullOrEmpty(element.ElementID) && readinessLookup.TryGetValue(element.ElementID, out var readInfo))
        {
            readyForGa = readInfo.ReadyForGa;
            missingReason = readInfo.MissingReadinessReason;
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
            StoreyLevel = ResolveStoreyLevel(element, levelLabels),
            Material = string.IsNullOrWhiteSpace(element.ElementMaterial) ? "\u2014" : element.ElementMaterial,
            Length = length,
            Width = width,
            Height = height,
            VolumeM3 = volume,
            VolumeSource = volumeSource,
            TopLevelElevation = topElevation,
            BottomLevelElevation = bottomElevation,
            WeightTons = ReportWeightMapper.ResolveWeight(element),
            WeightSource = string.IsNullOrWhiteSpace(element.WeightSource) ? "Missing" : element.WeightSource,
            WeightVerified = mapping.WeightVerified,
            WeightStatus = mapping.WeightStatus,
            WeightWarning = mapping.WarningMessage,
            IncludedInCraneOptimization = element.IncludeInGa ? "Yes" : "No",
            ReadyForGa = readyForGa,
            MissingReadinessReason = missingReason,

            // New validation properties
            ValidationStatus = valStatus,
            ValidationWarnings = valWarnings,
            WeightTrustLevel = element.WeightTrustLevel.ToString(),
            ReadinessStatus = valStatus,
            BlockingReasons = missingReason
        };
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

    private static IReadOnlyDictionary<double, string> BuildLevelLabels(IReadOnlyList<PrecastElement> elements)
    {
        return elements
            .Select(GetBaseElevationMeters)
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
        IReadOnlyDictionary<double, string> levelLabels)
    {
        var baseElevation = GetBaseElevationMeters(element);
        if (baseElevation.HasValue && levelLabels.TryGetValue(baseElevation.Value, out var label))
            return label;

        return "\u2014";
    }

    private static double? GetBaseElevationMeters(PrecastElement element)
    {
        var height = FirstPositive(element.ElementHeight, element.BBoxHeight, element.BoundingBox_M?.Z);
        if (height is not > 0)
            return null;

        var z = ResolveCogZ(element);
        if (!z.HasValue)
            return null;

        return Math.Round(z.Value - height.Value / 2d, 0);
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

    private static (string Bottom, string Top) ResolveElevationRange(PrecastElement element, double? height)
    {
        var z = ResolveCogZ(element);
        if (!z.HasValue || height is not > 0)
            return ("\u2014", "\u2014");

        var halfHeight = height.Value / 2d;
        return ($"{z.Value - halfHeight:0.00} m", $"{z.Value + halfHeight:0.00} m");
    }

    private static double? ResolveCogZ(PrecastElement element)
    {
        if (element.COG_M != null && HasNonZeroPoint(element.COG_M))
            return element.COG_M.Z;

        if (element.ElementCOG != null && HasNonZeroPoint(element.ElementCOG))
            return element.ElementCOG.Z;

        return null;
    }

    private static bool HasNonZeroPoint(CraneOptimizer.Domain.Common.DomainPoint3D point) =>
        Math.Abs(point.X) > double.Epsilon
        || Math.Abs(point.Y) > double.Epsilon
        || Math.Abs(point.Z) > double.Epsilon;



    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static double? FirstPositive(params double?[] values) =>
        values.FirstOrDefault(value => value is > 0);
}
