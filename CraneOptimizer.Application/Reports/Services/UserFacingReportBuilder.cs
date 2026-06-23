using System.Globalization;
using CraneOptimizer.Application.Reports.Definitions;
using CraneOptimizer.Application.Reports.DTOs;

namespace CraneOptimizer.Application.Reports.Services;

public sealed class UserFacingReportBuilder : IReportBuilder
{
    private readonly IElementReportDataProvider _elementProvider;
    private readonly ILiftingResultReportDataProvider _liftingProvider;

    public UserFacingReportBuilder(
        IElementReportDataProvider elementProvider,
        ILiftingResultReportDataProvider liftingProvider)
    {
        _elementProvider = elementProvider;
        _liftingProvider = liftingProvider;
    }

    public Task<ReportDocumentDto> BuildAsync(
        ReportBuildRequestDto request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var columns = request.SelectedColumns
            .Where(column => column.IsSelected)
            .OrderBy(column => column.Order)
            .ToList();

        IReadOnlyList<ReportDataRowDto> rows;
        IReadOnlyList<ReportSummaryItemDto> summary;
        var effectiveItemsIncluded = request.ItemsIncluded;

        if (request.Family == ReportFamily.ElementList)
        {
            var elementRows = _elementProvider.BuildRows(
                request.TemplateName,
                effectiveItemsIncluded,
                request.SourceElements,
                request.SelectedElements);

            if (IsGaReadinessReadyOnlyEmpty(request, elementRows))
            {
                effectiveItemsIncluded = "All Elements";
                elementRows = _elementProvider.BuildRows(
                    request.TemplateName,
                    effectiveItemsIncluded,
                    request.SourceElements,
                    request.SelectedElements);
            }

            var liftingRows = _liftingProvider.BuildRows(
                request.TemplateName,
                "Optimization Result",
                request.LiftingResults);

            rows = elementRows.Select(ToReportRow).ToList();
            EnrichElementRowsWithLiftingValues(rows, liftingRows);
            EnsureSelectedColumnValues(rows, columns);
            summary = BuildElementSummary(elementRows);
        }
        else
        {
            var liftingRows = _liftingProvider.BuildRows(
                request.TemplateName,
                request.ItemsIncluded,
                request.LiftingResults);

            rows = liftingRows.Select(ToReportRow).ToList();
            if (!request.Metadata.HasGaRun && rows.Count == 0)
                rows = [BuildNoGaRunPlaceholderRow(columns)];

            EnsureSelectedColumnValues(rows, columns);

            summary = request.Metadata.HasGaRun
                ? BuildLiftingSummary(liftingRows)
                : BuildNoGaRunSummary();
        }

        ApplySequentialNumbers(rows);

        var metadata = new ReportMetadataDto
        {
            ReportName = request.TemplateName,
            ItemsIncluded = effectiveItemsIncluded,
            ProjectName = request.Metadata.ProjectName,
            ReportDate = request.Metadata.ReportDate,
            CompanyName = request.Metadata.CompanyName,
            CompanyLogoPath = request.Metadata.CompanyLogoPath,
            CreatedBy = request.Metadata.CreatedBy,
            IssuedTo = request.Metadata.IssuedTo,
            Revision = request.Metadata.Revision,
            HasGaRun = request.Metadata.HasGaRun,
            GaRunStatusText = request.Metadata.GaRunStatusText,
            GaWarningText = request.Metadata.GaWarningText,
            CraneInfo = request.Metadata.CraneInfo,
            LiftingAccessories = request.Metadata.LiftingAccessories,
            SummaryItems = summary,
            GeneralNotes = request.Metadata.GeneralNotes
        };

        return Task.FromResult(new ReportDocumentDto
        {
            InternalFamily = request.Family,
            Metadata = metadata,
            Columns = columns,
            Rows = rows,
            CraneSetups = request.Family == ReportFamily.LiftingResult
                ? request.CraneSetups
                : Array.Empty<CraneSetupReportRowDto>(),
            PreferA3Landscape = columns.Count > 10,
            TextAlignment = columns.FirstOrDefault()?.Alignment ?? "Left",
            TextSizePt = ParseTextSize(columns.FirstOrDefault()?.TextSize)
        });
    }

    private static double ParseTextSize(string? value)
    {
        if (double.TryParse(value, CultureInfo.InvariantCulture, out var parsed))
            return Math.Clamp(parsed, 6, 32);

        return value switch
        {
            "Small" => 7,
            "Large" => 12,
            _ => 10
        };
    }

