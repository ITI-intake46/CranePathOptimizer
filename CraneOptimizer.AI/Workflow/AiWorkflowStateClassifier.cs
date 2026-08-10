using CraneOptimizer.AI.Host;

namespace CraneOptimizer.AI.Workflow
{
    /// <summary>
    /// Deterministic workflow-state classifier used by AI surfaces.
    ///
    /// The classifier intentionally does not duplicate the GA engine. It may classify
    /// visible project prerequisites and result-review state, but the optimizer's own
    /// built-in preflight remains authoritative for the current run decision.
    /// </summary>
    public enum AiWorkflowStateKind
    {
        NoProject,
        NoIfcModel,
        LiftUnitReadinessBlocked, // legacy value retained for compatibility; not inferred from advisory import signals.
        ZonesMissing,
        CraneMissing,
        LoadChartMissing,
        OptimizerVerificationRequired,
        ReadyForOptimization, // reserved for a future explicit authoritative host contract.
        ResultReview,
        ReportCandidate
    }

    public sealed class AiWorkflowState
    {
        public AiWorkflowStateKind Kind { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Status { get; init; } = "Info";
        public string RootCause { get; init; } = string.Empty;
        public string RecommendedAction { get; init; } = string.Empty;
        public string Risk { get; init; } = string.Empty;
        public string Confidence { get; init; } = "High";
        public bool HasLoadedElements { get; init; }

        // Legacy property names retained for compatibility. In strict-isolation mode
        // these values mean advisory AI-visible signals, not optimizer-run blockers.
        public bool HasNoGaCandidateBlocker { get; init; }
        public bool HasEstimatedWeightBlocker { get; init; }

        /// <summary>
        /// True only when the AI has an authoritative current optimizer eligibility fact.
        /// The current div host does not expose such a fact, so this remains false for
        /// the strict-isolation implementation.
        /// </summary>
        public bool CanRunOptimizerNow { get; init; }

        /// <summary>
        /// True when the visible project prerequisites are sufficient to open the
        /// optimizer and let its own built-in preflight make the run decision.
        /// </summary>
        public bool CanProceedToOptimizerCheck { get; init; }

        public bool OptimizerEligibilityIsAuthoritative { get; init; }
        public bool CandidateEvidenceConflict { get; init; }
        public int? AdvisoryCandidateCount { get; init; }
        public string EvidencePrefix { get; init; } = string.Empty;
    }

