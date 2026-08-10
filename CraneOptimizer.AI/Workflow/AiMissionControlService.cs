using System.Text;
using CraneOptimizer.AI.Host;
using CraneOptimizer.AI.Services;

namespace CraneOptimizer.AI.Workflow
{
    /// <summary>
    /// Deterministic AI-product layer that makes the copilot visible before the user
    /// asks a question. It converts workflow evidence into Mission Control diagnosis,
    /// intelligence cards, recovery plans, what-if advice, and report wording.
    /// </summary>
    public sealed class AiMissionControlService
    {
        public AiMissionControlSnapshot Build(AiWorkflowContextSnapshot context, WorkflowReadinessSnapshot readiness, string activeScreen)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (readiness == null) throw new ArgumentNullException(nameof(readiness));

            var state = AiWorkflowStateClassifier.Classify(context, readiness);
            var recommendedAction = BuildRecommendedAction(context, readiness, state);
            var priorityIssues = GetPriorityIssues(context).Take(8).ToList();
            var insights = BuildInsightCards(context, readiness, state, recommendedAction, priorityIssues).ToList();

            return new AiMissionControlSnapshot
            {
                DiagnosisTitle = state.Title,
                DiagnosisStatus = state.Status,
                RootCause = state.RootCause,
                Evidence = BuildEvidence(context, readiness, activeScreen, state, priorityIssues),
                RecommendedAction = recommendedAction,
                Confidence = state.Confidence,
                Risk = state.Risk,
                RecoveryPlan = AiWorkflowEvidenceTextBuilder.BuildRecoveryPlan(context, readiness, activeScreen),
                WhatIfAdvisor = AiWorkflowEvidenceTextBuilder.BuildWhatIfAdvisorBrief(context, readiness),
                ReportNarrative = AiWorkflowEvidenceTextBuilder.BuildAiReportNarrativeDraft(context, readiness),
                Insights = insights
            };
        }

        private static string BuildRecommendedAction(AiWorkflowContextSnapshot context, WorkflowReadinessSnapshot readiness, AiWorkflowState state)
        {
            var immediate = readiness.Items.FirstOrDefault(item => !item.IsComplete && item.IsCritical && IsCorePrerequisiteName(item.Name));
            return immediate != null ? $"Immediate workflow step: {immediate.NextClick}." : state.RecommendedAction;
        }

        private static bool IsCorePrerequisiteName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            return name.Contains("IFC", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Placement zones", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Crane setup", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Load chart evidence", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildEvidence(
            AiWorkflowContextSnapshot context,
            WorkflowReadinessSnapshot readiness,
            string activeScreen,
            AiWorkflowState state,
            IReadOnlyList<(string ScreenName, AiContextIssueSnapshot Issue)> priorityIssues)
        {
            var builder = new StringBuilder();
            builder.Append(state.EvidencePrefix);
            builder.Append($" Inputs: {context.ElementCount:N0} element(s), {context.ZoneCount:N0} zone(s), {context.CraneCount:N0} crane(s); active screen: {Normalize(activeScreen, readiness.ActiveScreen)}; readiness {readiness.CompleteCount}/{readiness.TotalCount} complete.");

            if (context.OptimizationResult.HasResult)
            {
                var result = context.OptimizationResult;
                builder.Append($" Captured result: {result.CoverageRatio:P2}, {result.LiftedElementCount:N0}/{result.TotalElementCount:N0} lifted, {result.PositionCount:N0} setup position(s).");
            }
            else
            {
                builder.Append(" No completed optimization result is attached to this session.");
                if (state.CanProceedToOptimizerCheck)
                    builder.Append(" Current run eligibility must be verified in the optimizer's built-in Current Run Readiness; AI import/readiness candidate signals are not that preflight.");
            }

            if (priorityIssues.Count > 0)
            {
                var top = priorityIssues[0];
                builder.Append($" Highest issue: {top.ScreenName} / {top.Issue.Code} — {Normalize(top.Issue.Evidence, "no issue evidence")}");
            }

            AppendCountReconciliation(builder, context);
            return builder.ToString();
        }

