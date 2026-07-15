using CraneOptimizer.Application.Reports.DTOs;

namespace CraneOptimizer.Application.Reports.Services;

public sealed class LiftingResultReportDataProvider : ILiftingResultReportDataProvider
{
    public IReadOnlyList<LiftingResultReportRowDto> BuildRows(
        string templateName,
        string itemsIncluded,
        IEnumerable<LiftingResultSourceRowDto> sourceRows)
    {
        var rows = sourceRows.ToList();
        if (!templateName.Equals(ReportTemplateCatalog.GaAssignmentReview, StringComparison.OrdinalIgnoreCase))
            rows = SelectOneFinalReportRowPerElement(rows);

        rows = ApplyItemsIncluded(templateName, itemsIncluded, rows).ToList();

        return rows
            .Select((row, index) =>
            {
                var values = new Dictionary<string, object?>(row.Values, StringComparer.OrdinalIgnoreCase);
                var rowNumber = index + 1;
                var elementMark = FirstNonEmpty(row.CanonicalMark, row.ElementMark, row.ElementId);
                var utilization = CalculateUtilization(row);
                var liftStatus = MapLiftStatus(row.InternalLiftStatus);
                var effectiveRadius = FirstPositive(row.Radius, GetDouble(values, "BestFailedRequiredRadius"));
                var effectiveBoomLength = FirstPositive(row.BoomLength, GetDouble(values, "BestFailedRequiredBoomLength"));
                var elementWeight = FirstPositiveDouble(GetDouble(values, "Weight"), GetDouble(values, "ElementWeight"), GetDouble(values, "ElementWeightTons"));
                var hookRiggingWeight = FirstPositiveDouble(GetDouble(values, "HookRiggingWeight"), GetDouble(values, "AdditionalWeight"), GetDouble(values, "AdditionalWt"));
                var requiredCapacity = FirstPositiveDouble(row.RequiredCapacity, GetDouble(values, "RequiredCapacity"), GetDouble(values, "RequiredCapacityTons"));
                if (!requiredCapacity.HasValue && elementWeight.HasValue && hookRiggingWeight.HasValue)
                    requiredCapacity = elementWeight.Value + hookRiggingWeight.Value;

                var rawChartCapacity = FirstPositiveDouble(GetDouble(values, "RawChartCapacity"), GetDouble(values, "ChartCapacity"));
                var availableCapacity = FirstPositiveDouble(row.AvailableCapacity, GetDouble(values, "AvailableCapacity"), GetDouble(values, "AvailableCapacityTons"));
                utilization = availableCapacity is > 0 && requiredCapacity is > 0
                    ? requiredCapacity.Value / availableCapacity.Value * 100.0
                    : (double?)null;
                var calculatedSafetyMargin = availableCapacity is > 0 && requiredCapacity is > 0
                    ? availableCapacity.Value - requiredCapacity.Value
                    : (double?)null;
                var effectiveSafetyMargin = FirstRealValue(GetValue(values, "SafetyMargin"), GetValue(values, "BestFailedSafetyMargin"), calculatedSafetyMargin);

                SetIfMissing(values, "No", rowNumber);
                SetIfMissing(values, "ElementId", row.ElementId);
                SetIfMissing(values, "ElementMark", elementMark);
                SetIfMissing(values, "ElementType", row.ElementType);
                SetIfMissing(values, "Weight", elementWeight);
                SetIfMissing(values, "ElementWeight", elementWeight);
                SetIfMissing(values, "HookRiggingWeight", hookRiggingWeight);
                SetIfMissing(values, "RawChartCapacity", rawChartCapacity);
                SetIfMissing(values, "StoreyOrElevation", row.StoreyOrElevation);
                SetIfMissing(values, "GARunTimestamp", row.Timestamp);
                SetIfMissing(values, "ProjectName", row.ProjectName);
                SetIfMissing(values, "IfcFileName", row.IfcFileName);
                SetIfMissing(values, "CraneNameOrId", row.CraneNameOrId);
                SetIfMissing(values, "AssignedSetupNo", row.AssignedSetupNo);
                SetIfMissing(values, "AssignedSetupIndex", row.AssignedSetupNo);
                SetIfMissing(values, "AssignedGaPoint", ResolveAssignedGaPoint(values, row));
                SetIfMissing(values, "OperatingRadiusM", effectiveRadius);
                SetIfMissing(values, "BestRadius", effectiveRadius);
                SetIfMissing(values, "RequiredRadius", effectiveRadius);
                SetIfMissing(values, "RequiredBoomLengthM", effectiveBoomLength);
                SetIfMissing(values, "RequiredBoomLength", effectiveBoomLength);
                SetIfMissing(values, "RequiredCapacityTons", requiredCapacity);
                SetIfMissing(values, "RequiredCapacity", requiredCapacity);
                SetIfMissing(values, "AvailableCapacityTons", availableCapacity);
                SetIfMissing(values, "AvailableCapacity", availableCapacity);
                SetIfMissing(values, "CapacityUtilizationPercent", utilization ?? (object)"\u2014");
                SetIfMissing(values, "CapacityMargin", calculatedSafetyMargin);
                SetIfMissing(values, "LiftStatus", liftStatus);
                SetIfMissing(values, "SafetyMargin", effectiveSafetyMargin);

                SetIfMissing(values, "ParentAssemblyGlobalId", row.ParentAssemblyGlobalId);
                SetIfMissing(values, "MainPartGlobalId", row.MainPartGlobalId);
                SetIfMissing(values, "AssemblyLiftWeight", row.AssemblyLiftWeight);
                SetIfMissing(values, "AssemblyLiftCOG", row.AssemblyLiftCOG != null ? $"({row.AssemblyLiftCOG.X.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}; {row.AssemblyLiftCOG.Y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}; {row.AssemblyLiftCOG.Z.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)})" : null);
                SetIfMissing(values, "AssemblyMemberIds", row.AssemblyMemberIds != null ? string.Join(", ", row.AssemblyMemberIds) : null);

                return new LiftingResultReportRowDto
                {
                    Values = values,
                    No = rowNumber,
                    ElementMark = elementMark,
                    AssignedSetupNo = row.AssignedSetupNo,
                    OperatingRadiusM = effectiveRadius as double?,
                    RequiredBoomLengthM = effectiveBoomLength as double?,
                    RequiredCapacityTons = requiredCapacity,
                    AvailableCapacityTons = availableCapacity,
                    CapacityUtilizationPercent = utilization,
                    LiftStatus = liftStatus
                };
            })
            .ToList();
    }

