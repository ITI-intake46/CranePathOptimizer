using CraneOptimizer.AI.Host;
using CraneOptimizer.Application.Services;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.UI.Shell.ViewModels;

namespace CraneOptimizer.UI.AIIntegration.Contributors
{
    /// <summary>
    /// Exposes IFC/import-readiness evidence to the AI companion.
    /// </summary>
    public sealed class IfcAiContextContributor : IAiScreenContextContributor
    {
        private readonly IImportReadinessService _readinessService;

        public IfcAiContextContributor(IImportReadinessService readinessService)
        {
            _readinessService = readinessService ?? throw new ArgumentNullException(nameof(readinessService));
        }

        public string ScreenName => "IFC import readiness";

        public AiScreenContextSnapshot BuildSnapshot(MainViewModel shell)
        {
            var elements = shell.Scene.GetElements();
            var zones = shell.Scene.GetZones();
            var cranes = shell.Scene.GetCranes();
            var summary = _readinessService.BuildSummary(elements);
            // Narrow host-adapter responsibility: expose the two authoritative count
            // scopes. The AI project decides how to explain them and when to block.
            var sceneBimObjectCount = elements.Count;
            var readinessElementCount = summary.TotalElements;
            var excludedReferenceGeometryCount = Math.Max(0, sceneBimObjectCount - readinessElementCount);
            var issues = new List<AiContextIssueSnapshot>();

            if (summary.TotalElements == 0)
            {
                issues.Add(AiContextContributorHelpers.Issue(
                    "IfcModelMissing",
                    "Warning",
                    "IFC",
                    "No model elements are currently loaded in the scene.",
                    "Load an IFC model before validating lift readiness or running GA.",
                    "IFC MODEL → Load IFC"));
            }
            else if (summary.GaCandidateElements == 0)
            {
                issues.Add(BuildNoCandidateIssue(summary, zones.Count, cranes.Count, sceneBimObjectCount, excludedReferenceGeometryCount));
            }

            issues.AddRange(summary.Messages
                .Where(message => !message.Code.Equals("IfcImportHasWarnings", StringComparison.OrdinalIgnoreCase))
                .Where(message => summary.TotalElements > 0 || !message.Code.Equals("NoGaCandidates", StringComparison.OrdinalIgnoreCase))
                .Where(message => !message.Code.Equals("NoGaCandidates", StringComparison.OrdinalIgnoreCase)
                                  || !issues.Any(issue => issue.Code.Equals("GaCannotStartNoCandidates", StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(message => IsNoCandidateOrWeightBlocker(message.Code))
                .ThenByDescending(message => SeverityRank(message.Severity))
                .ThenByDescending(message => message.AffectedCount)
                .Take(8)
                .Select(message => AiContextContributorHelpers.FromValidationMessage(
                    message,
                    BuildReadinessNextClick(summary))));

            var status = summary.OverallStatus == ValidationStatus.Valid
                ? "Ready"
                : summary.OverallStatus.ToString();

            var stage = summary.TotalElements == 0
                ? "No model"
                : summary.GaCandidateElements == 0
                    ? "GA blocked — no trusted GA-ready lift units"
                    : status;

            var metadataFacts = BuildMetadataFacts(shell, elements);

            var elementTableTotal = shell.ElementManagement.Elements.Count;
            var elementTableVisible = shell.ElementManagement.FilteredElements.Count;
            var rawCandidateLikeElements = summary.GaCandidateElements + summary.GaBlockedElements;

            return new AiScreenContextSnapshot
            {
                ScreenName = ScreenName,
                Stage = stage,
                Summary = BuildSummaryText(summary, sceneBimObjectCount, readinessElementCount, excludedReferenceGeometryCount, zones.Count, cranes.Count, elementTableVisible, elementTableTotal),
                Metrics = new[]
                {
                    AiContextContributorHelpers.Metric("Planning/readiness elements", readinessElementCount.ToString("N0"), $"Scene BIM objects: {sceneBimObjectCount:N0}; reference geometry excluded: {excludedReferenceGeometryCount:N0}; renderable: {summary.RenderableElements:N0}"),
                    AiContextContributorHelpers.Metric("Trusted GA-ready lift units", summary.GaCandidateElements.ToString("N0"), $"Deterministically blocked: {summary.GaBlockedElements:N0}"),
                    AiContextContributorHelpers.Metric("Trusted weight gaps", summary.MissingTrustedWeightCount.ToString("N0"), $"Estimated fallback blocked: {summary.EstimatedWeightBlockedCount:N0}"),
                    AiContextContributorHelpers.Metric("Identity review", summary.NeedsReviewCount.ToString("N0"), $"Missing marks: {summary.MissingAssemblyMarkCount:N0}; low confidence: {summary.LowConfidenceMappingCount:N0}"),
                    AiContextContributorHelpers.Metric("Geometry/material review", summary.MissingCogCount.ToString("N0"), $"COG issues; volume gaps: {summary.MissingVolumeCount:N0}; material gaps: {summary.MissingMaterialCount:N0}")
                },
                Issues = issues,
                ActionHints = BuildActionHints(summary),
                Facts = metadataFacts.Concat(new Dictionary<string, string>
                {
                    ["OverallStatus"] = summary.OverallStatus.ToString(),
                    ["OptimizerPrecheckStatus"] = summary.TotalElements > 0 && summary.GaCandidateElements == 0 ? "BlockedNoGaCandidates" : "CandidateCheckPassedOrNoModel",
                    ["ModelDisplayName"] = string.IsNullOrWhiteSpace(shell.BuildingName) ? shell.CurrentProjectName : shell.BuildingName,
                    [AiWorkflowFactKeys.TotalElements] = sceneBimObjectCount.ToString(),
                    [AiWorkflowFactKeys.TotalBimObjects] = sceneBimObjectCount.ToString(),
                    [AiWorkflowFactKeys.SceneBimObjectCount] = sceneBimObjectCount.ToString(),
                    [AiWorkflowFactKeys.ImportedSceneBimObjectCount] = sceneBimObjectCount.ToString(),
                    [AiWorkflowFactKeys.ReadinessElementCount] = readinessElementCount.ToString(),
                    [AiWorkflowFactKeys.PlanningElementCount] = readinessElementCount.ToString(),
                    [AiWorkflowFactKeys.ReferenceGeometryExcludedCount] = excludedReferenceGeometryCount.ToString(),
                    [AiWorkflowFactKeys.ReadinessExcludedReferenceGeometryCount] = excludedReferenceGeometryCount.ToString(),
                    [AiWorkflowFactKeys.ElementCountScopeNote] = excludedReferenceGeometryCount > 0
                        ? $"Scene BIM/model objects include {excludedReferenceGeometryCount:N0} IFC reference-geometry object(s) excluded from readiness/GA planning counts."
                        : "Scene BIM/model objects and readiness planning elements currently have the same count.",
                    [AiWorkflowFactKeys.ElementTableItems] = elementTableTotal.ToString(),
                    [AiWorkflowFactKeys.VisibleElementTableItems] = elementTableVisible.ToString(),
                    [AiWorkflowFactKeys.GaCandidateElements] = summary.GaCandidateElements.ToString(),
                    [AiWorkflowFactKeys.TrustedGaLiftUnits] = summary.GaCandidateElements.ToString(),
                    [AiWorkflowFactKeys.TrustedGroupedLiftUnits] = summary.GaCandidateElements.ToString(),
                    [AiWorkflowFactKeys.RawCandidateLikeElements] = rawCandidateLikeElements.ToString(),
                    [AiWorkflowFactKeys.GaBlockedElements] = summary.GaBlockedElements.ToString(),
                    [AiWorkflowFactKeys.EstimatedWeightBlockedCount] = summary.EstimatedWeightBlockedCount.ToString(),
                    [AiWorkflowFactKeys.MissingTrustedWeightCount] = summary.MissingTrustedWeightCount.ToString(),
                    ["MissingAssemblyMarkCount"] = summary.MissingAssemblyMarkCount.ToString(),
                    ["LowConfidenceMappingCount"] = summary.LowConfidenceMappingCount.ToString(),
                    ["NeedsReviewCount"] = summary.NeedsReviewCount.ToString(),
                    ["MissingCogCount"] = summary.MissingCogCount.ToString(),
                    ["MissingVolumeCount"] = summary.MissingVolumeCount.ToString(),
                    ["MissingMaterialCount"] = summary.MissingMaterialCount.ToString(),
                    ["ParentChildDoubleCountRiskCount"] = summary.ParentChildDoubleCountRiskCount.ToString(),
                    ["ZoneCount"] = zones.Count.ToString(),
                    ["CraneCount"] = cranes.Count.ToString()
                }).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
            };
        }


        private static IReadOnlyDictionary<string, string> BuildMetadataFacts(MainViewModel shell, IReadOnlyList<CraneOptimizer.Domain.Schemas.PrecastElement> elements)
        {
            var facts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(shell.BuildingName)
                && !shell.BuildingName.Equals("No Building Loaded", StringComparison.OrdinalIgnoreCase))
            {
                facts["BuildingName"] = shell.BuildingName.Trim();
            }

            if (elements.Count > 0)
            {
                var rawGroups = elements
                    .Select(element => string.IsNullOrWhiteSpace(element.RawIfcType) ? element.ElementType : element.RawIfcType)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!.Trim())
                    .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(group => group.Count())
                    .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (rawGroups.Count > 0)
                {
                    facts[AiWorkflowFactKeys.RawIfcTypeCounts] = BuildCountFact(rawGroups);
                    facts[AiWorkflowFactKeys.TopRawIfcTypes] = BuildTopCountFact(rawGroups, 8);
                }

                var categoryGroups = elements
                    .Select(element => string.IsNullOrWhiteSpace(element.ResolvedCategory) ? element.MainPartCategory : element.ResolvedCategory)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!.Trim())
                    .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(group => group.Count())
                    .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (categoryGroups.Count > 0)
                {
                    facts[AiWorkflowFactKeys.ResolvedCategoryCounts] = BuildCountFact(categoryGroups);
                    facts[AiWorkflowFactKeys.TopResolvedCategories] = BuildTopCountFact(categoryGroups, 10);
                }

                var elementTypeGroups = elements
                    .Select(element => element.ElementType)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!.Trim())
                    .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(group => group.Count())
                    .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (elementTypeGroups.Count > 0)
                    facts[AiWorkflowFactKeys.ElementTypeCounts] = BuildCountFact(elementTypeGroups);

                facts[AiWorkflowFactKeys.WeightOver1000KgCount] = CountWeightOverKg(elements, 1000d).ToString();
                facts[AiWorkflowFactKeys.WeightOver3000KgCount] = CountWeightOverKg(elements, 3000d).ToString();
                facts[AiWorkflowFactKeys.WeightOver5000KgCount] = CountWeightOverKg(elements, 5000d).ToString();
            }

