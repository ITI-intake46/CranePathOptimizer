using CraneOptimizer.AI.Host;

namespace CraneOptimizer.AI.Workflow
{
    /// <summary>
    /// Deterministic AI workflow-completeness engine.
    ///
    /// This service classifies visible project prerequisites and review work. It does
    /// not reimplement the GA engine and never treats import/readiness candidate signals
    /// as the optimizer dialog's authoritative current-run preflight.
    /// </summary>
    public sealed class WorkflowReadinessService : IWorkflowReadinessService
    {
        public WorkflowReadinessSnapshot Evaluate(AiWorkflowContextSnapshot context, IUiInteractionContextService uiContext)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(uiContext);

            var assessment = AiOptimizerEvidenceAssessor.Assess(context);
            var result = context.OptimizationResult ?? new AiOptimizationResultSnapshot();
            var hasResult = result.HasResult;
            var hasDiagnostics = hasResult && !string.IsNullOrWhiteSpace(result.DiagnosticsPath);

            var items = new List<WorkflowReadinessItem>
            {
                Item(
                    "Project workspace",
                    assessment.HasProject,
                    true,
                    assessment.HasProject ? "OK" : "Warning",
                    assessment.HasProject ? $"Active project: {context.CurrentProjectName}" : "No active project session detected.",
                    "Open or create a project before importing IFC data or reviewing optimization readiness.",
                    "HOME → Open Project or New Project"),

                Item(
                    "IFC model and elements",
                    assessment.HasIfcElements,
                    true,
                    assessment.HasIfcElements ? "OK" : "Warning",
                    assessment.HasIfcElements ? $"{context.ElementCount:N0} BIM/model object(s) loaded in the scene." : "No IFC elements are available in the current scene.",
                    assessment.HasIfcElements
                        ? "Review import/data-quality warnings separately from the optimizer's own run-readiness decision."
                        : "Load an IFC model and verify model content before continuing.",
                    assessment.HasIfcElements ? "IFC MODEL → review import readiness" : "IFC MODEL → Load IFC"),

                Item(
                    "Placement zones",
                    assessment.HasZones,
                    true,
                    assessment.HasZones ? "OK" : "Warning",
                    assessment.HasZones ? $"{context.ZoneCount:N0} zone(s) defined." : "No allowed/forbidden placement zones are defined.",
                    "Define at least one allowed zone and any required forbidden zones before checking an optimizer run.",
                    "ZONES → Allowed Zone"),

                Item(
                    "Crane setup",
                    assessment.HasCrane,
                    true,
                    assessment.HasCrane ? "OK" : "Warning",
                    assessment.HasCrane ? $"{context.CraneCount:N0} crane instance(s) exist in the scene." : "No crane is available in the scene.",
                    "Add or select the crane model to be used for inspection and optimization.",
                    "CRANES → MOBILE CRANE"),

                Item(
                    "Current load chart evidence",
                    assessment.HasCurrentLoadChartEvidence,
                    true,
                    assessment.HasCurrentLoadChartEvidence ? "OK" : "Warning",
                    assessment.HasCurrentLoadChartEvidence
                        ? "The active crane context exposes a specification, current radius, or current capacity value."
                        : "The AI cannot confirm current crane specification/capacity evidence from the active crane context.",
                    "Import or verify the current manufacturer load chart before relying on capacity-based conclusions. A historical result is not treated as proof of current chart evidence.",
                    "CRANES → Import Load Chart...")
            };

            if (assessment.CorePrerequisitesComplete && !hasResult)
            {
                items.Add(Item(
                    "Optimizer built-in readiness verification",
                    false,
                    false,
                    assessment.HasNoCandidateAdvisory || assessment.CandidateEvidenceConflict ? "Warning" : "Info",
                    BuildOptimizerVerificationEvidence(assessment),
                    "Open the optimizer and use its built-in Current Run Readiness as the definitive run decision. AI import/readiness counts are advisory only.",
                    "ANALYSIS → Run Optimizer"));
            }

            items.Add(Item(
                "Optimization result",
                hasResult,
                false,
                hasResult ? "OK" : "Info",
                hasResult
                    ? $"AI-captured result evidence reports {result.CoverageRatio:P2} coverage and {result.LiftedElementCount:N0}/{result.TotalElementCount:N0} completed. The strict host contract does not identify this snapshot as the saved Analysis result currently selected on screen."
                    : "No completed optimization result is currently attached.",
                hasResult
                    ? "Open Lift Summary and review the result selected in the application before reporting or choosing a rerun strategy."
                    : assessment.CorePrerequisitesComplete
                        ? "Use ANALYSIS → Run Optimizer and rely on the optimizer's built-in readiness check before starting a run."
                        : "Complete the visible project prerequisites before opening the optimizer workflow.",
                hasResult ? "ANALYSIS → Lift Summary" : "ANALYSIS → Run Optimizer"));

