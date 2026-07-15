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
        var levelLabels = BuildLevelLabels(filtered, elementsList);

        var readinessSummary = _readinessService.BuildSummary(filtered, isCheckerEnabled: true);
        var lookup = BuildElementReadinessLookup(readinessSummary);

        return filtered
            .Select((element, index) => ToRow(element, index + 1, levelLabels, lookup, elementsList))
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
                        ReadyForGa = diagnostic.IsReadyForGa ? "Yes" : "No",
                        MissingReadinessReason = diagnostic.IsReadyForGa ? "\u2014" : blockingReasons
                    };
                },
                StringComparer.OrdinalIgnoreCase);
    private static IReadOnlyList<PrecastElement> ApplyTemplateFilter(
        IReadOnlyList<PrecastElement> elements,
        string templateName)
    {
        if (templateName.Equals(ReportTemplateCatalog.GaReadiness, StringComparison.OrdinalIgnoreCase))
        {
            return elements
                .Where(element => element.Role == IfcElementRole.LiftAssembly && element.IncludeInGa)
                .ToList();
        }

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
            StoreyLevel = ResolveStoreyLevel(element, levelLabels, allElements),
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



    private static string JoinMessages(IReadOnlyList<string> messages) =>
        messages.Count == 0
            ? "\u2014"
            : string.Join("; ", messages.Where(message => !string.IsNullOrWhiteSpace(message)).Distinct(StringComparer.OrdinalIgnoreCase));
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
}
