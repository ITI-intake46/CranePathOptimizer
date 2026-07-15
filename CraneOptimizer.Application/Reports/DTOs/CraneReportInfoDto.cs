namespace CraneOptimizer.Application.Reports.DTOs;

public sealed class CraneReportInfoDto
{
    public string? CraneName { get; init; }
    public string? CraneModelOrSpecification { get; init; }
    public string? ModelName { get; init; }
    public string? SpecificationName { get; init; }
    public string? Manufacturer { get; init; }
    public string? CraneType { get; init; }
    public string? LoadChartName { get; init; }
    public string? WorkingRadiusRangeText { get; init; }
    public string? BoomLengthRangeText { get; init; }
    public string? MinRadiusText { get; init; }
    public string? MaxRadiusText { get; init; }
    public string? MinBoomLengthText { get; init; }
    public string? MaxBoomLengthText { get; init; }
    public double? MaximumCapacityTons { get; init; }
    public double? CounterweightTons { get; init; }
    public string? OutriggerConfiguration { get; init; }
    public string? VehicleLengthText { get; init; }
    public string? VehicleWidthText { get; init; }
    public string? VehicleHeightText { get; init; }
    public string? OutriggerLengthText { get; init; }
    public string? OutriggerWidthText { get; init; }
    public string? SafetyFactorText { get; init; }
    public string? DeratingRatioText { get; init; }
    public string? CombinedCapacityFactorText { get; init; }
    public string? WireLengthText { get; init; }
    public string? CraneHoistWireLengthText { get; init; }
    public string? LoadChartSource { get; init; }
    public double CapacityFactor { get; init; } = 0.90;
}