            facts["BuildingUsage"] = "Not exposed in current AI context; inspect IFC property sets/classifications or request it from the BIM author.";
            facts["BuildingType"] = "Not exposed in current AI context; do not infer it from geometry alone.";
            return facts;
        }

        private static string BuildCountFact(IEnumerable<IGrouping<string, string>> groups) =>
            string.Join("; ", groups.Select(group => $"{group.Key}={group.Count()}"));

        private static string BuildTopCountFact(IEnumerable<IGrouping<string, string>> groups, int take) =>
            string.Join(", ", groups.Take(take).Select(group => $"{group.Key} ({group.Count():N0})"));

        private static int CountWeightOverKg(IReadOnlyList<CraneOptimizer.Domain.Schemas.PrecastElement> elements, double thresholdKg) =>
            elements.Count(element => element.WeightTons.HasValue && element.WeightTons.Value * 1000d > thresholdKg);

        private static AiContextIssueSnapshot BuildNoCandidateIssue(ImportReadinessSummary summary, int zoneCount, int craneCount, int sceneBimObjectCount, int excludedReferenceGeometryCount)
        {
            var cause = summary.EstimatedWeightBlockedCount > 0
                ? $"Likely cause: {summary.EstimatedWeightBlockedCount:N0} element(s) use estimated fallback weight, which is blocked from GA."
                : summary.MissingTrustedWeightCount > 0
                    ? $"Likely cause: {summary.MissingTrustedWeightCount:N0} element(s) lack trusted explicit weight evidence."
                    : "Likely cause: element roles, target filtering, weight trust, COG, or parent/child readiness prevent GA lift-unit creation.";

            return AiContextContributorHelpers.Issue(
                "GaCannotStartNoCandidates",
                "Error",
                "GA pre-run",
                $"IFC scene has {sceneBimObjectCount:N0} BIM/model object(s); readiness/planning scope has {summary.TotalElements:N0} element(s) ({excludedReferenceGeometryCount:N0} IFC reference-geometry object(s) excluded). Scene has {zoneCount:N0} zone(s) and {craneCount:N0} crane(s), but deterministic import readiness reports 0 trusted GA-ready grouped lift unit candidate(s). {cause}",
                "Do not bypass the optimizer gate. Review/export explicit BIM mass or weight quantities, verify assembly lift marks and COG, then re-import or re-check readiness.",
                "IFC MODEL → Export Element List / trusted weight-source review",
                summary.GaBlockedElements,
                "High");
        }

