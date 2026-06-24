using CraneOptimizer.Application.Reports.DTOs;

namespace CraneOptimizer.Application.Reports.Services;

public static class ReportDataSourceAuditProvider
{
    public static IReadOnlyList<ReportDataSourceAuditRowDto> BuildRows(ReportBuildRequestDto request)
    {
        var rows = new List<ReportDataSourceAuditRowDto>();
        var projectElementCount = request.SourceElements.Count;
        var gaRowCount = request.LiftingResults.Count;
        var matchedCount = request.SourceElements
            .Where(element => !string.IsNullOrWhiteSpace(element.ElementID))
            .Select(element => element.ElementID)
            .Intersect(
                request.LiftingResults
                    .Where(row => !string.IsNullOrWhiteSpace(row.ElementId))
                    .Select(row => row.ElementId),
                StringComparer.Ordinal)
            .Count();
        var unmatchedCount = gaRowCount == 0
            ? projectElementCount
            : Math.Max(0, projectElementCount - matchedCount);
        var gaSourceObject = ResolveGaSourceObject(request.LiftingResults);

        AddHealthRows(rows, request, projectElementCount, gaRowCount, matchedCount, unmatchedCount, gaSourceObject);
        AddElementRows(rows, projectElementCount);
        AddGaRows(rows, gaRowCount, matchedCount, unmatchedCount, request.Metadata.HasGaRun, gaSourceObject);

        return rows
            .Select((row, index) => new ReportDataSourceAuditRowDto
            {
                No = index + 1,
                Area = row.Area,
                ReportColumn = row.ReportColumn,
                DisplayedHeader = row.DisplayedHeader,
                CurrentSource = row.CurrentSource,
                SourceObjectOrFile = row.SourceObjectOrFile,
                SourceField = row.SourceField,
                CurrentAvailability = row.CurrentAvailability,
                RowsFound = row.RowsFound,
                WhyItMayShowDash = row.WhyItMayShowDash,
                RequiredAction = row.RequiredAction
            })
            .ToList();
    }

    private static void AddHealthRows(
        List<ReportDataSourceAuditRowDto> rows,
        ReportBuildRequestDto request,
        int projectElementCount,
        int gaRowCount,
        int matchedCount,
        int unmatchedCount,
        string gaSourceObject)
    {
        rows.Add(Row(
            "Source Health",
            "Project elements count",
            "Project elements count",
            "Already-loaded project/session elements",
            "PrecastElement collection",
            "ReportBuildRequestDto.SourceElements",
            projectElementCount > 0 ? "Available" : "Missing",
            projectElementCount,
            "Scene/project has no normalized elements loaded.",
            "Load or restore a project IFC session before building reports."));

        rows.Add(Row(
            "Source Health",
            "Current in-memory GA result present/missing",
            "Current in-memory GA result present/missing",
            "Current in-memory GA result, then diagnostics fallback",
            gaSourceObject,
            "ReportBuildRequestDto.LiftingResults",
            gaRowCount > 0 ? "Available" : "No GA source rows found",
            gaRowCount,
            "No current GA result in memory, no project Reports/GA diagnostics, diagnostics missing, or diagnostics disabled.",
            gaRowCount > 0 ? "GA source rows found." : "No GA source rows found; run GA with diagnostics or keep the current GA result in memory."));

        rows.Add(Row(
            "Source Health",
            "Element-to-GA matching count",
            "Element-to-GA matching count",
            "Exact stable ID join",
            $"PrecastElement + {gaSourceObject}",
            "PrecastElement.ElementID = GA ElementId",
            matchedCount > 0 ? "Available" : "Missing",
            matchedCount,
            "Element matching failed between project element IDs and GA element IDs.",
            "Ensure GA result/diagnostics use the same stable ElementID values as loaded project elements."));

        rows.Add(Row(
            "Source Health",
            "Element-to-GA unmatched count",
            "Element-to-GA unmatched count",
            "Exact stable ID join",
            $"PrecastElement + {gaSourceObject}",
            "PrecastElement.ElementID = GA ElementId",
            unmatchedCount == 0 ? "None" : "Unmatched rows exist",
            unmatchedCount,
            "GA columns show dash when a report element has no matching GA row.",
            unmatchedCount == 0 ? "No action required." : "Check project element IDs against GA diagnostics element IDs."));
    }

