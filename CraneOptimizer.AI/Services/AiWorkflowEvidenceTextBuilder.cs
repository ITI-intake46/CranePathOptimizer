using System.Text;
using CraneOptimizer.AI.Host;
using CraneOptimizer.AI.Workflow;

namespace CraneOptimizer.AI.Services
{
    /// <summary>
    /// Builds deterministic, clipboard-safe text artifacts from AI workflow context.
    /// The text is explanatory only: it never changes optimizer eligibility, load-chart,
    /// import, or report rules.
    /// </summary>
    public static class AiWorkflowEvidenceTextBuilder
    {
        private static readonly string[] NoCandidateCodes =
        {
            "GaCannotStartNoCandidates",
            "GaPreRunNoCandidates",
            "NoGaCandidates"
        };

        public static bool HasNoGaCandidateBlocker(AiWorkflowContextSnapshot? context)
        {
            return FindIssues(context)
                .Any(issue => NoCandidateCodes.Any(code => issue.Code.Equals(code, StringComparison.OrdinalIgnoreCase)));
        }

        public static bool HasEstimatedWeightBlocker(AiWorkflowContextSnapshot? context)
        {
            return FindIssues(context)
                .Any(issue => issue.Code.Equals("EstimatedWeightBlockedFromGA", StringComparison.OrdinalIgnoreCase)
                              || issue.Code.Equals("ElementWeightReviewNeeded", StringComparison.OrdinalIgnoreCase)
                              || issue.Code.Equals("MissingTrustedWeight", StringComparison.OrdinalIgnoreCase)
                              || issue.Code.Equals("SelectedElementWeightNotTrusted", StringComparison.OrdinalIgnoreCase));
        }

        public static string BuildWorkflowBlockerSummary(AiWorkflowContextSnapshot? context)
        {
            if (context == null)
                return "No live workflow context is available.";

            var state = AiWorkflowStateClassifier.Classify(context);
            var issues = GetPriorityIssues(context).Take(8).ToList();
            var builder = new StringBuilder();
            builder.AppendLine("Deterministic workflow state summary");
            builder.AppendLine($"- Project: {(context.HasProject ? context.CurrentProjectName : "none")}");
            builder.AppendLine($"- Model context: {NormalizeInline(context.ModelDisplayName, string.IsNullOrWhiteSpace(context.CurrentProjectName) ? "Imported IFC model" : context.CurrentProjectName)}");
            builder.AppendLine($"- Model revision: {context.ModelRevision:N0}");
            builder.AppendLine($"- Inputs: {context.ElementCount:N0} total BIM object(s), {context.ZoneCount:N0} zone(s), {context.CraneCount:N0} crane(s)");
            if (context.ElementTableTotalCount > 0)
                builder.AppendLine($"- Element table scope: {context.ElementTableVisibleCount:N0} visible of {context.ElementTableTotalCount:N0} table item(s)");
            builder.AppendLine($"- State: {state.Status} — {state.RootCause}");
            builder.AppendLine($"- Optimization result: {(context.OptimizationResult.HasResult ? "available" : "none")}");
            builder.AppendLine($"- Recommended next action: {state.RecommendedAction}");

            if (issues.Count == 0)
            {
                builder.AppendLine("- Current AI evidence has no warning/error issue.");
            }
            else
            {
                builder.AppendLine("- Highest-priority issues:");
                foreach (var item in issues)
                {
                    builder.AppendLine($"  - {item.ScreenName}: {item.Issue.Severity} {item.Issue.Code}. {item.Issue.Evidence}");
                    if (!string.IsNullOrWhiteSpace(item.Issue.Recommendation))
                        builder.AppendLine($"    Recommended: {item.Issue.Recommendation}");
                    if (!string.IsNullOrWhiteSpace(item.Issue.NextClick))
                        builder.AppendLine($"    Next click: {item.Issue.NextClick}");
                }
            }

            builder.AppendLine();
            if (state.Kind == AiWorkflowStateKind.NoIfcModel)
            {
                builder.AppendLine("Interpretation: no IFC elements are loaded, so GA-candidate readiness should not be framed as a BIM weight/grouping blocker yet. Load IFC first.");
            }
            else if (state.Kind == AiWorkflowStateKind.LiftUnitReadinessBlocked)
            {
                builder.AppendLine("Interpretation: IFC elements are loaded and displayable, but the optimizer has zero trusted GA-ready grouped lift units. This is an element-readiness blocker, not a zone-placement or crane-placement bypass request.");
                if (state.HasEstimatedWeightBlocker)
                    builder.AppendLine("Likely cause from deterministic evidence: displayed weights are estimated/fallback values rather than trusted explicit BIM mass or weight quantities, so the GA safety gate correctly blocks them.");
                builder.AppendLine("AI action boundary: explain the blocker, copy evidence, and prepare a BIM/export checklist; do not relax GA eligibility or use estimated weights for engineering optimization.");
            }
            else if (state.CanRunOptimizerNow)
            {
                builder.AppendLine("Interpretation: core prerequisites are present. A controlled optimizer run is the next deterministic step, followed by diagnostics review.");
            }
            else
            {
                builder.AppendLine("Interpretation: complete the state-specific prerequisite above before treating this as an optimizer-result question.");
            }

            return builder.ToString().Trim();
        }

