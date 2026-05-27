namespace CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs
{
    /// <summary>
    /// Simple export model for one GA debug report file.
    /// Keeps the CSV writer independent from GA engine objects.
    /// </summary>
    internal sealed record GADebugReportDto(
        Guid RequestId,
        DateTime CreatedAtUtc,
        IReadOnlyList<GADebugReportRowDto> Rows,
        string? ErrorMessage);

    /// <summary>
    /// Simple export row for one element checked at one crane point.
    /// </summary>
    internal sealed record GADebugReportRowDto(
        int PointNumber,
        double PointX,
        double PointY,
        string ElementId,
        string ElementMark,
        string ElementType,
        double? Weight,
        double CogX,
        double CogY,
        double CogZ,
        double Radius,
        double BoomLength,
        double RequiredCapacity,
        double AvailableCapacity,
        string Status,
        bool NewlyCovered,
        string ReachStatus,
        string ChartStatus,
        bool IsExactMatch,
        bool IsSteppedRadius,
        bool IsBlankCell,
        bool IsZeroCell,
        double SafetyMargin,
        string LookupMode,
        double RequiredRadius,
        double SelectedChartRadius,
        double SelectedChartBoomLength);
}