            if (hasResult)
            {
                items.Add(Item(
                    "Result review",
                    false,
                    false,
                    "Warning",
                    "AI-captured result evidence exists, but the strict host contract does not expose the current Analysis UI selection. Review Lift Summary in the application directly before relying on any captured percentage.",
                    "Inspect Lift Summary and unresolved failure modes before deciding whether to change inputs, report, or rerun.",
                    "ANALYSIS → Lift Summary"));

                items.Add(Item(
                    "Optimizer rerun eligibility",
                    true,
                    false,
                    "Info",
                    "A completed result does not establish current rerun eligibility. The AI snapshot does not expose the optimizer dialog's authoritative current-run preflight.",
                    "If another run is desired, reopen Run Optimizer and use its built-in Current Run Readiness.",
                    "ANALYSIS → Run Optimizer"));
            }

            items.Add(Item(
                "Diagnostics available",
                hasDiagnostics,
                false,
                hasDiagnostics ? "OK" : "Info",
                hasDiagnostics ? $"Diagnostics path from AI-captured result evidence: {result.DiagnosticsPath}" : "No diagnostics path is attached to the AI-captured result evidence.",
                "Use diagnostics to distinguish geometry/reachability, capacity, and convergence issues.",
                "ANALYSIS → Lift Summary"));

            items.AddRange(BuildScreenContextEvidenceItems(context));

            var complete = items.Count(item => item.IsComplete);
            var missingCritical = items.Count(item => item.IsCritical && !item.IsComplete);
            var stage = InferStage(items, hasResult, assessment.CorePrerequisitesComplete);

            return new WorkflowReadinessSnapshot
            {
                ActiveScreen = uiContext.ActiveTabName,
                CurrentStage = stage,
                CompleteCount = complete,
                TotalCount = items.Count,
                CompletionRatio = items.Count == 0 ? 0 : complete / (double)items.Count,
                MissingCriticalCount = missingCritical,
                Summary = BuildSummary(missingCritical, hasResult, assessment),
                Items = items
            };
        }

        public IReadOnlyList<StructuredRecommendationCard> BuildStructuredCards(WorkflowReadinessSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            var cards = snapshot.Items
                .Where(item => !item.IsComplete || item.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => CorePrerequisiteOrder(item.Name))
                .ThenByDescending(item => item.IsCritical)
                .ThenByDescending(item => SeverityRank(item.Severity))
                .ThenBy(item => item.IsComplete)
                .Take(6)
                .Select(item => new StructuredRecommendationCard
                {
                    Status = item.IsCritical && item.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase)
                        ? "Blocked"
                        : item.IsComplete ? "Review" : item.IsCritical ? "Needs action" : "Review",
                    Finding = item.Name,
                    Evidence = item.Evidence,
                    Recommendation = item.Recommendation,
                    NextClick = item.NextClick,
                    Risk = item.IsCritical
                        ? "Visible prerequisite required before the workflow can continue."
                        : "Advisory/review item. It does not replace the optimizer's built-in current-run preflight.",
                    Confidence = item.IsCritical ? "High" : "High for the exposed evidence; optimizer eligibility is checked by the optimizer itself"
                })
                .ToList();

            if (cards.Count > 0)
                return cards;

