namespace CraneOptimizer.Application.Reports.DTOs;

public sealed class LiftingResultReportRowDto
{
    public IReadOnlyDictionary<string, object?> Values { get; init; } = new Dictionary<string, object?>();
    public int No { get; init; }
    public string ElementMark { get; init; } = string.Empty;
    public int AssignedSetupNo { get; init; }
    public double? OperatingRadiusM { get; init; }
    public double? RequiredBoomLengthM { get; init; }
    public double? RequiredCapacityTons { get; init; }
    public double? AvailableCapacityTons { get; init; }
    public double? CapacityUtilizationPercent { get; init; }
    public string LiftStatus { get; init; } = string.Empty;
}
