using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F0_CoordinateSystem;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CraneOptimizer.UI.Features.F10_ProjectManagement.Services;
using CraneOptimizer.UI.Features.F2_IfcViewer.ViewModels;
using CraneOptimizer.Application.Optimization.Utilities;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services
{
    public class CranePlacementService : ICranePlacementService
    {
        private readonly IOptimizationInputBuilder _inputBuilder;
        private readonly ICranePlacementEngine _placementEngine;
        private readonly IMultiStageGAOrchestrator _multiStageGAOrchestrator;
        private readonly IOptimizationScopeEngine _scopeEngine;
        private readonly ISceneService _sceneService;
        private readonly CoordinateService _coordinateService;
        private readonly IProjectWorkspaceService _workspaceService;
        private readonly IfcViewerViewModel _ifcViewer;
        private readonly IOptimizationHistoryLogger _historyLogger;

        public CranePlacementService(
            IOptimizationInputBuilder inputBuilder,
            ICranePlacementEngine placementEngine,
            IMultiStageGAOrchestrator multiStageGAOrchestrator,
            IOptimizationScopeEngine scopeEngine,
            ISceneService sceneService,
            CoordinateService coordinateService,
            IProjectWorkspaceService workspaceService,
            IfcViewerViewModel ifcViewer,
            IOptimizationHistoryLogger historyLogger)
        {
            _inputBuilder = inputBuilder;
            _placementEngine = placementEngine;
            _multiStageGAOrchestrator = multiStageGAOrchestrator;
            _scopeEngine = scopeEngine;
            _sceneService = sceneService;
            _coordinateService = coordinateService;
            _workspaceService = workspaceService;
            _ifcViewer = ifcViewer;
            _historyLogger = historyLogger;
        }

        public async Task<CranePlacementResult> RunOptimizationAsync(
            GASettings settings,
            Crane crane,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sceneElements = _sceneService.GetElements();
            if (sceneElements == null || sceneElements.Count == 0)
                throw new InvalidOperationException("No elements found in the scene.");

            var sceneZones = _sceneService.GetZones();

            // Filter elements using OptimizationScopeEngine before normalizing
            var selectedElements = _sceneService.GetSelectedElements();
            var selectedIds = (selectedElements != null && selectedElements.Count > 0)
                ? selectedElements.Select(e => e.ElementID).Where(id => !string.IsNullOrEmpty(id)).ToHashSet()
                : null;

            var filter = new OptimizationScopeFilter
            {
                SelectedElementIds = selectedIds
            };

            var filteredElements = _scopeEngine.FilterElements(sceneElements, filter);

            // Normalize elements to meters, applying strict GA input gating (Phase 2A requirement)
            var elements = new List<PrecastElement>();
            var blockedElements = new List<(PrecastElement element, string reason)>();

            for (int i = 0; i < filteredElements.Count; i++)
            {
                var element = filteredElements[i];

                //if (!element.IncludeInGa)
                //{
                //    blockedElements.Add((element, $"IncludeInGa flag is false (Role: {element.Role}, Reason: {element.NormalizationDecisionReason})"));
                //    continue;
                //}

                if (element.Role == CraneOptimizer.Domain.Diagnostics.IfcElementRole.NeedsReview)
                {
                    blockedElements.Add((element, "NeedsReview element blocked until user decision exists."));
                    continue;
                }

                if (element.Role == CraneOptimizer.Domain.Diagnostics.IfcElementRole.GroupingOnly)
                {
                    blockedElements.Add((element, "GroupingOnly container elements cannot enter GA."));
                    continue;
                }

                if (element.Role == CraneOptimizer.Domain.Diagnostics.IfcElementRole.InternalAuxiliary)
                {
                    blockedElements.Add((element, "Reinforcements/internal auxiliary elements cannot enter GA."));
                    continue;
                }

                if (element.Role == CraneOptimizer.Domain.Diagnostics.IfcElementRole.Excluded)
                {
                    blockedElements.Add((element, "Consolidated child components swallowed/excluded."));
                    continue;
                }

                if (!element.WeightTons.HasValue)
                {
                    blockedElements.Add((element, "Missing weight."));
                    continue;
                }

                if (element.WeightTons.Value <= 0)
                {
                    blockedElements.Add((element, "Explicit zero or negative weight."));
                    continue;
                }

                var hasExplicitTrustedWeight =
     element.WeightTrustLevel == CraneOptimizer.Domain.Diagnostics.IfcWeightTrustLevel.ExplicitQuantity ||
     element.WeightTrustLevel == CraneOptimizer.Domain.Diagnostics.IfcWeightTrustLevel.ExplicitProperty;

                var source = element.WeightSource ?? string.Empty;

                var hasTrustedRepairedFallback =
                    element.WeightTrustLevel == CraneOptimizer.Domain.Diagnostics.IfcWeightTrustLevel.EstimatedFallback_NotAllowedForGA
                    && element.WeightTons is > 0
                    && element.ElementWT is > 0
                    && !string.IsNullOrWhiteSpace(element.ElementMaterial)
                    && !element.ElementMaterial.Equals("N/A", StringComparison.OrdinalIgnoreCase)
                    && !element.ElementMaterial.Equals("<Unnamed>", StringComparison.OrdinalIgnoreCase)
                    && (
                        source.Contains("MaterialDensityFallback", StringComparison.OrdinalIgnoreCase)
                        || source.Contains("DensityFallback", StringComparison.OrdinalIgnoreCase)
                        || source.Contains("PostPropagation", StringComparison.OrdinalIgnoreCase)
                        || source.Contains("InheritedParent", StringComparison.OrdinalIgnoreCase)
                    );
                var isEffectivelyGaIncluded =
    element.IncludeInGa ||
    (
        element.Role == CraneOptimizer.Domain.Diagnostics.IfcElementRole.LiftableElement
        && hasTrustedRepairedFallback
    );

                if (!isEffectivelyGaIncluded)
                {
                    blockedElements.Add((element,
                        $"IncludeInGa flag is false and element was not repair-approved (Role: {element.Role}, TrustLevel: {element.WeightTrustLevel}, Source: {element.WeightSource}, Reason: {element.NormalizationDecisionReason})"));
                    continue;
                }


                if (!hasExplicitTrustedWeight && !hasTrustedRepairedFallback)
                {
                    blockedElements.Add((element,
                        $"Untrusted weight source/trust level (TrustLevel: {element.WeightTrustLevel}, Source: {element.WeightSource})."));
                    continue;
                }
                System.Diagnostics.Debug.WriteLine(
    $"[GA_GATE_PASS_CANDIDATE] Id={element.ElementID}, Role={element.Role}, IncludeInGa={element.IncludeInGa}, " +
    $"Trust={element.WeightTrustLevel}, Source='{element.WeightSource}', WeightTons={element.WeightTons}, " +
    $"ElementWT={element.ElementWT}, Material='{element.ElementMaterial}', COG=({element.ElementCOG?.X}, {element.ElementCOG?.Y}, {element.ElementCOG?.Z})");
                if (!_coordinateService.IsValidPoint(element.ElementCOG))
                {
                    blockedElements.Add((element, "Invalid COG coordinates."));
                    continue;
                }

                elements.Add(_coordinateService.NormalizeElementToMeters(element));
            }

            // Write detailed extraction and GA gating diagnostics breakdown summaries to Debug.WriteLine (Phase 2A requirement)
            System.Diagnostics.Debug.WriteLine("=== Phase 2A Genetic Algorithm Input Gating Diagnostics ===");
            System.Diagnostics.Debug.WriteLine($"Total Scene Elements: {sceneElements?.Count ?? 0}");
            System.Diagnostics.Debug.WriteLine($"Selected/Filtered Scene Elements: {filteredElements.Count}");
            System.Diagnostics.Debug.WriteLine($"GA-Ready Elements (Normalized & Passed Gating): {elements.Count}");
            System.Diagnostics.Debug.WriteLine($"GA-Blocked Elements: {blockedElements.Count}");
            foreach (var blocked in blockedElements.GroupBy(b => b.reason))
            {
                System.Diagnostics.Debug.WriteLine($"  - Reason: {blocked.Key} -> Count: {blocked.Count()}");
            }
            System.Diagnostics.Debug.WriteLine("==========================================================");

            if (filteredElements.Count > 0 && elements.Count == 0)
            {
                throw new InvalidOperationException(BuildGaEligibilityFailureMessage(
                    sceneElements.Count,
                    filteredElements.Count,
                    blockedElements));
            }

            // Normalize zones to meters
            var zones = new List<GreenAreaZone>(sceneZones.Count);
            for (int i = 0; i < sceneZones.Count; i++)
            {
                zones.Add(_coordinateService.NormalizeZoneToMeters(sceneZones[i]));
            }

            // Determine profiling logs folder path from ProjectWorkspaceService
            string? projectFolder = _workspaceService?.CurrentSession?.ProjectFolderPath;
            string logsDir;
            if (!string.IsNullOrWhiteSpace(projectFolder))
            {
                logsDir = System.IO.Path.Combine(projectFolder, "Logs", "GA");
            }
            else
            {
                logsDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CraneOptimizer", "Logs", "GA");
            }
            settings.ProfilingLogDirectory = logsDir;

            // Determine diagnostics reports folder path from ProjectWorkspaceService (Phase 8)
            string reportsDir;
            if (!string.IsNullOrWhiteSpace(projectFolder))
            {
                reportsDir = System.IO.Path.Combine(projectFolder, "Reports", "GA");
            }
            else
            {
                reportsDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CraneOptimizer", "Reports", "GA");
            }

            // Build input in normalized meters
            var input = _inputBuilder.Build(settings, crane, elements, zones);

            // Populate benchmark metadata (Phase 7C)
            var currentSession = _workspaceService?.CurrentSession;
            input.ProjectName = currentSession?.ProjectName;
            input.ProjectId = !string.IsNullOrWhiteSpace(currentSession?.ProjectGuid)
                ? currentSession.ProjectGuid
                : currentSession?.ProjectId.ToString("N");
            input.ProjectFolderPath = currentSession?.ProjectFolderPath;
            input.RawElementCount = sceneElements?.Count ?? 0;
            input.OptimizationScopeElementCount = filteredElements.Count;
            input.FeatureScopeElements = filteredElements;
            input.TotalElementsCount = sceneElements?.Count ?? 0;
            input.DiagnosticsReportsDirectory = reportsDir;

            string? ifcFileName = null;
            if (_workspaceService?.CurrentSession?.ProjectCard != null && !string.IsNullOrWhiteSpace(_workspaceService.CurrentSession.ProjectCard.IfcFilePath))
            {
                ifcFileName = System.IO.Path.GetFileName(_workspaceService.CurrentSession.ProjectCard.IfcFilePath);
            }
            if (string.IsNullOrWhiteSpace(ifcFileName) && _ifcViewer != null && !string.IsNullOrWhiteSpace(_ifcViewer.LoadedIfcFilePath))
            {
                ifcFileName = System.IO.Path.GetFileName(_ifcViewer.LoadedIfcFilePath);
            }
            input.IfcFileName = ifcFileName;

            // Run optimization in a Task
            var sw = System.Diagnostics.Stopwatch.StartNew();
            PlacementResult appResult;
            if (settings.EnableMultiStageDiscovery)
            {
                var multiStageResult = await _multiStageGAOrchestrator.RunAsync(
                    input,
                    crane,
                    settings,
                    settings.MultiStageCoverageTarget,
                    cancellationToken);
                appResult = multiStageResult.BestResult;
            }
            else
            {
                appResult = await Task.Run(
                    () => _placementEngine.RunOptimization(input, cancellationToken),
                    cancellationToken);
            }
            sw.Stop();

            // Map results back to UI DTOs
            var finalResult = MapPlacementResult(appResult, sceneElements, input.CraneSpec, input.Settings);

            // Log optimization run history
            if (!string.IsNullOrWhiteSpace(projectFolder))
            {
                try
                {
                    var bestPos = finalResult.Positions.FirstOrDefault();
                    var allLiftedElements = finalResult.Positions
                        .SelectMany(p => p.ElementResults)
                        .Where(e => e.Status == LiftStatus.Liftable)
                        .ToList();

                    double maxRadius = allLiftedElements.Any() ? allLiftedElements.Max(e => e.Radius) : 0.0;
                    double averageRadius = allLiftedElements.Any() ? allLiftedElements.Average(e => e.Radius) : 0.0;

                    var entry = new OptimizationRunEntry
                    {
                        Timestamp = DateTime.Now,
                        ProjectName = input.ProjectName ?? string.Empty,
                        IfcFileName = input.IfcFileName ?? string.Empty,
                        CraneModel = crane.Name ?? string.Empty,

                        PopulationSize = settings.PopulationSize,
                        MaxGenerations = settings.MaxGenerations,
                        MutationRate = settings.MutationRate,
                        CrossoverRate = settings.CrossoverRate,
                        EliteCount = (int)(settings.PopulationSize * 0.1),
                        TournamentSize = 3,
                        ConvergenceThreshold = settings.ConvergenceThresholdPercent,
                        EarlyStopGenerations = settings.EarlyStoppingGenerations,
                        WireLength = settings.WireLength,
                        AdditionalWt = settings.AdditionalWt,
                        SafetyFactor = settings.SafetyFactor,
                        DeratingRatio = settings.DeratingRatio,
                        MaxCranePositions = settings.MaxCranePositions,
                        MinimumRelocationDistance = settings.MinimumRelocationDistance,
                        MutationSigma = settings.MutationSigma,
                        GridStep = settings.GridStep,

                        CandidatePointsGenerated = GAProfiler.CandidatePointsGenerated,
                        ValidPointsGenerated = GAProfiler.ValidPointsGenerated,
                        FinalSolutionsCount = appResult.Positions.Count,
                        BestFitness = appResult.FitnessScore,
                        CoveragePercent = appResult.CoverageRatio * 100.0,
                        ExecutionTimeMs = sw.ElapsedMilliseconds,

                        TotalElements = appResult.TotalElementCount,
                        LiftableElements = appResult.Statistics.Liftable,
                        UnliftableElements = appResult.TotalElementCount - appResult.Statistics.Liftable,

                        BestPointX = bestPos?.X ?? 0.0,
                        BestPointY = bestPos?.Y ?? 0.0,
                        BestPointZ = bestPos?.Z ?? 0.0,
                        ElementsLiftedByBestPoint = bestPos?.LiftableCount ?? 0,
                        CoveragePercentBestPoint = appResult.TotalElementCount > 0 ? (double)(bestPos?.LiftableCount ?? 0) / appResult.TotalElementCount * 100.0 : 0.0,
                        AverageRadius = averageRadius,
                        MaxRadius = maxRadius,

                        GenerationReached = GAProfiler.ActualGenerationLoopCount,
                        StoppedByConvergence = GAProfiler.StoppedByConvergence,
                        StoppedByMaxGenerations = GAProfiler.StoppedByMaxGenerations
                    };

                    // Execute logging asynchronously in background context (failsafe)
                    _ = Task.Run(async () => await _historyLogger.LogRunAsync(entry, projectFolder));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[History Logger Pre-Check Error] {ex.Message}");
                }
            }

            // Map the newly generated per-project diagnostics path to DebugReportPath
            // (Note: This now points to the project-scoped GA diagnostics CSV instead of the old global one)
            finalResult.DebugReportPath = appResult.DiagnosticsReportPath;
            finalResult.DebugReportError = null;

            return finalResult;
        }

        private CranePlacementResult MapPlacementResult(
            PlacementResult appResult,
            List<PrecastElement> allElements,
            CraneSpecification craneSpec,
            GASettings settings)
        {
            var result = new CranePlacementResult
            {
                FitnessScore = appResult.FitnessScore,
                CoverageRatio = appResult.CoverageRatio,
                LiftedElementCount = appResult.LiftedElementCount,
                TotalElementCount = appResult.TotalElementCount,
                ReachabilityDiagnostics = appResult.ReachabilityDiagnostics,
                Statistics = new DTOs.LiftStatistics
                {
                    Liftable = appResult.Statistics.Liftable,
                    OutOfRadius = appResult.Statistics.OutOfRadius,
                    Overweight = appResult.Statistics.Overweight,
                    HeightFail = appResult.Statistics.HeightFail,
                    SpecialCase = appResult.Statistics.SpecialCase,
                    NotCharted = appResult.Statistics.NotCharted
                },
                Positions = new List<DTOs.CranePositionResult>(appResult.Positions.Count)
            };

            var elementLookup = allElements
                .Where(element => !string.IsNullOrWhiteSpace(element.ElementID))
                .GroupBy(element => element.ElementID, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var appPos in appResult.Positions)
            {
                var scenePoint = _coordinateService.DenormalizePoint(new Domain.Common.DomainPoint3D(appPos.X, appPos.Y, appPos.Z));
                var posResult = new DTOs.CranePositionResult
                {
                    X = scenePoint.X,
                    Y = scenePoint.Y,
                    Z = scenePoint.Z,
                    NewLiftableCount = appPos.NewLiftableCount,
                    ElementResults = appPos.ElementResults.Select(appElem =>
                    {
                        elementLookup.TryGetValue(appElem.ElementId, out var element);
                        var sceneCog = element == null
                            ? new Domain.Common.DomainPoint3D()
                            : _coordinateService.DenormalizePoint(element.ElementCOG);

                        return new DTOs.ElementLiftResult
                        {
                            ElementId = appElem.ElementId,
                            ElementMark = appElem.ElementMark,
                            ElementType = appElem.ElementType,
                            Weight = appElem.Weight,
                            CogX = sceneCog.X,
                            CogY = sceneCog.Y,
                            CogZ = sceneCog.Z,
                            Status = appElem.Status,
                            Radius = appElem.Radius,
                            BoomLength = appElem.BoomLength,
                            AvailableCapacity = appElem.AvailableCapacity,
                            RequiredCapacity = appElem.RequiredCapacity,
                            ReachStatus = appElem.ReachStatus,
                            ChartStatus = appElem.ChartStatus,
                            IsExactMatch = appElem.IsExactMatch,
                            IsSteppedRadius = appElem.IsSteppedRadius,
                            IsBlankCell = appElem.IsBlankCell,
                            IsZeroCell = appElem.IsZeroCell,
                            SafetyMargin = appElem.SafetyMargin,
                            LookupMode = appElem.LookupMode,
                            RequiredRadius = appElem.RequiredRadius,
                            SelectedChartRadius = appElem.SelectedChartRadius,
                            SelectedChartBoomLength = appElem.SelectedChartBoomLength,
                            CraneBaseX = appElem.CraneBaseX,
                            CraneBaseY = appElem.CraneBaseY,
                            CraneBaseZ = appElem.CraneBaseZ,
                            SlewCenterX = appElem.SlewCenterX,
                            SlewCenterY = appElem.SlewCenterY,
                            SlewCenterZ = appElem.SlewCenterZ,
                            BoomHingeX = appElem.BoomHingeX,
                            BoomHingeY = appElem.BoomHingeY,
                            BoomHingeZ = appElem.BoomHingeZ,
                            LoadPointX = appElem.LoadPointX,
                            LoadPointY = appElem.LoadPointY,
                            LoadPointZ = appElem.LoadPointZ,
                            RadiusOriginType = appElem.RadiusOriginType,
                            BoomOriginType = appElem.BoomOriginType,
                            CandidateBoomsChecked = appElem.CandidateBoomsChecked,
                            RejectedCandidates = appElem.RejectedCandidates,
                            
                            // GA Assignment and Uniqueness Fields
                            CandidatePointId = appElem.CandidatePointId,
                            CandidatePointIndex = appElem.CandidatePointIndex,
                            IsFeasibleFromPoint = appElem.IsFeasibleFromPoint,
                            IsFinalAssignedToPoint = appElem.IsFinalAssignedToPoint,
                            AssignedPointId = appElem.AssignedPointId,
                            AssignedPointIndex = appElem.AssignedPointIndex,
                            AssignmentReason = appElem.AssignmentReason,
                            AssignmentScore = appElem.AssignmentScore,
                            DuplicateAssignmentResolved = appElem.DuplicateAssignmentResolved,
                            FeasiblePointIds = appElem.FeasiblePointIds,
                            FinalOwnerPointId = appElem.FinalOwnerPointId,
                            IsFinalAssigned = appElem.IsFinalAssigned,
                            BestFailedPointId = appElem.BestFailedPointId,
                            BestFailureReason = appElem.BestFailureReason,
                            
                            HasBestFailedCandidate = appElem.HasBestFailedCandidate,
                            BestFailedLiftStatus = appElem.BestFailedLiftStatus,
                            BestFailedReachStatus = appElem.BestFailedReachStatus,
                            BestFailedChartStatus = appElem.BestFailedChartStatus,
                            BestFailedSafetyMargin = appElem.BestFailedSafetyMargin,
                            BestFailedRequiredRadius = appElem.BestFailedRequiredRadius,
                            BestFailedRequiredBoomLength = appElem.BestFailedRequiredBoomLength
                        };
                    }).ToList()
                };
                result.Positions.Add(posResult);
            }

            return result;
        }

        private static void WriteDebugReport(CranePlacementResult result)
        {
            try
            {
                result.DebugReportPath = GADebugReportWriter.Write(result);
                result.DebugReportError = null;
            }
            catch (Exception ex)
            {
                result.DebugReportPath = null;
                result.DebugReportError = ex.Message;
            }
        }

        private static string BuildGaEligibilityFailureMessage(
            int sceneElementCount,
            int filteredElementCount,
            IReadOnlyList<(PrecastElement element, string reason)> blockedElements)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Elements are visible in the IFC viewer, but none are eligible for optimization.");
            sb.AppendLine($"Visible scene elements: {sceneElementCount}. Selected/filtered elements checked: {filteredElementCount}. Blocked elements: {blockedElements.Count}.");

            var firstReason = blockedElements.Count > 0 ? blockedElements[0].reason : "Unknown.";
            sb.AppendLine($"First rule that converted a visible element to non-eligible: {firstReason}");
            sb.AppendLine("Blocked reasons:");

            foreach (var group in blockedElements.GroupBy(b => b.reason).Take(8))
            {
                sb.AppendLine($"- {group.Count()} element(s): {group.Key}");
            }

            var sampleBlocked = blockedElements
                .Where(b => b.element != null)
                .Take(5)
                .Select(b =>
                {
                    var id = string.IsNullOrWhiteSpace(b.element.ElementID) ? "<missing id>" : b.element.ElementID;
                    var name = string.IsNullOrWhiteSpace(b.element.ElementMark) ? b.element.ElementType : b.element.ElementMark;
                    return $"{id} ({name})";
                })
                .ToList();

            if (sampleBlocked.Count > 0)
            {
                sb.AppendLine("Sample blocked elements:");
                foreach (var sample in sampleBlocked)
                {
                    sb.AppendLine($"- {sample}");
                }
            }

            sb.Append("Check the IFC normalization report for Role, IncludeInGa, WeightTrustLevel, and DecisionReason.");
            return sb.ToString();
        }
    }
}
