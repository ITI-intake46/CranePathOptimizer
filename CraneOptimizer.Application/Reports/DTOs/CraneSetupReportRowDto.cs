namespace CraneOptimizer.Application.Reports.DTOs;

public sealed class CraneSetupReportRowDto
{
    public int SetupNo { get; init; }
    public double CraneX { get; init; }
    public double CraneY { get; init; }
    public double CraneZ { get; init; }
    public int LiftedItemsCount { get; init; }
    public double LiftedItemsPercentage { get; init; }
}
