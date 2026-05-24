using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.Utilities;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    public class GACsvDiagnosticsService : IGACsvDiagnosticsService
    {
        private readonly ILiftingCalculationEngine _liftingEngine;
        private readonly ICraneConfigurationEngine _configEngine;
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public GACsvDiagnosticsService(
            ILiftingCalculationEngine liftingEngine,
            ICraneConfigurationEngine configEngine)
        {
            _liftingEngine = liftingEngine;
            _configEngine = configEngine;
        }

        public string GenerateDiagnostics(PlacementInput input, PlacementResult finalResult)
        {
            // 1. Resolve output folder
            string? reportsDir = input.DiagnosticsReportsDirectory;
            if (string.IsNullOrWhiteSpace(reportsDir))
            {
                string? projectFolder = input.ProjectFolderPath;
                if (!string.IsNullOrWhiteSpace(projectFolder))
                {
                    reportsDir = Path.Combine(projectFolder, "Reports", "GA");
                }
                else
                {
                    reportsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CraneOptimizer", "Reports", "GA");
                }
            }

            Directory.CreateDirectory(reportsDir);

            string timestampStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string timeFormatted = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            string detailedFileName = $"ga_diagnostics_{timestampStr}.csv";
            string detailedPath = Path.Combine(reportsDir, detailedFileName);

            string breakdownFileName = $"ga_failure_breakdown_{timestampStr}.csv";
            string breakdownPath = Path.Combine(reportsDir, breakdownFileName);

            string historyPath = Path.Combine(reportsDir, "ga_run_history.csv");

            // 2. Perform deterministic post-run diagnostics pass for every selected element
            var allElements = input.Elements ?? new List<PrecastElement>();
            var detailedRows = new List<ElementDiagnosticRow>();

            foreach (var element in allElements)
            {
                if (string.IsNullOrWhiteSpace(element.ElementID)) continue;

                bool isLifted = false;
                int assignedIndex = -1;
                ElementEvaluationResult? bestEval = null;

                // Check if the element was successfully lifted at any of the final positions
                for (int i = 0; i < finalResult.Positions.Count; i++)
                {
                    var pos = finalResult.Positions[i];
                    var elemResult = pos.ElementResults.FirstOrDefault(r => string.Equals(r.ElementId, element.ElementID, StringComparison.OrdinalIgnoreCase));
                    if (elemResult != null && elemResult.Status == LiftStatus.Liftable)
                    {
                        isLifted = true;
                        assignedIndex = i;
                        bestEval = new ElementEvaluationResult
                        {
                            ElementId = element.ElementID,
                            AssignedCraneIndex = i,
                            Status = LiftStatus.Liftable,
                            Radius = elemResult.Radius,
                            BoomLength = elemResult.BoomLength,
                            AvailableCapacity = elemResult.AvailableCapacity,
                            RequiredCapacity = elemResult.RequiredCapacity
                        };
                        break;
                    }
                }

                // If not successfully lifted, evaluate deterministic diagnostic checks against all setup positions to find least-bad setup
                if (!isLifted && finalResult.Positions.Count > 0)
                {
                    for (int i = 0; i < finalResult.Positions.Count; i++)
                    {
                        var pos = finalResult.Positions[i];
                        var cranePosPoint = new DomainPoint3D(pos.X, pos.Y, pos.Z);

                        double radius = _liftingEngine.CalculateRadius(cranePosPoint, element);
                        double height = _liftingEngine.CalculateLiftHeight(element, cranePosPoint, input.Settings);
                        double boomLength = _configEngine.CalculateRequiredBoomLength(radius, height);
                        double requiredCapacity = _liftingEngine.CalculateRequiredWeight(element, input.Settings);
                        double availableCapacity = _configEngine.CraneCapacity(input.CraneSpec!, radius, boomLength, input.Settings);
                        LiftStatus status = _liftingEngine.DetermineLiftStatus(radius, boomLength, requiredCapacity, input.CraneSpec!, input.Settings);

                        var candidate = new ElementEvaluationResult
                        {
                            ElementId = element.ElementID,
                            AssignedCraneIndex = i,
                            Status = status,
                            Radius = radius,
                            BoomLength = boomLength,
                            AvailableCapacity = availableCapacity,
                            RequiredCapacity = requiredCapacity
                        };

                        if (bestEval == null || IsBetterThan(candidate, bestEval))
                        {
                            bestEval = candidate;
                            assignedIndex = i;
                        }
                    }
                }

                // Apply classification
                string failureReason = "Liftable";
                string failureCategory = "Liftable";
                string suggestedAction = "";

                if (!isLifted)
                {
                    if (finalResult.Positions.Count == 0)
                    {
                        failureReason = "NoValidSetupPosition";
                        failureCategory = "NoValidSetupPosition";
                        suggestedAction = "suggest adjusting green/red zones.";
                    }
                    else if (!element.ElementWT.HasValue || element.ElementWT.Value <= 0)
                    {
                        failureReason = "MissingWeightOrInvalidData";
                        failureCategory = "MissingWeightOrInvalidData";
                        suggestedAction = "suggest validating IFC quantities or fallback density rules.";
                    }
                    else if (bestEval != null)
                    {
                        if (bestEval.BoomLength > input.CraneSpec!.MaxBoomLength)
                        {
                            failureReason = "BoomLengthExceeded";
                            failureCategory = "BoomLengthExceeded";
                            suggestedAction = "suggest longer boom or different crane configuration.";
                        }
                        else
                        {
                            switch (bestEval.Status)
                            {
                                case LiftStatus.OutOfRadius:
                                    failureReason = "OutOfRadius";
                                    failureCategory = "OutOfRadius";
                                    suggestedAction = "suggest increasing allowed green area, adding setup locations, or selecting a longer-radius crane.";
                                    break;
                                case LiftStatus.Overweight:
                                    failureReason = "Overweight";
                                    failureCategory = "Overweight";
                                    suggestedAction = "suggest larger crane capacity or reviewing element weight.";
                                    break;
                                case LiftStatus.HeightFail:
                                    failureReason = "HeightFail";
                                    failureCategory = "HeightFail";
                                    suggestedAction = "suggest longer boom or different crane configuration.";
                                    break;
                                case LiftStatus.SpecialCase:
                                    var capacityDiag = _configEngine.EvaluateCraneCapacityDiagnostic(
                                        input.CraneSpec!,
                                        bestEval.Radius,
                                        bestEval.BoomLength,
                                        input.Settings);

                                    switch (capacityDiag.FailureReason)
                                    {
                                        case CapacityFailureReason.RadiusBeyondLoadChart:
                                            failureReason = "RadiusBeyondLoadChart";
                                            failureCategory = "RadiusBeyondLoadChart";
                                            suggestedAction = "suggest select a crane with larger working radius or move/expand green area closer to the elements.";
                                            break;
                                        case CapacityFailureReason.BoomBeyondLoadChart:
                                            failureReason = "BoomBeyondLoadChart";
                                            failureCategory = "BoomBeyondLoadChart";
                                            suggestedAction = "suggest select a longer boom configuration or a different crane model.";
                                            break;
                                        case CapacityFailureReason.NoDiscreteLoadChartEntry:
                                            failureReason = "NoDiscreteLoadChartEntry";
                                            failureCategory = "NoDiscreteLoadChartEntry";
                                            suggestedAction = "suggest review crane load chart completeness for this radius/boom combination.";
                                            break;
                                        case CapacityFailureReason.ZeroCapacityChartEntry:
                                            failureReason = "ZeroCapacityChartEntry";
                                            failureCategory = "ZeroCapacityChartEntry";
                                            suggestedAction = "suggest review the crane chart value or select a larger crane.";
                                            break;
                                        case CapacityFailureReason.MissingLoadChartData:
                                            failureReason = "MissingLoadChartData";
                                            failureCategory = "MissingLoadChartData";
                                            suggestedAction = "suggest validate crane load chart configuration.";
                                            break;
                                        default:
                                            failureReason = "SpecialCase";
                                            failureCategory = "SpecialCase";
                                            suggestedAction = "suggest reviewing element geometry and extracted data.";
                                            break;
                                    }
                                    break;
                                default:
                                    failureReason = "Unknown";
                                    failureCategory = "Unknown";
                                    suggestedAction = "suggest reviewing element geometry and extracted data.";
                                    break;
                            }
                        }
                    }
                    else
                    {
                        failureReason = "Unknown";
                        failureCategory = "Unknown";
                        suggestedAction = "suggest reviewing element geometry and extracted data.";
                    }
                }

                detailedRows.Add(new ElementDiagnosticRow
                {
                    Timestamp = timeFormatted,
                    ProjectName = input.ProjectName ?? "N/A",
                    IfcFileName = input.IfcFileName ?? "N/A",
                    CraneNameOrId = input.CraneSpec?.Crane?.Name ?? (input.CraneSpec != null ? $"Spec ID: {input.CraneSpec.Id}" : "N/A"),
                    ElementId = element.ElementID,
                    ElementName = element.ElementMark ?? "N/A",
                    ElementType = element.ElementType ?? "N/A",
                    StoreyOrElevation = element.ElementCOG.Z.ToString("0.###", Invariant),
                    Weight = element.ElementWT ?? 0.0,
                    CogX = element.ElementCOG.X,
                    CogY = element.ElementCOG.Y,
                    CogZ = element.ElementCOG.Z,
                    AssignedSetupIndex = assignedIndex >= 0 ? (assignedIndex + 1) : -1,
                    AssignedCraneX = assignedIndex >= 0 ? finalResult.Positions[assignedIndex].X : 0.0,
                    AssignedCraneY = assignedIndex >= 0 ? finalResult.Positions[assignedIndex].Y : 0.0,
                    AssignedCraneZ = assignedIndex >= 0 ? finalResult.Positions[assignedIndex].Z : 0.0,
                    BestRadius = bestEval?.Radius ?? 0.0,
                    RequiredBoomLength = bestEval?.BoomLength ?? 0.0,
                    RequiredCapacity = bestEval?.RequiredCapacity ?? 0.0,
                    AvailableCapacity = bestEval?.AvailableCapacity ?? 0.0,
                    LiftStatus = bestEval != null ? bestEval.Status.ToString() : LiftStatus.SpecialCase.ToString(),
                    IsLifted = isLifted,
                    FailureReason = failureReason,
                    FailureCategory = failureCategory,
                    SuggestedAction = suggestedAction
                });
            }

            // 3. Write detailed diagnostics CSV file (UTF-8, properly escaped)
            using (var writer = new StreamWriter(detailedPath, false, new UTF8Encoding(true)))
            {
                writer.WriteLine(string.Join(",", new[]
                {
                    "Timestamp", "ProjectName", "IfcFileName", "CraneNameOrId", "ElementId", "ElementName", "ElementType",
                    "StoreyOrElevation", "Weight", "COG_X", "COG_Y", "COG_Z", "AssignedSetupIndex", "AssignedCraneX",
                    "AssignedCraneY", "AssignedCraneZ", "BestRadius", "RequiredBoomLength", "RequiredCapacity",
                    "AvailableCapacity", "LiftStatus", "IsLifted", "FailureReason", "FailureCategory", "SuggestedAction"
                }));

                foreach (var row in detailedRows)
                {
                    writer.WriteLine(string.Join(",", new[]
                    {
                        row.Timestamp,
                        Escape(row.ProjectName),
                        Escape(row.IfcFileName),
                        Escape(row.CraneNameOrId),
                        Escape(row.ElementId),
                        Escape(row.ElementName),
                        Escape(row.ElementType),
                        row.StoreyOrElevation,
                        row.Weight.ToString("0.###", Invariant),
                        row.CogX.ToString("0.###", Invariant),
                        row.CogY.ToString("0.###", Invariant),
                        row.CogZ.ToString("0.###", Invariant),
                        row.AssignedSetupIndex.ToString(),
                        row.AssignedSetupIndex >= 0 ? row.AssignedCraneX.ToString("0.###", Invariant) : "N/A",
                        row.AssignedSetupIndex >= 0 ? row.AssignedCraneY.ToString("0.###", Invariant) : "N/A",
                        row.AssignedSetupIndex >= 0 ? row.AssignedCraneZ.ToString("0.###", Invariant) : "N/A",
                        row.BestRadius.ToString("0.###", Invariant),
                        row.RequiredBoomLength.ToString("0.###", Invariant),
                        row.RequiredCapacity.ToString("0.###", Invariant),
                        row.AvailableCapacity.ToString("0.###", Invariant),
                        row.LiftStatus,
                        row.IsLifted ? "true" : "false",
                        Escape(row.FailureReason),
                        Escape(row.FailureCategory),
                        Escape(row.SuggestedAction)
                    }));
                }
            }

            // 4. Calculate failure breakdown percentages
            var categories = new[]
            {
                "OutOfRadius", "Overweight", "HeightFail", "BoomLengthExceeded", "NoValidSetupPosition",
                "ForbiddenZoneBlocked", "NotAssigned", "MissingWeightOrInvalidData", "SpecialCase", "Unknown",
                "RadiusBeyondLoadChart", "BoomBeyondLoadChart", "NoDiscreteLoadChartEntry", "ZeroCapacityChartEntry", "MissingLoadChartData"
            };
            var failureCounts = categories.ToDictionary(c => c, c => 0);

            int totalFailed = allElements.Count - finalResult.LiftedElementCount;
            int totalSelected = allElements.Count;

            foreach (var row in detailedRows)
            {
                if (!row.IsLifted && failureCounts.ContainsKey(row.FailureCategory))
                {
                    failureCounts[row.FailureCategory]++;
                }
            }

            // 5. Write breakdown CSV file
            using (var writer = new StreamWriter(breakdownPath, false, new UTF8Encoding(true)))
            {
                writer.WriteLine("FailureCategory,Count,PercentageOfFailedElements,PercentageOfSelectedElements");
                foreach (var cat in categories)
                {
                    int count = failureCounts[cat];
                    double pctFailed = totalFailed > 0 ? (double)count / totalFailed * 100.0 : 0.0;
                    double pctSelected = totalSelected > 0 ? (double)count / totalSelected * 100.0 : 0.0;
                    writer.WriteLine($"{cat},{count},{pctFailed.ToString("F2", Invariant)}%,{pctSelected.ToString("F2", Invariant)}%");
                }
            }

            // 6. Write/Append to Run History CSV
            double coverage = finalResult.CoverageRatio;
            string healthLabel = "Critical";
            if (coverage >= 0.90) healthLabel = "Excellent";
            else if (coverage >= 0.70) healthLabel = "Good";
            else if (coverage >= 0.40) healthLabel = "Warning";

            bool writeHeader = !File.Exists(historyPath);

            using (var writer = new StreamWriter(historyPath, true, new UTF8Encoding(true)))
            {
                if (writeHeader)
                {
                    writer.WriteLine(string.Join(",", new[]
                    {
                        "Timestamp", "ProjectName", "ProjectFolderPath", "IfcFileName", "CraneNameOrId", "TotalTargetElements",
                        "SelectedElements", "LiftedElements", "FailedElements", "CoverageRatio", "HealthLabel",
                        "FinalCraneSetupCount", "GreedyIterations", "PopulationSize", "ConfiguredGenerations",
                        "ActualGenerationLoopCount", "TotalGATimeMs", "ElementAssignmentTimeMs", "CraneCapacityLookupCount",
                        "CraneCapacityCacheHits", "CraneCapacityCacheMisses", "CraneCapacityCacheHitRatio",
                        "MemoryBeforeMB", "MemoryAfterMB", "MemoryDeltaMB", "GCGen0Delta", "GCGen1Delta", "GCGen2Delta",
                        "ProfilingLogPath", "DetailedDiagnosticsCsvPath", "FailureBreakdownCsvPath"
                    }));
                }

                string gaTime = GAProfiler.Enabled ? GAProfiler.GetElapsedTimeMs("GAEngine.Run").ToString("F1", Invariant) : "N/A";
                string assignmentTime = GAProfiler.Enabled ? GAProfiler.GetElapsedTimeMs("ElementAssignmentService.EvaluateAssignment").ToString("F1", Invariant) : "N/A";
                string capacityLookups = "N/A";
                string capacityHits = "N/A";
                string capacityMisses = "N/A";
                string capacityRatio = "N/A";
                string logPath = "N/A";

                if (GAProfiler.Enabled)
                {
                    logPath = GAProfiler.ProfilingLogPath ?? "N/A";
                    // Accumulators are thread-safe and timing exists
                    // Let's get times from GAProfiler
                    // Total time: CranePlacementEngine.TotalRun or sum
                    // Let's use simple logic or N/A
                }

                // If GAProfiler was enabled we can output timing, otherwise "N/A"
                // Let's check hit counters
                long lookupHits = GAProfiler.CapacityCacheHits;
                long lookupMisses = GAProfiler.CapacityCacheMisses;
                long totalLookups = lookupHits + lookupMisses;
                double hitRatio = totalLookups > 0 ? (double)lookupHits / totalLookups * 100.0 : 0.0;

                capacityHits = lookupHits.ToString();
                capacityMisses = lookupMisses.ToString();
                capacityRatio = totalLookups > 0 ? hitRatio.ToString("F2", Invariant) + "%" : "0.00%";
                capacityLookups = totalLookups.ToString();

                string memBefore = (GAProfiler.MemoryBeforeBytes / 1024.0 / 1024.0).ToString("F3", Invariant);
                string memAfter = (GAProfiler.MemoryAfterBytes / 1024.0 / 1024.0).ToString("F3", Invariant);
                string memDelta = ((GAProfiler.MemoryAfterBytes - GAProfiler.MemoryBeforeBytes) / 1024.0 / 1024.0).ToString("F3", Invariant);
                string gc0 = (GAProfiler.GcCount0After - GAProfiler.GcCount0Before).ToString();
                string gc1 = (GAProfiler.GcCount1After - GAProfiler.GcCount1Before).ToString();
                string gc2 = (GAProfiler.GcCount2After - GAProfiler.GcCount2Before).ToString();

                // Let's write the history row
                writer.WriteLine(string.Join(",", new[]
                {
                    timeFormatted,
                    Escape(input.ProjectName ?? "N/A"),
                    Escape(input.ProjectFolderPath ?? "N/A"),
                    Escape(input.IfcFileName ?? "N/A"),
                    Escape(input.CraneSpec?.Crane?.Name ?? (input.CraneSpec != null ? $"Spec ID: {input.CraneSpec.Id}" : "N/A")),
                    input.TotalElementsCount.ToString(),
                    allElements.Count.ToString(),
                    finalResult.LiftedElementCount.ToString(),
                    totalFailed.ToString(),
                    (coverage * 100.0).ToString("F2", Invariant) + "%",
                    healthLabel,
                    finalResult.Positions.Count.ToString(),
                    GAProfiler.GreedySetCoverIterationCount.ToString(),
                    input.Settings.PopulationSize.ToString(),
                    input.Settings.MaxGenerations.ToString(),
                    GAProfiler.ActualGenerationLoopCount.ToString(),
                    gaTime,
                    assignmentTime,
                    capacityLookups,
                    capacityHits,
                    capacityMisses,
                    capacityRatio,
                    memBefore,
                    memAfter,
                    memDelta,
                    gc0,
                    gc1,
                    gc2,
                    Escape(logPath),
                    Escape(detailedPath),
                    Escape(breakdownPath)
                }));
            }

            return detailedPath;
        }

        private static bool IsBetterThan(ElementEvaluationResult candidate, ElementEvaluationResult best)
        {
            if (candidate.Status == LiftStatus.Liftable && best.Status != LiftStatus.Liftable)
                return true;
            if (candidate.Status != LiftStatus.Liftable && best.Status == LiftStatus.Liftable)
                return false;
            if (candidate.Status == LiftStatus.Liftable && best.Status == LiftStatus.Liftable)
                return candidate.Radius < best.Radius;

            int rankCand = GetFailureRank(candidate.Status);
            int rankBest = GetFailureRank(best.Status);
            if (rankCand > rankBest)
                return true;
            if (rankCand < rankBest)
                return false;

            return candidate.Radius < best.Radius;
        }

        private static int GetFailureRank(LiftStatus status)
        {
            return status switch
            {
                LiftStatus.Overweight => 3,
                LiftStatus.HeightFail => 2,
                LiftStatus.SpecialCase => 1,
                LiftStatus.OutOfRadius => 0,
                _ => 0
            };
        }

        private static string Escape(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            bool mustQuote = value.Contains(',')
                || value.Contains('"')
                || value.Contains('\r')
                || value.Contains('\n');

            if (!mustQuote)
                return value;

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        private class ElementDiagnosticRow
        {
            public string Timestamp { get; set; } = "";
            public string ProjectName { get; set; } = "";
            public string IfcFileName { get; set; } = "";
            public string CraneNameOrId { get; set; } = "";
            public string ElementId { get; set; } = "";
            public string ElementName { get; set; } = "";
            public string ElementType { get; set; } = "";
            public string StoreyOrElevation { get; set; } = "";
            public double Weight { get; set; }
            public double CogX { get; set; }
            public double CogY { get; set; }
            public double CogZ { get; set; }
            public int AssignedSetupIndex { get; set; }
            public double AssignedCraneX { get; set; }
            public double AssignedCraneY { get; set; }
            public double AssignedCraneZ { get; set; }
            public double BestRadius { get; set; }
            public double RequiredBoomLength { get; set; }
            public double RequiredCapacity { get; set; }
            public double AvailableCapacity { get; set; }
            public string LiftStatus { get; set; } = "";
            public bool IsLifted { get; set; }
            public string FailureReason { get; set; } = "";
            public string FailureCategory { get; set; } = "";
            public string SuggestedAction { get; set; } = "";
        }
    }
}
