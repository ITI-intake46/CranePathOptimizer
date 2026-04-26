using System.Collections.Generic;
using System.Linq;

namespace CraneOptimizer.Domain.Assignment;

/// <summary>
/// Consolidated source of truth for element-to-crane assignments and validation metrics.
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

    /// <summary>
    /// Detailed evaluation result for every element.
    /// </summary>
    public List<ElementEvaluationResult> EvaluationResults { get; init; } = new();

    /// <summary>
    /// Mapping between crane index and the string IDs of elements assigned to it.
    /// (For backward compatibility and UI display).
    /// </summary>
    public Dictionary<int, List<string>> AssignedElements { get; init; } = new();

    /// <summary>
    /// Number of crane positions that fall inside forbidden zones.
    /// </summary>
    public int ForbiddenZoneViolations { get; set; }

    /// <summary>
    /// Aggregated soft penalty score from all constraints.
    /// </summary>
    public double TotalConstraintPenalty { get; set; }

    public int TotalAssigned => CraneToElements.Values.Sum(l => l.Count);
    public int UnusedCraneCount => CraneToElements.Count(kv => kv.Value.Count == 0);
}
