namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models
{
    /// <summary>
    /// Represents the granular failure reason when a discrete load chart lookup results in zero capacity.
    /// </summary>
    public enum CapacityFailureReason
    {
        None,
        MissingLoadChartData,
        RadiusBeyondLoadChart,
        BoomBeyondLoadChart,
        BelowMinimumOperatingRadius,
        BelowMinimumBoomLength,
        NoDiscreteLoadChartEntry,
        ZeroCapacityChartEntry, // kept for backward-compatibility if referenced elsewhere
        NoListedCapacityForBoomRadius,
        LoadChartConditionMismatch,
        OutsideChartEnvelope,
        InvalidBlankCell,
        InvalidZeroCell
    }
}
