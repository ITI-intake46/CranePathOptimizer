namespace CraneOptimizer.Application.Reports.DTOs;

public sealed class CraneSetupReportRowDto
{
    public int SetupNo { get; init; }
    public string SetupReference { get; init; } = string.Empty;
    public double CraneX { get; init; }
    public double CraneY { get; init; }
    public double CraneZ { get; init; }
    public int LiftedItemsCount { get; init; }
    public double LiftedItemsPercentage { get; init; }
    public string Zone { get; init; } = string.Empty;
}
