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
        bool hasFinalAssignments = rows.Any(row => row.IsFinalAssignedToPoint || row.IsFinalAssigned);
        if (hasFinalAssignments)
            rows = rows.Where(row => row.IsFinalAssignedToPoint || row.IsFinalAssigned).ToList();

        rows = ApplyItemsIncluded(templateName, itemsIncluded, rows).ToList();

        return rows
            .Select((row, index) =>
            {
                var values = new Dictionary<string, object?>(row.Values, StringComparer.OrdinalIgnoreCase);
                var rowNumber = index + 1;
                var elementMark = string.IsNullOrWhiteSpace(row.ElementMark) ? row.ElementId : row.ElementMark!;
                var utilization = CalculateUtilization(row);
                var liftStatus = MapLiftStatus(row.InternalLiftStatus);

                SetIfMissing(values, "No", rowNumber);
                SetIfMissing(values, "ElementId", row.ElementId);
                SetIfMissing(values, "ElementMark", elementMark);
                SetIfMissing(values, "ElementType", row.ElementType);
                SetIfMissing(values, "StoreyOrElevation", row.StoreyOrElevation);
                SetIfMissing(values, "GARunTimestamp", row.Timestamp);
                SetIfMissing(values, "ProjectName", row.ProjectName);
                SetIfMissing(values, "IfcFileName", row.IfcFileName);
                SetIfMissing(values, "CraneNameOrId", row.CraneNameOrId);
                SetIfMissing(values, "AssignedSetupNo", row.AssignedSetupNo);
                SetIfMissing(values, "AssignedSetupIndex", row.AssignedSetupNo);
                SetIfMissing(values, "OperatingRadiusM", row.Radius > 0 ? row.Radius : null);
                SetIfMissing(values, "BestRadius", row.Radius > 0 ? row.Radius : null);
                SetIfMissing(values, "RequiredRadius", row.Radius > 0 ? row.Radius : null);
                SetIfMissing(values, "RequiredBoomLengthM", row.BoomLength > 0 ? row.BoomLength : null);
                SetIfMissing(values, "RequiredBoomLength", row.BoomLength > 0 ? row.BoomLength : null);
                SetIfMissing(values, "RequiredCapacityTons", row.RequiredCapacity > 0 ? row.RequiredCapacity : null);
                SetIfMissing(values, "RequiredCapacity", row.RequiredCapacity > 0 ? row.RequiredCapacity : null);
                SetIfMissing(values, "AvailableCapacityTons", row.AvailableCapacity > 0 ? row.AvailableCapacity : null);
                SetIfMissing(values, "AvailableCapacity", row.AvailableCapacity > 0 ? row.AvailableCapacity : null);
                SetIfMissing(values, "CapacityUtilizationPercent", utilization ?? (object)"\u2014");
                SetIfMissing(values, "CapacityMargin", row.AvailableCapacity > 0 && row.RequiredCapacity > 0
                    ? row.AvailableCapacity - row.RequiredCapacity
                    : null);
                SetIfMissing(values, "LiftStatus", liftStatus);

                return new LiftingResultReportRowDto
                {
                    Values = values,
                    No = rowNumber,
                    ElementMark = elementMark,
                    AssignedSetupNo = row.AssignedSetupNo,
                    OperatingRadiusM = row.Radius > 0 ? row.Radius : null,
                    RequiredBoomLengthM = row.BoomLength > 0 ? row.BoomLength : null,
                    RequiredCapacityTons = row.RequiredCapacity > 0 ? row.RequiredCapacity : null,
                    AvailableCapacityTons = row.AvailableCapacity > 0 ? row.AvailableCapacity : null,
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
        if (!IsCapacityUtilizationApplicable(row))
            return null;

        // The optimizer stores AvailableCapacity after its load-chart reduction factors
        // have been applied, so reporting must not apply the 0.90 factor a second time.
        return row.RequiredCapacity / row.AvailableCapacity * 100.0;
    }

    public static string MapLiftStatus(string value)
    {
        return value switch
        {
            "Liftable" or "LIFTABLE" => "Liftable",
            "OutOfRadius" or "Out Of Radius" => "Out Of Radius",
            "Overweight" => "Overweight",
            "HeightFail" or "Height Fail" => "Height Fail",
            "NotCharted" or "No Chart Data" or "NoListedCapacity" => "No Chart Data",
            "SpecialCase" or "MissingLoadChartData" => "No Chart Data",
            _ => "No Chart Data"
        };
    }

    private static void SetIfMissing(IDictionary<string, object?> values, string key, object? value)
    {
        if (!values.TryGetValue(key, out var existing)
            || existing == null
            || existing is string text && string.IsNullOrWhiteSpace(text))
        {
            values[key] = value;
        }
    }

    private static bool IsCapacityUtilizationApplicable(LiftingResultSourceRowDto row)
    {
        var status = MapLiftStatus(row.InternalLiftStatus);
        if (row.AvailableCapacity <= 0 || row.RequiredCapacity <= 0)
            return false;

        return status is "Liftable" or "Overweight" or "Height Fail";
    }

    private static bool IsLiftableOrOverweight(LiftingResultSourceRowDto row)
    {
        var status = MapLiftStatus(row.InternalLiftStatus);
        return status is "Liftable" or "Overweight";
    }
}
