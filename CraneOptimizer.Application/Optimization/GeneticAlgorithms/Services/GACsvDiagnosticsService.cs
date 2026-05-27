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
        private readonly ILoadChartCapacityResolver _capacityResolver;
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public GACsvDiagnosticsService(
            ILiftingCalculationEngine liftingEngine,
            ICraneConfigurationEngine configEngine)
            : this(liftingEngine, configEngine, null)
        {
        }

        public GACsvDiagnosticsService(
            ILiftingCalculationEngine liftingEngine,
            ICraneConfigurationEngine configEngine,
            ILoadChartCapacityResolver? capacityResolver)
        {
            _liftingEngine = liftingEngine;
            _configEngine = configEngine;
            _capacityResolver = capacityResolver ?? new LoadChartCapacityResolver();
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

                // Find all candidate evaluations for this element across final positions
                var candidates = new List<(int Index, ElementLiftResult Result)>();
                for (int i = 0; i < finalResult.Positions.Count; i++)
                {
                    var pos = finalResult.Positions[i];
                    var elemResult = pos.ElementResults.FirstOrDefault(r => string.Equals(r.ElementId, element.ElementID, StringComparison.OrdinalIgnoreCase));
                    if (elemResult != null)
                    {
                        candidates.Add((i, elemResult));
                    }
                }

                if (candidates.Count == 0) continue;

                // Find owner candidate (IsFinalAssignedToPoint == true)
                var owner = candidates.FirstOrDefault(c => c.Result.IsFinalAssignedToPoint);
                
                ElementLiftResult bestResult;
                int assignedIndex;
                bool isLifted;

                if (owner.Result != null)
                {
                    bestResult = owner.Result;
                    assignedIndex = owner.Index;
                    isLifted = (bestResult.Status == LiftStatus.Liftable);
                }
                else
                {
                    // No owner (Unassigned) -> find best failed candidate
                    var bestFailed = candidates.FirstOrDefault(c => string.Equals(c.Result.CandidatePointId, c.Result.BestFailedPointId, StringComparison.OrdinalIgnoreCase));
                    if (bestFailed.Result == null)
                    {
                        bestFailed = candidates.OrderByDescending(c => c.Result.AssignmentScore).First();
                    }
                    bestResult = bestFailed.Result;
                    assignedIndex = bestFailed.Index;
                    isLifted = false;
                }

                // Apply classification
                string failureReason = "Liftable";
                string failureCategory = "Liftable";
                string suggestedAction = "";

                if (!isLifted)
                {
                    var classification = LiftClassificationPolicy.Classify(
                        bestResult.Status,
                        bestResult.ChartStatus,
                        element.ElementWT,
                        bestResult.BoomLength,
                        input.CraneSpec?.MaxBoomLength ?? 0.0,
                        finalResult.Positions.Count > 0);

                    failureReason = classification.FailureReason;
                    failureCategory = classification.FailureCategory;
                    suggestedAction = classification.SuggestedAction;
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
                    BestRadius = bestResult.Radius,
                    RequiredBoomLength = bestResult.BoomLength,
                    RequiredCapacity = bestResult.RequiredCapacity,
                    AvailableCapacity = bestResult.AvailableCapacity,
                    LiftStatus = bestResult.Status.ToString(),
                    IsLifted = isLifted,
                    FailureReason = failureReason,
                    FailureCategory = failureCategory,
                    SuggestedAction = suggestedAction,
                    ReachStatus = bestResult.ReachStatus.ToString(),
                    ChartStatus = bestResult.ChartStatus.ToString(),
                    IsExactMatch = bestResult.IsExactMatch,
                    IsSteppedRadius = bestResult.IsSteppedRadius,
                    IsBlankCell = bestResult.IsBlankCell,
                    IsZeroCell = bestResult.IsZeroCell,
                    SafetyMargin = bestResult.SafetyMargin,
                    LookupMode = bestResult.LookupMode,
                    RequiredRadius = bestResult.RequiredRadius,
                    SelectedChartRadius = bestResult.SelectedChartRadius,
                    SelectedChartBoomLength = bestResult.SelectedChartBoomLength,
                    CraneBaseX = bestResult.CraneBaseX,
                    CraneBaseY = bestResult.CraneBaseY,
                    CraneBaseZ = bestResult.CraneBaseZ,
                    SlewCenterX = bestResult.SlewCenterX,
                    SlewCenterY = bestResult.SlewCenterY,
                    SlewCenterZ = bestResult.SlewCenterZ,
                    BoomHingeX = bestResult.BoomHingeX,
                    BoomHingeY = bestResult.BoomHingeY,
                    BoomHingeZ = bestResult.BoomHingeZ,
                    LoadPointX = bestResult.LoadPointX,
                    LoadPointY = bestResult.LoadPointY,
                    LoadPointZ = bestResult.LoadPointZ,
                    RadiusOriginType = bestResult.RadiusOriginType,
                    BoomOriginType = bestResult.BoomOriginType,
                    CandidateBoomsChecked = bestResult.CandidateBoomsChecked,
                    RejectedCandidates = bestResult.RejectedCandidates,
                    CandidatePointId = bestResult.CandidatePointId,
                    CandidatePointIndex = bestResult.CandidatePointIndex,
                    IsFeasibleFromPoint = bestResult.IsFeasibleFromPoint,
                    IsFinalAssignedToPoint = bestResult.IsFinalAssignedToPoint,
                    AssignedPointId = bestResult.AssignedPointId,
                    AssignedPointIndex = bestResult.AssignedPointIndex,
                    AssignmentReason = bestResult.AssignmentReason,
                    AssignmentScore = bestResult.AssignmentScore,
                    DuplicateAssignmentResolved = bestResult.DuplicateAssignmentResolved,
                    FeasiblePointIds = bestResult.FeasiblePointIds,
                    FinalOwnerPointId = bestResult.FinalOwnerPointId,
                    IsFinalAssigned = bestResult.IsFinalAssigned,
                    BestFailedPointId = bestResult.BestFailedPointId,
                    BestFailureReason = bestResult.BestFailureReason,
                    
                    HasBestFailedCandidate = bestResult.HasBestFailedCandidate,
                    BestFailedLiftStatus = bestResult.BestFailedLiftStatus.ToString(),
                    BestFailedReachStatus = bestResult.BestFailedReachStatus.ToString(),
                    BestFailedChartStatus = bestResult.BestFailedChartStatus.ToString(),
                    BestFailedSafetyMargin = bestResult.BestFailedSafetyMargin,
                    BestFailedRequiredRadius = bestResult.BestFailedRequiredRadius,
                    BestFailedRequiredBoomLength = bestResult.BestFailedRequiredBoomLength
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
                    "AvailableCapacity", "LiftStatus", "IsLifted", "FailureReason", "FailureCategory", "SuggestedAction",
                    "ReachStatus", "ChartStatus", "IsExactMatch", "IsSteppedRadius", "IsBlankCell", "IsZeroCell", "SafetyMargin",
                    "LookupMode", "RequiredRadius", "SelectedChartRadius", "SelectedChartBoomLength",
                    "CraneBasePoint", "SlewCenterPoint", "BoomHingePoint", "LoadPoint", "RadiusOriginType", "BoomOriginType",
                    "CandidateBoomsChecked", "RejectedCandidates",
                    "CandidatePointId", "CandidatePointIndex", "IsFeasibleFromPoint", "IsFinalAssignedToPoint",
                    "AssignedPointId", "AssignedPointIndex", "AssignmentReason", "AssignmentScore",
                    "DuplicateAssignmentResolved", "FeasiblePointIds", "FinalOwnerPointId", "IsFinalAssigned",
                    "BestFailedPointId", "BestFailureReason",
                    "HasBestFailedCandidate", "BestFailedLiftStatus", "BestFailedReachStatus", "BestFailedChartStatus",
                    "BestFailedSafetyMargin", "BestFailedRequiredRadius", "BestFailedRequiredBoomLength"
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
                        Escape(row.SuggestedAction),
                        row.ReachStatus,
                        row.ChartStatus,
                        row.IsExactMatch ? "true" : "false",
                        row.IsSteppedRadius ? "true" : "false",
                        row.IsBlankCell ? "true" : "false",
                        row.IsZeroCell ? "true" : "false",
                        row.SafetyMargin.ToString("0.###", Invariant),
                        Escape(row.LookupMode),
                        row.RequiredRadius.ToString("0.###", Invariant),
                        row.SelectedChartRadius.ToString("0.###", Invariant),
                        row.SelectedChartBoomLength.ToString("0.###", Invariant),
                        Escape($"({row.CraneBaseX.ToString("0.###", Invariant)}; {row.CraneBaseY.ToString("0.###", Invariant)}; {row.CraneBaseZ.ToString("0.###", Invariant)})"),
                        Escape($"({row.SlewCenterX.ToString("0.###", Invariant)}; {row.SlewCenterY.ToString("0.###", Invariant)}; {row.SlewCenterZ.ToString("0.###", Invariant)})"),
                        Escape($"({row.BoomHingeX.ToString("0.###", Invariant)}; {row.BoomHingeY.ToString("0.###", Invariant)}; {row.BoomHingeZ.ToString("0.###", Invariant)})"),
                        Escape($"({row.LoadPointX.ToString("0.###", Invariant)}; {row.LoadPointY.ToString("0.###", Invariant)}; {row.LoadPointZ.ToString("0.###", Invariant)})"),
                        Escape(row.RadiusOriginType),
                        Escape(row.BoomOriginType),
                        Escape(row.CandidateBoomsChecked),
                        Escape(row.RejectedCandidates),
                        Escape(row.CandidatePointId),
                        row.CandidatePointIndex.ToString(),
                        row.IsFeasibleFromPoint ? "true" : "false",
                        row.IsFinalAssignedToPoint ? "true" : "false",
                        Escape(row.AssignedPointId),
                        row.AssignedPointIndex.ToString(),
                        Escape(row.AssignmentReason),
                        row.AssignmentScore.ToString("0.###", Invariant),
                        row.DuplicateAssignmentResolved ? "true" : "false",
                        Escape(row.FeasiblePointIds),
                        Escape(row.FinalOwnerPointId),
                        row.IsFinalAssigned ? "true" : "false",
                        Escape(row.BestFailedPointId),
                        Escape(row.BestFailureReason),
                        row.HasBestFailedCandidate ? "true" : "false",
                        Escape(row.BestFailedLiftStatus),
                        Escape(row.BestFailedReachStatus),
                        Escape(row.BestFailedChartStatus),
                        row.BestFailedSafetyMargin.ToString("0.###", Invariant),
                        row.BestFailedRequiredRadius.ToString("0.###", Invariant),
                        row.BestFailedRequiredBoomLength.ToString("0.###", Invariant)
                    }));
                }
            }

            // 3A. Write ga_assignment_matrix_*.csv (one row per ElementGlobalId per CandidatePointId)
            string matrixFileName = $"ga_assignment_matrix_{timestampStr}.csv";
            string matrixPath = Path.Combine(reportsDir, matrixFileName);

            using (var writer = new StreamWriter(matrixPath, false, new UTF8Encoding(true)))
            {
                writer.WriteLine(string.Join(",", new[]
                {
                    "Timestamp", "ProjectName", "IfcFileName", "ElementGlobalId", "CandidatePointId", "CandidatePointIndex",
                    "CraneBasePoint", "SlewCenterPoint", "BoomHingePoint", "LoadPoint", "RequiredRadius", "RequiredBoomLength",
                    "SelectedChartRadius", "SelectedChartBoomLength", "AvailableCapacity", "ReachStatus", "ChartStatus",
                    "LiftStatus", "FailureReason", "SafetyMargin", "CandidateBoomsChecked", "RejectedCandidates", "AssignmentScore"
                }));

                foreach (var element in allElements)
                {
                    if (string.IsNullOrWhiteSpace(element.ElementID)) continue;

                    for (int i = 0; i < finalResult.Positions.Count; i++)
                    {
                        var pos = finalResult.Positions[i];
                        var er = pos.ElementResults.FirstOrDefault(r => string.Equals(r.ElementId, element.ElementID, StringComparison.OrdinalIgnoreCase));
                        if (er == null) continue;

                        string failReason = "Liftable";
                        if (er.Status != LiftStatus.Liftable)
                        {
                            var classification = LiftClassificationPolicy.Classify(
                                er.Status, er.ChartStatus, element.ElementWT, er.BoomLength, input.CraneSpec?.MaxBoomLength ?? 0.0, true);
                            failReason = classification.FailureReason;
                        }

                        writer.WriteLine(string.Join(",", new[]
                        {
                            timeFormatted,
                            Escape(input.ProjectName ?? "N/A"),
                            Escape(input.IfcFileName ?? "N/A"),
                            Escape(element.ElementID),
                            Escape(er.CandidatePointId),
                            er.CandidatePointIndex.ToString(),
                            Escape($"({er.CraneBaseX.ToString("0.###", Invariant)}; {er.CraneBaseY.ToString("0.###", Invariant)}; {er.CraneBaseZ.ToString("0.###", Invariant)})"),
                            Escape($"({er.SlewCenterX.ToString("0.###", Invariant)}; {er.SlewCenterY.ToString("0.###", Invariant)}; {er.SlewCenterZ.ToString("0.###", Invariant)})"),
                            Escape($"({er.BoomHingeX.ToString("0.###", Invariant)}; {er.BoomHingeY.ToString("0.###", Invariant)}; {er.BoomHingeZ.ToString("0.###", Invariant)})"),
                            Escape($"({er.LoadPointX.ToString("0.###", Invariant)}; {er.LoadPointY.ToString("0.###", Invariant)}; {er.LoadPointZ.ToString("0.###", Invariant)})"),
                            er.Radius.ToString("0.###", Invariant),
                            er.BoomLength.ToString("0.###", Invariant),
                            er.SelectedChartRadius.ToString("0.###", Invariant),
                            er.SelectedChartBoomLength.ToString("0.###", Invariant),
                            er.AvailableCapacity.ToString("0.###", Invariant),
                            er.ReachStatus.ToString(),
                            er.ChartStatus.ToString(),
                            er.Status.ToString(),
                            Escape(failReason),
                            er.SafetyMargin.ToString("0.###", Invariant),
                            Escape(er.CandidateBoomsChecked),
                            Escape(er.RejectedCandidates),
                            er.AssignmentScore.ToString("0.###", Invariant)
                        }));
                    }
                }
            }

            // 3B. Write ga_final_assignments_*.csv (one row per ElementGlobalId)
            string finalAssignmentsFileName = $"ga_final_assignments_{timestampStr}.csv";
            string finalAssignmentsPath = Path.Combine(reportsDir, finalAssignmentsFileName);

            using (var writer = new StreamWriter(finalAssignmentsPath, false, new UTF8Encoding(true)))
            {
                writer.WriteLine(string.Join(",", new[]
                {
                    "Timestamp", "ProjectName", "IfcFileName", "ElementGlobalId", "AssignedPointId", "AssignedPointIndex",
                    "AssignedCraneId", "AssignmentStatus", "AssignmentReason", "AssignmentScore", "IsFinalAssigned",
                    "BestFailedPointId", "BestFailureReason"
                }));

                foreach (var row in detailedRows)
                {
                    writer.WriteLine(string.Join(",", new[]
                    {
                        row.Timestamp,
                        Escape(row.ProjectName),
                        Escape(row.IfcFileName),
                        Escape(row.ElementId),
                        Escape(row.AssignedPointId),
                        row.AssignedPointIndex.ToString(),
                        Escape(row.CraneNameOrId),
                        row.IsLifted ? "Liftable" : "Unassigned",
                        Escape(row.AssignmentReason),
                        row.AssignmentScore.ToString("0.###", Invariant),
                        row.IsFinalAssigned ? "true" : "false",
                        Escape(row.BestFailedPointId),
                        Escape(row.BestFailureReason)
                    }));
                }
            }

            // 4. Calculate failure breakdown percentages
            var categories = new[]
            {
                "OutOfRadius", "Overweight", "HeightFail", "BoomLengthExceeded", "NoValidSetupPosition",
                "ForbiddenZoneBlocked", "NotAssigned", "MissingWeightOrInvalidData", "SpecialCase", "Unknown",
                "RadiusBeyondLoadChart", "BoomBeyondLoadChart", "NoDiscreteLoadChartEntry", "NoListedCapacityForBoomRadius",
                "OutsideChartEnvelope", "InvalidBlankCell", "InvalidZeroCell", "MissingLoadChartData"
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
            return LiftClassificationPolicy.GetFailureRank(status);
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
            public string ReachStatus { get; set; } = "";
            public string ChartStatus { get; set; } = "";
            public bool IsExactMatch { get; set; }
            public bool IsSteppedRadius { get; set; }
            public bool IsBlankCell { get; set; }
            public bool IsZeroCell { get; set; }
            public double SafetyMargin { get; set; }
            public string LookupMode { get; set; } = "";
            public double RequiredRadius { get; set; }
            public double SelectedChartRadius { get; set; }
            public double SelectedChartBoomLength { get; set; }
            public double CraneBaseX { get; set; }
            public double CraneBaseY { get; set; }
            public double CraneBaseZ { get; set; }
            public double SlewCenterX { get; set; }
            public double SlewCenterY { get; set; }
            public double SlewCenterZ { get; set; }
            public double BoomHingeX { get; set; }
            public double BoomHingeY { get; set; }
            public double BoomHingeZ { get; set; }
            public double LoadPointX { get; set; }
            public double LoadPointY { get; set; }
            public double LoadPointZ { get; set; }
            public string RadiusOriginType { get; set; } = "SlewCenter";
            public string BoomOriginType { get; set; } = "BoomHinge";
            public string CandidateBoomsChecked { get; set; } = "";
            public string RejectedCandidates { get; set; } = "";

            // GA Assignment and Uniqueness Fields
            public string CandidatePointId { get; set; } = "";
            public int CandidatePointIndex { get; set; } = -1;
            public bool IsFeasibleFromPoint { get; set; }
            public bool IsFinalAssignedToPoint { get; set; }
            public string AssignedPointId { get; set; } = "";
            public int AssignedPointIndex { get; set; } = -1;
            public string AssignmentReason { get; set; } = "";
            public double AssignmentScore { get; set; }
            public bool DuplicateAssignmentResolved { get; set; }
            public string FeasiblePointIds { get; set; } = "";
            public string FinalOwnerPointId { get; set; } = "";
            public bool IsFinalAssigned { get; set; }
            public string BestFailedPointId { get; set; } = "";
            public string BestFailureReason { get; set; } = "";

            public bool HasBestFailedCandidate { get; set; }
            public string BestFailedLiftStatus { get; set; } = "";
            public string BestFailedReachStatus { get; set; } = "";
            public string BestFailedChartStatus { get; set; } = "";
            public double BestFailedSafetyMargin { get; set; }
            public double BestFailedRequiredRadius { get; set; }
            public double BestFailedRequiredBoomLength { get; set; }
        }
    }
}
