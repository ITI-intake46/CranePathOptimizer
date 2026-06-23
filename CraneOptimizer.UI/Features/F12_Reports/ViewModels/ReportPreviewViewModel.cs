using System.Data;
using System.Globalization;
using System.Windows.Input;
using CraneOptimizer.Application.Reports.Definitions;
using CraneOptimizer.Application.Reports.DTOs;
using CraneOptimizer.UI.Core.Base;
using CraneOptimizer.UI.Core.Commands;
using CraneOptimizer.UI.Features.F10_ProjectManagement.ViewModels;
using CraneOptimizer.UI.Features.F12_Reports.Services;

namespace CraneOptimizer.UI.Features.F12_Reports.ViewModels;

public sealed class ReportPreviewViewModel : ViewModelBase
{
    private readonly Func<Task> _exportPdfAsync;
    private readonly Func<Task> _exportExcelAsync;

    public ReportPreviewViewModel(
        ReportDocumentDto report,
        ReportLayoutFitResult layoutFit,
        Func<Task> exportPdfAsync,
        Func<Task> exportExcelAsync)
    {
        _exportPdfAsync = exportPdfAsync;
        _exportExcelAsync = exportExcelAsync;

        ReportName = report.Metadata.ReportName;
        ItemsIncluded = report.Metadata.ItemsIncluded;
        PreviewGeneratedOn = report.Metadata.ReportDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
        PreviewRowsText = $"{Math.Min(50, report.Rows.Count)} of {report.Rows.Count}";
        WarningCount = CountWarnings(report);
        PreviewColumns = BuildPreviewColumns(report.Columns);
        PreviewTableWidth = Math.Max(900, PreviewColumns.Sum(column => column.Width) + 24);
        PreviewRows = BuildPreviewTable(report).DefaultView;
        HasNoRows = report.Rows.Count == 0;
        EmptyStateText = ResolveEmptyStateText(report);
        LayoutWarningText = layoutFit.Message;
        HasLayoutWarning = layoutFit.Severity != ReportLayoutFitSeverity.Ok;
        GaWarningText = report.Metadata.GaWarningText ?? string.Empty;
        HasGaWarning = !string.IsNullOrWhiteSpace(GaWarningText);
        PreviewTextSize = report.TextSizePt;
        PreviewTextAlignment = report.TextAlignment;

        CloseCommand = new RelayCommand(() => RequestClose?.Invoke());
        ExportPdfCommand = new AsyncRelayCommand(_exportPdfAsync);
        ExportExcelCommand = new AsyncRelayCommand(_exportExcelAsync);
    }

    public event Action? RequestClose;

    public string ReportName { get; }
    public string ItemsIncluded { get; }
    public string PreviewRowsText { get; }
    public int WarningCount { get; }
    public string PreviewGeneratedOn { get; }
    public DataView PreviewRows { get; }
    public bool HasNoRows { get; }
    public string EmptyStateText { get; }
    public IReadOnlyList<ReportPreviewColumnViewModel> PreviewColumns { get; }
    public double PreviewTableWidth { get; }
    public string LayoutWarningText { get; }
    public bool HasLayoutWarning { get; }
    public string GaWarningText { get; }
    public bool HasGaWarning { get; }
    public double PreviewTextSize { get; }
    public string PreviewTextAlignment { get; }

    public ICommand CloseCommand { get; }
    public ICommand ExportPdfCommand { get; }
    public ICommand ExportExcelCommand { get; }

    private static int CountWarnings(ReportDocumentDto report) =>
        report.Rows.Count(row =>
            row.Values.TryGetValue("WeightWarning", out var value)
            && value is string warning
            && !string.IsNullOrWhiteSpace(warning)
            && warning != "-");

    private static string ResolveEmptyStateText(ReportDocumentDto report)
    {
        if (!string.IsNullOrWhiteSpace(report.Metadata.GaWarningText))
            return "No GA analysis is available for this project. Run GA optimization first to populate lift result columns.";

        if (report.Metadata.ReportName.Equals("GA Readiness", StringComparison.OrdinalIgnoreCase)
            && report.Metadata.ItemsIncluded.Equals("GA-Ready Elements", StringComparison.OrdinalIgnoreCase))
        {
            return "No elements match GA-Ready Elements. This scope only includes elements with valid verified weights that are included in crane optimization.";
        }

        return "No rows match the selected report scope.";
    }

