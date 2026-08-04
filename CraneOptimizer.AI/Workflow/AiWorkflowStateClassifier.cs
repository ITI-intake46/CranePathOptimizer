using CraneOptimizer.AI.Host;
using CraneOptimizer.AI.Services;

namespace CraneOptimizer.AI.Workflow
{
    /// <summary>
    /// Deterministic workflow-state classifier used by all AI surfaces. The purpose is
    /// to keep Mission Control, the docked assistant, and the full AI Studio from giving
    /// contradictory next actions such as recommending GA while pre-run eligibility is blocked.
    /// </summary>
    public enum AiWorkflowStateKind
    {
        NoProject,
        NoIfcModel,
        LiftUnitReadinessBlocked,
        ZonesMissing,
        CraneMissing,
        LoadChartMissing,
        ReadyForOptimization,
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
        public bool HasNoGaCandidateBlocker { get; init; }
        public bool HasEstimatedWeightBlocker { get; init; }
        public bool CanRunOptimizerNow { get; init; }
        public string EvidencePrefix { get; init; } = string.Empty;
    }

    public static class AiWorkflowStateClassifier
    {
        public static AiWorkflowState Classify(AiWorkflowContextSnapshot context, WorkflowReadinessSnapshot? readiness = null)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var hasLoadedElements = context.ElementCount > 0;
            var hasNoGaBlocker = hasLoadedElements && AiWorkflowEvidenceTextBuilder.HasNoGaCandidateBlocker(context);
            var hasEstimatedWeightBlocker = hasLoadedElements && AiWorkflowEvidenceTextBuilder.HasEstimatedWeightBlocker(context);
            var hasResult = context.OptimizationResult.HasResult;
            var hasLoadChartEvidence = context.HasLoadChartEvidence
                || (hasResult && context.OptimizationResult.MaxCapacity > 0 && context.OptimizationResult.MaxRadius > 0);

            if (!context.HasProject)
            {
                return new AiWorkflowState
                {
                    Kind = AiWorkflowStateKind.NoProject,
                    Title = "AI Diagnosis: project not opened",
                    Status = "Waiting",
                    RootCause = "No active project workspace is available for AI to analyze.",
                    RecommendedAction = "HOME → Open Project or New Project.",
                    Risk = "AI guidance is limited until a project workspace exists.",
                    HasLoadedElements = false,
                    HasNoGaCandidateBlocker = false,
                    HasEstimatedWeightBlocker = false,
                    CanRunOptimizerNow = false,
                    EvidencePrefix = "No active project is attached."
                };
            }

            if (!hasLoadedElements)
            {
                return new AiWorkflowState
                {
                    Kind = AiWorkflowStateKind.NoIfcModel,
                    Title = "AI Diagnosis: IFC model not loaded",
                    Status = "Needs IFC",
                    RootCause = "No IFC elements are loaded in the current scene. GA candidate readiness cannot be evaluated until model elements exist.",
                    RecommendedAction = "IFC MODEL → Load IFC.",
                    Risk = "Optimization cannot start because there are no model elements to group, weigh, or lift.",
                    HasLoadedElements = false,
                    HasNoGaCandidateBlocker = false,
                    HasEstimatedWeightBlocker = false,
                    CanRunOptimizerNow = false,
                    EvidencePrefix = "Element count is zero; treat any GA-candidate message as secondary until IFC elements are loaded."
                };
            }

            if (hasNoGaBlocker || hasEstimatedWeightBlocker)
            {
                var estimatedDetail = hasEstimatedWeightBlocker
                    ? " Weight evidence includes estimated/fallback or otherwise untrusted values, which are blocked from GA."
                    : string.Empty;

                return new AiWorkflowState
                {
                    Kind = AiWorkflowStateKind.LiftUnitReadinessBlocked,
                    Title = "AI Diagnosis: optimizer blocked before GA",
                    Status = "Blocked",
                    RootCause = "IFC elements are loaded, but GA has zero trusted GA-ready grouped lift-unit candidates." + estimatedDetail,
                    RecommendedAction = "IFC MODEL → Export Element List / trusted weight-source review, then request explicit trusted mass/weight, stable assembly/lift marks, and COG/geometry evidence from the BIM/export owner.",
                    Risk = "High if bypassed: changing crane, zone, or load-chart settings cannot solve a zero trusted-lift-unit state. Do not bypass the optimizer gate, force GA, or use estimated weights for engineering optimization.",
                    HasLoadedElements = true,
                    HasNoGaCandidateBlocker = hasNoGaBlocker,
                    HasEstimatedWeightBlocker = hasEstimatedWeightBlocker,
                    CanRunOptimizerNow = false,
                    EvidencePrefix = "IFC elements are present, but deterministic readiness reports no trusted GA-ready lift units."
                };
            }

