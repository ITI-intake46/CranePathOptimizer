using CraneOptimizer.AI.Host;
using CraneOptimizer.AI.Services;

namespace CraneOptimizer.AI.Workflow
{
    /// <summary>
    /// Deterministic workflow-completeness engine. It transforms live shell state
    /// into an auditable checklist that the AI can explain but not override.
    /// </summary>
    public sealed class WorkflowReadinessService : IWorkflowReadinessService
    {
        public WorkflowReadinessSnapshot Evaluate(AiWorkflowContextSnapshot context, IUiInteractionContextService uiContext)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (uiContext == null) throw new ArgumentNullException(nameof(uiContext));

            var elementCount = context.ElementCount;
            var zoneCount = context.ZoneCount;
            var craneCount = context.CraneCount;
            var hasProject = context.HasProject;
            var result = context.OptimizationResult ?? new AiOptimizationResultSnapshot();
            var hasResult = result.HasResult;
            var hasDiagnostics = hasResult && !string.IsNullOrWhiteSpace(result.DiagnosticsPath);
            var hasLoadChartEvidence = context.HasLoadChartEvidence
                || (hasResult && result.MaxCapacity > 0 && result.MaxRadius > 0);
            var resultIsReportReady = hasResult && result.CoverageRatio >= 0.95;

            var items = new List<WorkflowReadinessItem>
            {
                Item(
                    "Project workspace",
                    hasProject,
                    true,
                    hasProject ? "OK" : "Warning",
                    hasProject ? $"Active project: {context.CurrentProjectName}" : "No active project session detected.",
                    "Open or create a project before importing IFC data or running optimization.",
                    "HOME → Open Project or New Project"),

                Item(
                    "IFC model and elements",
                    elementCount > 0,
                    true,
                    elementCount > 0 ? "OK" : "Warning",
                    elementCount > 0 ? $"{elementCount:N0} element(s) loaded in the scene." : "No IFC elements are available in the current scene.",
                    "Load an IFC model and verify element categories, weights, COG markers, and coordinate assumptions.",
                    "IFC MODEL → Load IFC"),

                Item(
                    "Placement zones",
                    zoneCount > 0,
                    true,
                    zoneCount > 0 ? "OK" : "Warning",
                    zoneCount > 0 ? $"{zoneCount:N0} zone(s) defined." : "No allowed/forbidden placement zones are defined.",
                    "Define at least one allowed zone and any required forbidden zones before running GA placement.",
                    "ZONES → Allowed Zone"),

                Item(
                    "Crane setup",
                    craneCount > 0,
                    true,
                    craneCount > 0 ? "OK" : "Warning",
                    craneCount > 0 ? $"{craneCount:N0} crane instance(s) exist in the scene." : "No crane is available in the scene.",
                    "Add or select the crane model to be used for manual inspection and optimization.",
                    "CRANES → MOBILE CRANE"),

                Item(
                    "Load chart evidence",
                    hasLoadChartEvidence,
                    true,
                    hasLoadChartEvidence ? "OK" : "Warning",
                    hasLoadChartEvidence ? "Crane capacity/radius data is visible in the current crane/result context." : "No active load-chart/capacity evidence was detected from the current UI context.",
                    "Import or verify manufacturer load chart data before relying on capacity-based feasibility results.",
                    "CRANES → Import Load Chart..."),

                Item(
                    "Optimization result",
                    hasResult,
                    false,
                    hasResult ? "OK" : "Info",
                    hasResult ? $"Latest result coverage: {result.CoverageRatio:P2}; lifted {result.LiftedElementCount:N0}/{result.TotalElementCount:N0}." : "No optimization result is currently available.",
                    "Run the GA optimizer after IFC, zones, crane, and load chart inputs are ready.",
                    "ANALYZE → Run Optimizer"),

                Item(
                    "Diagnostics available",
                    hasDiagnostics,
                    false,
                    hasDiagnostics ? "OK" : "Info",
                    hasDiagnostics ? $"Diagnostics path: {result.DiagnosticsPath}" : "No diagnostics path is attached to the latest result.",
                    "Use diagnostics to distinguish geometry/reachability issues from GA convergence issues.",
                    "ANALYZE → GA Diagnostics"),

                Item(
                    "Report readiness",
                    resultIsReportReady,
                    false,
                    resultIsReportReady ? "OK" : hasResult ? "Warning" : "Info",
                    hasResult ? $"Coverage is {result.CoverageRatio:P2}." : "No result is available for report generation.",
                    hasResult ? "Review unresolved elements and assumptions before generating final reports." : "Generate reports after a completed optimization run.",
                    "REPORTS → Lifting Summary Report")
            };

            var complete = items.Count(item => item.IsComplete);
            var missingCritical = items.Count(item => item.IsCritical && !item.IsComplete);
            var stage = InferStage(items, hasResult, result.CoverageRatio);

            return new WorkflowReadinessSnapshot
            {
                ActiveScreen = uiContext.ActiveTabName,
                CurrentStage = stage,
                CompleteCount = complete,
                TotalCount = items.Count,
                CompletionRatio = items.Count == 0 ? 0 : complete / (double)items.Count,
                MissingCriticalCount = missingCritical,
                Summary = missingCritical == 0
                    ? "Core inputs are available. Review diagnostics and reporting readiness before issuing deliverables."
                    : $"{missingCritical} critical workflow prerequisite(s) still need attention before optimization/reporting.",
                Items = items
            };
        }

        public IReadOnlyList<StructuredRecommendationCard> BuildStructuredCards(WorkflowReadinessSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            var cards = snapshot.Items
                .Where(item => !item.IsComplete || item.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .Select(item => new StructuredRecommendationCard
                {
                    Status = item.IsComplete ? "Review" : (item.IsCritical ? "Needs action" : "Optional"),
                    Finding = item.Name,
                    Evidence = item.Evidence,
                    Recommendation = item.Recommendation,
                    NextClick = item.NextClick,
                    Risk = item.IsCritical
                        ? "Blocking prerequisite for reliable optimization or report interpretation."
                        : "May reduce explainability, reproducibility, or reporting quality if ignored.",
                    Confidence = "High"
                })
                .ToList();

            if (cards.Count > 0)
                return cards;

            return new[]
            {
                new StructuredRecommendationCard
                {
                    Status = "Ready",
                    Finding = "Workflow prerequisites look complete",
                    Evidence = snapshot.Summary,
                    Recommendation = "Proceed with engineering review, diagnostics inspection, and report generation.",
                    NextClick = "REPORTS → Lifting Summary Report",
                    Risk = "AI guidance remains advisory and does not certify crane-planning safety.",
                    Confidence = "High"
                }
            };
        }

        private static WorkflowReadinessItem Item(string name, bool isComplete, bool isCritical, string severity, string evidence, string recommendation, string nextClick)
        {
            return new WorkflowReadinessItem
            {
                Name = name,
                IsComplete = isComplete,
                IsCritical = isCritical,
                Severity = severity,
                Evidence = evidence,
                Recommendation = recommendation,
                NextClick = nextClick
            };
        }

        private static string InferStage(IReadOnlyList<WorkflowReadinessItem> items, bool hasResult, double coverage)
        {
            var firstMissingCritical = items.FirstOrDefault(item => item.IsCritical && !item.IsComplete);
            if (firstMissingCritical != null)
                return firstMissingCritical.Name;

            if (!hasResult)
                return "Optimization";

            return coverage >= 0.95 ? "Reporting" : "Result review";
        }
    }
}