    private static DataTable BuildPreviewTable(ReportDocumentDto report)
    {
        var table = new DataTable();
        foreach (var column in report.Columns)
            table.Columns.Add(column.Key);

        var rowNumber = 1;
        foreach (var row in report.Rows.Take(50))
        {
            var values = report.Columns.Select(column =>
            {
                row.Values.TryGetValue(column.Key, out var value);
                return FormatPreviewValue(value, column, rowNumber);
            }).ToArray();

            table.Rows.Add(values);
            rowNumber++;
        }

        return table;
    }

    private static IReadOnlyList<ReportPreviewColumnViewModel> BuildPreviewColumns(
        IReadOnlyList<ReportColumnDefinition> columns) =>
        columns
            .Select(column => new ReportPreviewColumnViewModel(
                column.Key,
                column.Header,
                ResolveColumnWidth(column),
                ResolveColumnMinWidth(column)))
            .ToList();

    private static double ResolveColumnWidth(ReportColumnDefinition column)
    {
        if (column.Key.Equals("No", StringComparison.OrdinalIgnoreCase))
            return 64;

        if (column.Key.Contains("Mark", StringComparison.OrdinalIgnoreCase))
            return 360;

        if (column.Key.Contains("Source", StringComparison.OrdinalIgnoreCase)
            || column.Key.Contains("Revit", StringComparison.OrdinalIgnoreCase)
            || column.Key.Contains("ElementId", StringComparison.OrdinalIgnoreCase))
            return 230;

        if (column.Key.Contains("Warning", StringComparison.OrdinalIgnoreCase)
            || column.Key.Contains("Reason", StringComparison.OrdinalIgnoreCase)
            || column.Key.Contains("Action", StringComparison.OrdinalIgnoreCase))
            return 420;

        if (column.Key.Contains("Optimization", StringComparison.OrdinalIgnoreCase))
            return 320;

        if (column.Key.Contains("BoomLength", StringComparison.OrdinalIgnoreCase)
            || column.Header.Contains("Boom Length", StringComparison.OrdinalIgnoreCase))
            return 260;

        if (column.Key.Contains("CapacityUtilization", StringComparison.OrdinalIgnoreCase)
            || column.Header.Contains("Capacity Utilization", StringComparison.OrdinalIgnoreCase))
            return 280;

        if (column.Key.Contains("Capacity", StringComparison.OrdinalIgnoreCase)
            || column.Key.Contains("Margin", StringComparison.OrdinalIgnoreCase))
            return 220;

        return column.ValueKind switch
        {
            ReportColumnValueKind.Status or ReportColumnValueKind.YesNo => 180,
            ReportColumnValueKind.Percentage => 160,
            ReportColumnValueKind.Number or ReportColumnValueKind.Meters or ReportColumnValueKind.Tons => 140,
            _ => 220
        };
    }

    private static double ResolveColumnMinWidth(ReportColumnDefinition column)
    {
        if (column.Key.Equals("No", StringComparison.OrdinalIgnoreCase))
            return 50;

        return column.ValueKind switch
        {
            ReportColumnValueKind.Number or ReportColumnValueKind.Meters or ReportColumnValueKind.Tons => 90,
            ReportColumnValueKind.Status or ReportColumnValueKind.YesNo => 120,
            _ => 120
        };
    }

    private static string FormatPreviewValue(object? value, ReportColumnDefinition column, int rowNumber)
    {
        if (column.Key.Equals("No", StringComparison.OrdinalIgnoreCase))
            return rowNumber.ToString(CultureInfo.CurrentCulture);

        if (value == null)
            return "\u2014";

        if (value is double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d))
                return "N/A";

            var converted = ConvertUnit(d, column);
            return column.ValueKind switch
            {
                ReportColumnValueKind.Percentage => $"{converted:0.0}%",
                ReportColumnValueKind.Tons when column.Unit == "kg" => converted.ToString("0", CultureInfo.CurrentCulture),
                ReportColumnValueKind.Meters when column.Unit == "mm" => converted.ToString("0", CultureInfo.CurrentCulture),
                ReportColumnValueKind.Tons or ReportColumnValueKind.Meters or ReportColumnValueKind.Number => converted.ToString("0.00", CultureInfo.CurrentCulture),
                _ => converted.ToString("0.##", CultureInfo.CurrentCulture)
            };
        }

        return value.ToString() ?? string.Empty;
    }

    private static double ConvertUnit(double value, ReportColumnDefinition column)
    {
        if ((column.ValueKind == ReportColumnValueKind.Meters || column.Unit == "mm") && column.Unit == "mm")
            return value * 1000.0;

        if ((column.ValueKind == ReportColumnValueKind.Tons || column.Unit == "kg") && column.Unit == "kg")
            return value * 1000.0;

        return value;
    }
}

public sealed record ReportPreviewColumnViewModel(
    string Key,
    string Header,
    double Width,
    double MinWidth);