            if (context.ZoneCount <= 0)
            {
                return new AiWorkflowState
                {
                    Kind = AiWorkflowStateKind.ZonesMissing,
                    Title = "AI Diagnosis: placement zones missing",
                    Status = "Needs zones",
                    RootCause = "IFC elements are loaded, but no placement zones are defined for crane setup search.",
                    RecommendedAction = "ZONES → Allowed Zone, then add forbidden zones where required.",
                    Risk = "GA placement cannot be interpreted reliably without explicit allowed/forbidden zone constraints.",
                    HasLoadedElements = true,
                    CanRunOptimizerNow = false,
                    EvidencePrefix = "Element evidence exists but zone count is zero."
                };
            }

            if (context.CraneCount <= 0)
            {
                return new AiWorkflowState
                {
                    Kind = AiWorkflowStateKind.CraneMissing,
                    Title = "AI Diagnosis: crane setup missing",
                    Status = "Needs crane",
                    RootCause = "IFC and zones are present, but no crane instance is available for feasibility or GA setup search.",
                    RecommendedAction = "CRANES → MOBILE CRANE, then select/import the crane and its load chart.",
                    Risk = "No capacity/radius feasibility can be reviewed without an explicit crane setup.",
                    HasLoadedElements = true,
                    CanRunOptimizerNow = false,
                    EvidencePrefix = "Elements and zones exist, but crane count is zero."
                };
            }

            if (!hasLoadChartEvidence)
            {
                return new AiWorkflowState
                {
                    Kind = AiWorkflowStateKind.LoadChartMissing,
                    Title = "AI Diagnosis: load-chart evidence missing",
                    Status = "Needs chart",
                    RootCause = "A crane is present, but active manufacturer load-chart/capacity-radius evidence was not detected.",
                    RecommendedAction = "CRANES → Import Load Chart..., then verify the selected crane specification and capacity table.",
                    Risk = "Capacity/radius conclusions are not defensible without load-chart evidence.",
                    HasLoadedElements = true,
                    CanRunOptimizerNow = false,
                    EvidencePrefix = "Crane setup exists, but no active load-chart evidence is attached."
                };
            }

            if (hasResult)
            {
                var reportCandidate = context.OptimizationResult.CoverageRatio >= 0.95;
                return new AiWorkflowState
                {
                    Kind = reportCandidate ? AiWorkflowStateKind.ReportCandidate : AiWorkflowStateKind.ResultReview,
                    Title = reportCandidate ? "AI Diagnosis: result is report-candidate" : "AI Diagnosis: result needs engineering review",
                    Status = reportCandidate ? "Review" : "Review",
                    RootCause = reportCandidate
                        ? $"The latest optimization covers {context.OptimizationResult.CoverageRatio:P2} of target elements; review assumptions before reporting."
                        : $"The latest optimization covers {context.OptimizationResult.CoverageRatio:P2}; unresolved elements and diagnostics should be reviewed before reporting.",
                    RecommendedAction = reportCandidate
                        ? "ANALYSIS → Lifting Summary Report, then review AI report language before issue."
                        : "ANALYSIS → Lift Summary, then inspect unresolved elements and dominant failure modes.",
                    Risk = "AI wording is advisory and does not certify crane capacity, rigging design, or site safety.",
                    HasLoadedElements = true,
                    CanRunOptimizerNow = false,
                    EvidencePrefix = "A completed optimization result is attached."
                };
            }

            return new AiWorkflowState
            {
                Kind = AiWorkflowStateKind.ReadyForOptimization,
                Title = "AI Diagnosis: ready for controlled optimization",
                Status = "Ready",
                RootCause = "Core deterministic prerequisites are present and no zero-candidate blocker is currently exposed.",
                RecommendedAction = "ANALYSIS → Run Optimizer, then open AI ASSIST → STUDIO for result diagnosis.",
                Risk = "Low before run, but final crane planning still requires load-chart/source-data review and qualified engineering approval.",
                HasLoadedElements = true,
                CanRunOptimizerNow = true,
                EvidencePrefix = "IFC, zones, crane setup, and load-chart evidence are present."
            };
        }
    }
}