        private static string BuildSummaryText(ImportReadinessSummary summary, int sceneBimObjectCount, int readinessElementCount, int excludedReferenceGeometryCount, int zoneCount, int craneCount, int elementTableVisible, int elementTableTotal)
        {
            if (sceneBimObjectCount == 0)
                return "No IFC element evidence is available yet.";

            var countExplanation = BuildElementCountExplanation(sceneBimObjectCount, readinessElementCount, excludedReferenceGeometryCount, elementTableVisible, elementTableTotal);
            if (summary.GaCandidateElements == 0)
            {
                return $"{countExplanation} Scene has {zoneCount:N0} zone(s) and {craneCount:N0} crane(s), but no trusted GA-ready lift units are available. Displayed weights may exist while still being untrusted for optimization.";
            }

            return $"{countExplanation} {summary.GaCandidateElements:N0} trusted GA-ready lift unit(s); {summary.GaBlockedElements:N0} blocked by deterministic readiness checks.";
        }

        private static string BuildElementCountExplanation(int sceneBimObjectCount, int readinessElementCount, int excludedReferenceGeometryCount, int visibleTableItems, int totalTableItems)
        {
            var parts = new List<string> { $"{sceneBimObjectCount:N0} scene BIM/model object(s)" };

            if (readinessElementCount != sceneBimObjectCount || excludedReferenceGeometryCount > 0)
                parts.Add($"{readinessElementCount:N0} readiness/planning element(s)");

            if (excludedReferenceGeometryCount > 0)
                parts.Add($"{excludedReferenceGeometryCount:N0} IFC reference-geometry object(s) excluded from readiness");

            if (totalTableItems > 0 && (totalTableItems != sceneBimObjectCount || visibleTableItems != totalTableItems))
                parts.Add($"{totalTableItems:N0} element-table item(s); {visibleTableItems:N0} currently visible after filters");

            return string.Join("; ", parts) + ".";
        }

