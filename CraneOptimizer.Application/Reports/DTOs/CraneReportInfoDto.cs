namespace CraneOptimizer.Application.Reports.DTOs;

public sealed class CraneReportInfoDto
{
    public string? CraneName { get; init; }
    public string? Manufacturer { get; init; }
    public string? LoadChartName { get; init; }
    public string? WorkingRadiusRangeText { get; init; }
    public string? BoomLengthRangeText { get; init; }
    public double? MaximumCapacityTons { get; init; }
    public double CapacityFactor { get; init; } = 0.90;
}
