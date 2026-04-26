namespace CraneOptimizer.Domain.Assignment;

/// <summary>
/// Output of one greedy assignment pass over a decoded chromosome.
/// </summary>
public sealed class CraneAssignment
{
    /// <summary>
    /// CraneIndex -> list of ElementIndex values assigned to it.
    /// </summary>
    public Dictionary<int, List<int>> CraneToElements { get; init; } = new();

    /// <summary>
    /// ElementIndex values that no crane could cover.
    /// </summary>
    public List<int> UnassignedElements { get; init; } = new();

    public int TotalAssigned => CraneToElements.Values.Sum(l => l.Count);
    public int UnusedCraneCount => CraneToElements.Count(kv => kv.Value.Count == 0);
}
