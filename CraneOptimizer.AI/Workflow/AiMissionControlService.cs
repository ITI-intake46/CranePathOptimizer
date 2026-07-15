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
            if (state.Kind == AiWorkflowStateKind.LiftUnitReadinessBlocked && immediate != null)
            {
                return $"Immediate workflow step: {immediate.NextClick}. Known downstream optimizer data blocker: IFC MODEL → Export Element List / trusted weight-source review, then request explicit trusted mass/weight, stable assembly/lift marks, and COG/geometry evidence.";
            }

            if (state.Kind == AiWorkflowStateKind.LiftUnitReadinessBlocked)
            {
                return "Optimizer data blocker: IFC MODEL → Export Element List / trusted weight-source review, then request explicit trusted mass/weight, stable assembly/lift marks, and COG/geometry evidence.";
            }

            return state.RecommendedAction;
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
                builder.Append($" Latest result: {result.CoverageRatio:P2}, {result.LiftedElementCount:N0}/{result.TotalElementCount:N0} lifted, {result.PositionCount:N0} setup position(s).");
            }
            else if (state.Kind == AiWorkflowStateKind.LiftUnitReadinessBlocked)
            {
                builder.Append(" No completed optimization result exists because pre-run lift-unit readiness is blocked.");
            }
            else
            {
                builder.Append(" No completed optimization result is attached to this session.");
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
            AddFact(labels, facts, AiWorkflowFactKeys.TrustedGroupedLiftUnits, "trusted GA-ready lift units");
            AddFact(labels, facts, AiWorkflowFactKeys.TrustedGaLiftUnits, "trusted GA-ready lift units");
            if (!facts.ContainsKey(AiWorkflowFactKeys.RawCandidateLikeElements) && !facts.ContainsKey(AiWorkflowFactKeys.TrustedGroupedLiftUnits) && !facts.ContainsKey(AiWorkflowFactKeys.TrustedGaLiftUnits))
                AddFact(labels, facts, AiWorkflowFactKeys.GaCandidateElements, "legacy GA-candidate/readiness count");
            AddFact(labels, facts, AiWorkflowFactKeys.GaBlockedElements, "deterministically blocked elements");
            AddFact(labels, facts, AiWorkflowFactKeys.EstimatedWeightBlockedCount, "estimated/fallback blocked");
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
                Status = state.HasEstimatedWeightBlocker ? "Blocked" : state.HasNoGaCandidateBlocker ? "Review" : "Monitor",
                Finding = state.HasEstimatedWeightBlocker
                    ? "Estimated/fallback weight evidence is blocked from GA."
                    : state.HasNoGaCandidateBlocker
                        ? "GA candidate creation is blocked; trusted weight evidence should be verified."
                        : "No estimated-weight blocker is currently exposed by the AI context.",
                Evidence = state.HasEstimatedWeightBlocker
                    ? FirstIssueEvidence(priorityIssues, "Weight", "EstimatedWeightBlockedFromGA or equivalent issue is present.")
                    : FirstIssueEvidence(priorityIssues, "Weight", "No weight-specific blocker found in priority issues."),
                WhyItMatters = "Crane capacity feasibility depends on explicit trusted mass/weight, not display-only estimates.",
                NextAction = state.HasNoGaCandidateBlocker || state.HasEstimatedWeightBlocker ? "Export element list and request explicit mass/weight quantities." : "Keep weight-source evidence in the QA pack.",
                Risk = state.HasEstimatedWeightBlocker ? "High if bypassed." : "Medium if source evidence is not auditable.",
                Confidence = state.HasNoGaCandidateBlocker || state.HasEstimatedWeightBlocker ? "High" : "Medium"
            };

            yield return new AiMissionControlInsight
            {
                Title = "GA Feasibility Intelligence",
                Status = state.Kind == AiWorkflowStateKind.LiftUnitReadinessBlocked ? "Blocked" : context.OptimizationResult.HasResult ? "Result available" : state.CanRunOptimizerNow ? "Ready" : "Prerequisite missing",
                Finding = state.Kind == AiWorkflowStateKind.LiftUnitReadinessBlocked
                    ? "GA cannot start because zero trusted GA-ready lift candidates are available."
                    : context.OptimizationResult.HasResult
                        ? $"Latest coverage is {context.OptimizationResult.CoverageRatio:P2}."
                        : state.CanRunOptimizerNow
                            ? "Core prerequisites are present for a controlled optimizer run."
                            : "GA should wait until the current prerequisite is completed.",
                Evidence = state.Kind == AiWorkflowStateKind.LiftUnitReadinessBlocked
                    ? "NoGaCandidates / GaCannotStartNoCandidates / GaPreRunNoCandidates in screen evidence after IFC elements are loaded."
                    : context.OptimizationResult.HasResult
                        ? $"{context.OptimizationResult.LiftedElementCount:N0}/{context.OptimizationResult.TotalElementCount:N0} lifted over {context.OptimizationResult.PositionCount:N0} setup position(s)."
                        : readiness.Summary,
                WhyItMatters = "This separates data-readiness problems from actual crane reach/capacity optimization problems.",
                NextAction = recommendedAction,
                Risk = state.Kind == AiWorkflowStateKind.LiftUnitReadinessBlocked ? "High if treated as a crane-position issue." : "Advisory; deterministic optimizer remains source of truth.",
                Confidence = "High"
            };

            yield return new AiMissionControlInsight
            {
                Title = "Crane Capacity Intelligence",
                Status = context.HasLoadChartEvidence ? "Evidence present" : "Missing evidence",
                Finding = context.HasLoadChartEvidence ? "Load-chart/capacity evidence is visible to AI." : "No active load-chart evidence was detected.",
                Evidence = context.HasLoadChartEvidence ? "Host context reports load-chart evidence or result capacity/radius values." : "HasLoadChartEvidence is false and no result capacity/radius is attached.",
                WhyItMatters = "Capacity/radius checks need manufacturer load-chart evidence before they can support engineering decisions.",
                NextAction = context.HasLoadChartEvidence ? "Keep the load-chart source in the QA pack." : "CRANES → Import Load Chart / verify capacity table.",
                Risk = context.HasLoadChartEvidence ? "Low after review." : "High if reports imply capacity verification without a chart source.",
                Confidence = "Medium"
            };

            yield return new AiMissionControlInsight
            {
                Title = "Report Readiness Intelligence",
                Status = context.OptimizationResult.HasResult && context.OptimizationResult.CoverageRatio >= 0.95 ? "Candidate" : "Not final",
                Finding = context.OptimizationResult.HasResult
                    ? $"Report narrative can describe {context.OptimizationResult.CoverageRatio:P2} coverage."
                    : state.Kind == AiWorkflowStateKind.LiftUnitReadinessBlocked
                        ? "No final optimization result exists; AI can draft a blocker/resolution note instead of a lift-plan conclusion."
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
            AddFact(parts, ifc.Facts, AiWorkflowFactKeys.TopResolvedCategories, "top categories");
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
                .OrderByDescending(item => IsNoCandidateCode(item.Issue.Code) && context.ElementCount > 0)
                .ThenByDescending(item => SeverityRank(item.Issue.Severity))
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
