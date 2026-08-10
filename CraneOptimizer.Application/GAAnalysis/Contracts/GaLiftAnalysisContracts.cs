using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;

namespace CraneOptimizer.Application.GAAnalysis;

public interface IGaLiftAnalysisService
{
    Task<GaLiftAnalysisResult> AnalyzeAsync(
        GaLiftAnalysisRequest request,
        CancellationToken cancellationToken = default);
}

public interface IGaAlternativeScreeningService
{
    Task<GaAlternativeScreeningResult> ScreenAsync(
        GaResultSnapshotDto snapshot,
        CurrentCraneAdequacy adequacy,
        IReadOnlyList<GaCraneCandidate> candidates,
        CancellationToken cancellationToken = default);
}

public interface IGaCraneCatalogProvider
{
    Task<IReadOnlyList<GaCraneCandidate>> GetCandidatesAsync(
        int? currentCraneSpecificationId,
        CancellationToken cancellationToken = default);
}

public interface IGaTechnicalEvidenceProvider
{
    Task<IReadOnlyList<GaTechnicalEvidenceFile>> CollectAsync(
        GaResultSnapshotDto snapshot,
        CancellationToken cancellationToken = default);
}

public interface IGaLiftAnalysisStore
{
    Task SaveAsync(GaLiftAnalysisResult analysis, CancellationToken cancellationToken = default);
    Task<GaLiftAnalysisResult?> LoadAsync(
        string projectGuid,
        string optimizationRunId,
        CancellationToken cancellationToken = default);
    Task<GaLiftAnalysisResult?> LoadLatestAsync(
        string projectGuid,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(
        string projectGuid,
        string optimizationRunId,
        CancellationToken cancellationToken = default);
}

public interface IGaLiftAnalysisPathResolver
{
    GaLiftAnalysisPaths Resolve(string projectGuid, string optimizationRunId);
}

public interface IGaLiftAnalysisSerializer
{
    byte[] Serialize(GaLiftAnalysisResult analysis);
    byte[] Serialize(GaLiftAnalysisManifest manifest);
    GaLiftAnalysisResult? DeserializeAnalysis(ReadOnlySpan<byte> json);
    GaLiftAnalysisManifest? DeserializeManifest(ReadOnlySpan<byte> json);
}

/// <summary>
/// Compares the persisted analysis fingerprint with the exact GA snapshot selected
/// for display. The historical plan named this service "Currency" in the sense of
/// whether an analysis is current, not monetary currency.
/// </summary>
public interface IGaAnalysisCurrencyService
{
    GaAnalysisFreshness Compare(
        GaLiftAnalysisResult analysis,
        GaAnalysisCurrentContext currentContext);
}

public interface IGaAnalysisCurrentContextProvider
{
    GaAnalysisCurrentContext Capture(GaResultSnapshotDto snapshot);
}