        public static string BuildOptimizerIssueSummary(AiWorkflowContextSnapshot context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var builder = new StringBuilder();
            builder.AppendLine("CranePathOptimizer AI issue summary — deterministic evidence");
            builder.AppendLine();
            builder.AppendLine($"Project: {(context.HasProject ? context.CurrentProjectName : "none")}");
            builder.AppendLine($"Current state: {context.ElementCount:N0} element(s), {context.ZoneCount:N0} zone(s), {context.CraneCount:N0} crane(s).");

            if (context.OptimizationResult.HasResult)
            {
                var result = context.OptimizationResult;
                builder.AppendLine($"Latest result: {result.CoverageRatio:P2} coverage, {result.LiftedElementCount:N0}/{result.TotalElementCount:N0} lifted, {result.PositionCount:N0} setup position(s).");
                if (!string.IsNullOrWhiteSpace(result.DiagnosticsPath))
                    builder.AppendLine($"Diagnostics path: {result.DiagnosticsPath}");
            }
            else
            {
                builder.AppendLine("Latest result: none available in this session.");
            }

            builder.AppendLine();
            builder.AppendLine("Primary findings:");
            var issues = GetPriorityIssues(context).Take(10).ToList();
            if (issues.Count == 0)
            {
                builder.AppendLine("- No deterministic warning/error issues are present in the AI context snapshot.");
            }
            else
            {
                foreach (var item in issues)
                {
                    builder.AppendLine($"- [{item.Issue.Severity}] {item.ScreenName} / {item.Issue.Code}: {item.Issue.Evidence}");
                    if (!string.IsNullOrWhiteSpace(item.Issue.Recommendation))
                        builder.AppendLine($"  Recommended action: {item.Issue.Recommendation}");
                    if (!string.IsNullOrWhiteSpace(item.Issue.NextClick))
                        builder.AppendLine($"  Next click: {item.Issue.NextClick}");
                }
            }

            builder.AppendLine();
            builder.AppendLine("Scope note: this summary is generated from deterministic application context for review. It does not certify crane capacity, site safety, rigging design, or engineering compliance.");
            return builder.ToString().Trim();
        }

        public static string BuildBimExporterChecklist(AiWorkflowContextSnapshot context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var builder = new StringBuilder();
            builder.AppendLine("BIM / IFC export checklist for CranePathOptimizer GA readiness");
            builder.AppendLine();
            builder.AppendLine("Purpose");
            builder.AppendLine("Provide explicit, trusted lift-unit data so CranePathOptimizer can create GA-ready lift units without relying on estimated fallback weights.");
            builder.AppendLine();
            builder.AppendLine("Detected application state");
            builder.AppendLine($"- Model context: {NormalizeInline(context.ModelDisplayName, string.IsNullOrWhiteSpace(context.CurrentProjectName) ? "Imported IFC model" : context.CurrentProjectName)}");
            builder.AppendLine($"- Model revision: {context.ModelRevision:N0}");
            builder.AppendLine($"- Total BIM objects: {context.ElementCount:N0}");
            if (context.ElementTableTotalCount > 0)
                builder.AppendLine($"- Element table scope: {context.ElementTableVisibleCount:N0} visible of {context.ElementTableTotalCount:N0} table item(s)");
            builder.AppendLine($"- Placement zones: {context.ZoneCount:N0}");
            builder.AppendLine($"- Cranes in scene: {context.CraneCount:N0}");
            builder.AppendLine($"- Optimization result available: {context.OptimizationResult.HasResult}");

            var priorityIssues = GetPriorityIssues(context).Take(8).ToList();
            if (priorityIssues.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Current deterministic blockers/warnings");
                foreach (var item in priorityIssues)
                    builder.AppendLine($"- {item.ScreenName}: {item.Issue.Code} — {item.Issue.Evidence}");
            }

            builder.AppendLine();
            var state = AiWorkflowStateClassifier.Classify(context);
            if (state.Kind == AiWorkflowStateKind.NoIfcModel)
            {
                builder.AppendLine("Immediate action");
                builder.AppendLine("No IFC elements are loaded yet, so there is no element list to audit. Load the IFC first; then regenerate this checklist if readiness remains blocked.");
                builder.AppendLine();
            }

            builder.AppendLine("Required IFC/BIM data to provide or verify");
            builder.AppendLine("- Explicit element or assembly mass/weight quantity, not a geometry-density estimate.");
            builder.AppendLine("- Clear assembly/lift mark for each lift unit, preferably from a stable assembly-level property.");
            builder.AppendLine("- Source GUID / element ID / IFC name / IFC type preserved for traceability.");
            builder.AppendLine("- Element or assembly volume and material/density basis when weight must be audited.");
            builder.AppendLine("- Center of gravity or enough valid geometry to compute a reviewed COG.");
            builder.AppendLine("- Parent/child assembly structure so child components are not optimized alongside their parent assembly.");
            builder.AppendLine("- Consistent units for mass, weight, length, elevation, and coordinates.");
            builder.AppendLine();
            builder.AppendLine("Exporter request wording");
            if (state.Kind == AiWorkflowStateKind.NoIfcModel)
                builder.AppendLine("Please provide/load the IFC model so CranePathOptimizer can inspect elements, metadata, weights, COG, and assembly/lift-unit readiness. No optimization conclusion can be made from the current zero-element state.");
            else
                builder.AppendLine("Please re-export the IFC with explicit precast lift-unit weight or mass properties and stable assembly marks. The current import can be displayed, but deterministic readiness indicates zero trusted GA-ready lift candidates because required trusted lift-unit data is missing or blocked. Do not rely on estimated fallback weights for crane optimization.");
            builder.AppendLine();
            builder.AppendLine("AI scope note: this checklist is advisory text only. It does not change optimizer eligibility, safety factors, load-chart capacity, or report validity rules.");
            return builder.ToString().Trim();
        }



