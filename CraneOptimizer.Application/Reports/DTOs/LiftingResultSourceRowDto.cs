namespace CraneOptimizer.Application.Reports.DTOs;

public sealed class LiftingResultSourceRowDto
{
    public IReadOnlyDictionary<string, object?> Values { get; init; } = new Dictionary<string, object?>();
    public string SourceType { get; init; } = string.Empty;
    public string ElementId { get; init; } = string.Empty;
    public string? ElementMark { get; init; }

    // Phase 5: Canonical Display Identity
    public string? CanonicalMark { get; init; }
    public string? MarkSource { get; init; }
    public string? MarkConfidence { get; init; }
    public bool IsExplicitMark { get; init; }

    public string? ElementType { get; init; }
    public string? StoreyOrElevation { get; init; }
    public string? Timestamp { get; init; }
    public string? ProjectName { get; init; }
    public string? IfcFileName { get; init; }
    public string? CraneNameOrId { get; init; }
    public int AssignedSetupNo { get; init; }
    public double Radius { get; init; }
    public double BoomLength { get; init; }
    public double RequiredCapacity { get; init; }
    public double AvailableCapacity { get; init; }
    public string InternalLiftStatus { get; init; } = string.Empty;
    public bool IsFinalAssignedToPoint { get; init; }
    public bool IsFinalAssigned { get; init; }

    // Phase 5 Assembly Rollup fields
    public string? ParentAssemblyGlobalId { get; init; }
    public string? MainPartGlobalId { get; init; }
    public double? AssemblyLiftWeight { get; init; }
    public Domain.Common.DomainPoint3D? AssemblyLiftCOG { get; init; }
    public IReadOnlyList<string>? AssemblyMemberIds { get; init; }
}