        private static void AppendCountReconciliation(StringBuilder builder, AiWorkflowContextSnapshot context)
        {
            var facts = context.ScreenContexts
                .SelectMany(screen => screen.Facts)
                .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.OrdinalIgnoreCase);
            var labels = new List<string>();
            AddFact(labels, facts, AiWorkflowFactKeys.SceneBimObjectCount, "scene BIM/model objects");
            if (!facts.ContainsKey(AiWorkflowFactKeys.SceneBimObjectCount))
                AddFact(labels, facts, AiWorkflowFactKeys.TotalElements, "scene BIM/model objects");
            AddFact(labels, facts, AiWorkflowFactKeys.ReadinessElementCount, "readiness/planning elements");
            AddFact(labels, facts, AiWorkflowFactKeys.ReferenceGeometryExcludedCount, "IFC reference geometry excluded from readiness");
            AddFact(labels, facts, AiWorkflowFactKeys.RawCandidateLikeElements, "raw/import-screen candidate-like elements");
            AddFact(labels, facts, AiWorkflowFactKeys.TrustedGroupedLiftUnits, "AI import/readiness grouped-candidate signal");
            AddFact(labels, facts, AiWorkflowFactKeys.TrustedGaLiftUnits, "AI import/readiness candidate signal");
            if (!facts.ContainsKey(AiWorkflowFactKeys.RawCandidateLikeElements) && !facts.ContainsKey(AiWorkflowFactKeys.TrustedGroupedLiftUnits) && !facts.ContainsKey(AiWorkflowFactKeys.TrustedGaLiftUnits))
                AddFact(labels, facts, AiWorkflowFactKeys.GaCandidateElements, "legacy AI candidate/readiness signal");
            AddFact(labels, facts, AiWorkflowFactKeys.GaBlockedElements, "AI host blocked-element signal");
            AddFact(labels, facts, AiWorkflowFactKeys.EstimatedWeightBlockedCount, "estimated/fallback weight advisory");
            AddFact(labels, facts, AiWorkflowFactKeys.MissingTrustedWeightCount, "missing trusted weight");