    private static IEnumerable<LiftingResultSourceRowDto> ApplyItemsIncluded(
        string templateName,
        string itemsIncluded,
        IEnumerable<LiftingResultSourceRowDto> rows)
    {
        if (itemsIncluded == "Liftable Items Only")
            return rows.Where(row => MapLiftStatus(row.InternalLiftStatus) == "Liftable");

        if (itemsIncluded == "Failed Items Only"
            || templateName.Equals(ReportTemplateCatalog.FailedLiftsOnly, StringComparison.OrdinalIgnoreCase))
        {
            return rows.Where(row => MapLiftStatus(row.InternalLiftStatus) != "Liftable");
        }

        return rows;
    }

    public static double? CalculateUtilization(LiftingResultSourceRowDto row)
    {
        if (row.AvailableCapacity <= 0 || row.RequiredCapacity <= 0)
            return null;

        // The optimizer stores AvailableCapacity after its load-chart reduction factors
        // have been applied, so reporting must not apply the 0.90 factor a second time.
        return row.RequiredCapacity / row.AvailableCapacity * 100.0;
    }

    public static string MapLiftStatus(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "\u2014";

        return value switch
        {
            "Liftable" or "LIFTABLE" => "Liftable",
            "OutOfRadius" or "Out Of Radius" => "Out Of Radius",
            "Overweight" => "Overweight",
            "HeightFail" or "Height Fail" => "Height Fail",
            "NotCharted" or "No Chart Data" or "NoListedCapacity" => "No Chart Data",
            "MissingLoadChartData" => "No Chart Data",
            "SpecialCase" => "SpecialCase",
            _ => "\u2014"
        };
    }

    private static List<LiftingResultSourceRowDto> SelectOneFinalReportRowPerElement(IReadOnlyList<LiftingResultSourceRowDto> rows)
    {
        return rows
            .GroupBy(GetElementGroupKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(row => row.IsFinalAssignedToPoint || row.IsFinalAssigned)
                .ThenByDescending(HasBestFailedCandidate)
                .ThenByDescending(HasFailureDetails)
                .ThenBy(row => row.AssignedSetupNo <= 0 ? int.MaxValue : row.AssignedSetupNo)
                .First())
            .ToList();
    }