    private static void AddElementRows(List<ReportDataSourceAuditRowDto> rows, int projectElementCount)
    {
        rows.Add(ElementRow("Element Mark", "Element Mark", "PrecastElement.ElementMark, fallback display from PrecastElement.ElementName", projectElementCount, "Mark missing/placeholder, polluted with type/material, and ElementName missing."));
        rows.Add(ElementRow("Element Name", "Element Name", "PrecastElement.ElementName", projectElementCount, "ElementName was not normalized during import or project restore."));
        rows.Add(ElementRow("Element Type", "Element Type", "PrecastElement.ElementType", projectElementCount, "ElementType is missing or still Unknown in normalized project state."));
        rows.Add(ElementRow("Material", "Material", "PrecastElement.ElementMaterial", projectElementCount, "Material is missing from normalized project state."));
        rows.Add(ElementRow("Length", "Length", "PrecastElement.ElementLength / BBoxLength", projectElementCount, "Neither explicit dimension nor bounding box dimension is available."));
        rows.Add(ElementRow("Width", "Width", "PrecastElement.ElementWidth / BBoxWidth", projectElementCount, "Neither explicit dimension nor bounding box dimension is available."));
        rows.Add(ElementRow("Height", "Height", "PrecastElement.ElementHeight / BBoxHeight", projectElementCount, "Neither explicit dimension nor bounding box dimension is available."));
        rows.Add(ElementRow("Volume", "Volume (m3)", "PrecastElement.VolumeM3 / ElementVolume or report-only Length x Width x Height fallback", projectElementCount, "Explicit volume is zero/missing and dimensions are incomplete."));
        rows.Add(ElementRow("Storey / Level", "Storey / Level", "PrecastElement normalized location/elevation fields", projectElementCount, "IFC import/session state does not contain storey/level data."));
        rows.Add(ElementRow("Top Elevation", "Top Level / Elevation", "Bounding box max Z or COG.Z + Height / 2", projectElementCount, "COG/height or bounding box data is unavailable."));
        rows.Add(ElementRow("Bottom Elevation", "Bottom Level / Elevation", "Bounding box min Z or COG.Z - Height / 2", projectElementCount, "COG/height or bounding box data is unavailable."));
        rows.Add(ElementRow("Element Weight", "Element Weight (t)", "PrecastElement.ElementWT / WeightTons", projectElementCount, "Weight is missing, zero, or not trusted in normalized project state."));
        rows.Add(ElementRow("Included in GA Input", "Included in GA Input", "PrecastElement.IncludeInGa", projectElementCount, "Element was intentionally excluded from GA input."));
        rows.Add(ElementRow("Ready for GA", "Ready for GA", "Pre-GA readiness from IncludeInGa, weight, COG, and geometry", projectElementCount, "The element is excluded or required pre-GA fields are missing."));
    }

