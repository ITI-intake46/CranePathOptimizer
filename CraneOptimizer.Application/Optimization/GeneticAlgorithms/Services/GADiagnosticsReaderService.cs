using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Linq;
using CraneOptimizer.Application.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    public class GADiagnosticsReaderService : IGADiagnosticsReaderService
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private readonly IProjectOutputPathService? _outputPathService;

        public GADiagnosticsReaderService(IProjectOutputPathService outputPathService)
        {
            _outputPathService = outputPathService ?? throw new ArgumentNullException(nameof(outputPathService));
        }

        [Obsolete("For tests only")]
        public GADiagnosticsReaderService()
        {
            _outputPathService = null;
        }

        public IReadOnlyList<GARunHistoryDto> ReadRunHistory(string? projectFolderPath)
        {
            var results = new List<GARunHistoryDto>();

            // 1. Resolve output folder path
            string reportsDir;
            if (!string.IsNullOrWhiteSpace(projectFolderPath))
            {
                reportsDir = Path.Combine(projectFolderPath, "Reports", "GA");
            }
            else
            {
                // No active project — no diagnostics to read.
                return results;
            }

            var historyFiles = EnumerateHistoryFiles(reportsDir).ToList();
            if (historyFiles.Count == 0)
            {
                return results;
            }

            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var historyPath in historyFiles)
            {
                try
                {
                    using (var reader = new StreamReader(historyPath, Encoding.UTF8))
                    {
                        string? headerLine = reader.ReadLine();
                        if (string.IsNullOrWhiteSpace(headerLine))
                        {
                            continue;
                        }

                        var headers = ParseCsvLine(headerLine);

                        int optimizationRunIdIdx = GetHeaderIndex(headers, "OptimizationRunId");
                        int stageRunIdIdx = GetHeaderIndex(headers, "StageRunId");
                        int timestampIdx = GetHeaderIndex(headers, "Timestamp");
                        int projectNameIdx = GetHeaderIndex(headers, "ProjectName");
                        int projectFolderPathIdx = GetHeaderIndex(headers, "ProjectFolderPath");
                        int ifcFileNameIdx = GetHeaderIndex(headers, "IfcFileName");
                        int craneNameIdx = GetHeaderIndex(headers, "CraneNameOrId");
                        int totalTargetElementsIdx = GetHeaderIndex(headers, "TotalTargetElements");
                        int selectedElementsIdx = GetHeaderIndex(headers, "SelectedElements");
                        int liftedElementsIdx = GetHeaderIndex(headers, "LiftedElements");
                        int failedElementsIdx = GetHeaderIndex(headers, "FailedElements");
                        int coverageIdx = GetHeaderIndex(headers, "CoverageRatio");
                        int healthLabelIdx = GetHeaderIndex(headers, "HealthLabel");
                        int setupCountIdx = GetHeaderIndex(headers, "FinalCraneSetupCount");
                        int greedyIterationsIdx = GetHeaderIndex(headers, "GreedyIterations");
                        int popSizeIdx = GetHeaderIndex(headers, "PopulationSize");
                        int configGenIdx = GetHeaderIndex(headers, "ConfiguredGenerations");
                        int actualGenIdx = GetHeaderIndex(headers, "ActualGenerationLoopCount");
                        int totalGATimeIdx = GetHeaderIndex(headers, "TotalGATimeMs");
                        int assignmentTimeIdx = GetHeaderIndex(headers, "ElementAssignmentTimeMs");
                        int capacityLookupCountIdx = GetHeaderIndex(headers, "CraneCapacityLookupCount");
                        int capacityCacheHitsIdx = GetHeaderIndex(headers, "CraneCapacityCacheHits");
                        int capacityCacheMissesIdx = GetHeaderIndex(headers, "CraneCapacityCacheMisses");
                        int capacityCacheHitRatioIdx = GetHeaderIndex(headers, "CraneCapacityCacheHitRatio");
                        int memoryBeforeIdx = GetHeaderIndex(headers, "MemoryBeforeMB");
                        int memoryAfterIdx = GetHeaderIndex(headers, "MemoryAfterMB");
                        int memoryDeltaIdx = GetHeaderIndex(headers, "MemoryDeltaMB");
                        int gcGen0Idx = GetHeaderIndex(headers, "GCGen0Delta");
                        int gcGen1Idx = GetHeaderIndex(headers, "GCGen1Delta");
                        int gcGen2Idx = GetHeaderIndex(headers, "GCGen2Delta");
                        int profilingLogPathIdx = GetHeaderIndex(headers, "ProfilingLogPath");
                        int detailedPathIdx = GetHeaderIndex(headers, "DetailedDiagnosticsCsvPath");
                        int breakdownPathIdx = GetHeaderIndex(headers, "FailureBreakdownCsvPath");

                        string? line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;

                            var row = ParseCsvLine(line);
                            var dto = new GARunHistoryDto
                            {
                                OptimizationRunId = GetValue(row, optimizationRunIdIdx, string.Empty),
                                StageRunId = GetValue(row, stageRunIdIdx, string.Empty),
                                Timestamp = GetValue(row, timestampIdx),
                                ProjectName = GetValue(row, projectNameIdx, "N/A"),
                                ProjectFolderPath = GetValue(row, projectFolderPathIdx, "N/A"),
                                IfcFileName = GetValue(row, ifcFileNameIdx, "N/A"),
                                CraneNameOrId = GetValue(row, craneNameIdx, "N/A"),
                                TotalTargetElements = GetValueInt(row, totalTargetElementsIdx),
                                SelectedElements = GetValueInt(row, selectedElementsIdx),
                                LiftedElements = GetValueInt(row, liftedElementsIdx),
                                FailedElements = GetValueInt(row, failedElementsIdx),
                                CoverageRatio = GetValue(row, coverageIdx, "0.00%"),
                                HealthLabel = GetValue(row, healthLabelIdx, "Critical"),
                                FinalCraneSetupCount = GetValueInt(row, setupCountIdx),
                                GreedyIterations = GetValueInt(row, greedyIterationsIdx),
                                PopulationSize = GetValueInt(row, popSizeIdx),
                                ConfiguredGenerations = GetValueInt(row, configGenIdx),
                                ActualGenerationLoopCount = GetValueInt(row, actualGenIdx),
                                TotalGATimeMs = GetValue(row, totalGATimeIdx, "N/A"),
                                ElementAssignmentTimeMs = GetValue(row, assignmentTimeIdx, "N/A"),
                                CraneCapacityLookupCount = GetValue(row, capacityLookupCountIdx, "N/A"),
                                CraneCapacityCacheHits = GetValue(row, capacityCacheHitsIdx, "N/A"),
                                CraneCapacityCacheMisses = GetValue(row, capacityCacheMissesIdx, "N/A"),
                                CraneCapacityCacheHitRatio = GetValue(row, capacityCacheHitRatioIdx, "0.00%"),
                                MemoryBeforeMB = GetValue(row, memoryBeforeIdx, "N/A"),
                                MemoryAfterMB = GetValue(row, memoryAfterIdx, "N/A"),
                                MemoryDeltaMB = GetValue(row, memoryDeltaIdx, "N/A"),
                                GCGen0Delta = GetValue(row, gcGen0Idx, "N/A"),
                                GCGen1Delta = GetValue(row, gcGen1Idx, "N/A"),
                                GCGen2Delta = GetValue(row, gcGen2Idx, "N/A"),
                                ProfilingLogPath = GetValue(row, profilingLogPathIdx, "N/A"),
                                DetailedDiagnosticsCsvPath = GetValue(row, detailedPathIdx, "N/A"),
                                FailureBreakdownCsvPath = GetValue(row, breakdownPathIdx, "N/A")
                            };

                            string key = $"{dto.Timestamp}_{dto.OptimizationRunId}_{dto.StageRunId}";
                            if (seenKeys.Add(key))
                            {
                                results.Add(dto);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[GADiagnosticsReaderService] Error reading history file {historyPath}: {ex.Message}");
                }
            }

            return results;
        }

        public IReadOnlyList<GAElementDiagnosticDto> ReadDetailedDiagnostics(string detailedCsvPath)
        {
            var results = new List<GAElementDiagnosticDto>();

            detailedCsvPath = ResolvePathWithFallback(detailedCsvPath);
            if (string.IsNullOrWhiteSpace(detailedCsvPath) || !File.Exists(detailedCsvPath))
            {
                return results;
            }

            try
            {
                using (var reader = new StreamReader(detailedCsvPath, Encoding.UTF8))
                {
                    string? headerLine = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(headerLine))
                    {
                        return results;
                    }

                    var headers = ParseCsvLine(headerLine);

                    int timestampIdx = GetHeaderIndex(headers, "Timestamp");
                    int projectNameIdx = GetHeaderIndex(headers, "ProjectName");
                    int ifcFileNameIdx = GetHeaderIndex(headers, "IfcFileName");
                    int craneNameIdx = GetHeaderIndex(headers, "CraneNameOrId");
                    int elementIdIdx = GetHeaderIndex(headers, "ElementId");
                    int elementNameIdx = GetHeaderIndex(headers, "ElementName");
                    int elementTypeIdx = GetHeaderIndex(headers, "ElementType");
                    int storeyIdx = GetHeaderIndex(headers, "StoreyOrElevation");
                    int weightIdx = GetHeaderIndex(headers, "Weight");
                    int cogXIdx = GetHeaderIndex(headers, "COG_X");
                    int cogYIdx = GetHeaderIndex(headers, "COG_Y");
                    int cogZIdx = GetHeaderIndex(headers, "COG_Z");
                    int assignedSetupIndexIdx = GetHeaderIndex(headers, "AssignedSetupIndex");
                    int assignedCraneXIdx = GetHeaderIndex(headers, "AssignedCraneX");
                    int assignedCraneYIdx = GetHeaderIndex(headers, "AssignedCraneY");
                    int assignedCraneZIdx = GetHeaderIndex(headers, "AssignedCraneZ");
                    int bestRadiusIdx = GetHeaderIndex(headers, "BestRadius");
                    int requiredBoomIdx = GetHeaderIndex(headers, "RequiredBoomLength");
                    int requiredCapacityIdx = GetFirstHeaderIndex(headers, "RequiredCapacity", "RequiredWeight");
                    int rawChartCapacityIdx = GetFirstHeaderIndex(headers, "RawChartCapacity", "ChartCapacity");
                    int availableCapacityIdx = GetHeaderIndex(headers, "AvailableCapacity");
                    int deratingRatioIdx = GetHeaderIndex(headers, "DeratingRatio");
                    int safetyFactorIdx = GetHeaderIndex(headers, "SafetyFactor");
                    int additionalWtIdx = GetFirstHeaderIndex(headers, "AdditionalWt", "AdditionalWeight", "HookRiggingWeight");
                    int wireLengthIdx = GetHeaderIndex(headers, "WireLength");
                    int liftStatusIdx = GetHeaderIndex(headers, "LiftStatus");
                    int isLiftedIdx = GetHeaderIndex(headers, "IsLifted");
                    int failureReasonIdx = GetHeaderIndex(headers, "FailureReason");
                    int failureCategoryIdx = GetHeaderIndex(headers, "FailureCategory");
                    int suggestedActionIdx = GetHeaderIndex(headers, "SuggestedAction");
                    int reachStatusIdx = GetHeaderIndex(headers, "ReachStatus");
                    int chartStatusIdx = GetHeaderIndex(headers, "ChartStatus");
                    int isExactMatchIdx = GetHeaderIndex(headers, "IsExactMatch");
                    int isSteppedRadiusIdx = GetHeaderIndex(headers, "IsSteppedRadius");
                    int isBlankCellIdx = GetHeaderIndex(headers, "IsBlankCell");
                    int isZeroCellIdx = GetHeaderIndex(headers, "IsZeroCell");
                    int safetyMarginIdx = GetHeaderIndex(headers, "SafetyMargin");
                    int lookupModeIdx = GetHeaderIndex(headers, "LookupMode");
                    int requiredRadiusIdx = GetHeaderIndex(headers, "RequiredRadius");
                    int selectedChartRadiusIdx = GetHeaderIndex(headers, "SelectedChartRadius");
                    int selectedChartBoomLengthIdx = GetHeaderIndex(headers, "SelectedChartBoomLength");
                    int selectedVisualColumnIndexIdx = GetHeaderIndex(headers, "SelectedVisualColumnIndex");
                    int selectedVisualColumnHeaderIdx = GetHeaderIndex(headers, "SelectedVisualColumnHeader");
                    int selectedNumberOfLinesIdx = GetHeaderIndex(headers, "SelectedNumberOfLines");
                    int selectedOperatingConditionLabelIdx = GetHeaderIndex(headers, "SelectedOperatingConditionLabel");
                    int conservativeDuplicateFallbackIdx = GetHeaderIndex(headers, "IsConservativeDuplicateFallback");
                    int duplicateAlternativeCountIdx = GetHeaderIndex(headers, "DuplicateAlternativeCount");
                    int duplicateAlternativeSummaryIdx = GetHeaderIndex(headers, "DuplicateAlternativeSummary");
                    int craneBasePointIdx = GetFirstHeaderIndex(headers, "CraneBasePoint_Local", "CraneBasePoint");
                    int slewCenterPointIdx = GetFirstHeaderIndex(headers, "SlewCenterPoint_Local", "SlewCenterPoint");
                    int boomHingePointIdx = GetFirstHeaderIndex(headers, "BoomHingePoint_Local", "BoomHingePoint");
                    int loadPointIdx = GetFirstHeaderIndex(headers, "LoadPoint_Local", "LoadPoint");
                    int radiusOriginTypeIdx = GetHeaderIndex(headers, "RadiusOriginType");
                    int boomOriginTypeIdx = GetHeaderIndex(headers, "BoomOriginType");
                    int candidateBoomsCheckedIdx = GetHeaderIndex(headers, "CandidateBoomsChecked");
                    int rejectedCandidatesIdx = GetHeaderIndex(headers, "RejectedCandidates");

                    int candidatePointIdIdx = GetHeaderIndex(headers, "CandidatePointId");
                    int candidatePointIndexIdx = GetHeaderIndex(headers, "CandidatePointIndex");
                    int isFeasibleFromPointIdx = GetHeaderIndex(headers, "IsFeasibleFromPoint");
                    int isFinalAssignedToPointIdx = GetHeaderIndex(headers, "IsFinalAssignedToPoint");
                    int assignedPointIdIdx = GetHeaderIndex(headers, "AssignedPointId");
                    int assignedPointIndexIdx = GetHeaderIndex(headers, "AssignedPointIndex");
                    int assignmentReasonIdx = GetHeaderIndex(headers, "AssignmentReason");
                    int assignmentScoreIdx = GetHeaderIndex(headers, "AssignmentScore");
                    int duplicateAssignmentResolvedIdx = GetHeaderIndex(headers, "DuplicateAssignmentResolved");
                    int feasiblePointIdsIdx = GetHeaderIndex(headers, "FeasiblePointIds");
                    int finalOwnerPointIdIdx = GetHeaderIndex(headers, "FinalOwnerPointId");
                    int isFinalAssignedIdx = GetHeaderIndex(headers, "IsFinalAssigned");
                    int bestFailedPointIdIdx = GetHeaderIndex(headers, "BestFailedPointId");
                    int bestFailureReasonIdx = GetHeaderIndex(headers, "BestFailureReason");

                    int hasBestFailedCandidateIdx = GetHeaderIndex(headers, "HasBestFailedCandidate");
                    int bestFailedLiftStatusIdx = GetHeaderIndex(headers, "BestFailedLiftStatus");
                    int bestFailedReachStatusIdx = GetHeaderIndex(headers, "BestFailedReachStatus");
                    int bestFailedChartStatusIdx = GetHeaderIndex(headers, "BestFailedChartStatus");
                    int bestFailedSafetyMarginIdx = GetHeaderIndex(headers, "BestFailedSafetyMargin");
                    int bestFailedRequiredRadiusIdx = GetHeaderIndex(headers, "BestFailedRequiredRadius");
                    int bestFailedRequiredBoomLengthIdx = GetHeaderIndex(headers, "BestFailedRequiredBoomLength");

                    // Phase 5: Canonical Display Identity (optional columns; backward compatible)
                    int canonicalMarkIdx = GetHeaderIndex(headers, "CanonicalMark");
                    int markSourceIdx = GetHeaderIndex(headers, "MarkSource");
                    int markConfidenceIdx = GetHeaderIndex(headers, "MarkConfidence");
                    int isExplicitMarkIdx = GetHeaderIndex(headers, "IsExplicitMark");

                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var row = ParseCsvLine(line);
                        var basePt = ParsePoint(GetValue(row, craneBasePointIdx, ""));
                        var slewPt = ParsePoint(GetValue(row, slewCenterPointIdx, ""));
                        var hingePt = ParsePoint(GetValue(row, boomHingePointIdx, ""));
                        var loadPt = ParsePoint(GetValue(row, loadPointIdx, ""));

                        var dto = new GAElementDiagnosticDto
                        {
                            Timestamp = GetValue(row, timestampIdx),
                            ProjectName = GetValue(row, projectNameIdx, "N/A"),
                            IfcFileName = GetValue(row, ifcFileNameIdx, "N/A"),
                            CraneNameOrId = GetValue(row, craneNameIdx, "N/A"),
                            ElementId = GetValue(row, elementIdIdx),
                            ElementName = GetValue(row, elementNameIdx, "N/A"),
                            ElementType = GetValue(row, elementTypeIdx, "N/A"),
                            StoreyOrElevation = GetValue(row, storeyIdx, "0"),
                            Weight = GetValueDouble(row, weightIdx),
                            CogX = GetValueDouble(row, cogXIdx),
                            CogY = GetValueDouble(row, cogYIdx),
                            CogZ = GetValueDouble(row, cogZIdx),
                            AssignedSetupIndex = GetValueInt(row, assignedSetupIndexIdx, -1),
                            AssignedCraneX = GetValue(row, assignedCraneXIdx, "N/A"),
                            AssignedCraneY = GetValue(row, assignedCraneYIdx, "N/A"),
                            AssignedCraneZ = GetValue(row, assignedCraneZIdx, "N/A"),
                            BestRadius = GetValueDouble(row, bestRadiusIdx),
                            RequiredBoomLength = GetValueDouble(row, requiredBoomIdx),
                            RequiredCapacity = GetValueDouble(row, requiredCapacityIdx),
                            RawChartCapacity = GetValueDouble(row, rawChartCapacityIdx),
                            AvailableCapacity = GetValueDouble(row, availableCapacityIdx),
                            AdditionalWt = GetValueDouble(row, additionalWtIdx),
                            DeratingRatio = GetValueDouble(row, deratingRatioIdx),
                            SafetyFactor = GetValueDouble(row, safetyFactorIdx),
                            WireLength = GetValueDouble(row, wireLengthIdx),
                            LiftStatus = GetValue(row, liftStatusIdx),
                            IsLifted = GetValueBool(row, isLiftedIdx),
                            FailureReason = GetValue(row, failureReasonIdx),
                            FailureCategory = GetValue(row, failureCategoryIdx),
                            SuggestedAction = GetValue(row, suggestedActionIdx, ""),
                            ReachStatus = GetValue(row, reachStatusIdx),
                            ChartStatus = GetValue(row, chartStatusIdx),
                            IsExactMatch = GetValueBool(row, isExactMatchIdx),
                            IsSteppedRadius = GetValueBool(row, isSteppedRadiusIdx),
                            IsBlankCell = GetValueBool(row, isBlankCellIdx),
                            IsZeroCell = GetValueBool(row, isZeroCellIdx),
                            SafetyMargin = GetValueDouble(row, safetyMarginIdx),
                            LookupMode = GetValue(row, lookupModeIdx, "ConservativeStep"),
                            RequiredRadius = GetValueDouble(row, requiredRadiusIdx, GetValueDouble(row, bestRadiusIdx)),
                            SelectedChartRadius = GetValueDouble(row, selectedChartRadiusIdx),
                            SelectedChartBoomLength = GetValueDouble(row, selectedChartBoomLengthIdx),
                            SelectedVisualColumnIndex = GetNullableInt(row, selectedVisualColumnIndexIdx),
                            SelectedVisualColumnHeader = GetValue(row, selectedVisualColumnHeaderIdx, ""),
                            SelectedNumberOfLines = GetNullableInt(row, selectedNumberOfLinesIdx),
                            SelectedOperatingConditionLabel = GetValue(row, selectedOperatingConditionLabelIdx, ""),
                            IsConservativeDuplicateFallback = GetValueBool(row, conservativeDuplicateFallbackIdx),
                            DuplicateAlternativeCount = GetValueInt(row, duplicateAlternativeCountIdx),
                            DuplicateAlternativeSummary = GetValue(row, duplicateAlternativeSummaryIdx, ""),
                            CraneBaseX = basePt.X,
                            CraneBaseY = basePt.Y,
                            CraneBaseZ = basePt.Z,
                            SlewCenterX = slewPt.X,
                            SlewCenterY = slewPt.Y,
                            SlewCenterZ = slewPt.Z,
                            BoomHingeX = hingePt.X,
                            BoomHingeY = hingePt.Y,
                            BoomHingeZ = hingePt.Z,
                            LoadPointX = loadPt.X,
                            LoadPointY = loadPt.Y,
                            LoadPointZ = loadPt.Z,
                            RadiusOriginType = GetValue(row, radiusOriginTypeIdx, "SlewCenter"),
                            BoomOriginType = GetValue(row, boomOriginTypeIdx, "BoomHinge"),
                            CandidateBoomsChecked = GetValue(row, candidateBoomsCheckedIdx, ""),
                            RejectedCandidates = GetValue(row, rejectedCandidatesIdx, ""),

                            // GA Assignment and Uniqueness Fields
                            CandidatePointId = GetValue(row, candidatePointIdIdx, ""),
                            CandidatePointIndex = GetValueInt(row, candidatePointIndexIdx, -1),
                            IsFeasibleFromPoint = GetValueBool(row, isFeasibleFromPointIdx),
                            IsFinalAssignedToPoint = GetValueBool(row, isFinalAssignedToPointIdx),
                            AssignedPointId = GetValue(row, assignedPointIdIdx, ""),
                            AssignedPointIndex = GetValueInt(row, assignedPointIndexIdx, -1),
                            AssignmentReason = GetValue(row, assignmentReasonIdx, ""),
                            AssignmentScore = GetValueDouble(row, assignmentScoreIdx),
                            DuplicateAssignmentResolved = GetValueBool(row, duplicateAssignmentResolvedIdx),
                            FeasiblePointIds = GetValue(row, feasiblePointIdsIdx, ""),
                            FinalOwnerPointId = GetValue(row, finalOwnerPointIdIdx, ""),
                            IsFinalAssigned = GetValueBool(row, isFinalAssignedIdx),
                            BestFailedPointId = GetValue(row, bestFailedPointIdIdx, ""),
                            BestFailureReason = GetValue(row, bestFailureReasonIdx, ""),

                            HasBestFailedCandidate = GetValueBool(row, hasBestFailedCandidateIdx),
                            BestFailedLiftStatus = GetValue(row, bestFailedLiftStatusIdx),
                            BestFailedReachStatus = GetValue(row, bestFailedReachStatusIdx),
                            BestFailedChartStatus = GetValue(row, bestFailedChartStatusIdx),
                            BestFailedSafetyMargin = GetValueDouble(row, bestFailedSafetyMarginIdx, 0.0),
                            BestFailedRequiredRadius = GetValueDouble(row, bestFailedRequiredRadiusIdx, 0.0),
                            BestFailedRequiredBoomLength = GetValueDouble(row, bestFailedRequiredBoomLengthIdx, 0.0),

                            // Phase 5: Canonical Display Identity (empty string when reading old CSVs)
                            CanonicalMark = GetValue(row, canonicalMarkIdx, string.Empty),
                            MarkSource = GetValue(row, markSourceIdx, string.Empty),
                            MarkConfidence = GetValue(row, markConfidenceIdx, string.Empty),
                            IsExplicitMark = GetValueBool(row, isExplicitMarkIdx)
                        };

                        results.Add(dto);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GADiagnosticsReaderService] Error reading diagnostics: {ex.Message}");
            }

            return results;
        }

        public IReadOnlyList<GAFailureBreakdownDto> ReadFailureBreakdown(string breakdownCsvPath)
        {
            var results = new List<GAFailureBreakdownDto>();

            breakdownCsvPath = ResolvePathWithFallback(breakdownCsvPath);
            if (string.IsNullOrWhiteSpace(breakdownCsvPath) || !File.Exists(breakdownCsvPath))
            {
                return results;
            }

            try
            {
                using (var reader = new StreamReader(breakdownCsvPath, Encoding.UTF8))
                {
                    string? headerLine = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(headerLine))
                    {
                        return results;
                    }

                    var headers = ParseCsvLine(headerLine);

                    int categoryIdx = GetHeaderIndex(headers, "FailureCategory");
                    int countIdx = GetHeaderIndex(headers, "Count");
                    int pctFailedIdx = GetHeaderIndex(headers, "PercentageOfFailedElements");
                    int pctSelectedIdx = GetHeaderIndex(headers, "PercentageOfSelectedElements");

                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var row = ParseCsvLine(line);
                        var dto = new GAFailureBreakdownDto
                        {
                            FailureCategory = GetValue(row, categoryIdx, "Unknown"),
                            Count = GetValueInt(row, countIdx),
                            PercentageOfFailedElements = GetValue(row, pctFailedIdx, "0.00%"),
                            PercentageOfSelectedElements = GetValue(row, pctSelectedIdx, "0.00%")
                        };

                        results.Add(dto);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GADiagnosticsReaderService] Error reading breakdown: {ex.Message}");
            }

            return results;
        }

        #region Helper Primitives

        private static IEnumerable<string> EnumerateHistoryFiles(string rootDir)
        {
            if (!Directory.Exists(rootDir))
                yield break;

            var patterns = new[]
            {
                "debug_ga_run_history_*.csv",
                "debug_ga_run_history.csv",
                "ga_audit_run_history.csv",
                "ga_run_history.csv"
            };

            var foundFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pattern in patterns)
            {
                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(rootDir, pattern, SearchOption.AllDirectories);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[GADiagnosticsReaderService] EnumerateHistoryFiles error for pattern {pattern}: {ex.Message}");
                    continue;
                }

                foreach (var file in files)
                {
                    if (foundFiles.Add(file))
                    {
                        yield return file;
                    }
                }
            }
        }

        private string ResolvePathWithFallback(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            if (File.Exists(path))
                return path;

            // 1. Try translating filename in the same directory first
            string? sameFolderTranslated = TryTranslateFilename(path);
            if (sameFolderTranslated != null && File.Exists(sameFolderTranslated))
                return sameFolderTranslated;

            // 2. If not found and _outputPathService is available, reconstruct relative to the active project root
            if (_outputPathService != null)
            {
                try
                {
                    string activeRoot = _outputPathService.GetProjectRoot();
                    if (!string.IsNullOrEmpty(activeRoot) && Directory.Exists(activeRoot))
                    {
                        // Look for "Reports" directory segment
                        int reportsIdx = path.IndexOf(Path.DirectorySeparatorChar + "Reports" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
                        if (reportsIdx < 0)
                        {
                            reportsIdx = path.IndexOf(Path.AltDirectorySeparatorChar + "Reports" + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
                        }

                        if (reportsIdx >= 0)
                        {
                            string relativeSegment = path.Substring(reportsIdx + 1); // e.g. "Reports/GA/run-123/debug_ga_diagnostics_..."
                            string newPath = Path.Combine(activeRoot, relativeSegment);
                            if (File.Exists(newPath))
                                return newPath;

                            string? newFolderTranslated = TryTranslateFilename(newPath);
                            if (newFolderTranslated != null && File.Exists(newFolderTranslated))
                                return newFolderTranslated;
                        }
                    }
                }
                catch
                {
                    // Ignore path resolution errors (e.g. if project root throws because no project is active)
                }
            }

            return path;
        }

        private string? TryTranslateFilename(string originalPath)
        {
            try
            {
                string directory = Path.GetDirectoryName(originalPath) ?? string.Empty;
                if (!Directory.Exists(directory))
                    return null;

                string filename = Path.GetFileName(originalPath);

                // Detailed diagnostics translation
                if (filename.Contains("ga_diagnostics") || filename.Contains("ga_audit_diagnostics"))
                {
                    var patterns = new[]
                    {
                        "debug_ga_diagnostics_*.csv",
                        "ga_audit_diagnostics_*.csv",
                        "debug_ga_diagnostics.csv",
                        "ga_audit_diagnostics.csv"
                    };
                    foreach (var pattern in patterns)
                    {
                        foreach (var file in Directory.EnumerateFiles(directory, pattern))
                        {
                            return file;
                        }
                    }
                }

                // Failure breakdown translation
                if (filename.Contains("failure_breakdown") || filename.Contains("ga_audit_failure_breakdown"))
                {
                    var patterns = new[]
                    {
                        "debug_ga_failure_breakdown_*.csv",
                        "ga_audit_failure_breakdown_*.csv",
                        "debug_ga_failure_breakdown.csv",
                        "ga_audit_failure_breakdown.csv"
                    };
                    foreach (var pattern in patterns)
                    {
                        foreach (var file in Directory.EnumerateFiles(directory, pattern))
                        {
                            return file;
                        }
                    }
                }
            }
            catch
            {
                // Ignore folder access errors, etc.
            }

            return null;
        }

        private static int GetHeaderIndex(List<string> headers, string name)
        {
            return headers.FindIndex(h => string.Equals(h.Trim(), name, StringComparison.OrdinalIgnoreCase));
        }

        private static int GetFirstHeaderIndex(List<string> headers, params string[] names)
        {
            foreach (var name in names)
            {
                int index = GetHeaderIndex(headers, name);
                if (index >= 0)
                    return index;
            }

            return -1;
        }

        private static string GetValue(List<string> row, int index, string fallback = "")
        {
            if (index >= 0 && index < row.Count)
            {
                return row[index];
            }
            return fallback;
        }

        private static int GetValueInt(List<string> row, int index, int fallback = 0)
        {
            string val = GetValue(row, index);
            if (int.TryParse(val, NumberStyles.Integer, Invariant, out int res))
            {
                return res;
            }
            return fallback;
        }

        private static int? GetNullableInt(List<string> row, int index)
        {
            string val = GetValue(row, index);
            if (int.TryParse(val, NumberStyles.Integer, Invariant, out int res))
                return res;

            return null;
        }

        private static double GetValueDouble(List<string> row, int index, double fallback = 0.0)
        {
            string val = GetValue(row, index);
            if (double.TryParse(val, NumberStyles.Float, Invariant, out double res))
            {
                return res;
            }
            return fallback;
        }

        private static bool GetValueBool(List<string> row, int index, bool fallback = false)
        {
            string val = GetValue(row, index);
            if (bool.TryParse(val, out bool res))
            {
                return res;
            }
            if (string.Equals(val, "true", StringComparison.OrdinalIgnoreCase) || val == "1")
            {
                return true;
            }
            return fallback;
        }

        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(line)) return result;

            bool inQuotes = false;
            var currentToken = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        currentToken.Append('"');
                        i++; // Skip second quote
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(currentToken.ToString());
                    currentToken.Clear();
                }
                else
                {
                    currentToken.Append(c);
                }
            }
            result.Add(currentToken.ToString());
            return result;
        }

        private static (double X, double Y, double Z) ParsePoint(string pointStr)
        {
            if (string.IsNullOrWhiteSpace(pointStr)) return (0, 0, 0);
            var trimmed = pointStr.Trim('(', ')', ' ', '\t');
            var parts = trimmed.Split(';');
            if (parts.Length < 3) parts = trimmed.Split(','); // fallback
            if (parts.Length >= 3 &&
                double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double x) &&
                double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double y) &&
                double.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double z))
            {
                return (x, y, z);
            }
            return (0, 0, 0);
        }

        #endregion
    }
}
