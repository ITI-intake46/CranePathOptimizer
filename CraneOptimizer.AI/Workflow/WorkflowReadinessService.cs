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
            var hasNoGaCandidateBlocker = elementCount > 0 && AiWorkflowEvidenceTextBuilder.HasNoGaCandidateBlocker(context);

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
                    elementCount > 0
                        ? "Review import readiness, trusted weights, COG markers, and coordinate assumptions. The IFC is loaded; remaining warnings are data-quality issues, not a load-model failure."
                        : "Load an IFC model and verify element categories, weights, COG markers, and coordinate assumptions.",
                    elementCount > 0 ? "IFC MODEL → review import readiness" : "IFC MODEL → Load IFC"),

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
                    hasResult ? $"Latest result coverage: {result.CoverageRatio:P2}; lifted {result.LiftedElementCount:N0}/{result.TotalElementCount:N0}." : hasNoGaCandidateBlocker ? "No optimization result is available because deterministic pre-run evidence has zero trusted GA-ready lift units." : "No optimization result is currently available.",
                    hasNoGaCandidateBlocker
                        ? "Resolve import/lift-unit readiness first; keep the optimizer gate closed until trusted lift-unit evidence is available."
                        : "Run the GA optimizer after IFC, zones, crane, and load chart inputs are ready.",
                    hasNoGaCandidateBlocker ? "IFC MODEL → Export Element List / trusted weight-source review" : "ANALYZE → Run Optimizer"),

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

            items.AddRange(BuildScreenContextEvidenceItems(context));

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
                    ? (hasNoGaCandidateBlocker
                        ? "Core scene inputs exist, but GA is blocked by element readiness. Review trusted GA-ready lift-unit data before optimization."
                        : "Core inputs are available. Review diagnostics and reporting readiness before issuing deliverables.")
                    : $"{missingCritical} critical workflow prerequisite(s) still need attention before optimization/reporting.",
                Items = items
            };
        }

        public IReadOnlyList<StructuredRecommendationCard> BuildStructuredCards(WorkflowReadinessSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            var cards = snapshot.Items
                .Where(item => !item.IsComplete || item.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => CorePrerequisiteOrder(item.Name))
                .ThenByDescending(item => item.IsCritical)
                .ThenByDescending(item => SeverityRank(item.Severity))
                .ThenBy(item => item.IsComplete)
                .Take(6)
                .Select(item => new StructuredRecommendationCard
                {
                    Status = item.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase)
                        ? "Blocked"
                        : item.IsComplete ? "Review" : (item.IsCritical ? "Needs action" : "Optional"),
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

        private static int CorePrerequisiteOrder(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return 100;
            if (name.Contains("Project workspace", StringComparison.OrdinalIgnoreCase)) return 0;
            if (name.Contains("IFC model", StringComparison.OrdinalIgnoreCase)) return 1;
            if (name.Contains("Placement zones", StringComparison.OrdinalIgnoreCase)) return 2;
            if (name.Contains("Crane setup", StringComparison.OrdinalIgnoreCase)) return 3;
            if (name.Contains("Load chart evidence", StringComparison.OrdinalIgnoreCase)) return 4;
            return 50;
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


        private static IEnumerable<WorkflowReadinessItem> BuildScreenContextEvidenceItems(AiWorkflowContextSnapshot context)
        {
            if (context.ScreenContexts.Count == 0)
                yield break;

            var yieldedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var prioritized in context.ScreenContexts
                         .SelectMany(screen => screen.Issues.Select(issue => new { screen, issue }))
                         .Where(item => context.ElementCount > 0 && IsNoGaCandidateBlocker(item.issue.Code))
                         .OrderByDescending(item => SeverityRank(item.issue.Severity))
                         .ThenByDescending(item => item.issue.AffectedCount)
                         .Take(2))
            {
                var key = $"{prioritized.screen.ScreenName}|{prioritized.issue.Code}";
                if (!yieldedKeys.Add(key))
                    continue;

                yield return Item(
                    $"{prioritized.screen.ScreenName}: {prioritized.issue.Code}",
                    false,
                    true,
                    "Error",
                    string.IsNullOrWhiteSpace(prioritized.issue.Evidence) ? prioritized.screen.Summary : prioritized.issue.Evidence,
                    string.IsNullOrWhiteSpace(prioritized.issue.Recommendation)
                        ? "Review trusted BIM lift-unit data before running GA. Do not bypass optimizer eligibility."
                        : prioritized.issue.Recommendation,
                    string.IsNullOrWhiteSpace(prioritized.issue.NextClick)
                        ? "IFC MODEL → Export Element List / trusted weight-source review"
                        : prioritized.issue.NextClick);
            }

            foreach (var screenContext in context.ScreenContexts)
            {
                foreach (var issue in screenContext.Issues
                             .Where(issue => issue.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase)
                                             || issue.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase))
                             .Where(issue => !IsNoGaCandidateBlocker(issue.Code))
                             .OrderByDescending(issue => IsEstimatedWeightBlocker(issue.Code))
                             .ThenByDescending(issue => SeverityRank(issue.Severity))
                             .ThenByDescending(issue => issue.AffectedCount)
                             .Take(2))
                {
                    var key = $"{screenContext.ScreenName}|{issue.Code}";
                    if (!yieldedKeys.Add(key))
                        continue;

                    var isError = issue.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase);
                    yield return Item(
                        $"{screenContext.ScreenName}: {issue.Code}",
                        false,
                        isError,
                        issue.Severity,
                        string.IsNullOrWhiteSpace(issue.Evidence) ? screenContext.Summary : issue.Evidence,
                        string.IsNullOrWhiteSpace(issue.Recommendation) ? "Review deterministic feature diagnostics before proceeding." : issue.Recommendation,
                        string.IsNullOrWhiteSpace(issue.NextClick) ? screenContext.ScreenName : issue.NextClick);
                }
            }

            var reportContext = context.ScreenContexts.FirstOrDefault(screen => screen.ScreenName.Equals("Reports", StringComparison.OrdinalIgnoreCase));
            if (reportContext?.Facts.TryGetValue("HasOptimizationResult", out var hasResultValue) == true
                && bool.TryParse(hasResultValue, out var hasResult)
                && hasResult)
            {
                yield return Item(
                    "AI-assisted report narrative",
                    true,
                    false,
                    "Warning",
                    "AI can draft a report note from deterministic context, but it is not an engineering certification.",
                    "Copy any AI narrative into reports only after explicit engineer/user review.",
                    "AI ASSIST → Copy AI report narrative");
            }
        }

        private static bool IsNoGaCandidateBlocker(string? code) => code != null &&
            (code.Equals("GaCannotStartNoCandidates", StringComparison.OrdinalIgnoreCase)
             || code.Equals("GaPreRunNoCandidates", StringComparison.OrdinalIgnoreCase)
             || code.Equals("NoGaCandidates", StringComparison.OrdinalIgnoreCase));

        private static bool IsEstimatedWeightBlocker(string? code) => code != null &&
            (code.Equals("EstimatedWeightBlockedFromGA", StringComparison.OrdinalIgnoreCase)
             || code.Equals("MissingTrustedWeight", StringComparison.OrdinalIgnoreCase)
             || code.Equals("ElementWeightReviewNeeded", StringComparison.OrdinalIgnoreCase)
             || code.Equals("SelectedElementWeightNotTrusted", StringComparison.OrdinalIgnoreCase));

        private static int SeverityRank(string? severity) => severity?.ToUpperInvariant() switch
        {
            "ERROR" => 3,
            "WARNING" => 2,
            "INFO" => 1,
            _ => 0
        };

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