        public static string BuildNoGaCandidateAssistantAnswer(AiWorkflowContextSnapshot context, string visibleQuestion, string deterministicNextAction, string activeScreen)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var state = AiWorkflowStateClassifier.Classify(context);
            if (state.Kind == AiWorkflowStateKind.NoIfcModel)
                return BuildNoIfcAssistantAnswer(context, activeScreen);

            var questionMode = ClassifyBlockerQuestion(visibleQuestion);
            var priorityIssues = GetPriorityIssues(context).Take(5).ToList();
            var builder = new StringBuilder();

            builder.AppendLine("## Status");
            if (state.Kind == AiWorkflowStateKind.LiftUnitReadinessBlocked)
            {
                builder.AppendLine("Do **not** run the optimizer yet. IFC elements are loaded, but GA is blocked because deterministic readiness found **zero trusted GA-ready grouped lift units**.");
            }
            else
            {
                builder.AppendLine($"Do **not** treat this as a completed optimization result yet. Current deterministic state: **{state.Status}** — {state.RootCause}");
            }
            builder.AppendLine();

            builder.AppendLine("## Evidence");
            builder.AppendLine($"- Current screen: {NormalizeInline(activeScreen, "unknown")}");
            builder.AppendLine($"- Model context: {NormalizeInline(context.ModelDisplayName, string.IsNullOrWhiteSpace(context.CurrentProjectName) ? "Imported IFC model" : context.CurrentProjectName)}");
            builder.AppendLine($"- Model revision: {context.ModelRevision:N0}");
            builder.AppendLine($"- Project inputs: {context.ElementCount:N0} total BIM object(s), {context.ZoneCount:N0} zone(s), {context.CraneCount:N0} crane(s).");
            if (context.ElementTableTotalCount > 0)
                builder.AppendLine($"- Element table scope: {context.ElementTableVisibleCount:N0} visible of {context.ElementTableTotalCount:N0} table item(s).");
            if (state.Kind == AiWorkflowStateKind.LiftUnitReadinessBlocked)
            {
                builder.AppendLine("- Blocker: `NoGaCandidates` / `GaCannotStartNoCandidates` after IFC elements are loaded.");
                if (state.HasEstimatedWeightBlocker)
                    builder.AppendLine("- Weight issue: visible weights are being treated as estimated or fallback values, not trusted BIM mass/weight evidence for GA.");
                else
                    builder.AppendLine("- Lift-unit issue: no imported element group is currently eligible to become a GA lift candidate.");
            }

            foreach (var item in priorityIssues)
                builder.AppendLine($"- {item.ScreenName}: `{item.Issue.Code}` — {NormalizeInline(item.Issue.Evidence, "no evidence text")}");
            builder.AppendLine();

            builder.AppendLine("## Recommendation");
            if (state.Kind != AiWorkflowStateKind.LiftUnitReadinessBlocked)
            {
                builder.AppendLine(state.RecommendedAction);
                builder.AppendLine();
            }
            else if (questionMode == "Explain")
            {
                builder.AppendLine("The displayed weight column is not the same thing as trusted engineering weight evidence. The app can display calculated or fallback weights for review, but the optimizer correctly requires explicit, trusted lift-unit quantities before it performs crane-capacity optimization.");
                builder.AppendLine();
            }
            else if (questionMode == "Troubleshoot")
            {
                builder.AppendLine("Treat this as an element-readiness problem, not a crane-position or load-chart problem yet. First prove that each intended lift unit has trusted weight/mass evidence and a stable assembly/lift mark.");
                builder.AppendLine();
            }
            else if (questionMode == "Blocker")
            {
                builder.AppendLine("Ask the BIM/export owner to provide explicit mass or weight quantities at the element/assembly level, stable assembly/lift marks, source identifiers, and COG or enough auditable geometry for COG review. Then re-import and re-check readiness.");
                builder.AppendLine();
            }
            else
            {
                builder.AppendLine("Review the IFC import/element readiness evidence and fix the trusted-weight source before trying GA again.");
                builder.AppendLine();
            }

            builder.AppendLine("## Next Click");
            var nextClick = string.IsNullOrWhiteSpace(deterministicNextAction) ? state.RecommendedAction : deterministicNextAction.Trim();
            if (state.Kind == AiWorkflowStateKind.LiftUnitReadinessBlocked && nextClick.Contains("Run Optimizer", StringComparison.OrdinalIgnoreCase))
                nextClick = "IFC MODEL → Export Element List / trusted weight-source review";
            builder.AppendLine(nextClick);
            builder.AppendLine();

            builder.AppendLine("## What to verify afterward");
            if (state.Kind == AiWorkflowStateKind.LiftUnitReadinessBlocked)
            {
                builder.AppendLine("1. The exported element list identifies which elements are blocked and why.");
                builder.AppendLine("2. The IFC/BIM source contains explicit mass or weight quantities, not only geometry-derived estimates.");
                builder.AppendLine("3. Assembly/lift marks are stable enough to group elements into lift units.");
                builder.AppendLine("4. After re-import, the readiness card no longer reports zero trusted GA-ready lift units.");
                builder.AppendLine("5. Only after that, verify crane setup and load-chart evidence before running GA.");
            }
            else
            {
                builder.AppendLine("1. Complete the recommended prerequisite.");
                builder.AppendLine("2. Refresh AI Mission Control and confirm this state is no longer reported.");
                builder.AppendLine("3. Run GA only when the state says the optimizer is ready.");
            }
            builder.AppendLine();