            return new[]
            {
                new StructuredRecommendationCard
                {
                    Status = "Review",
                    Finding = "No pending AI workflow card",
                    Evidence = snapshot.Summary,
                    Recommendation = "Use the visible application workflow and the optimizer's own readiness indicator for run decisions.",
                    NextClick = "ANALYSIS",
                    Risk = "AI guidance remains advisory and does not certify crane-planning safety or optimizer eligibility.",
                    Confidence = "High"
                }
            };
        }

        private static string BuildOptimizerVerificationEvidence(AiOptimizerEvidenceAssessment assessment)
        {
            if (assessment.CandidateEvidenceConflict)
            {
                return $"AI import/readiness signals include a zero-candidate warning, while a completed optimization result previously evaluated {assessment.HistoricalEvaluatedLiftUnits:N0} lift unit(s). The evidence is contradictory or scope-mismatched and cannot be used to declare the current optimizer blocked.";
            }

            if (assessment.HasNoCandidateAdvisory)
            {
                var countText = assessment.AdvisoryTrustedLiftUnits.HasValue
                    ? $" (reported candidate count {assessment.AdvisoryTrustedLiftUnits.Value:N0})"
                    : string.Empty;
                return "AI import/readiness evidence reports a no-candidate signal" + countText + ". This signal is advisory to the AI and is not the optimizer dialog's authoritative current-run preflight.";
            }

            return assessment.AuthorityNote;
        }

        private static string BuildSummary(int missingCritical, bool hasResult, AiOptimizerEvidenceAssessment assessment)
        {
            if (missingCritical > 0)
                return $"{missingCritical:N0} visible project prerequisite(s) still need attention before checking an optimizer run.";

            if (hasResult)
            {
                return "Core project prerequisites are visible and AI-captured optimization result evidence exists. " +
                       "Because the strict host contract does not identify the current Analysis UI selection, review Lift Summary in the application rather than attributing the AI-captured percentage to that UI selection. " +
                       "Result quality does not establish current rerun eligibility; use the optimizer's built-in readiness check.";
            }

            if (assessment.HasNoCandidateAdvisory)
                return "Core project prerequisites are visible. AI import/readiness evidence contains a no-candidate warning, but the AI cannot use that advisory signal as the optimizer's authoritative run gate. Verify Current Run Readiness in Run Optimizer.";

            return "Core project prerequisites are visible. The AI cannot certify current optimizer eligibility from this snapshot; verify Current Run Readiness in Run Optimizer.";
        }

        private static int CorePrerequisiteOrder(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return 100;
            if (name.Contains("Project workspace", StringComparison.OrdinalIgnoreCase)) return 0;
            if (name.Contains("IFC model", StringComparison.OrdinalIgnoreCase)) return 1;
            if (name.Contains("Placement zones", StringComparison.OrdinalIgnoreCase)) return 2;
            if (name.Contains("Crane setup", StringComparison.OrdinalIgnoreCase)) return 3;
            if (name.Contains("Current load chart", StringComparison.OrdinalIgnoreCase)) return 4;
            if (name.Contains("Result review", StringComparison.OrdinalIgnoreCase)) return 5;
            if (name.Contains("Report review", StringComparison.OrdinalIgnoreCase)) return 5;
            if (name.Contains("Optimizer built-in readiness", StringComparison.OrdinalIgnoreCase)) return 6;
            if (name.Contains("Optimization result", StringComparison.OrdinalIgnoreCase)) return 10;
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

            foreach (var screenContext in context.ScreenContexts)
            {
                foreach (var issue in screenContext.Issues
                             .Where(issue => issue.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase)
                                             || issue.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase))
                             .OrderByDescending(issue => IsCandidateOrWeightSignal(issue.Code))
                             .ThenByDescending(issue => SeverityRank(issue.Severity))
                             .ThenByDescending(issue => issue.AffectedCount)
                             .Take(3))
                {
                    var key = $"{screenContext.ScreenName}|{issue.Code}";
                    if (!yieldedKeys.Add(key))
                        continue;

                    var candidateSignal = IsCandidateOrWeightSignal(issue.Code);
                    var evidence = string.IsNullOrWhiteSpace(issue.Evidence) ? screenContext.Summary : issue.Evidence;
                    var recommendation = string.IsNullOrWhiteSpace(issue.Recommendation)
                        ? "Review deterministic feature diagnostics before proceeding."
                        : issue.Recommendation;

                    if (candidateSignal)
                    {
                        evidence = "AI advisory import/readiness signal (not the optimizer's authoritative current-run preflight): " + evidence;
                        recommendation = "Review this data-quality signal, but do not infer that the optimizer is ready or blocked from it alone. Open Run Optimizer and use Current Run Readiness; if the optimizer confirms a blocker, use this evidence for troubleshooting.";
                    }

                    yield return Item(
                        candidateSignal
                            ? $"Advisory: {screenContext.ScreenName}: {issue.Code}"
                            : $"{screenContext.ScreenName}: {issue.Code}",
                        false,
                        false,
                        candidateSignal ? "Warning" : issue.Severity,
                        evidence,
                        recommendation,
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

        private static bool IsCandidateOrWeightSignal(string? code) => code != null &&
            (code.Equals("GaCannotStartNoCandidates", StringComparison.OrdinalIgnoreCase)
             || code.Equals("GaPreRunNoCandidates", StringComparison.OrdinalIgnoreCase)
             || code.Equals("NoGaCandidates", StringComparison.OrdinalIgnoreCase)
             || code.Equals("EstimatedWeightBlockedFromGA", StringComparison.OrdinalIgnoreCase)
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

        private static string InferStage(IReadOnlyList<WorkflowReadinessItem> items, bool hasResult, bool coreComplete)
        {
            var firstMissingCritical = items.FirstOrDefault(item => item.IsCritical && !item.IsComplete);
            if (firstMissingCritical != null)
                return firstMissingCritical.Name;

            if (hasResult)
                return "Result review";

            return coreComplete ? "Optimizer verification" : "Project setup";
        }
    }
}
