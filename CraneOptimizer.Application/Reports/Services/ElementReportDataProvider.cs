using CraneOptimizer.Application.Reports.DTOs;
using CraneOptimizer.Application.Services.Identity;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Application.Reports.Services;

public sealed class ElementReportDataProvider : IElementReportDataProvider
{
    private readonly IReportableElementResolver _resolver;

    public ElementReportDataProvider(IReportableElementResolver resolver)
    {
        _resolver = resolver;
    }

    public IReadOnlyList<ElementReportRowDto> BuildRows(
        string templateName,
        string itemsIncluded,
        IEnumerable<PrecastElement> sourceElements,
        IEnumerable<PrecastElement> selectedElements)
    {
        var selectedIds = selectedElements
            .Where(element => !string.IsNullOrWhiteSpace(element.ElementID))
            .Select(element => element.ElementID)
            .ToHashSet(StringComparer.Ordinal);

        var scoped = ApplyItemsIncluded(sourceElements, itemsIncluded, selectedIds);
        var reportable = _resolver.ResolveReportableElements(scoped);
        var filtered = ApplyTemplateFilter(reportable, templateName);
        var levelLabels = BuildLevelLabels(filtered);

        return filtered
            .Select((element, index) => ToRow(element, index + 1, levelLabels))
            .ToList();
    }

    private static IEnumerable<PrecastElement> ApplyItemsIncluded(
        IEnumerable<PrecastElement> sourceElements,
        string itemsIncluded,
        ISet<string> selectedIds)
    {
        return itemsIncluded switch
        {
            "Rendered Elements Only" => sourceElements.Where(element => element.IncludeInRender),
            "GA-Ready Elements" => sourceElements.Where(IsGaReady),
            "Needs Review Elements" => sourceElements.Where(element => ReportWeightMapper.Map(element).WeightStatus != "OK" || !element.IncludeInGa),
            "Selected Elements Only" => sourceElements.Where(element => !string.IsNullOrWhiteSpace(element.ElementID) && selectedIds.Contains(element.ElementID)),
            _ => sourceElements
        };
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
        IReadOnlyDictionary<double, string> levelLabels)
    {
        var mapping = ReportWeightMapper.Map(element);
        var identity = CanonicalMarkResolver.Resolve(element);

        var length = FirstPositive(element.ElementLength, element.BBoxLength, element.BoundingBox_M?.X);
        var width = FirstPositive(element.ElementWidth, element.BBoxWidth, element.BoundingBox_M?.Y);
        var height = FirstPositive(element.ElementHeight, element.BBoxHeight, element.BoundingBox_M?.Z);

        var (volume, volumeSource) = ResolveReportVolume(element, length, width, height);
        var (bottomElevation, topElevation) = ResolveElevationRange(element, height);

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
            ReadyForGa = IsGaReady(element) ? "Yes" : "No",
            MissingReadinessReason = ResolveReadinessReason(element, mapping)
        };
    }

    private static string ResolveReadinessReason(PrecastElement element, WeightReportMapping mapping)
    {
        if (!element.IncludeInGa)
            return "Element is not included in crane optimization.";

        if (mapping.WeightStatus != "OK")
            return mapping.WarningMessage == "-" ? $"Weight status is {mapping.WeightStatus}." : mapping.WarningMessage;

        if (ReportWeightMapper.ResolveWeight(element) is not > 0)
            return "Weight is missing or invalid.";

        return "\u2014";
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

    private static bool IsGaReady(PrecastElement element)
    {
        var mapping = ReportWeightMapper.Map(element);
        return element.IncludeInGa
            && mapping.WeightStatus == "OK"
            && ReportWeightMapper.ResolveWeight(element) is > 0;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static double? FirstPositive(params double?[] values) =>
        values.FirstOrDefault(value => value is > 0);
}
