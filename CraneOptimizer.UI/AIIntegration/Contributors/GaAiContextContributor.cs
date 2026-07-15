using CraneOptimizer.AI.Host;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Services;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using CraneOptimizer.UI.Shell.ViewModels;

namespace CraneOptimizer.UI.AIIntegration.Contributors
{
    /// <summary>
    /// Exposes GA result, reachability, and per-position failure evidence.
    /// </summary>
    public sealed class GaAiContextContributor : IAiScreenContextContributor
    {
        private readonly IImportReadinessService _readinessService;

        public GaAiContextContributor(IImportReadinessService readinessService)
        {
            _readinessService = readinessService ?? throw new ArgumentNullException(nameof(readinessService));
        }

        public string ScreenName => "GA optimization";

        public AiScreenContextSnapshot BuildSnapshot(MainViewModel shell)
        {
            var result = shell.CranePlacement.Result;
            if (result == null)
            {
                return BuildPreRunSnapshot(shell);
            }

            var elements = shell.Scene.GetElements();
            var readiness = _readinessService.BuildSummary(elements);
            // Narrow host-adapter responsibility: expose GA/readiness source-of-truth
            // counts through AI-owned keys; AI interpretation remains in CraneOptimizer.AI.
            var sceneBimObjectCount = elements.Count;
            var readinessElementCount = readiness.TotalElements;
            var excludedReferenceGeometryCount = Math.Max(0, sceneBimObjectCount - readinessElementCount);
            var stats = result.Statistics ?? new LiftStatistics();
            var failureTotal = stats.OutOfRadius + stats.Overweight + stats.HeightFail + stats.SpecialCase + stats.NotCharted;
            var dominantFailure = GetDominantFailure(stats);
            var selectedPosition = shell.CranePlacement.SelectedPosition;
            var selectedFailures = selectedPosition?.ElementResults
                .Where(e => e.Status != LiftStatus.Liftable)
                .GroupBy(e => e.Status)
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key.ToString(), g => g.Count())
                ?? new Dictionary<string, int>();

            var issues = new List<AiContextIssueSnapshot>();
            if (result.CoverageRatio < 0.95)
            {
                issues.Add(AiContextContributorHelpers.Issue(
                    "GaCoverageBelowReportThreshold",
                    result.CoverageRatio < 0.75 ? "Error" : "Warning",
                    "Optimization",
                    $"Latest coverage is {result.CoverageRatio:P2}; {failureTotal:N0} unresolved lift status records are present.",
                    BuildFailureRecommendation(dominantFailure),
                    "ANALYZE → GA Diagnostics",
                    failureTotal));
            }

            if (stats.NotCharted > 0)
            {
                issues.Add(AiContextContributorHelpers.Issue(
                    "GaNotChartedCapacity",
                    "Warning",
                    "Load chart",
                    $"{stats.NotCharted:N0} element(s) are not charted in the selected capacity lookup.",
                    "Review chart radius/boom ranges, blank cells, and imported manufacturer table coverage before increasing GA generations.",
                    "CRANES → Import Load Chart",
                    stats.NotCharted));
            }

            if (stats.OutOfRadius > 0)
            {
                issues.Add(AiContextContributorHelpers.Issue(
                    "GaOutOfRadius",
                    "Warning",
                    "Reachability",
                    $"{stats.OutOfRadius:N0} element(s) are outside the working radius of the evaluated setup(s).",
                    "Review allowed zones, crane reach, candidate placement coverage, and whether more setup positions are needed.",
                    "ANALYZE → GA Diagnostics",
                    stats.OutOfRadius));
            }

            if (result.ReachabilityDiagnostics != null && result.ReachabilityDiagnostics.ValidCandidatePoints == 0)
            {
                issues.Add(AiContextContributorHelpers.Issue(
                    "GaNoValidCandidatePoints",
                    "Error",
                    "Reachability",
                    "Reachability diagnostics report zero valid candidate points.",
                    "Fix zones/geometry/candidate generation before tuning GA population, generations, or mutation settings.",
                    "ZONES → review allowed/forbidden zones"));
            }