    private static bool IsGaReadinessReadyOnlyEmpty(
        ReportBuildRequestDto request,
        IReadOnlyList<ElementReportRowDto> elementRows) =>
        request.TemplateName.Equals(ReportTemplateCatalog.GaReadiness, StringComparison.OrdinalIgnoreCase)
        && request.ItemsIncluded.Equals("GA-Ready Elements", StringComparison.OrdinalIgnoreCase)
        && elementRows.Count == 0
        && request.SourceElements.Count > 0;

    private static ReportDataRowDto ToReportRow(ElementReportRowDto row) => new()
    {
        Values =
        {
            ["No"] = row.No,
            ["ElementId"] = row.ElementId,
            ["ElementID"] = row.ElementId,
            ["ElementMark"] = row.ElementMark,
            ["SourceRevitId"] = row.SourceRevitId,
            ["ElementRevitId"] = row.SourceRevitId,
            ["StoreyLevel"] = row.StoreyLevel,
            ["Material"] = row.Material,
            ["Length"] = row.Length,
            ["Width"] = row.Width,
            ["Height"] = row.Height,
            ["VolumeM3"] = row.VolumeM3,
            ["WeightTons"] = row.WeightTons,
            ["WeightSource"] = row.WeightSource,
            ["WeightVerified"] = row.WeightVerified,
            ["WeightStatus"] = row.WeightStatus,
            ["WeightWarning"] = row.WeightWarning,
            ["IncludedInCraneOptimization"] = row.IncludedInCraneOptimization
        }
    };

    private static ReportDataRowDto ToReportRow(LiftingResultReportRowDto row)
    {
        var values = new Dictionary<string, object?>(row.Values, StringComparer.OrdinalIgnoreCase)
        {
            ["No"] = row.No,
            ["ElementMark"] = row.ElementMark,
            ["AssignedSetupNo"] = row.AssignedSetupNo,
            ["OperatingRadiusM"] = row.OperatingRadiusM,
            ["RequiredBoomLengthM"] = row.RequiredBoomLengthM,
            ["RequiredCapacityTons"] = row.RequiredCapacityTons,
            ["AvailableCapacityTons"] = row.AvailableCapacityTons,
            ["LiftStatus"] = row.LiftStatus
        };

        values["CapacityUtilizationPercent"] = row.CapacityUtilizationPercent.HasValue
            ? row.CapacityUtilizationPercent.Value
            : values.TryGetValue("CapacityUtilizationPercent", out var existing) && existing != null
                ? existing
                : "\u2014";

        return new ReportDataRowDto { Values = values };
    }

