using CraneOptimizer.Application.Reports.Definitions;
using CraneOptimizer.Application.Reports.DTOs;
using CraneOptimizer.UI.Features.F12_Reports.ViewModels;

namespace CraneOptimizer.UI.Features.F12_Reports.Services;

public enum ReportLayoutFitSeverity
{
    Ok,
    Warning,
    TooWide
}

public sealed record ReportLayoutFitResult(
    ReportLayoutFitSeverity Severity,
    double EstimatedWidthPoints,
    double AvailableWidthPoints,
    string Message)
{
    public static ReportLayoutFitResult Ok(double estimatedWidthPoints, double availableWidthPoints) =>
        new(ReportLayoutFitSeverity.Ok, estimatedWidthPoints, availableWidthPoints, string.Empty);
}

public static class ReportLayoutFitEstimator
{
    private const double PointsPerMillimeter = 2.834645669;
    private const double A4LandscapeWidthMillimeters = 297;
    private const double PdfLeftMarginMillimeters = 12;
    private const double PdfRightMarginMillimeters = 12;
    private const double MinimumColumnWidthPoints = 42;
    private const double CellPaddingPoints = 12;
    private const double AverageGlyphWidthFactor = 0.55;

    public static ReportLayoutFitResult EstimateForBuilder(
        IReadOnlyList<ReportColumnOptionViewModel> selectedColumns,
        double textSizePoints)
    {
        var columns = selectedColumns
            .Select(column => new ColumnEstimateInput(column.DisplayHeader, textSizePoints, Array.Empty<string>()))
            .ToList();

        return Estimate(columns);
    }

    public static ReportLayoutFitResult EstimateForReport(ReportDocumentDto report)
    {
        var columns = report.Columns
            .Select(column => new ColumnEstimateInput(
                column.Header,
                report.TextSizePt,
                report.Rows
                    .Take(50)
                    .Select(row =>
                    {
                        row.Values.TryGetValue(column.Key, out var value);
                        return FormatSampleValue(value, column);
                    })
                    .ToList()))
            .ToList();

        return Estimate(columns);
    }

    private static ReportLayoutFitResult Estimate(IReadOnlyList<ColumnEstimateInput> columns)
    {
        var availableWidth = (A4LandscapeWidthMillimeters - PdfLeftMarginMillimeters - PdfRightMarginMillimeters)
            * PointsPerMillimeter;

        var estimatedWidth = columns.Sum(column =>
        {
            var maxCharacters = Math.Max(
                column.Header.Length,
                column.SampleValues.Count == 0 ? 0 : column.SampleValues.Max(value => value.Length));
            var textWidth = maxCharacters * Math.Max(6, column.TextSizePoints) * AverageGlyphWidthFactor;
            return Math.Max(MinimumColumnWidthPoints, textWidth + CellPaddingPoints);
        });

        if (estimatedWidth <= availableWidth)
            return ReportLayoutFitResult.Ok(estimatedWidth, availableWidth);

        var message = "This report may not fit on A4 because it has many columns and large text size. "
            + "Consider reducing text size, removing columns, or exporting in landscape orientation.";

        var severity = estimatedWidth <= availableWidth * 1.15
            ? ReportLayoutFitSeverity.Warning
            : ReportLayoutFitSeverity.TooWide;

        return new ReportLayoutFitResult(severity, estimatedWidth, availableWidth, message);
    }

    private static string FormatSampleValue(object? value, ReportColumnDefinition column)
    {
        if (value == null)
            return column.ValueKind == ReportColumnValueKind.Percentage ? "N/A" : string.Empty;

        if (value is double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d))
                return "N/A";

            var converted = ConvertUnit(d, column);
            return column.ValueKind switch
            {
                ReportColumnValueKind.Percentage => $"{converted:0.0}%",
                ReportColumnValueKind.Tons when column.Unit == "kg" => $"{converted:0}",
                ReportColumnValueKind.Meters when column.Unit == "mm" => $"{converted:0}",
                ReportColumnValueKind.Number or ReportColumnValueKind.Tons or ReportColumnValueKind.Meters => $"{converted:0.00}",
                _ => $"{converted:0.##}"
            };
        }

        return value.ToString() ?? string.Empty;
    }

    private static double ConvertUnit(double value, ReportColumnDefinition column)
    {
        if (column.ValueKind == ReportColumnValueKind.Meters && column.Unit == "mm")
            return value * 1000.0;

        if (column.ValueKind == ReportColumnValueKind.Tons && column.Unit == "kg")
            return value * 1000.0;

        return value;
    }

    private sealed record ColumnEstimateInput(
        string Header,
        double TextSizePoints,
        IReadOnlyList<string> SampleValues);
}