            builder.AppendLine("## Risk");
            builder.AppendLine(state.Risk);
            builder.AppendLine();

            builder.AppendLine("## Confidence");
            builder.AppendLine("High — this answer is based on deterministic live workflow evidence, not a model guess.");

            return builder.ToString().Trim();
        }

        private static string BuildNoIfcAssistantAnswer(AiWorkflowContextSnapshot context, string activeScreen)
        {
            var builder = new StringBuilder();
            builder.AppendLine("## Status");
            builder.AppendLine("No IFC/building elements are loaded yet, so GA readiness and lift-candidate blockers cannot be meaningfully diagnosed.");
            builder.AppendLine();
            builder.AppendLine("## Evidence");
            builder.AppendLine($"- Current screen: {NormalizeInline(activeScreen, "unknown")}");
            builder.AppendLine($"- Model context: {NormalizeInline(context.ModelDisplayName, string.IsNullOrWhiteSpace(context.CurrentProjectName) ? "Imported IFC model" : context.CurrentProjectName)}");
            builder.AppendLine($"- Model revision: {context.ModelRevision:N0}");
            builder.AppendLine($"- Project inputs: {context.ElementCount:N0} total BIM object(s), {context.ZoneCount:N0} zone(s), {context.CraneCount:N0} crane(s).");
            if (context.ElementTableTotalCount > 0)
                builder.AppendLine($"- Element table scope: {context.ElementTableVisibleCount:N0} visible of {context.ElementTableTotalCount:N0} table item(s).");
            builder.AppendLine();
            builder.AppendLine("## Recommendation");
            builder.AppendLine("Load an IFC first. Do not export an element list or run the optimizer while the element count is zero.");
            builder.AppendLine();
            builder.AppendLine("## Next Click");
            builder.AppendLine("IFC MODEL → Load IFC");
            builder.AppendLine();
            builder.AppendLine("## Confidence");
            builder.AppendLine("High — the live element count is zero.");
            return builder.ToString().Trim();
        }

        public static string BuildRecoveryPlan(AiWorkflowContextSnapshot context, WorkflowReadinessSnapshot readiness, string activeScreen)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (readiness == null) throw new ArgumentNullException(nameof(readiness));

            var state = AiWorkflowStateClassifier.Classify(context, readiness);
            var priorityIssues = GetPriorityIssues(context).Take(8).ToList();
            var builder = new StringBuilder();

            builder.AppendLine("AI Recovery Plan — deterministic engineering workflow");
            builder.AppendLine();
            builder.AppendLine($"Project: {(context.HasProject ? context.CurrentProjectName : "none")}");
            builder.AppendLine($"Active screen: {NormalizeInline(activeScreen, readiness.ActiveScreen)}");
            builder.AppendLine($"Inputs: {context.ElementCount:N0} element(s), {context.ZoneCount:N0} zone(s), {context.CraneCount:N0} crane(s).");
            builder.AppendLine($"Readiness: {readiness.CompleteCount}/{readiness.TotalCount} complete; {readiness.MissingCriticalCount} critical missing.");
            builder.AppendLine();

            builder.AppendLine("1. Stop and classify the current state");
            builder.AppendLine($"- Classification: {state.Status} — {state.Title.Replace("AI Diagnosis: ", string.Empty)}.");
            builder.AppendLine($"- Meaning: {state.RootCause}");
            builder.AppendLine();

            builder.AppendLine("2. Do the next safe action");
            var immediate = readiness.Items.FirstOrDefault(item => !item.IsComplete && item.IsCritical && IsCorePrerequisiteName(item.Name));
            if (state.Kind == AiWorkflowStateKind.LiftUnitReadinessBlocked && immediate != null)
            {
                builder.AppendLine($"- Immediate workflow step: {immediate.NextClick}");
                builder.AppendLine($"- Known downstream optimizer data blocker: {state.RecommendedAction}");
            }
            else
            {
                builder.AppendLine($"- {state.RecommendedAction}");
            }
            if (!state.CanRunOptimizerNow)
                builder.AppendLine("- Do not run GA until AI Mission Control no longer reports a blocking prerequisite.");
            else
                builder.AppendLine("- Run GA as a controlled attempt, then review diagnostics and unresolved elements before reporting.");
            builder.AppendLine();

            builder.AppendLine("3. Gather the evidence pack");
            builder.AppendLine("- Copy readiness evidence from AI Assist.");
            if (state.Kind == AiWorkflowStateKind.NoIfcModel)
                builder.AppendLine("- Load IFC first; there are no elements to export yet.");
            else
                builder.AppendLine("- Export the element list / import-readiness review from IFC MODEL.");
            builder.AppendLine("- Keep load-chart source, crane setup, zone definitions, and GA diagnostics together with the issue summary when they exist.");
            if (priorityIssues.Count > 0)
            {
                builder.AppendLine("- Current warning/error evidence:");
                foreach (var item in priorityIssues)
                    builder.AppendLine($"  - {item.ScreenName}: {item.Issue.Severity} {item.Issue.Code} — {NormalizeInline(item.Issue.Evidence, "no evidence text")}");
            }
            builder.AppendLine();