    private static void EnrichElementRowsWithLiftingValues(
        IReadOnlyList<ReportDataRowDto> elementRows,
        IReadOnlyList<LiftingResultReportRowDto> liftingRows)
    {
        if (elementRows.Count == 0 || liftingRows.Count == 0)
            return;

        var liftingValues = liftingRows
            .Select(row => row.Values)
            .Where(values => values.Count > 0)
            .ToList();

        foreach (var elementRow in elementRows)
        {
            var keys = GetElementMatchKeys(elementRow.Values);
            var liftValues = liftingValues.FirstOrDefault(values =>
                keys.Any(key =>
                    Matches(values, "ElementId", key)
                    || Matches(values, "ElementID", key)
                    || Matches(values, "SourceRevitId", key)
                    || Matches(values, "ElementRevitId", key)
                    || Matches(values, "ElementMark", key)));

            if (liftValues == null)
                continue;

            foreach (var (columnKey, value) in liftValues)
            {
                if (columnKey.Equals("No", StringComparison.OrdinalIgnoreCase)
                    || columnKey.Equals("ElementMark", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!elementRow.Values.ContainsKey(columnKey) || IsMissingValue(elementRow.Values[columnKey]))
                    elementRow.Values[columnKey] = value;
            }
        }
    }

    private static IReadOnlyList<string> GetElementMatchKeys(IReadOnlyDictionary<string, object?> values) =>
    [
        .. new[]
        {
            GetStringValue(values, "ElementId"),
            GetStringValue(values, "ElementID"),
            GetStringValue(values, "SourceRevitId"),
            GetStringValue(values, "ElementRevitId"),
            GetStringValue(values, "ElementMark")
        }
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
    ];

    private static void EnsureSelectedColumnValues(
        IReadOnlyList<ReportDataRowDto> rows,
        IReadOnlyList<ReportColumnDefinition> columns)
    {
        foreach (var row in rows)
        {
            foreach (var column in columns)
            {
                if (!row.Values.TryGetValue(column.Key, out var value) || IsMissingValue(value))
                    row.Values[column.Key] = "\u2014";
            }
        }
    }

    private static string GetStringValue(IReadOnlyDictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out var value) ? value?.ToString() ?? string.Empty : string.Empty;

    private static bool Matches(IReadOnlyDictionary<string, object?> values, string key, string expected) =>
        !string.IsNullOrWhiteSpace(expected)
        && values.TryGetValue(key, out var value)
        && IsSameElementToken(value?.ToString(), expected);

    private static bool IsSameElementToken(string? actual, string expected)
    {
        if (string.IsNullOrWhiteSpace(actual) || string.IsNullOrWhiteSpace(expected))
            return false;

        var actualText = actual.Trim();
        var expectedText = expected.Trim();
        if (string.Equals(actualText, expectedText, StringComparison.OrdinalIgnoreCase))
            return true;

        return expectedText.Length >= 4
            && actualText.Contains(expectedText, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMissingValue(object? value) =>
        value == null
        || value is string text && string.IsNullOrWhiteSpace(text);

    private static ReportDataRowDto BuildNoGaRunPlaceholderRow(IReadOnlyList<ReportColumnDefinition> columns)
    {
        var row = new ReportDataRowDto();
        foreach (var column in columns)
            row.Values[column.Key] = column.Key.Equals("No", StringComparison.OrdinalIgnoreCase) ? 1 : "\u2014";

        return row;
    }

    private static void ApplySequentialNumbers(IReadOnlyList<ReportDataRowDto> rows)
    {
        for (var index = 0; index < rows.Count; index++)
            rows[index].Values["No"] = index + 1;
    }

    private static IReadOnlyList<ReportSummaryItemDto> BuildElementSummary(IReadOnlyList<ElementReportRowDto> rows)
    {
        int total = rows.Count;
        int verified = rows.Count(row => row.WeightVerified == "Yes");
        int estimated = rows.Count(row => row.WeightStatus == "Estimated");
        int missing = rows.Count(row => row.WeightStatus == "Missing");
        int gaReady = rows.Count(row => row.IncludedInCraneOptimization == "Yes");

        return
        [
            Summary("Total Items", total.ToString(CultureInfo.InvariantCulture)),
            Summary("Verified Weights", CountPercent(verified, total)),
            Summary("Estimated Weights", CountPercent(estimated, total)),
            Summary("Missing Weights", CountPercent(missing, total)),
            Summary("GA-Ready Items", CountPercent(gaReady, total))
        ];
    }

    private static IReadOnlyList<ReportSummaryItemDto> BuildLiftingSummary(IReadOnlyList<LiftingResultReportRowDto> rows)
    {
        int total = rows.Count;
        var utilizationValues = rows
            .Select(row => row.CapacityUtilizationPercent)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();

        var items = new List<ReportSummaryItemDto>
        {
            Summary("Total Items", total.ToString(CultureInfo.InvariantCulture)),
            Summary("Liftable", CountPercent(rows.Count(row => row.LiftStatus == "Liftable"), total)),
            Summary("Out Of Radius", CountPercent(rows.Count(row => row.LiftStatus == "Out Of Radius"), total)),
            Summary("Overweight", CountPercent(rows.Count(row => row.LiftStatus == "Overweight"), total)),
            Summary("Height Fail", CountPercent(rows.Count(row => row.LiftStatus == "Height Fail"), total)),
            Summary("No Chart Data", CountPercent(rows.Count(row => row.LiftStatus == "No Chart Data"), total))
        };

        if (utilizationValues.Count > 0)
        {
            items.Add(Summary("Average Capacity Utilization", $"{utilizationValues.Average():0.0}%"));
            items.Add(Summary("Maximum Capacity Utilization", $"{utilizationValues.Max():0.0}%"));
        }

        return items;
    }

    private static IReadOnlyList<ReportSummaryItemDto> BuildNoGaRunSummary() =>
    [
        Summary("GA Run", "No GA run"),
        Summary("Crane", "\u2014"),
        Summary("Coverage", "\u2014"),
        Summary("Setup Count", "\u2014"),
        Summary("Runtime", "\u2014")
    ];

    private static ReportSummaryItemDto Summary(string label, string value) => new()
    {
        Label = label,
        Value = value
    };

    private static string CountPercent(int count, int total)
    {
        if (total <= 0)
            return count.ToString(CultureInfo.InvariantCulture);

        return $"{count} ({count * 100.0 / total:0.0}%)";
    }
}