            return new AiScreenContextSnapshot
            {
                ScreenName = ScreenName,
                Stage = result.CoverageRatio >= 0.95 ? "Report review" : "Result review",
                Summary = $"Latest GA run lifted {result.LiftedElementCount:N0}/{result.TotalElementCount:N0} element(s) with {result.Positions.Count:N0} setup position(s); dominant unresolved status: {dominantFailure}.",
                Metrics = new[]
                {
                    AiContextContributorHelpers.Metric("Coverage", result.CoverageRatio.ToString("P2"), $"Lifted {result.LiftedElementCount:N0}/{result.TotalElementCount:N0}"),
                    AiContextContributorHelpers.Metric("Fitness", result.FitnessScore.ToString("0.###"), $"Positions: {result.Positions.Count:N0}"),
                    AiContextContributorHelpers.Metric("Dominant failure", dominantFailure, $"OutRadius {stats.OutOfRadius:N0}; Overweight {stats.Overweight:N0}; Height {stats.HeightFail:N0}; NotCharted {stats.NotCharted:N0}"),
                    AiContextContributorHelpers.Metric("Selected point failures", selectedFailures.Count == 0 ? "None" : string.Join(", ", selectedFailures.Select(kvp => $"{kvp.Key}:{kvp.Value}")), selectedPosition == null ? "No selected GA point" : $"Selected point X={selectedPosition.X:0.###}, Y={selectedPosition.Y:0.###}")
                },
                Issues = issues,
                ActionHints = new[]
                {
                    AiContextContributorHelpers.Hint("Open GA diagnostics", "ANALYZE", "Review deterministic run diagnostics and unresolved element groups."),
                    AiContextContributorHelpers.Hint("Open reports", "REPORTS", "Generate reports only after unresolved assumptions are reviewed."),
                    AiContextContributorHelpers.Hint("Review load chart", "CRANES", "Investigate not-charted or overweight failures at the crane/load-chart source.")
                },
                Facts = BuildFacts(result, selectedPosition, dominantFailure, selectedFailures, sceneBimObjectCount, readinessElementCount, excludedReferenceGeometryCount)
            };
        }


