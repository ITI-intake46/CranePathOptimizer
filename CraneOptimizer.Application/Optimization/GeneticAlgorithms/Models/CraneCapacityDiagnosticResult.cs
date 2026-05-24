namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models
{
    /// <summary>
    /// Represents the full diagnostic result of a crane capacity load chart lookup.
    /// </summary>
    public record CraneCapacityDiagnosticResult(
        double Capacity,
        double EffectiveRadius,
        double EffectiveBoomLength,
        CapacityFailureReason FailureReason);
}
