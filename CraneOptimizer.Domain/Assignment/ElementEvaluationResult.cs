using CraneOptimizer.Domain.Schemas.Enums;

namespace CraneOptimizer.Domain.Assignment;

/// <summary>
/// Represents the evaluation result of a single element
/// against the crane positions defined in a chromosome.
/// </summary>
public sealed class ElementEvaluationResult
{
    /// <summary>
    /// Unique identifier of the evaluated element.
    /// </summary>
    public string ElementId { get; init; } = string.Empty;

    /// <summary>
    /// Index of the crane position in Chromosome.CranePositions
    /// that is responsible for lifting this element.
    /// </summary>
    public int AssignedCraneIndex { get; init; }

    /// <summary>
    /// Result of the lift feasibility evaluation.
    /// </summary>
    public LiftStatus Status { get; init; }

    /// <summary>
    /// Working radius between crane and element center of gravity.
    /// </summary>
    public double Radius { get; init; }

    /// <summary>
    /// Required boom length to perform the lift.
    /// </summary>
    public double BoomLength { get; init; }

    /// <summary>
    /// Required lifting capacity to lift the element.
    /// </summary>
    public double RequiredCapacity { get; init; }
}