        private AiScreenContextSnapshot BuildPreRunSnapshot(MainViewModel shell)
        {
            var elements = shell.Scene.GetElements();
            var zones = shell.Scene.GetZones();
            var cranes = shell.Scene.GetCranes();
            var readiness = _readinessService.BuildSummary(elements);
            var sceneBimObjectCount = elements.Count;
            var readinessElementCount = readiness.TotalElements;
            var excludedReferenceGeometryCount = Math.Max(0, sceneBimObjectCount - readinessElementCount);
            var rawCandidateLikeElements = readiness.GaCandidateElements + readiness.GaBlockedElements;
            var issues = new List<AiContextIssueSnapshot>
            {
                AiContextContributorHelpers.Issue(
                    "GaResultMissing",
                    "Info",
                    "Optimization",
                    "No optimization run has been completed in this session.",
                    readiness.TotalElements == 0
                        ? "Load/import IFC elements before opening the GA run workflow."
                        : readiness.GaCandidateElements == 0
                            ? "Fix deterministic input readiness before trying another GA run; the optimizer dialog will block when no trusted GA-ready lift units exist."
                            : "Run GA after deterministic input readiness checks pass.",
                    readiness.TotalElements == 0
                        ? "IFC MODEL → Load IFC"
                        : readiness.GaCandidateElements == 0
                            ? "IFC MODEL → Export Element List / trusted weight-source review"
                            : "ANALYZE → Run Optimizer")
            };

            if (readiness.TotalElements > 0 && readiness.GaCandidateElements == 0)
            {
                var cause = readiness.EstimatedWeightBlockedCount > 0
                    ? $"{readiness.EstimatedWeightBlockedCount:N0} element(s) use estimated fallback weight blocked from GA."
                    : readiness.MissingTrustedWeightCount > 0
                        ? $"{readiness.MissingTrustedWeightCount:N0} element(s) lack trusted weight evidence."
                        : "Element roles, target filters, trusted weight, COG, or assembly grouping prevent lift-unit creation.";

                issues.Insert(0, AiContextContributorHelpers.Issue(
                    "GaPreRunNoCandidates",
                    "Error",
                    "GA pre-run",
                    $"The optimizer pre-run state matches the dialog blocker: 0 trusted GA-ready grouped lift unit(s). IFC scene has {sceneBimObjectCount:N0} BIM/model object(s); readiness/planning scope has {readiness.TotalElements:N0} element(s) ({excludedReferenceGeometryCount:N0} IFC reference-geometry object(s) excluded); zones={zones.Count:N0}; cranes={cranes.Count:N0}. {cause}",
                    "Do not bypass or relax optimizer eligibility. Review/export explicit BIM mass or weight quantities and re-check import readiness before running GA.",
                    "IFC MODEL → Export Element List / trusted weight-source review",
                    readiness.GaBlockedElements));
            }

            var stage = readiness.TotalElements == 0
                ? "Waiting for IFC"
                : readiness.GaCandidateElements == 0
                    ? "Pre-run blocked — no lift units"
                    : "Ready to run";

            var summary = readiness.TotalElements == 0
                ? "No GA result is available because no IFC element scope is loaded."
                : readiness.GaCandidateElements == 0
                    ? "No GA result is available because deterministic readiness currently exposes zero trusted GA-ready lift units. If Run Optimizer is clicked, the dialog will block rather than use estimated/untrusted element weights."
                    : $"No completed GA run yet. Current pre-run readiness has {readiness.GaCandidateElements:N0} trusted GA-ready lift unit(s) and {readiness.GaBlockedElements:N0} blocked element(s).";

            return new AiScreenContextSnapshot
            {
                ScreenName = ScreenName,
                Stage = stage,
                Summary = summary,
                Metrics = new[]
                {
                    AiContextContributorHelpers.Metric("Optimization result", "None", "No completed GA run in current session"),
                    AiContextContributorHelpers.Metric("Trusted pre-run lift units", readiness.GaCandidateElements.ToString("N0"), $"Deterministically blocked: {readiness.GaBlockedElements:N0}"),
                    AiContextContributorHelpers.Metric("Scene setup", $"{zones.Count:N0} zone(s), {cranes.Count:N0} crane(s)", $"Scene BIM objects: {sceneBimObjectCount:N0}; readiness/planning elements: {readinessElementCount:N0}"),
                    AiContextContributorHelpers.Metric("Weight gate", readiness.EstimatedWeightBlockedCount.ToString("N0"), $"Estimated fallback blocked; missing trusted: {readiness.MissingTrustedWeightCount:N0}")
                },
                Issues = issues,
                ActionHints = new[]
                {
                    AiContextContributorHelpers.Hint("Open optimizer", "ANALYZE", "Open the GA workflow only after import readiness has at least one trusted GA-ready lift unit."),
                    AiContextContributorHelpers.Hint("Review IFC readiness", "IFC MODEL", "Inspect/import readiness, element weights, assembly marks, COG, and source assumptions."),
                    AiContextContributorHelpers.Hint("Copy BIM exporter checklist", "BIM_EXPORT_CHECKLIST", "Prepare a safe exporter checklist for explicit weight/mass and lift-unit evidence.", "CopyBimExporterChecklist", "Low — copies advisory text only; no optimizer rules are changed."),
                    AiContextContributorHelpers.Hint("Copy optimizer issue summary", "OPTIMIZER_ISSUE_SUMMARY", "Copy the deterministic no-candidate blocker summary for review.", "CopyIssueSummary", "Low — copies text only; no project data is changed."),
                    AiContextContributorHelpers.Hint("Ask AI to explain blocker", "Why is the optimizer blocked, and what should I ask the BIM exporter to fix?", "Stage a focused diagnostic question using live blocker evidence.", "SetQuestion", "Low — fills the prompt box only; the user still sends explicitly.")
                },
                Facts = new Dictionary<string, string>
                {
                    ["HasOptimizationResult"] = "False",
                    ["PreRunStatus"] = readiness.TotalElements > 0 && readiness.GaCandidateElements == 0 ? "BlockedNoGaCandidates" : "NotRun",
                    [AiWorkflowFactKeys.TotalElements] = sceneBimObjectCount.ToString(),
                    [AiWorkflowFactKeys.TotalBimObjects] = sceneBimObjectCount.ToString(),
                    [AiWorkflowFactKeys.SceneBimObjectCount] = sceneBimObjectCount.ToString(),
                    [AiWorkflowFactKeys.ImportedSceneBimObjectCount] = sceneBimObjectCount.ToString(),
                    [AiWorkflowFactKeys.ReadinessElementCount] = readinessElementCount.ToString(),
                    [AiWorkflowFactKeys.PlanningElementCount] = readinessElementCount.ToString(),
                    [AiWorkflowFactKeys.ReferenceGeometryExcludedCount] = excludedReferenceGeometryCount.ToString(),
                    [AiWorkflowFactKeys.ElementCountScopeNote] = excludedReferenceGeometryCount > 0
                        ? $"Scene BIM/model objects include {excludedReferenceGeometryCount:N0} IFC reference-geometry object(s) excluded from readiness/GA planning counts."
                        : "Scene BIM/model objects and readiness planning elements currently have the same count.",
                    [AiWorkflowFactKeys.GaCandidateElements] = readiness.GaCandidateElements.ToString(),
                    [AiWorkflowFactKeys.TrustedGaLiftUnits] = readiness.GaCandidateElements.ToString(),
                    [AiWorkflowFactKeys.TrustedGroupedLiftUnits] = readiness.GaCandidateElements.ToString(),
                    [AiWorkflowFactKeys.RawCandidateLikeElements] = rawCandidateLikeElements.ToString(),
                    [AiWorkflowFactKeys.GaBlockedElements] = readiness.GaBlockedElements.ToString(),
                    [AiWorkflowFactKeys.EstimatedWeightBlockedCount] = readiness.EstimatedWeightBlockedCount.ToString(),
                    [AiWorkflowFactKeys.MissingTrustedWeightCount] = readiness.MissingTrustedWeightCount.ToString(),
                    ["ZoneCount"] = zones.Count.ToString(),
                    ["CraneCount"] = cranes.Count.ToString(),
                    ["ReadinessStatus"] = readiness.OverallStatus.ToString()
                }
            };
        }

