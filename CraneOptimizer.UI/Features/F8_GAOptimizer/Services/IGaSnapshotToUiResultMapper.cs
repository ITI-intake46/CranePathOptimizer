using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services;

public interface IGaSnapshotToUiResultMapper
{
    CranePlacementResult Map(
        GaResultSnapshotDto snapshot,
        GaSnapshotUiMappingContext context);
}

public sealed record GaSnapshotUiMappingContext(
    string SourceKind,
    string Classification,
    bool IsReadOnly,
    bool AllowViewportIdentityMapping);
