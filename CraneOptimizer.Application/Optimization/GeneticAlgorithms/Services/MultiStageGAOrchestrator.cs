using ProjectContext = CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs.PlacementInput;
using CraneModel = CraneOptimizer.Domain.Entities.Crane;
using GAResult = CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs.PlacementResult;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    /// <summary>
    /// Executes the multi-stage Genetic Algorithm optimization loop.
    /// Scales positions from 1 up to Math.Min(3, MaxCranePositions) and adapts relocation weights.
    /// </summary>
    public class MultiStageGAOrchestrator : IMultiStageGAOrchestrator
    {
        private readonly ICranePlacementEngine _placementEngine;

        public MultiStageGAOrchestrator(ICranePlacementEngine placementEngine)
        {
            _placementEngine = placementEngine;
        }

        public async Task<MultiStageGAResult> RunAsync(
            ProjectContext project,
            CraneModel crane,
            GASettings baseSettings,
            double coverageTarget,
            CancellationToken cancellationToken)
        {
            var allStageResults = new List<GAResult>();
            int stagesExecuted = 0;
            string terminationReason = "MaxStagesReached";
            string optimizationRunId = Guid.NewGuid().ToString();

            int targetMaxPositions = Math.Min(3, baseSettings.MaxCranePositions);
            if (targetMaxPositions < 1)
                targetMaxPositions = 1;

            GAResult? bestResult = null;

            try
            {
                for (int posCount = 1; posCount <= targetMaxPositions; posCount++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    stagesExecuted++;

                    // 1. Prepare settings for this stage
                    var stageSettings = baseSettings.Clone();
                    stageSettings.MaxCranePositions = posCount;
                    stageSettings.OptimizationRunId = optimizationRunId;
                    stageSettings.StageRunId = Guid.NewGuid().ToString();
                    stageSettings.StageIndex = stagesExecuted;

                    if (posCount == 1)
                    {
                        stageSettings.RelocationWeight = 0;
                    }
                    else
                    {
                        double previousCoverage = bestResult?.CoverageRatio ?? 0.0;
                        stageSettings.RelocationWeight = ComputeAdaptiveRelocationWeight(
                            previousCoverage,
                            baseSettings.CoverageWeight,
                            baseSettings.AdaptiveRelocationScaleFactor);
                    }

                    System.Diagnostics.Debug.WriteLine($"[MultiStageGAOrchestrator] Running Stage {stagesExecuted}: MaxCranePositions={posCount}, RelocationWeight={stageSettings.RelocationWeight:F4}, AdaptiveRelocationScaleFactor={baseSettings.AdaptiveRelocationScaleFactor:F4}");

                    // 2. Prepare input for this run
                    var stageInput = CloneInput(project, stageSettings);

                    // 3. Execute GA optimization run
                    var sw = Stopwatch.StartNew();
                    var runResult = await Task.Run(() => _placementEngine.RunOptimization(stageInput, cancellationToken), cancellationToken);
                    sw.Stop();

                    System.Diagnostics.Debug.WriteLine($"[MultiStageGAOrchestrator] Stage {stagesExecuted} Finished: Coverage={runResult.CoverageRatio:P2}, ActivePositions={runResult.Positions.Count}, Fitness={runResult.FitnessScore:F4}, Time={sw.ElapsedMilliseconds}ms");

                    allStageResults.Add(runResult);

                    // Track best result
                    if (bestResult == null || IsBetterResult(runResult, bestResult))
                    {
                        bestResult = runResult;
                    }

                    // 4. Evaluate coverage target
                    if (runResult.CoverageRatio >= coverageTarget)
                    {
                        terminationReason = "CoverageTargetMet";
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                terminationReason = "Cancelled";
                if (bestResult == null)
                    throw; // Propagate if we don't have any completed stage results
            }

            if (bestResult == null)
            {
                throw new InvalidOperationException("No GA optimization stages completed successfully.");
            }

            return new MultiStageGAResult(
                bestResult,
                stagesExecuted,
                allStageResults,
                terminationReason
            );
        }

        public static double ComputeAdaptiveRelocationWeight(double previousCoverage, double coverageWeight, double scaleFactor)
        {
            double weight = (1.0 - previousCoverage) * coverageWeight * scaleFactor;
            // Guard: Assert adaptiveWeight is always strictly less than CoverageWeight
            if (weight >= coverageWeight)
            {
                weight = coverageWeight * 0.99;
            }
            return weight;
        }

        private static bool IsBetterResult(GAResult candidate, GAResult best)
        {
            if (candidate.CoverageRatio > best.CoverageRatio)
                return true;
            if (candidate.CoverageRatio < best.CoverageRatio)
                return false;

            // Ties are extremely rare but we prefer fewer positions, then higher fitness score
            if (candidate.Positions.Count < best.Positions.Count)
                return true;
            if (candidate.Positions.Count > best.Positions.Count)
                return false;

            return candidate.FitnessScore > best.FitnessScore;
        }

        private static ProjectContext CloneInput(ProjectContext source, GASettings settings)
        {
            return new ProjectContext
            {
                Zones = source.Zones,
                ForbiddenZones = source.ForbiddenZones,
                Elements = source.Elements,
                Assignment = source.Assignment,
                CraneSpec = source.CraneSpec,
                Settings = settings,
                ProjectName = source.ProjectName,
                ProjectId = source.ProjectId,
                ProjectFolderPath = source.ProjectFolderPath,
                IfcFileName = source.IfcFileName,
                ScenarioId = null,
                ProjectFeatureSnapshotId = source.ProjectFeatureSnapshotId,
                ScopeDefinitionHash = source.ScopeDefinitionHash,
                RawElementCount = source.RawElementCount,
                OptimizationScopeElementCount = source.OptimizationScopeElementCount,
                RequestedMaxCranePositions = source.RequestedMaxCranePositions,
                FeatureScopeElements = source.FeatureScopeElements,
                TotalElementsCount = source.TotalElementsCount,
                DiagnosticsReportsDirectory = source.DiagnosticsReportsDirectory
            };
        }
    }
}