            builder.AppendLine("4. Assign the next owner");
            switch (state.Kind)
            {
                case AiWorkflowStateKind.NoProject:
                    builder.AppendLine("- Project owner: open/create the project workspace.");
                    break;
                case AiWorkflowStateKind.NoIfcModel:
                    builder.AppendLine("- BIM/model owner: provide or load the IFC model.");
                    break;
                case AiWorkflowStateKind.LiftUnitReadinessBlocked:
                    builder.AppendLine("- BIM/export owner: provide explicit mass/weight quantities, stable assembly/lift marks, source identifiers, and auditable COG/geometry evidence.");
                    builder.AppendLine("- Crane planning owner: do not force GA and do not use estimated/fallback weights for engineering optimization.");
                    break;
                case AiWorkflowStateKind.ZonesMissing:
                    builder.AppendLine("- Planner: define allowed and forbidden placement zones.");
                    break;
                case AiWorkflowStateKind.CraneMissing:
                    builder.AppendLine("- Planner: add/select the crane and confirm setup assumptions.");
                    break;
                case AiWorkflowStateKind.LoadChartMissing:
                    builder.AppendLine("- Planner/reviewer: import or verify manufacturer load-chart evidence.");
                    break;
                default:
                    builder.AppendLine("- Reviewer: verify assumptions, run diagnostics, and approve any report wording before issue.");
                    break;
            }
            builder.AppendLine();

            builder.AppendLine("5. Verify after the fix");
            builder.AppendLine("- Refresh or re-import project data.");
            builder.AppendLine("- Confirm the same blocker/state is gone from AI Mission Control and readiness evidence.");
            builder.AppendLine("- Run optimization only after the classifier says the optimizer is ready.");
            builder.AppendLine("- If GA runs, review coverage, unresolved elements, diagnostics, and report caveats.");
            builder.AppendLine();

