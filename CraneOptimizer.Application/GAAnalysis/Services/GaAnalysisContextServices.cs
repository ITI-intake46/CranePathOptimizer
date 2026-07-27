using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Snapshots;

namespace CraneOptimizer.Application.GAAnalysis;

public sealed class GaAnalysisCurrencyService : IGaAnalysisCurrencyService
{
    public GaAnalysisFreshness Compare(
        GaLiftAnalysisResult analysis,
        GaAnalysisCurrentContext currentContext)
    {
        var changed = new SortedSet<string>(StringComparer.Ordinal);
        var componentNames = analysis.SourceFingerprintComponents.Keys
            .Concat(currentContext.FingerprintComponents.Keys)
            .Distinct(StringComparer.Ordinal);
        foreach (var componentName in componentNames)
        {
            analysis.SourceFingerprintComponents.TryGetValue(componentName, out var saved);
            currentContext.FingerprintComponents.TryGetValue(componentName, out var current);
            if (!string.Equals(saved, current, StringComparison.Ordinal))
                changed.Add(componentName);
        }

        var identityMatches = analysis.ProjectGuid == currentContext.ProjectGuid
            && analysis.OptimizationRunId == currentContext.OptimizationRunId;
        var aggregateMatches = string.Equals(
            analysis.SourceSnapshotFingerprint,
            currentContext.SourceFingerprint,
            StringComparison.Ordinal);
        var isCurrent = identityMatches && aggregateMatches && changed.Count == 0;
        return new GaAnalysisFreshness
        {
            IsCurrent = isCurrent,
            ChangedComponents = changed.ToArray(),
            Explanation = isCurrent
                ? "The saved analysis matches the selected GA run and engineering-input fingerprint."
                : changed.Count > 0
                    ? $"Engineering inputs changed: {string.Join(", ", changed)}."
                    : "The saved analysis does not match the selected GA run fingerprint."
        };
    }
}

public sealed class GaAnalysisCurrentContextProvider : IGaAnalysisCurrentContextProvider
{
    public GaAnalysisCurrentContext Capture(GaResultSnapshotDto snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var fingerprint = string.IsNullOrWhiteSpace(snapshot.Model.OptimizationInputFingerprint)
            ? snapshot.Model.ModelFingerprint
            : snapshot.Model.OptimizationInputFingerprint;
        return new GaAnalysisCurrentContext(
            snapshot.Project.ProjectGuid,
            snapshot.Run.OptimizationRunId,
            fingerprint,
            new Dictionary<string, string>(
                snapshot.Model.OptimizationInputFingerprintComponents,
                StringComparer.Ordinal));
    }
}