    private static string GetElementGroupKey(LiftingResultSourceRowDto row)
    {
        if (GetText(row.Values, "IsAssemblyGroup").Equals("Yes", StringComparison.OrdinalIgnoreCase))
        {
            string parentAssemblyId = GetText(row.Values, "ParentAssemblyGlobalId");
            if (!IsPlaceholder(parentAssemblyId))
                return $"assembly:{parentAssemblyId}";

            string mainPartId = GetText(row.Values, "MainPartGlobalId");
            if (!IsPlaceholder(mainPartId))
                return $"assembly-main:{mainPartId}";
        }

        if (!string.IsNullOrWhiteSpace(row.ElementId))
            return row.ElementId;

        if (!string.IsNullOrWhiteSpace(row.ElementMark))
            return row.ElementMark;

        return Guid.NewGuid().ToString("N");
    }

    private static bool HasBestFailedCandidate(LiftingResultSourceRowDto row) =>
        GetText(row.Values, "HasBestFailedCandidate").Equals("Yes", StringComparison.OrdinalIgnoreCase)
        || !IsPlaceholder(GetText(row.Values, "BestFailedPointId"))
        || !IsPlaceholder(GetText(row.Values, "BestFailureReason"));

    private static bool HasFailureDetails(LiftingResultSourceRowDto row) =>
        !IsPlaceholder(GetText(row.Values, "FailureReason"))
        || !IsPlaceholder(GetText(row.Values, "FailureCategory"))
        || MapLiftStatus(row.InternalLiftStatus) is not "Liftable" and not "\u2014";

    private static string ResolveAssignedGaPoint(IReadOnlyDictionary<string, object?> values, LiftingResultSourceRowDto row)
    {
        var id = FirstMeaningfulText(values, "FinalOwnerPointId", "AssignedPointId");
        if (!string.IsNullOrWhiteSpace(id))
            return NormalizePointId(id);

        var assignedPointIndex = FirstMeaningfulText(values, "AssignedPointIndex");
        if (int.TryParse(assignedPointIndex, out var parsedAssignedPoint) && parsedAssignedPoint >= 0)
            return $"Point_{parsedAssignedPoint + 1}";

        var assignedSetupIndex = FirstMeaningfulText(values, "AssignedSetupIndex");
        if (int.TryParse(assignedSetupIndex, out var parsedSetup) && parsedSetup > 0)
            return $"Point_{parsedSetup}";

        return row.AssignedSetupNo > 0 ? $"Point_{row.AssignedSetupNo}" : "\u2014";
    }

    private static string NormalizePointId(string id)
    {
        var text = id.Trim();
        if (text.StartsWith("Point", StringComparison.OrdinalIgnoreCase))
            return text;

        return int.TryParse(text, out var parsed) ? $"Point_{parsed}" : text;
    }

    private static string FirstMeaningfulText(IReadOnlyDictionary<string, object?> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = GetText(values, key);
            if (!IsPlaceholder(value))
                return value;
        }

        return string.Empty;
    }

    private static string GetText(IReadOnlyDictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out var value) ? value?.ToString() ?? string.Empty : string.Empty;

    private static bool IsPlaceholder(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || value == "\u2014"
        || value.Equals("N/A", StringComparison.OrdinalIgnoreCase)
        || value.Equals("null", StringComparison.OrdinalIgnoreCase);

    private static void SetIfMissing(IDictionary<string, object?> values, string key, object? value)
    {
        if (!values.TryGetValue(key, out var existing)
            || existing == null
            || existing is string text && IsPlaceholder(text))
        {
            values[key] = value;
        }
    }

    private static object? FirstPositive(params double[] values)
    {
        foreach (var value in values)
        {
            if (value > 0)
                return value;
        }

        return null;
    }

    private static double? FirstPositiveDouble(params double[] values)
    {
        foreach (var value in values)
        {
            if (value > 0)
                return value;
        }

        return null;
    }

    private static object? FirstRealValue(params object?[] values)
    {
        foreach (var value in values)
        {
            if (value == null)
                continue;

            if (value is string text && IsPlaceholder(text))
                continue;

            return value;
        }

        return null;
    }

    private static object? GetValue(IReadOnlyDictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;

    private static double GetDouble(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || value == null)
            return 0;

        if (value is double d)
            return d;

        return double.TryParse(value.ToString(), out var parsed) ? parsed : 0;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}
