using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    public class GADiagnosticsReaderService : IGADiagnosticsReaderService
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

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
                reportsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CraneOptimizer", "Reports", "GA");
            }

            string historyPath = Path.Combine(reportsDir, "ga_run_history.csv");

            if (!File.Exists(historyPath))
            {
                return results;
            }

            try
            {
                using (var reader = new StreamReader(historyPath, Encoding.UTF8))
                {
                    string? headerLine = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(headerLine))
                    {
                        return results;
                    }

                    var headers = ParseCsvLine(headerLine);

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

                        results.Add(dto);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GADiagnosticsReaderService] Error reading history: {ex.Message}");
            }

            return results;
        }

        public IReadOnlyList<GAElementDiagnosticDto> ReadDetailedDiagnostics(string detailedCsvPath)
        {
            var results = new List<GAElementDiagnosticDto>();

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
                    int requiredCapacityIdx = GetHeaderIndex(headers, "RequiredCapacity");
                    int availableCapacityIdx = GetHeaderIndex(headers, "AvailableCapacity");
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
                    int craneBasePointIdx = GetHeaderIndex(headers, "CraneBasePoint");
                    int slewCenterPointIdx = GetHeaderIndex(headers, "SlewCenterPoint");
                    int boomHingePointIdx = GetHeaderIndex(headers, "BoomHingePoint");
                    int loadPointIdx = GetHeaderIndex(headers, "LoadPoint");
                    int radiusOriginTypeIdx = GetHeaderIndex(headers, "RadiusOriginType");
                    int boomOriginTypeIdx = GetHeaderIndex(headers, "BoomOriginType");
                    int candidateBoomsCheckedIdx = GetHeaderIndex(headers, "CandidateBoomsChecked");
                    int rejectedCandidatesIdx = GetHeaderIndex(headers, "RejectedCandidates");

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
                            AvailableCapacity = GetValueDouble(row, availableCapacityIdx),
                            LiftStatus = GetValue(row, liftStatusIdx, "SpecialCase"),
                            IsLifted = GetValueBool(row, isLiftedIdx),
                            FailureReason = GetValue(row, failureReasonIdx, "N/A"),
                            FailureCategory = GetValue(row, failureCategoryIdx, "Unknown"),
                            SuggestedAction = GetValue(row, suggestedActionIdx, ""),
                            ReachStatus = GetValue(row, reachStatusIdx, "Reachable"),
                            ChartStatus = GetValue(row, chartStatusIdx, "NoListedCapacity"),
                            IsExactMatch = GetValueBool(row, isExactMatchIdx),
                            IsSteppedRadius = GetValueBool(row, isSteppedRadiusIdx),
                            IsBlankCell = GetValueBool(row, isBlankCellIdx),
                            IsZeroCell = GetValueBool(row, isZeroCellIdx),
                            SafetyMargin = GetValueDouble(row, safetyMarginIdx),
                            LookupMode = GetValue(row, lookupModeIdx, "ConservativeStep"),
                            RequiredRadius = GetValueDouble(row, requiredRadiusIdx, GetValueDouble(row, bestRadiusIdx)),
                            SelectedChartRadius = GetValueDouble(row, selectedChartRadiusIdx),
                            SelectedChartBoomLength = GetValueDouble(row, selectedChartBoomLengthIdx),
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
                            RejectedCandidates = GetValue(row, rejectedCandidatesIdx, "")
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

        private static int GetHeaderIndex(List<string> headers, string name)
        {
            return headers.FindIndex(h => string.Equals(h.Trim(), name, StringComparison.OrdinalIgnoreCase));
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
