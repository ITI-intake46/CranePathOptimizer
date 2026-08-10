using CraneOptimizer.AI.Host;

namespace CraneOptimizer.AI.Workflow
{
    /// <summary>
    /// AI-owned interpretation of the evidence already supplied by the host.
    ///
    /// Important boundary: this class does not reproduce or replace the GA engine's
    /// preflight. The current host contract does not expose the optimizer dialog's
    /// authoritative run-readiness decision, so candidate/readiness facts from import
    /// screens are treated as advisory evidence only.
    /// </summary>
    public sealed class AiOptimizerEvidenceAssessment
    {
        public bool HasProject { get; init; }
        public bool HasIfcElements { get; init; }
        public bool HasZones { get; init; }
        public bool HasCrane { get; init; }
        public bool HasCurrentLoadChartEvidence { get; init; }
        public bool CorePrerequisitesComplete { get; init; }
        public int MissingCorePrerequisiteCount { get; init; }

        public bool HasNoCandidateAdvisory { get; init; }
        public bool HasEstimatedWeightAdvisory { get; init; }
        public int? AdvisoryTrustedLiftUnits { get; init; }
        public IReadOnlyList<int> AdvisoryCandidateCounts { get; init; } = Array.Empty<int>();

        public bool HasOptimizationResult { get; init; }
        public int HistoricalEvaluatedLiftUnits { get; init; }
        public bool CandidateEvidenceConflict { get; init; }
        public bool CandidateScopeMismatch { get; init; }

        /// <summary>
        /// Always false for the current div host contract. The AI can inspect core
        /// prerequisites and advisory signals, but it cannot certify the optimizer's
        /// current internal preflight without an explicit authoritative host contract.
        /// </summary>
        public bool HasAuthoritativeOptimizerEligibility { get; init; }

        public string AuthorityNote { get; init; } =
            "The AI snapshot does not expose the optimizer dialog's authoritative current-run preflight. Use the optimizer's built-in readiness indicator for the definitive run decision.";
    }

    public static class AiOptimizerEvidenceAssessor
    {
        private static readonly string[] NoCandidateCodes =
        {
            "GaCannotStartNoCandidates",
            "GaPreRunNoCandidates",
            "NoGaCandidates"
        };

        private static readonly string[] EstimatedWeightCodes =
        {
            "EstimatedWeightBlockedFromGA",
            "ElementWeightReviewNeeded",
            "MissingTrustedWeight",
            "SelectedElementWeightNotTrusted"
        };

        public static AiOptimizerEvidenceAssessment Assess(AiWorkflowContextSnapshot context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var candidateCounts = context.ScreenContexts
                .SelectMany(screen => screen.Facts)
                .Where(pair => pair.Key.Equals(AiWorkflowFactKeys.TrustedGaLiftUnits, StringComparison.OrdinalIgnoreCase)
                               || pair.Key.Equals(AiWorkflowFactKeys.TrustedGroupedLiftUnits, StringComparison.OrdinalIgnoreCase)
                               || pair.Key.Equals(AiWorkflowFactKeys.GaCandidateElements, StringComparison.OrdinalIgnoreCase))
                .Select(pair => int.TryParse(pair.Value, out var parsed) ? parsed : -1)
                .Where(value => value >= 0)
                .Distinct()
                .OrderBy(value => value)
                .ToArray();

            var advisoryTrusted = candidateCounts.Length == 0 ? (int?)null : candidateCounts.Max();
            var issues = context.ScreenContexts.SelectMany(screen => screen.Issues).ToArray();
            var hasNoCandidateIssue = issues.Any(issue => NoCandidateCodes.Any(code => issue.Code.Equals(code, StringComparison.OrdinalIgnoreCase)));
            var hasNoCandidateCount = candidateCounts.Any(value => value == 0);
            var hasNoCandidateStatus = context.ScreenContexts
                .SelectMany(screen => screen.Facts)
                .Any(pair => (pair.Key.Equals("OptimizerPrecheckStatus", StringComparison.OrdinalIgnoreCase)
                              || pair.Key.Equals("PreRunStatus", StringComparison.OrdinalIgnoreCase))
                             && pair.Value.Equals("BlockedNoGaCandidates", StringComparison.OrdinalIgnoreCase));
            var hasNoCandidate = hasNoCandidateIssue || hasNoCandidateCount || hasNoCandidateStatus;
            var hasEstimatedWeight = issues.Any(issue => EstimatedWeightCodes.Any(code => issue.Code.Equals(code, StringComparison.OrdinalIgnoreCase)));

            var resultTotal = context.OptimizationResult.HasResult
                ? Math.Max(0, context.OptimizationResult.TotalElementCount)
                : 0;
            var conflictingCandidateCounts = candidateCounts.Length > 1;
            var candidateConflict = conflictingCandidateCounts
                || (resultTotal > 0 && hasNoCandidate);
            var candidateScopeMismatch = conflictingCandidateCounts
                || (resultTotal > 0 && candidateCounts.Any(value => value > 0 && value != resultTotal));

            var hasCurrentLoadChart = HasCurrentLoadChartEvidence(context);
            var hasProject = context.HasProject;
            var hasIfc = context.ElementCount > 0;
            var hasZones = context.ZoneCount > 0;
            var hasCrane = context.CraneCount > 0;
            var missingCore = (hasProject ? 0 : 1)
                + (hasIfc ? 0 : 1)
                + (hasZones ? 0 : 1)
                + (hasCrane ? 0 : 1)
                + (hasCurrentLoadChart ? 0 : 1);

            return new AiOptimizerEvidenceAssessment
            {
                HasProject = hasProject,
                HasIfcElements = hasIfc,
                HasZones = hasZones,
                HasCrane = hasCrane,
                HasCurrentLoadChartEvidence = hasCurrentLoadChart,
                CorePrerequisitesComplete = missingCore == 0,
                MissingCorePrerequisiteCount = missingCore,
                HasNoCandidateAdvisory = hasNoCandidate,
                HasEstimatedWeightAdvisory = hasEstimatedWeight,
                AdvisoryTrustedLiftUnits = advisoryTrusted,
                AdvisoryCandidateCounts = candidateCounts,
                HasOptimizationResult = context.OptimizationResult.HasResult,
                HistoricalEvaluatedLiftUnits = resultTotal,
                CandidateEvidenceConflict = candidateConflict,
                CandidateScopeMismatch = candidateScopeMismatch,
                HasAuthoritativeOptimizerEligibility = false
            };
        }

        public static bool HasCurrentLoadChartEvidence(AiWorkflowContextSnapshot context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var craneContext = context.ScreenContexts.FirstOrDefault(screen =>
                screen.ScreenName.Equals("Crane and load chart", StringComparison.OrdinalIgnoreCase));

            if (craneContext == null)
            {
                // Backward-compatible fallback for tests/hosts that do not contribute a
                // dedicated crane context. The production desktop host on div does.
                return context.HasLoadChartEvidence;
            }

            if (ReadBool(craneContext.Facts, "SelectedHasSpecification"))
                return true;

            return ReadPositiveDouble(craneContext.Facts, "SelectedCurrentRadius")
                   || ReadPositiveDouble(craneContext.Facts, "SelectedCurrentCapacity");
        }

        private static bool ReadBool(IReadOnlyDictionary<string, string> facts, string key)
            => facts.TryGetValue(key, out var value)
               && bool.TryParse(value, out var parsed)
               && parsed;

        private static bool ReadPositiveDouble(IReadOnlyDictionary<string, string> facts, string key)
            => facts.TryGetValue(key, out var value)
               && double.TryParse(value, out var parsed)
               && parsed > 0;
    }
}
