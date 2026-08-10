namespace CraneOptimizer.Application.GAAnalysis;

public sealed record GaAnalysisCurrentContext(
    string ProjectGuid,
    string OptimizationRunId,
    string SourceFingerprint,
    IReadOnlyDictionary<string, string> FingerprintComponents);

public sealed class GaAnalysisFreshness
{
    public bool IsCurrent { get; init; }
    public IReadOnlyList<string> ChangedComponents { get; init; } = Array.Empty<string>();
    public string Explanation { get; init; } = string.Empty;
}
