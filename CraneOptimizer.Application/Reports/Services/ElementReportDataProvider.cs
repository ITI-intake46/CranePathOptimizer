using CraneOptimizer.Application.Reports.DTOs;
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

        return new ElementReportRowDto
        {
            No = number,
            ElementId = element.ElementID,
            ElementMark = FirstNonEmpty(element.ElementMark, element.ParentAssemblyName, element.ElementID, "Unknown"),
            SourceRevitId = FirstNonEmpty(element.ElementRevitId, element.ElementID, "Unknown"),
            StoreyLevel = ResolveStoreyLevel(element, levelLabels),
            Material = string.IsNullOrWhiteSpace(element.ElementMaterial) ? "N/A" : element.ElementMaterial,
            Length = element.ElementLength ?? element.BBoxLength,
            Width = element.ElementWidth ?? element.BBoxWidth,
            Height = element.ElementHeight ?? element.BBoxHeight,
            VolumeM3 = element.VolumeM3 ?? element.ElementVolume,
            WeightTons = ReportWeightMapper.ResolveWeight(element),
            WeightSource = string.IsNullOrWhiteSpace(element.WeightSource) ? "Missing" : element.WeightSource,
            WeightVerified = mapping.WeightVerified,
            WeightStatus = mapping.WeightStatus,
            WeightWarning = mapping.WarningMessage,
            IncludedInCraneOptimization = element.IncludeInGa && IsGaReady(element) ? "Yes" : "No"
        };
    }

    private static IReadOnlyDictionary<double, string> BuildLevelLabels(IReadOnlyList<PrecastElement> elements)
    {
        return elements
            .Select(GetBaseElevationMeters)
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
        if (levelLabels.TryGetValue(baseElevation, out var label))
            return label;

        return "Level";
    }

    private static double GetBaseElevationMeters(PrecastElement element)
    {
        var z = element.ElementCOG?.Z ?? element.COG_M?.Z ?? 0d;
        var halfHeight = (element.BBoxHeight ?? element.ElementHeight ?? element.BoundingBox_M?.Z ?? 0d) / 2d;
        return Math.Round(z - halfHeight, 0);
    }

    private static bool IsGaReady(PrecastElement element)
    {
        var mapping = ReportWeightMapper.Map(element);
        return element.IncludeInGa
            && mapping.WeightStatus == "OK"
            && ReportWeightMapper.ResolveWeight(element) is > 0;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