            builder.AppendLine("6. Safety boundary");
            builder.AppendLine("- This AI recovery plan is advisory text generated from deterministic app evidence. It does not certify crane capacity, rigging design, site conditions, or compliance.");
            return builder.ToString().Trim();
        }

        public static string BuildWhatIfAdvisorBrief(AiWorkflowContextSnapshot context, WorkflowReadinessSnapshot readiness)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (readiness == null) throw new ArgumentNullException(nameof(readiness));

            var state = AiWorkflowStateClassifier.Classify(context, readiness);
            var builder = new StringBuilder();
            builder.AppendLine("AI What-if Advisor — next engineering lever");
            builder.AppendLine();

            switch (state.Kind)
            {
                case AiWorkflowStateKind.NoProject:
                    builder.AppendLine("Primary question: should I change crane, zones, or load chart now?");
                    builder.AppendLine("Answer: no. Open/create the project first so the AI can inspect deterministic workflow evidence.");
                    break;
                case AiWorkflowStateKind.NoIfcModel:
                    builder.AppendLine("Primary question: should I change crane, zones, or load chart now?");
                    builder.AppendLine("Answer: no. Load IFC first. With zero elements, there are no lift units, weights, COGs, or building metadata for the optimizer or AI to inspect.");
                    break;
                case AiWorkflowStateKind.LiftUnitReadinessBlocked:
                    builder.AppendLine("Primary question: would a larger crane, different zone, or load-chart change help?");
                    builder.AppendLine("Answer: not yet. The dominant blocker is data readiness, not crane reach or capacity. GA has zero trusted GA-ready lift candidates, so changing crane parameters cannot produce a reliable engineering optimization.");
                    builder.AppendLine();
                    builder.AppendLine("Best next lever: fix trusted lift-unit evidence — explicit mass/weight quantities, stable assembly/lift marks, source identifiers, and COG/geometry evidence.");
                    builder.AppendLine("After the blocker clears, AI can compare whether remaining failures are radius, capacity, height, chart coverage, or zone-placement driven.");
                    break;
                case AiWorkflowStateKind.ZonesMissing:
                    builder.AppendLine("Primary question: what should be changed first?");
                    builder.AppendLine("Answer: define placement zones before changing crane or load-chart settings. GA needs explicit allowed/forbidden placement space.");
                    break;
                case AiWorkflowStateKind.CraneMissing:
                    builder.AppendLine("Primary question: should I adjust BIM data or zones first?");
                    builder.AppendLine("Answer: add/select the crane next. IFC and zones exist, but there is no crane setup to evaluate.");
                    break;
                case AiWorkflowStateKind.LoadChartMissing:
                    builder.AppendLine("Primary question: should I run GA now?");
                    builder.AppendLine("Answer: no. Import or verify load-chart evidence first so capacity/radius feasibility has a manufacturer-data basis.");
                    break;
                case AiWorkflowStateKind.ResultReview:
                case AiWorkflowStateKind.ReportCandidate:
                    var result = context.OptimizationResult;
                    builder.AppendLine("Primary question: is this a report-ready result or a diagnostics problem?");
                    builder.AppendLine(result.CoverageRatio >= 0.95
                        ? $"Answer: coverage is {result.CoverageRatio:P2}, so treat this as report-candidate after engineering review."
                        : $"Answer: coverage is {result.CoverageRatio:P2}; use diagnostics to determine whether unresolved items are radius, capacity, height, or load-chart coverage driven.");
                    builder.AppendLine($"Current result evidence: {result.LiftedElementCount:N0}/{result.TotalElementCount:N0} lifted over {result.PositionCount:N0} setup position(s).");
                    if (!string.IsNullOrWhiteSpace(result.DiagnosticsPath))
                        builder.AppendLine($"Diagnostics path: {result.DiagnosticsPath}");
                    break;
                default:
                    builder.AppendLine("Primary question: what should be tested first?");
                    builder.AppendLine("Answer: run a controlled optimizer attempt only because the deterministic prerequisites are currently present. Then let AI classify the dominant failure mode from diagnostics.");
                    break;
            }

            builder.AppendLine();
            builder.AppendLine("Current next lever: " + state.RecommendedAction);
            builder.AppendLine();
            builder.AppendLine("AI boundary: this what-if advisor ranks next investigation levers; it does not change optimizer gates or certify any lift plan.");
            return builder.ToString().Trim();
        }

        public static string BuildAiReportNarrativeDraft(AiWorkflowContextSnapshot context, WorkflowReadinessSnapshot readiness)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (readiness == null) throw new ArgumentNullException(nameof(readiness));

            var state = AiWorkflowStateClassifier.Classify(context, readiness);
            var builder = new StringBuilder();
            builder.AppendLine("AI-assisted crane-planning report language — reviewer approval required");
            builder.AppendLine();
            builder.AppendLine($"Project: {(context.HasProject ? context.CurrentProjectName : "none")}");
            builder.AppendLine($"AI readiness state: {readiness.CompleteCount}/{readiness.TotalCount} readiness checks complete, with {readiness.MissingCriticalCount} critical gap(s) exposed at generation time.");

            builder.AppendLine();
            builder.AppendLine("Workflow status note");
            if (state.Kind == AiWorkflowStateKind.NoIfcModel)
            {
                builder.AppendLine("No IFC/model elements were available to the application when this AI narrative was generated. No crane optimization or lift-unit conclusion should be reported from this state.");
            }
            else if (state.Kind == AiWorkflowStateKind.LiftUnitReadinessBlocked)
            {
                builder.AppendLine("The IFC model elements were available to the application, but the optimizer was not run to a valid GA result because deterministic readiness reported zero trusted GA-ready grouped lift candidates. This condition indicates missing or untrusted lift-unit evidence rather than a proven crane-reach or crane-capacity failure.");
                if (state.HasEstimatedWeightBlocker)
                    builder.AppendLine("Visible weights should not be treated as engineering optimization inputs where they are classified as estimated or fallback values. Explicit trusted BIM mass or weight quantities must be provided and reviewed before optimization.");
            }
            else if (context.OptimizationResult.HasResult)
            {
                var result = context.OptimizationResult;
                builder.AppendLine($"The latest optimization snapshot lifted {result.LiftedElementCount:N0} of {result.TotalElementCount:N0} target element(s), corresponding to {result.CoverageRatio:P2} coverage across {result.PositionCount:N0} crane setup position(s).");
                if (!string.IsNullOrWhiteSpace(result.DiagnosticsPath))
                    builder.AppendLine($"Diagnostics evidence path: {result.DiagnosticsPath}");
                if (result.CoverageRatio < 0.95)
                    builder.AppendLine("Unresolved elements and diagnostics should be reviewed before issuing final lifting-route conclusions.");
            }
            else if (state.CanRunOptimizerNow)
            {
                builder.AppendLine("Core deterministic prerequisites were present, but no completed optimization result was available when this AI narrative was generated. Any report should clearly distinguish setup/readiness review from completed optimization results.");
            }
            else
            {
                builder.AppendLine($"No completed optimization result was available because the current prerequisite is not complete: {state.RootCause}");
            }

            var priorityIssues = GetPriorityIssues(context).Take(8).ToList();
            if (priorityIssues.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("AI-visible evidence requiring review");
                foreach (var item in priorityIssues)
                    builder.AppendLine($"- {item.ScreenName}: {item.Issue.Severity} {item.Issue.Code}. {NormalizeInline(item.Issue.Evidence, "no evidence text")}");
            }

            builder.AppendLine();
            builder.AppendLine("Review caveat");
            builder.AppendLine("This language was drafted from deterministic CranePathOptimizer context. It does not certify crane capacity, rigging design, site conditions, safety compliance, or final constructability. A qualified reviewer must validate source model data, load-chart evidence, diagnostics, assumptions, and project-specific safety requirements before issue.");
            return builder.ToString().Trim();
        }

        public static string BuildQaEvidencePack(AiWorkflowContextSnapshot context, WorkflowReadinessSnapshot readiness, string activeScreen)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (readiness == null) throw new ArgumentNullException(nameof(readiness));

            var builder = new StringBuilder();
            builder.AppendLine("AI QA evidence pack — deterministic context snapshot");
            builder.AppendLine();
            builder.AppendLine(BuildWorkflowBlockerSummary(context));
            builder.AppendLine();
            builder.AppendLine("Readiness checklist");
            foreach (var item in readiness.Items)
            {
                builder.AppendLine($"- [{item.Severity}] {item.Name}: {(item.IsComplete ? "complete" : "open")}");
                builder.AppendLine($"  Evidence: {item.Evidence}");
                builder.AppendLine($"  Recommendation: {item.Recommendation}");
                builder.AppendLine($"  Next click: {item.NextClick}");
            }
            builder.AppendLine();
            builder.AppendLine("Recovery plan");
            builder.AppendLine(BuildRecoveryPlan(context, readiness, activeScreen));
            builder.AppendLine();
            builder.AppendLine("What-if advisor");
            builder.AppendLine(BuildWhatIfAdvisorBrief(context, readiness));
            return builder.ToString().Trim();
        }

        public static bool IsIfcMetadataQuestion(string? question)
        {
            if (string.IsNullOrWhiteSpace(question))
                return false;

            var text = question.ToUpperInvariant();
            return text.Contains("BUILDING TYPE")
                || text.Contains("BUILDING USAGE")
                || text.Contains("BUILDING USE")
                || text.Contains("USAGE") && text.Contains("IFC")
                || text.Contains("IFCBUILDING")
                || text.Contains("PSET_BUILDINGCOMMON")
                || text.Contains("PROPERTY SET")
                || text.Contains("PROPERTYSET")
                || text.Contains("CLASSIFICATION")
                || text.Contains("METADATA")
                || text.Contains("IFC PROPERTY")
                || text.Contains("IFC PROPERTIES");
        }

        public static bool IsNoGaCandidateWorkflowQuestion(string? question)
        {
            if (string.IsNullOrWhiteSpace(question))
                return false;

            if (IsIfcMetadataQuestion(question))
                return false;

            var text = question.ToUpperInvariant();
            return text.Contains("WHAT SHOULD I DO NEXT")
                || text.Contains("WHAT ABOUT NOW")
                || text.Contains("NEXT STEP")
                || text.Contains("WHY IS THE OPTIMIZER BLOCKED")
                || text.Contains("OPTIMIZER BLOCKED")
                || text.Contains("BLOCKER")
                || text.Contains("NO VALID")
                || text.Contains("NO CANDIDATE")
                || text.Contains("ZERO VALID")
                || text.Contains("TROUBLESHOOT")
                || text.Contains("BIM EXPORTER")
                || text.Contains("WEIGHT")
                || text.Contains("RUN OPTIMIZER")
                || text.Contains("RUN GA")
                || text.Contains("EXPLAIN THIS SCREEN")
                || text.Contains("WHAT-IF") && (text.Contains("CRANE") || text.Contains("ZONE") || text.Contains("LOAD-CHART") || text.Contains("LOAD CHART"));
        }

        public static bool ShouldUseStrictNoGaCandidateRouting(AiWorkflowContextSnapshot? context, string? question)
        {
            return context != null
                && context.ElementCount > 0
                && HasNoGaCandidateBlocker(context)
                && IsNoGaCandidateWorkflowQuestion(question);
        }

        public static string BuildIfcMetadataAssistantAnswer(AiWorkflowContextSnapshot context, string visibleQuestion, string activeScreen)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var state = AiWorkflowStateClassifier.Classify(context);
            var ifc = context.ScreenContexts.FirstOrDefault(screen => screen.ScreenName.Contains("IFC", StringComparison.OrdinalIgnoreCase));
            var facts = ifc?.Facts ?? new Dictionary<string, string>();
            var builder = new StringBuilder();

            builder.AppendLine("## Status");
            if (context.ElementCount <= 0)
            {
                builder.AppendLine("Not yet. No IFC elements are loaded, so building type/usage metadata cannot be inspected from the current session.");
            }
            else
            {
                var modelName = !string.IsNullOrWhiteSpace(context.ModelDisplayName)
                    ? context.ModelDisplayName.Trim()
                    : !string.IsNullOrWhiteSpace(context.CurrentProjectName)
                        ? context.CurrentProjectName.Trim()
                        : "the current model";
                var usage = facts.TryGetValue("BuildingUsage", out var usageValue) ? usageValue : string.Empty;
                var type = facts.TryGetValue("BuildingType", out var typeValue) ? typeValue : string.Empty;
                var usageExposed = !string.IsNullOrWhiteSpace(usage) && !usage.StartsWith("Not exposed", StringComparison.OrdinalIgnoreCase);
                var typeExposed = !string.IsNullOrWhiteSpace(type) && !type.StartsWith("Not exposed", StringComparison.OrdinalIgnoreCase);

                if (usageExposed || typeExposed)
                    builder.AppendLine($"For {modelName}, the current AI context exposes building usage/type metadata below. IFC can contain these values when the BIM/export author includes them in standard entities, property sets, classifications, or custom properties.");
                else
                    builder.AppendLine($"For {modelName}, building type/usage is not exposed in the current AI context. I should not infer it from geometry alone. IFC can contain this metadata only when the BIM/export author included it in standard entities, property sets, classifications, or custom properties.");
            }
            builder.AppendLine();

            builder.AppendLine("## Where IFC usually stores this");
            builder.AppendLine("- `IfcProject`, `IfcSite`, `IfcBuilding`, and `IfcBuildingStorey` names/descriptions.");
            builder.AppendLine("- Property sets such as `Pset_BuildingCommon` or custom project property sets.");
            builder.AppendLine("- Classification references, object type fields, element categories, or discipline-specific custom parameters.");
            builder.AppendLine();

            builder.AppendLine("## Current evidence");
            builder.AppendLine($"- Current screen: {NormalizeInline(activeScreen, "unknown")}");
            builder.AppendLine($"- Model context: {NormalizeInline(context.ModelDisplayName, string.IsNullOrWhiteSpace(context.CurrentProjectName) ? "Imported IFC model" : context.CurrentProjectName)}");
            builder.AppendLine($"- Model revision: {context.ModelRevision:N0}");
            builder.AppendLine($"- Elements loaded / total BIM objects: {context.ElementCount:N0}");
            if (context.ElementTableTotalCount > 0)
                builder.AppendLine($"- Element table scope: {context.ElementTableVisibleCount:N0} visible of {context.ElementTableTotalCount:N0} table item(s)");
            AppendMetadataFact(builder, facts, "BuildingName", "Building name");
            AppendMetadataFact(builder, facts, "BuildingUsage", "Building usage");
            AppendMetadataFact(builder, facts, "BuildingType", "Building type");
            AppendMetadataFact(builder, facts, "TopRawIfcTypes", "Top IFC classes");
            AppendMetadataFact(builder, facts, "TopResolvedCategories", "Top resolved categories");
            if (context.ElementCount > 0 && !facts.ContainsKey("BuildingUsage") && !facts.ContainsKey("BuildingType"))
                builder.AppendLine("- Explicit building usage/type was not exposed in the current AI context. AI should not infer usage from geometry alone.");
            builder.AppendLine();

            builder.AppendLine("## Recommendation");
            if (context.ElementCount <= 0)
            {
                builder.AppendLine("Load the IFC first, then inspect/export metadata.");
                builder.AppendLine();
                builder.AppendLine("## Next Click");
                builder.AppendLine("IFC MODEL → Load IFC");
            }
            else
            {
                builder.AppendLine("Use IFC metadata inspection/export rather than GA blocker troubleshooting. Export the element list and review IFC class/category/name/type/property/classification fields. If usage/type is absent, ask the BIM author to add a clear building-usage property or classification before re-export.");
                builder.AppendLine();
                builder.AppendLine("## Next Click");
                builder.AppendLine("IFC MODEL → Export Element List / inspect metadata fields");
            }
            builder.AppendLine();

            if (state.Kind == AiWorkflowStateKind.LiftUnitReadinessBlocked)
            {
                builder.AppendLine("## Separate optimization caveat");
                builder.AppendLine("Separately, GA optimization is still blocked by lift-unit readiness. That does not prevent answering IFC metadata questions, but it does prevent producing a crane optimization plan.");
                builder.AppendLine();
            }

            builder.AppendLine("## Confidence");
            builder.AppendLine(context.ElementCount > 0
                ? "Medium — the current context exposes element-level metadata, but explicit building usage/type may require deeper IFC property-set inspection."
                : "High — no model elements are loaded.");

            return builder.ToString().Trim();
        }

        private static void AppendMetadataFact(StringBuilder builder, IReadOnlyDictionary<string, string> facts, string key, string label)
        {
            if (facts.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                builder.AppendLine($"- {label}: {value}");
        }

        public static string BuildStrictNoGaCandidatePrompt(AiWorkflowContextSnapshot context, string visibleQuestion, string deterministicNextAction, string activeScreen)
        {
            var builder = new StringBuilder();
            builder.AppendLine("CURRENT WORKFLOW BLOCKER — answer only because the visible user question is about next steps, troubleshooting, GA, candidates, or weights.");
            builder.AppendLine(BuildNoGaCandidateAssistantAnswer(context, visibleQuestion, deterministicNextAction, activeScreen));
            builder.AppendLine();
            builder.AppendLine("LLM response rules for this request:");
            builder.AppendLine("- Start from the blocker above, not from product documentation or workflow inventory.");
            builder.AppendLine("- Keep the response operational: status, evidence, recommendation, next click, risk, confidence.");
            builder.AppendLine("- Do not mention XAML inventory, local documentation snippets, prompt templates, internal implementation details, or internal metadata unless the user explicitly asks for software implementation details.");
            builder.AppendLine("- Do not suggest bypassing GA, forcing a run, or using estimated weights for engineering decisions.");
            builder.AppendLine("- Tell the user exactly what to review/export and what to ask the BIM exporter to fix.");
            return builder.ToString().Trim();
        }

        private static string ClassifyBlockerQuestion(string? question)
        {
            if (string.IsNullOrWhiteSpace(question))
                return "Next";

            var text = question.ToUpperInvariant();
            if (text.Contains("TROUBLESHOOT")) return "Troubleshoot";
            if (text.Contains("WHY") || text.Contains("EXPLAIN")) return "Explain";
            if (text.Contains("BIM EXPORTER") || text.Contains("BLOCKER")) return "Blocker";
            return "Next";
        }

        private static string NormalizeInline(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private static bool IsCorePrerequisiteName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;

            return name.Contains("IFC", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Placement zones", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Crane setup", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Load chart evidence", StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<(string ScreenName, AiContextIssueSnapshot Issue)> GetPriorityIssues(AiWorkflowContextSnapshot context)
        {
            return context.ScreenContexts
                .SelectMany(screen => screen.Issues.Select(issue => (screen.ScreenName, Issue: issue)))
                .Where(item => item.Issue.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase)
                               || item.Issue.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => context.ElementCount > 0 && IsNoCandidateCode(item.Issue.Code))
                .ThenByDescending(item => SeverityRank(item.Issue.Severity))
                .ThenByDescending(item => item.Issue.AffectedCount)
                .ThenBy(item => item.ScreenName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Issue.Code, StringComparer.OrdinalIgnoreCase);
        }

        private static IEnumerable<AiContextIssueSnapshot> FindIssues(AiWorkflowContextSnapshot? context)
        {
            if (context == null)
                return Array.Empty<AiContextIssueSnapshot>();

            return context.ScreenContexts.SelectMany(screen => screen.Issues);
        }

        private static bool IsNoCandidateCode(string code) =>
            NoCandidateCodes.Any(noCandidateCode => code.Equals(noCandidateCode, StringComparison.OrdinalIgnoreCase));

        private static int SeverityRank(string? severity) => severity?.ToUpperInvariant() switch
        {
            "ERROR" => 3,
            "WARNING" => 2,
            "INFO" => 1,
            _ => 0
        };
    }
}