        private static Dictionary<string, string> BuildFacts(
            CranePlacementResult result,
            CranePositionResult? selectedPosition,
            string dominantFailure,
            Dictionary<string, int> selectedFailures,
            int sceneBimObjectCount,
            int readinessElementCount,
            int excludedReferenceGeometryCount)
        {
            var stats = result.Statistics ?? new LiftStatistics();
            var facts = new Dictionary<string, string>
            {
                ["CoverageRatio"] = result.CoverageRatio.ToString("0.####"),
                [AiWorkflowFactKeys.TotalElements] = sceneBimObjectCount.ToString(),
                [AiWorkflowFactKeys.TotalBimObjects] = sceneBimObjectCount.ToString(),
                [AiWorkflowFactKeys.SceneBimObjectCount] = sceneBimObjectCount.ToString(),
                [AiWorkflowFactKeys.ImportedSceneBimObjectCount] = sceneBimObjectCount.ToString(),
                [AiWorkflowFactKeys.ReadinessElementCount] = readinessElementCount.ToString(),
                [AiWorkflowFactKeys.PlanningElementCount] = readinessElementCount.ToString(),
                [AiWorkflowFactKeys.ReferenceGeometryExcludedCount] = excludedReferenceGeometryCount.ToString(),
                [AiWorkflowFactKeys.ElementCountScopeNote] = excludedReferenceGeometryCount > 0
                    ? $"Scene BIM/model objects include {excludedReferenceGeometryCount:N0} IFC reference-geometry object(s) excluded from readiness/GA planning counts."
                    : "Scene BIM/model objects and readiness planning elements currently have the same count.",
                ["LiftedElementCount"] = result.LiftedElementCount.ToString(),
                ["TotalElementCount"] = result.TotalElementCount.ToString(),
                ["PositionCount"] = result.Positions.Count.ToString(),
                ["DominantFailure"] = dominantFailure,
                ["OutOfRadius"] = stats.OutOfRadius.ToString(),
                ["Overweight"] = stats.Overweight.ToString(),
                ["HeightFail"] = stats.HeightFail.ToString(),
                ["NotCharted"] = stats.NotCharted.ToString(),
                ["DiagnosticsPath"] = result.DebugReportPath ?? string.Empty,
                ["CraneName"] = result.CraneName ?? string.Empty,
                ["CraneSpecification"] = result.CraneModelOrSpecification ?? string.Empty,
                ["MaxCapacity"] = result.MaxCapacity.ToString("0.###"),
                ["MaxRadius"] = result.MaxRadius.ToString("0.###")
            };

            if (result.ReachabilityDiagnostics != null)
            {
                facts["ReachabilityValidCandidatePoints"] = result.ReachabilityDiagnostics.ValidCandidatePoints.ToString();
                facts["ReachabilityRejectedCandidatePoints"] = result.ReachabilityDiagnostics.RejectedCandidatePoints.ToString();
                facts["ReachabilityZeroCoveragePoints"] = result.ReachabilityDiagnostics.ZeroCoveragePoints.ToString();
                facts["ReachabilityHotSpotCount"] = result.ReachabilityDiagnostics.HotSpotCount.ToString();
            }

            if (selectedPosition != null)
            {
                facts["SelectedPosition"] = $"X={selectedPosition.X:0.###};Y={selectedPosition.Y:0.###};Z={selectedPosition.Z:0.###}";
                facts["SelectedPositionFailures"] = selectedFailures.Count == 0
                    ? "None"
                    : string.Join(";", selectedFailures.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            }

            return facts;
        }

        private static string GetDominantFailure(LiftStatistics stats)
        {
            var failures = new[]
            {
                (Name: "OutOfRadius", Count: stats.OutOfRadius),
                (Name: "Overweight", Count: stats.Overweight),
                (Name: "HeightFail", Count: stats.HeightFail),
                (Name: "SpecialCase", Count: stats.SpecialCase),
                (Name: "NotCharted", Count: stats.NotCharted)
            };

            var top = failures.OrderByDescending(failure => failure.Count).First();
            return top.Count <= 0 ? "None" : top.Name;
        }

        private static string BuildFailureRecommendation(string dominantFailure) => dominantFailure switch
        {
            "OutOfRadius" => "This looks primarily like reachability or zone/candidate coverage. Review setup positions, crane radius, and placement zones before increasing GA runtime.",
            "Overweight" => "This looks primarily like a capacity or lift-weight issue. Review load chart capacity, derating/safety factors, and explicit element weights.",
            "HeightFail" => "This looks primarily like a boom/height envelope issue. Review boom length/angle, lift point elevation, and selected crane geometry.",
            "NotCharted" => "This looks primarily like load-chart coverage. Review imported chart cells/ranges before changing GA settings.",
            _ => "Review unresolved GA diagnostics before changing optimizer settings or issuing reports."
        };
    }
}