            if (labels.Count > 0)
                builder.Append(" Count labels: " + string.Join("; ", labels) + ".");
        }

        private static void AddFact(ICollection<string> labels, IReadOnlyDictionary<string, string> facts, string key, string label)
        {
            if (facts.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                var entry = $"{label}={value}";
                if (!labels.Contains(entry))
                    labels.Add(entry);
            }
        }

        private static IEnumerable<AiMissionControlInsight> BuildInsightCards(
            AiWorkflowContextSnapshot context,
            WorkflowReadinessSnapshot readiness,
            AiWorkflowState state,
            string recommendedAction,
            IReadOnlyList<(string ScreenName, AiContextIssueSnapshot Issue)> priorityIssues)
        {
            yield return new AiMissionControlInsight
            {
                Title = "IFC Intelligence",
                Status = state.HasLoadedElements ? "Observed" : "Missing",
                Finding = state.HasLoadedElements ? "Model elements are available for review." : "No IFC elements are available yet.",
                Evidence = state.HasLoadedElements ? $"{context.ElementCount:N0} element(s) in live context." : "Element count is zero; load IFC before GA readiness is meaningful.",
                WhyItMatters = "Optimization starts from imported elements and their lift-unit evidence; display alone is not proof of GA eligibility.",
                NextAction = state.HasLoadedElements ? "IFC MODEL → review import readiness and exported element evidence." : "IFC MODEL → Load IFC.",
                Risk = state.HasLoadedElements ? "Review source properties before relying on GA." : "Optimization cannot start without model elements.",
                Confidence = "High"
            };

            yield return new AiMissionControlInsight
            {
                Title = "IFC Metadata Intelligence",
                Status = state.HasLoadedElements ? "Available for inspection" : "Needs IFC",
                Finding = state.HasLoadedElements
                    ? "Building name/type/usage may be discoverable from IFC project, site, building, property-set, classification, or element-type metadata if those fields were exported."
                    : "Building usage/type cannot be inspected until IFC data is loaded.",
                Evidence = BuildIfcMetadataEvidence(context),
                WhyItMatters = "Building usage is a BIM metadata question, not a GA blocker. It should be answered separately from lift-unit readiness.",
                NextAction = state.HasLoadedElements ? "IFC MODEL → Export Element List, then inspect IFC type/category/property/classification fields." : "IFC MODEL → Load IFC.",
                Risk = "Usage/type may be absent or stored in custom properties; AI must report unknown rather than infer from geometry.",
                Confidence = state.HasLoadedElements ? "Medium" : "High"
            };

            yield return new AiMissionControlInsight
            {
                Title = "Weight Trust Intelligence",
                Status = state.HasEstimatedWeightBlocker || state.HasNoGaCandidateBlocker ? "Review" : "Monitor",
                Finding = state.HasEstimatedWeightBlocker
                    ? "AI import/readiness evidence flags estimated or fallback weight data for review."
                    : state.HasNoGaCandidateBlocker
                        ? "AI import/readiness evidence reports no candidate units. Treat this as troubleshooting evidence, not the optimizer's run decision."
                        : "No weight-specific advisory is currently exposed by the AI context.",
                Evidence = state.HasEstimatedWeightBlocker
                    ? FirstIssueEvidence(priorityIssues, "Weight", "EstimatedWeightBlockedFromGA or equivalent advisory is present in host evidence.")
                    : FirstIssueEvidence(priorityIssues, "Weight", "No weight-specific advisory found in priority issues."),
                WhyItMatters = "Weight provenance can matter to engineering review, but only the optimizer's built-in preflight decides current run eligibility.",
                NextAction = state.CanProceedToOptimizerCheck
                    ? "Verify Current Run Readiness in the optimizer. If it reports a blocker, use the AI evidence to troubleshoot weight/source data."
                    : recommendedAction,
                Risk = "Do not bypass a blocker reported by the optimizer, and do not invent one from AI candidate counts.",
                Confidence = "High for the evidence boundary; optimizer eligibility requires built-in preflight"
            };

            yield return new AiMissionControlInsight
            {
                Title = "GA Feasibility Intelligence",
                Status = context.OptimizationResult.HasResult ? "Result available" : state.CanProceedToOptimizerCheck ? "Verify in optimizer" : "Prerequisite missing",
                Finding = context.OptimizationResult.HasResult
                    ? $"Selected result coverage is {context.OptimizationResult.CoverageRatio:P2}. A completed result does not establish current rerun eligibility."
                    : state.CanProceedToOptimizerCheck
                        ? "Visible project prerequisites are present; the optimizer's built-in Current Run Readiness must make the current run decision."
                        : "Complete the visible project prerequisite before checking optimizer readiness.",
                Evidence = context.OptimizationResult.HasResult
                    ? $"{context.OptimizationResult.LiftedElementCount:N0}/{context.OptimizationResult.TotalElementCount:N0} evaluated lift(s) completed over {context.OptimizationResult.PositionCount:N0} setup position(s)."
                    : readiness.Summary,
                WhyItMatters = "This keeps AI guidance separate from the untouched optimizer's authoritative preflight.",
                NextAction = recommendedAction,
                Risk = "AI must not certify ready or blocked from import/readiness candidate signals; never bypass a blocker reported by the optimizer.",
                Confidence = "High for workflow guidance; optimizer eligibility requires built-in preflight"
            };

            var optimizerEvidence = AiOptimizerEvidenceAssessor.Assess(context);
            yield return new AiMissionControlInsight
            {
                Title = "Crane Capacity Intelligence",
                Status = optimizerEvidence.HasCurrentLoadChartEvidence ? "Current evidence present" : "Missing current evidence",
                Finding = optimizerEvidence.HasCurrentLoadChartEvidence ? "Current crane specification/capacity evidence is visible to AI." : "Current crane context does not expose active specification/capacity evidence.",
                Evidence = optimizerEvidence.HasCurrentLoadChartEvidence
                    ? "Current crane context exposes an active specification, radius, or capacity value."
                    : "A historical optimization result is not accepted by AI as proof of the current crane's load-chart evidence.",
                WhyItMatters = "Capacity/radius checks need current manufacturer load-chart evidence before they can support engineering decisions.",
                NextAction = optimizerEvidence.HasCurrentLoadChartEvidence ? "Keep the current load-chart source in the QA pack." : "CRANES → Import Load Chart / verify the current capacity table.",
                Risk = optimizerEvidence.HasCurrentLoadChartEvidence ? "Low after review." : "High if reports imply current capacity verification without a current chart source.",
                Confidence = "Medium"
            };

            yield return new AiMissionControlInsight
            {
                Title = "Report Readiness Intelligence",
                Status = context.OptimizationResult.HasResult && context.OptimizationResult.CoverageRatio >= 0.95 ? "Candidate" : "Not final",
                Finding = context.OptimizationResult.HasResult
                    ? $"Report narrative can describe {context.OptimizationResult.CoverageRatio:P2} coverage."
                    : state.CanProceedToOptimizerCheck
                        ? "No final optimization result exists; current optimizer eligibility still requires the optimizer's built-in readiness check."
                        : "No optimization result exists for final report narrative yet.",
                Evidence = context.OptimizationResult.HasResult ? "Optimization result snapshot is attached." : "Result snapshot HasResult is false.",
                WhyItMatters = "AI-generated wording is useful for reports, but only after deterministic evidence is reviewed.",
                NextAction = context.OptimizationResult.HasResult ? "Copy AI report narrative and review before issuing." : "Generate reports after a valid optimization result or blocker-resolution note.",
                Risk = "AI wording is not engineering certification.",
                Confidence = "High"
            };
        }

        private static string BuildIfcMetadataEvidence(AiWorkflowContextSnapshot context)
        {
            var ifc = context.ScreenContexts.FirstOrDefault(screen => screen.ScreenName.Contains("IFC", StringComparison.OrdinalIgnoreCase));
            if (ifc == null)
                return context.ElementCount > 0 ? $"{context.ElementCount:N0} elements loaded; no IFC metadata contributor facts were attached." : "No IFC screen context is attached.";

            var parts = new List<string>();
            AddFact(parts, ifc.Facts, "BuildingName", "building name");
            AddFact(parts, ifc.Facts, AiWorkflowFactKeys.TopRawIfcTypes, "top IFC classes");
            if (ifc.Facts.ContainsKey(AiWorkflowFactKeys.TopPlanningCategories))
                AddFact(parts, ifc.Facts, AiWorkflowFactKeys.TopPlanningCategories, "top planning categories");
            else if (ifc.Facts.ContainsKey(AiWorkflowFactKeys.TopFilterableCategories))
                AddFact(parts, ifc.Facts, AiWorkflowFactKeys.TopFilterableCategories, "top filterable categories");
            else
                AddFact(parts, ifc.Facts, AiWorkflowFactKeys.TopResolvedCategories, "top complete-scene categories");
            AddFact(parts, ifc.Facts, "BuildingUsage", "building usage");
            AddFact(parts, ifc.Facts, "BuildingType", "building type");

            if (parts.Count == 0)
                parts.Add(ifc.Summary);

            return string.Join("; ", parts);
        }

        private static string FirstIssueEvidence(IReadOnlyList<(string ScreenName, AiContextIssueSnapshot Issue)> priorityIssues, string codeOrAreaFragment, string fallback)
        {
            var match = priorityIssues.FirstOrDefault(item => item.Issue.Code.Contains(codeOrAreaFragment, StringComparison.OrdinalIgnoreCase)
                                                              || item.Issue.Area.Contains(codeOrAreaFragment, StringComparison.OrdinalIgnoreCase));
            return match.Issue == null ? fallback : Normalize(match.Issue.Evidence, fallback);
        }

        private static IEnumerable<(string ScreenName, AiContextIssueSnapshot Issue)> GetPriorityIssues(AiWorkflowContextSnapshot context)
        {
            return context.ScreenContexts
                .SelectMany(screen => screen.Issues.Select(issue => (screen.ScreenName, Issue: issue)))
                .Where(item => item.Issue.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase)
                               || item.Issue.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => SeverityRank(item.Issue.Severity))
                .ThenBy(item => IsNoCandidateCode(item.Issue.Code) && context.ElementCount > 0)
                .ThenByDescending(item => item.Issue.AffectedCount)
                .ThenBy(item => item.ScreenName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Issue.Code, StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsNoCandidateCode(string code) =>
            code.Equals("GaCannotStartNoCandidates", StringComparison.OrdinalIgnoreCase)
            || code.Equals("GaPreRunNoCandidates", StringComparison.OrdinalIgnoreCase)
            || code.Equals("NoGaCandidates", StringComparison.OrdinalIgnoreCase);

        private static int SeverityRank(string? severity) => severity?.ToUpperInvariant() switch
        {
            "ERROR" => 3,
            "WARNING" => 2,
            "INFO" => 1,
            _ => 0
        };

        private static string Normalize(string? value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