    private static void AddGaRows(
        List<ReportDataSourceAuditRowDto> rows,
        int gaRowCount,
        int matchedCount,
        int unmatchedCount,
        bool hasGaRun,
        string gaSourceObject)
    {
        var source = "Current in-memory PlacementResult.Positions[].ElementResults, then latest diagnostics via GADiagnosticsReaderService / GAElementDiagnosticDto";
        var sourceObject = gaRowCount > 0 ? gaSourceObject : "No GA source rows found";
        var availability = gaRowCount > 0 ? "Available" : "No GA source rows found";
        var why = "No current GA result in memory, no project Reports/GA history, diagnostics path missing, diagnostics file missing, zero diagnostics rows, diagnostics disabled, or element matching failed between project element IDs and GA element IDs.";
        var action = gaRowCount > 0
            ? unmatchedCount > 0 ? "Some GA rows found; check unmatched ElementID values." : "GA source rows found."
            : "No GA source rows found; reports must show dash/No GA run and must not use selected UI crane.";

        rows.Add(GaRow("Lift Status", "Lift Status", "ElementLiftResult.Status / GAElementDiagnosticDto.LiftStatus", source, sourceObject, availability, gaRowCount, why, action));
        rows.Add(GaRow("Failure Reason", "Failure Reason", "ElementLiftResult failure fields / GAElementDiagnosticDto.BestFailureReason / FailureReason", source, sourceObject, availability, gaRowCount, why, action));
        rows.Add(GaRow("Radius", "Radius", "ElementLiftResult.Radius / GAElementDiagnosticDto.BestRadius", source, sourceObject, availability, gaRowCount, why, action));
        rows.Add(GaRow("Boom Length", "Required Boom Length", "ElementLiftResult.BoomLength / GAElementDiagnosticDto.RequiredBoomLength", source, sourceObject, availability, gaRowCount, why, action));
        rows.Add(GaRow("Hook / Rigging Weight", "Hook / Rigging Weight (t)", "GASettings.AdditionalWt from GA run settings", source, sourceObject, availability, gaRowCount, why, action));
        rows.Add(GaRow("Required Capacity", "Required Capacity (t)", "ElementLiftResult.RequiredCapacity or Element Weight + GASettings.AdditionalWt", source, sourceObject, availability, gaRowCount, why, "Required Capacity = Element Weight + Hook / Rigging Weight."));
        rows.Add(GaRow("Raw Chart Capacity", "Raw Chart Capacity (t)", "ElementLiftResult.RawChartCapacity / GAElementDiagnosticDto.RawChartCapacity", source, sourceObject, availability, gaRowCount, why, "Raw Chart Capacity is the original manufacturer/load-chart value before derating/safety."));
        rows.Add(GaRow("Available Capacity After Derating/Safety", "Available Capacity After Derating/Safety (t)", "ElementLiftResult.AvailableCapacity / GAElementDiagnosticDto.AvailableCapacity", source, sourceObject, availability, gaRowCount, why, "Available Capacity = Raw Chart Capacity x DeratingRatio x SafetyFactor and is already adjusted."));
        rows.Add(GaRow("Capacity Utilization", "Capacity Utilization (%)", "RequiredCapacity / AvailableCapacity x 100", "Report-only calculation from GA values", sourceObject, availability, gaRowCount, why, "Required Capacity is not the same as Element Weight."));
        rows.Add(GaRow("Safety Margin", "Safety Margin (t)", "ElementLiftResult.SafetyMargin or AvailableCapacity - RequiredCapacity", source, sourceObject, availability, gaRowCount, why, action));
        rows.Add(GaRow("Derating Ratio", "Derating Ratio", "GASettings.DeratingRatio used during GA run", source, sourceObject, hasGaRun ? availability : "No GA run", gaRowCount, why, "GA crane metadata must come from the GA run, not selected UI crane."));
        rows.Add(GaRow("Safety Factor", "Safety Factor", "GASettings.SafetyFactor used during GA run", source, sourceObject, hasGaRun ? availability : "No GA run", gaRowCount, why, "GA crane metadata must come from the GA run, not selected UI crane."));
        rows.Add(GaRow("Combined Capacity Factor", "Combined Capacity Factor", "GASettings.DeratingRatio x GASettings.SafetyFactor", "Report-only calculation from GA run settings", sourceObject, hasGaRun ? availability : "No GA run", gaRowCount, why, "Combined factor explains why adjusted capacity is lower than raw chart capacity."));
        rows.Add(GaRow("Wire Length", "Wire Length", "GASettings.WireLength used during GA run", source, sourceObject, hasGaRun ? availability : "No GA run", gaRowCount, why, "GA settings must be captured from the run source."));
        rows.Add(GaRow("Final GA Setup Points", "Final GA Setup Points", "PlacementResult.Positions / diagnostics assigned setup coordinates", source, sourceObject, availability, matchedCount, why, action));
        rows.Add(GaRow("Assigned GA Point", "Assigned GA Point", "ElementLiftResult.FinalOwnerPointId / AssignedPointId / AssignedPointIndex or GAElementDiagnosticDto equivalent", source, sourceObject, availability, gaRowCount, why, action));
    }

    private static ReportDataSourceAuditRowDto ElementRow(
        string reportColumn,
        string header,
        string sourceField,
        int rowsFound,
        string whyItMayShowDash) =>
        Row(
            "Element",
            reportColumn,
            header,
            "Already-loaded project/session elements; IFC import-time source only, reports do not read IFC files.",
            "PrecastElement",
            sourceField,
            rowsFound > 0 ? "Available" : "Missing",
            rowsFound,
            whyItMayShowDash,
            "If normalized PrecastElement contains wrong values, fix IFC extraction/normalization aliases, not Report Builder.");

    private static ReportDataSourceAuditRowDto GaRow(
        string reportColumn,
        string header,
        string sourceField,
        string currentSource,
        string sourceObject,
        string availability,
        int rowsFound,
        string whyItMayShowDash,
        string requiredAction) =>
        Row("GA / Lifting", reportColumn, header, currentSource, sourceObject, sourceField, availability, rowsFound, whyItMayShowDash, requiredAction);

    private static ReportDataSourceAuditRowDto Row(
        string area,
        string reportColumn,
        string displayedHeader,
        string currentSource,
        string sourceObjectOrFile,
        string sourceField,
        string currentAvailability,
        int rowsFound,
        string whyItMayShowDash,
        string requiredAction) =>
        new()
        {
            Area = area,
            ReportColumn = reportColumn,
            DisplayedHeader = displayedHeader,
            CurrentSource = currentSource,
            SourceObjectOrFile = sourceObjectOrFile,
            SourceField = sourceField,
            CurrentAvailability = currentAvailability,
            RowsFound = rowsFound,
            WhyItMayShowDash = whyItMayShowDash,
            RequiredAction = requiredAction
        };

    private static string ResolveGaSourceObject(IReadOnlyList<LiftingResultSourceRowDto> liftingResults)
    {
        if (liftingResults.Count == 0)
            return "No GA source rows found";

        var sourceTypes = liftingResults
            .Select(row => row.SourceType)
            .Where(source => !string.IsNullOrWhiteSpace(source))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (sourceTypes.Count == 0)
            return "PlacementResult / ElementLiftResult or GADiagnosticsReaderService / GAElementDiagnosticDto";

        return string.Join("; ", sourceTypes);
    }
}