    public static class AiWorkflowStateClassifier
    {
        public static AiWorkflowState Classify(AiWorkflowContextSnapshot context, WorkflowReadinessSnapshot? readiness = null)
        {
            ArgumentNullException.ThrowIfNull(context);

            var assessment = AiOptimizerEvidenceAssessor.Assess(context);
            var hasResult = context.OptimizationResult.HasResult;

            if (!context.HasProject)
            {
                return State(
                    AiWorkflowStateKind.NoProject,
                    "AI Diagnosis: project not opened",
                    "Waiting",
                    "No active project workspace is available for AI to analyze.",
                    "HOME → Open Project or New Project.",
                    "AI guidance is limited until a project workspace exists.",
                    assessment,
                    canProceed: false,
                    evidence: "No active project is attached.");
            }

            if (context.ElementCount <= 0)
            {
                return State(
                    AiWorkflowStateKind.NoIfcModel,
                    "AI Diagnosis: IFC model not loaded",
                    "Needs IFC",
                    "No IFC elements are loaded in the current scene.",
                    "IFC MODEL → Load IFC.",
                    "Optimization cannot be meaningfully checked until model elements exist.",
                    assessment,
                    canProceed: false,
                    evidence: "Element count is zero; optimizer eligibility cannot be inferred yet.");
            }

            if (context.ZoneCount <= 0)
            {
                return State(
                    AiWorkflowStateKind.ZonesMissing,
                    "AI Diagnosis: placement zones missing",
                    "Needs zones",
                    "IFC elements are loaded, but no crane-placement zones are defined.",
                    "ZONES → Allowed Zone, then add forbidden zones where required.",
                    "Placement search cannot be reviewed reliably without explicit zone constraints.",
                    assessment,
                    canProceed: false,
                    evidence: "Element evidence exists but zone count is zero.");
            }

            if (context.CraneCount <= 0)
            {
                return State(
                    AiWorkflowStateKind.CraneMissing,
                    "AI Diagnosis: crane setup missing",
                    "Needs crane",
                    "IFC and zones are present, but no crane instance is available.",
                    "CRANES → MOBILE CRANE, then select/import the crane and its load chart.",
                    "No capacity/radius feasibility can be reviewed without an explicit crane setup.",
                    assessment,
                    canProceed: false,
                    evidence: "Elements and zones exist, but crane count is zero.");
            }

            if (!assessment.HasCurrentLoadChartEvidence)
            {
                return State(
                    AiWorkflowStateKind.LoadChartMissing,
                    "AI Diagnosis: current load-chart evidence missing",
                    "Needs chart",
                    "A crane is present, but the AI cannot confirm current crane specification/capacity evidence from the active crane context.",
                    "CRANES → Import Load Chart..., then verify the active crane specification/capacity table.",
                    "Do not rely on a historical optimization result as proof that the current crane still has valid load-chart evidence.",
                    assessment,
                    canProceed: false,
                    evidence: "Current crane context does not expose an active specification, radius, or capacity value.");
            }

            if (hasResult)
            {
                var reportCandidate = context.OptimizationResult.CoverageRatio >= 0.95;
                var resultText = $"{context.OptimizationResult.CoverageRatio:P2} coverage; {context.OptimizationResult.LiftedElementCount:N0}/{context.OptimizationResult.TotalElementCount:N0} evaluated lift(s) completed.";
                var evidence = assessment.CandidateEvidenceConflict
                    ? $"A completed result exists ({resultText}) while AI import/readiness signals also report zero candidate units. Those signals conflict and must not be treated as the optimizer's current preflight."
                    : $"A completed optimization result is attached ({resultText}). Current rerun eligibility is still owned by the optimizer's built-in preflight.";

                return State(
                    reportCandidate ? AiWorkflowStateKind.ReportCandidate : AiWorkflowStateKind.ResultReview,
                    reportCandidate ? "AI Diagnosis: result is report-candidate" : "AI Diagnosis: result needs engineering review",
                    "Review",
                    reportCandidate
                        ? $"The selected optimization result covers {context.OptimizationResult.CoverageRatio:P2}; review assumptions before reporting."
                        : $"The selected optimization result covers {context.OptimizationResult.CoverageRatio:P2}; unresolved elements and diagnostics should be reviewed.",
                    reportCandidate
                        ? "ANALYSIS → Lifting Summary Report, then review the report evidence before issue."
                        : "ANALYSIS → Lift Summary, then inspect unresolved elements and dominant failure modes.",
                    "Result quality and current rerun eligibility are separate. The AI does not certify crane capacity or the optimizer's current preflight.",
                    assessment,
                    canProceed: true,
                    evidence: evidence);
            }

            var rootCause = assessment.CandidateEvidenceConflict
                ? "Core project prerequisites are present, but AI-visible candidate evidence conflicts with historical optimizer results."
                : assessment.HasNoCandidateAdvisory
                    ? "Core project prerequisites are present. AI import/readiness evidence reports zero trusted lift-unit candidates, but that evidence is not the optimizer dialog's authoritative preflight."
                    : "Core project prerequisites are present, but the AI snapshot does not expose the optimizer dialog's authoritative current-run preflight.";

            var evidencePrefix = assessment.HasNoCandidateAdvisory
                ? "Import/readiness candidate signals are advisory to AI. Verify the current run with the optimizer's own readiness indicator."
                : assessment.AuthorityNote;

            return State(
                AiWorkflowStateKind.OptimizerVerificationRequired,
                "AI Diagnosis: optimizer verification required",
                "Verify",
                rootCause,
                "ANALYSIS → Run Optimizer, then use the optimizer's built-in Current Run Readiness as the source of truth.",
                "Do not infer 'ready' or 'blocked' from AI import/readiness candidate counts alone. Do not bypass a blocker reported by the optimizer itself.",
                assessment,
                canProceed: true,
                evidence: evidencePrefix);
        }

        private static AiWorkflowState State(
            AiWorkflowStateKind kind,
            string title,
            string status,
            string rootCause,
            string recommendedAction,
            string risk,
            AiOptimizerEvidenceAssessment assessment,
            bool canProceed,
            string evidence)
        {
            return new AiWorkflowState
            {
                Kind = kind,
                Title = title,
                Status = status,
                RootCause = rootCause,
                RecommendedAction = recommendedAction,
                Risk = risk,
                Confidence = assessment.HasAuthoritativeOptimizerEligibility ? "High" : "High for visible project facts; optimizer eligibility requires built-in preflight",
                HasLoadedElements = assessment.HasIfcElements,
                HasNoGaCandidateBlocker = assessment.HasNoCandidateAdvisory,
                HasEstimatedWeightBlocker = assessment.HasEstimatedWeightAdvisory,
                CanRunOptimizerNow = false,
                CanProceedToOptimizerCheck = canProceed,
                OptimizerEligibilityIsAuthoritative = assessment.HasAuthoritativeOptimizerEligibility,
                CandidateEvidenceConflict = assessment.CandidateEvidenceConflict,
                AdvisoryCandidateCount = assessment.AdvisoryTrustedLiftUnits,
                EvidencePrefix = evidence
            };
        }
    }
}