        private static IReadOnlyList<AiContextActionHintSnapshot> BuildActionHints(ImportReadinessSummary summary)
        {
            var hints = new List<AiContextActionHintSnapshot>
            {
                AiContextContributorHelpers.Hint("Open IFC readiness", "IFC MODEL", "Review import status, source model assumptions, and validation messages."),
                AiContextContributorHelpers.Hint("Copy readiness evidence", "READINESS_EVIDENCE", "Copy the deterministic AI context and readiness evidence for audit/review.", "CopyContext", "Low — copies text only; no project data is changed."),
                AiContextContributorHelpers.Hint("Copy BIM exporter checklist", "BIM_EXPORT_CHECKLIST", "Prepare a checklist requesting explicit weight/mass, assembly marks, COG, and traceability from the BIM/IFC exporter.", "CopyBimExporterChecklist", "Low — copies advisory text only; no optimizer rules are changed."),
                AiContextContributorHelpers.Hint("Copy optimizer issue summary", "OPTIMIZER_ISSUE_SUMMARY", "Copy a concise deterministic explanation of why optimization cannot start or why inputs need review.", "CopyIssueSummary", "Low — copies text only; no project data is changed.")
            };

            if (summary.TotalElements > 0 && summary.GaCandidateElements == 0)
            {
                hints.Add(AiContextContributorHelpers.Hint(
                    "Ask AI to explain blocker",
                    "Why is the optimizer blocked, and what should I ask the BIM exporter to fix?",
                    "Stage a precise question using the live deterministic blocker evidence.",
                    "SetQuestion",
                    "Low — fills the prompt box only; the user still sends the question explicitly."));
            }

            return hints;
        }

        private static string BuildReadinessNextClick(ImportReadinessSummary summary)
        {
            if (summary.TotalElements == 0)
                return "IFC MODEL → Load IFC";

            if (summary.GaCandidateElements == 0)
                return "IFC MODEL → Export Element List / review trusted weight/source";

            return "IFC MODEL → review import readiness";
        }

        private static bool IsNoCandidateOrWeightBlocker(string? code) =>
            code != null &&
            (code.Equals("NoGaCandidates", StringComparison.OrdinalIgnoreCase)
             || code.Equals("EstimatedWeightBlockedFromGA", StringComparison.OrdinalIgnoreCase)
             || code.Equals("MissingTrustedWeight", StringComparison.OrdinalIgnoreCase));

        private static int SeverityRank(string? severity) => severity?.ToUpperInvariant() switch
        {
            "ERROR" => 3,
            "WARNING" => 2,
            "INFO" => 1,
            _ => 0
        };
    }
}
