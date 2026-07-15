using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;

public sealed class GaExecutionResult
{
    public PlacementInput OriginalInput { get; init; } = new();
    public PlacementResult BestResult { get; init; } = new();
    public string OptimizationRunId { get; init; } = string.Empty;
    public string FinalStageRunId { get; init; } = string.Empty;
    public int FinalStageIndex { get; init; }
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset CompletedAtUtc { get; init; }
    public long DurationMilliseconds { get; init; }
    public string? TerminationReason { get; init; }
    public GASettings RequestedSettings { get; init; } = new();
    public GASettings EffectiveFinalSettings { get; init; } = new();
    public IReadOnlyList<GaStageExecutionResult> Stages { get; init; } = Array.Empty<GaStageExecutionResult>();
}

public sealed class GaStageExecutionResult
{
    public string OptimizationRunId { get; init; } = string.Empty;
    public string StageRunId { get; init; } = string.Empty;
    public int StageIndex { get; init; }
    public GASettings EffectiveSettings { get; init; } = new();
    public PlacementResult Result { get; init; } = new();
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset CompletedAtUtc { get; init; }
    public long DurationMilliseconds { get; init; }
    public string? TerminationReason { get; init; }
    public bool WasSelectedAsFinal { get; init; }
}

public sealed class GaSnapshotMappingContext
{
    public string ProjectGuid { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public string? ProjectDatabaseId { get; init; }
    public string? ProjectRoot { get; init; }
    public string? IfcFilePath { get; init; }
    public string ApplicationVersion { get; init; } = string.Empty;
    public string? GitCommitHash { get; init; }
}
