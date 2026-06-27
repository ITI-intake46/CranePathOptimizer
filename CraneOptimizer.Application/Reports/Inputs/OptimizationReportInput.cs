using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;

namespace CraneOptimizer.Application.Reports.Inputs;

public sealed class OptimizationReportInput
{
    public PlacementInput PlacementInput { get; init; } = null!;
    public PlacementResult PlacementResult { get; init; } = null!;
    public string? PreparedBy { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
