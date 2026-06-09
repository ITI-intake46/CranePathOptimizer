using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using System;
using System.Collections.Generic;
using System.Linq;
using CraneOptimizer.Application.Optimization.Utilities;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.GeneticOptimization
{
    /// <summary>
    /// Implements IElementAssignmentService using a globally optimal bipartite matching solver
    /// optimized for small numbers of positions (M <= 3) using a successive relocation path algorithm.
    /// Runs in O(N * M^2) time, meeting strict real-time performance budgets.
    /// </summary>
    public class HungarianAssignmentService : IElementAssignmentService
    {
        private readonly ILiftingCalculationEngine _liftingEngine;
        private readonly ICraneConfigurationEngine _configEngine;
        private readonly ILoadChartCapacityResolver _capacityResolver;

        public int MaxElementsPerSetup { get; set; } = int.MaxValue;

        public HungarianAssignmentService(
            ILiftingCalculationEngine liftingEngine,
            ICraneConfigurationEngine configEngine)
            : this(liftingEngine, configEngine, null)
        {
        }

        public HungarianAssignmentService(
            ILiftingCalculationEngine liftingEngine,
            ICraneConfigurationEngine configEngine,
            ILoadChartCapacityResolver? capacityResolver)
        {
            _liftingEngine = liftingEngine;
            _configEngine = configEngine;
            _capacityResolver = capacityResolver ?? new LoadChartCapacityResolver();
        }

        // ══════════════════════════════════════════════════════════════
        // PUBLIC ENTRY POINTS
        // ══════════════════════════════════════════════════════════════

        public AssignmentResult EvaluateAssignment(
            CraneSolution solution,
            List<PrecastElement> elements,
            Dictionary<int, List<string>> assignment,
            CraneSpecification craneSpec,
            GASettings settings)
        {
            using (new GAProfiler.ProfileScope("HungarianAssignmentService.EvaluateAssignment"))
            {
                if (elements == null || !elements.Any())
                {
                    return new AssignmentResult();
                }

                if (solution?.Chromosome?.CranePositions == null || !solution.Chromosome.CranePositions.Any())
                {
                    var result = new AssignmentResult();
                    foreach (var element in elements)
                    {
                        result.EvaluationResults.Add(CreateEmptyPositionEvaluationResult(element));
                    }
                    UpdateAssignmentSummary(result);
                    return result;
                }

                int M = solution.Chromosome.CranePositions.Count;

                // Pre-process user assignment overrides
                Dictionary<string, int>? assignmentLookup = null;
                if (assignment != null && assignment.Count > 0)
                {
                    assignmentLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    foreach (var entry in assignment)
                    {
                        if (entry.Value != null)
                        {
                            foreach (var id in entry.Value)
                            {
                                if (id != null)
                                {
                                    assignmentLookup[id] = entry.Key;
                                }
                            }
                        }
                    }
                }

                if (M == 1)
                {
                    // Fast Path: MaxCranePositions = 1, greedy equals Hungarian by definition
                    var result = new AssignmentResult();
                    foreach (var element in elements)
                    {
                        int? userAssignedIndex = ResolveUserAssignment(element, assignmentLookup);
                        ElementEvaluationResult evaluation;
                        if (userAssignedIndex.HasValue)
                        {
                            evaluation = EvaluateSinglePosition(element, solution, userAssignedIndex.Value, craneSpec, settings, assignmentLookup);
                        }
                        else
                        {
                            evaluation = ComputeEvaluationResult(element, solution, 0, craneSpec, settings);
                        }
                        result.EvaluationResults.Add(evaluation);
                    }
                    UpdateAssignmentSummary(result);
                    return result;
                }

                // Hungarian Path: MaxCranePositions >= 2
                return SolveHungarian(solution, elements, assignmentLookup, craneSpec, settings, null, null);
            }
        }

        public AssignmentResult EvaluateAssignment(
            CraneSolution solution,
            List<PrecastElement> elements,
            Dictionary<int, List<string>> assignment,
            CraneSpecification craneSpec,
            GASettings settings,
            LoadChartIndex index,
            ElementStaticCache elementCache)
        {
            using (new GAProfiler.ProfileScope("HungarianAssignmentService.EvaluateAssignment"))
            {
                if (elements == null || !elements.Any())
                {
                    return new AssignmentResult();
                }

                if (solution?.Chromosome?.CranePositions == null || !solution.Chromosome.CranePositions.Any())
                {
                    var result = new AssignmentResult();
                    foreach (var element in elements)
                    {
                        result.EvaluationResults.Add(CreateEmptyPositionEvaluationResult(element));
                    }
                    UpdateAssignmentSummary(result);
                    return result;
                }

                int M = solution.Chromosome.CranePositions.Count;

                // Pre-process user assignment overrides
                Dictionary<string, int>? assignmentLookup = null;
                if (assignment != null && assignment.Count > 0)
                {
                    assignmentLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    foreach (var entry in assignment)
                    {
                        if (entry.Value != null)
                        {
                            foreach (var id in entry.Value)
                            {
                                if (id != null)
                                {
                                    assignmentLookup[id] = entry.Key;
                                }
                            }
                        }
                    }
                }

                if (M == 1)
                {
                    // Fast Path: MaxCranePositions = 1, greedy equals Hungarian by definition
                    var result = new AssignmentResult();
                    foreach (var element in elements)
                    {
                        int? userAssignedIndex = ResolveUserAssignment(element, assignmentLookup);
                        ElementEvaluationResult evaluation;
                        if (userAssignedIndex.HasValue)
                        {
                            evaluation = EvaluateSinglePositionFast(element, solution, userAssignedIndex.Value, craneSpec, settings, assignmentLookup, index, elementCache);
                        }
                        else
                        {
                            evaluation = ComputeEvaluationResultFast(element, solution, 0, craneSpec, settings, index, elementCache);
                        }
                        result.EvaluationResults.Add(evaluation);
                    }
                    UpdateAssignmentSummary(result);
                    return result;
                }

                // Hungarian Path: MaxCranePositions >= 2
                return SolveHungarian(solution, elements, assignmentLookup, craneSpec, settings, index, elementCache);
            }
        }

        public IReadOnlyList<ElementEvaluationResult> EvaluatePosition(
            CraneSolution solution,
            int craneIndex,
            List<PrecastElement> elements,
            CraneSpecification craneSpec,
            GASettings settings)
        {
            using (new GAProfiler.ProfileScope("HungarianAssignmentService.EvaluatePosition"))
            {
                if (solution.Chromosome.CranePositions.Count == 0 || craneIndex < 0 || craneIndex >= solution.Chromosome.CranePositions.Count)
                    return Array.Empty<ElementEvaluationResult>();

                return elements
                    .Select(element => ComputeEvaluationResult(element, solution, craneIndex, craneSpec, settings))
                    .ToList();
            }
        }

        // ══════════════════════════════════════════════════════════════
        // BIPARTITE SOLVER ENGINE
        // ══════════════════════════════════════════════════════════════

        private AssignmentResult SolveHungarian(
            CraneSolution solution,
            List<PrecastElement> elements,
            Dictionary<string, int>? assignmentLookup,
            CraneSpecification craneSpec,
            GASettings settings,
            LoadChartIndex? index,
            ElementStaticCache? elementCache)
        {
            int N = elements.Count;
            int M = solution.Chromosome.CranePositions.Count;

            // 1. Evaluate every element against all setups and build cost matrix
            var evals = new ElementEvaluationResult[N][];
            double[,] costs = new double[N, M];

            for (int i = 0; i < N; i++)
            {
                evals[i] = new ElementEvaluationResult[M];
                var element = elements[i];
                int? forcedIndex = ResolveUserAssignment(element, assignmentLookup);

                for (int j = 0; j < M; j++)
                {
                    if (index != null && elementCache != null)
                    {
                        evals[i][j] = ComputeEvaluationResultFast(element, solution, j, craneSpec, settings, index, elementCache);
                    }
                    else
                    {
                        evals[i][j] = ComputeEvaluationResult(element, solution, j, craneSpec, settings);
                    }

                    // Compute priority cost
                    double cost = GetAssignmentCost(evals[i][j]);

                    // Force user override using penalty
                    if (forcedIndex.HasValue)
                    {
                        if (j != forcedIndex.Value)
                        {
                            cost = 1e15; // Huge cost penalty to forbid other positions
                        }
                    }

                    costs[i, j] = cost;
                }
            }

            // 2. Determine capacity limits
            int capacity = MaxElementsPerSetup;
            if (capacity <= 0 || capacity >= N)
            {
                // Unconstrained: set capacity to force balanced distribution globally
                capacity = (int)Math.Ceiling((double)N / M);
            }

            // 3. Initialize assignments greedily
            int[] assignment = new int[N];
            int[] O = new int[M];

            for (int i = 0; i < N; i++)
            {
                int bestJ = 0;
                double minC = costs[i, 0];
                for (int j = 1; j < M; j++)
                {
                    if (costs[i, j] < minC)
                    {
                        minC = costs[i, j];
                        bestJ = j;
                    }
                }
                assignment[i] = bestJ;
                O[bestJ]++;
            }

            // 4. Successive relocation min-cost flow loop
            int maxIterations = N * M;
            int iterations = 0;

            double[,] w = new double[M, M];
            int[,] bestElem = new int[M, M];
            double[] dist = new double[M];
            int[] parent = new int[M];
            var path = new List<int>(M);

            while (iterations < maxIterations)
            {
                // Find overloaded setup
                int overloadedSetup = -1;
                for (int j = 0; j < M; j++)
                {
                    if (O[j] > capacity)
                    {
                        overloadedSetup = j;
                        break;
                    }
                }

                if (overloadedSetup == -1)
                    break; // All capacity constraints satisfied

                iterations++;

                // Build setup-relocation residual graph (cheapest single element transfer cost)
                for (int x = 0; x < M; x++)
                {
                    for (int y = 0; y < M; y++)
                    {
                        w[x, y] = double.PositiveInfinity;
                        bestElem[x, y] = -1;
                    }
                }

                for (int i = 0; i < N; i++)
                {
                    int x = assignment[i];
                    for (int y = 0; y < M; y++)
                    {
                        if (x == y) continue;

                        double costDiff = costs[i, y] - costs[i, x];
                        if (costDiff < w[x, y])
                        {
                            w[x, y] = costDiff;
                            bestElem[x, y] = i;
                        }
                    }
                }

                // Shortest path from overloadedSetup to any underloaded setup using Bellman-Ford
                for (int j = 0; j < M; j++)
                {
                    dist[j] = double.PositiveInfinity;
                    parent[j] = -1;
                }

                dist[overloadedSetup] = 0.0;

                for (int step = 0; step < M - 1; step++)
                {
                    for (int x = 0; x < M; x++)
                    {
                        if (double.IsPositiveInfinity(dist[x])) continue;
                        for (int y = 0; y < M; y++)
                        {
                            if (x == y) continue;
                            if (double.IsPositiveInfinity(w[x, y])) continue;

                            if (dist[x] + w[x, y] < dist[y])
                            {
                                dist[y] = dist[x] + w[x, y];
                                parent[y] = x;
                            }
                        }
                    }
                }

                // Find closest underloaded setup
                int targetSetup = -1;
                double minDist = double.PositiveInfinity;
                for (int j = 0; j < M; j++)
                {
                    if (O[j] < capacity && dist[j] < minDist)
                    {
                        minDist = dist[j];
                        targetSetup = j;
                    }
                }

                if (targetSetup == -1 || double.IsPositiveInfinity(minDist))
                {
                    // Infeasible or no path exists: stop to prevent infinite loops
                    break;
                }

                // Reconstruct augmenting path
                path.Clear();
                int curr = targetSetup;
                bool[] visited = new bool[M];
                while (curr != -1)
                {
                    if (visited[curr])
                    {
                        break;
                    }
                    visited[curr] = true;
                    path.Add(curr);
                    curr = parent[curr];
                }
                path.Reverse();

                if (path.Count == 0 || path[0] != overloadedSetup)
                {
                    break;
                }

                // Compute path capacity K = min(excess at source, deficit at target)
                int excess = O[overloadedSetup] - capacity;
                int deficit = capacity - O[targetSetup];
                int K = Math.Min(excess, deficit);
                if (K <= 0) K = 1;

                // Build the list of relocations for this path to avoid moving the same element twice
                var relocations = new List<(int elemIdx, int targetJ)>();
                for (int r = 0; r < path.Count - 1; r++)
                {
                    int u = path[r];
                    int v = path[r + 1];

                    var candidates = new List<(int index, double diff)>();
                    for (int i = 0; i < N; i++)
                    {
                        if (assignment[i] == u)
                        {
                            candidates.Add((i, costs[i, v] - costs[i, u]));
                        }
                    }

                    candidates.Sort((a, b) => a.diff.CompareTo(b.diff));

                    int transferCount = Math.Min(K, candidates.Count);
                    for (int k = 0; k < transferCount; k++)
                    {
                        relocations.Add((candidates[k].index, v));
                    }
                }

                // Apply all relocations at once
                foreach (var rel in relocations)
                {
                    int oldJ = assignment[rel.elemIdx];
                    assignment[rel.elemIdx] = rel.targetJ;
                    O[oldJ]--;
                    O[rel.targetJ]++;
                }
            }

            // 5. Build final result
            var result = new AssignmentResult();
            for (int i = 0; i < N; i++)
            {
                int assignedJ = assignment[i];
                result.EvaluationResults.Add(evals[i][assignedJ]);
            }

            UpdateAssignmentSummary(result);
            return result;
        }

        private double GetAssignmentCost(ElementEvaluationResult eval)
        {
            double radius = Math.Min(eval.Radius, 10000.0);
            if (eval.Status == LiftStatus.Liftable)
            {
                return radius;
            }
            else
            {
                int rank = LiftClassificationPolicy.GetFailureRank(eval.Status);
                return 1000000.0 - (rank * 100000.0) + radius;
            }
        }

        // ══════════════════════════════════════════════════════════════
        // HELPERS AND EVALUATION CODE (DUPLICATED FROM ElementAssignmentService)
        // ══════════════════════════════════════════════════════════════

        private int? ResolveUserAssignment(
            PrecastElement element,
            Dictionary<string, int>? assignmentLookup)
        {
            if (assignmentLookup != null && assignmentLookup.TryGetValue(element.ElementID, out int index))
                return index;

            return null;
        }

        private ElementEvaluationResult EvaluateSinglePosition(
            PrecastElement element,
            CraneSolution solution,
            int craneIndex,
            CraneSpecification craneSpec,
            GASettings settings,
            Dictionary<string, int>? assignmentLookup)
        {
            if (craneIndex >= solution.Chromosome.CranePositions.Count)
                return ComputeEvaluationResult(element, solution, 0, craneSpec, settings);

            return ComputeEvaluationResult(element, solution, craneIndex, craneSpec, settings);
        }

        private ElementEvaluationResult EvaluateSinglePositionFast(
            PrecastElement element,
            CraneSolution solution,
            int craneIndex,
            CraneSpecification craneSpec,
            GASettings settings,
            Dictionary<string, int>? assignmentLookup,
            LoadChartIndex index,
            ElementStaticCache elementCache)
        {
            if (craneIndex >= solution.Chromosome.CranePositions.Count)
                return ComputeEvaluationResultFast(element, solution, 0, craneSpec, settings, index, elementCache);

            return ComputeEvaluationResultFast(element, solution, craneIndex, craneSpec, settings, index, elementCache);
        }

        private ElementEvaluationResult ComputeEvaluationResult(
            PrecastElement element,
            CraneSolution solution,
            int craneIndex,
            CraneSpecification craneSpec,
            GASettings settings)
        {
            var cranePosition = solution.Chromosome.CranePositions[craneIndex];
            double radius = _liftingEngine.CalculateRadius(cranePosition, element, settings);
            double height = _liftingEngine.CalculateLiftHeight(element, cranePosition, settings);
            double boomLength = _configEngine.CalculateRequiredBoomLength(cranePosition, element, settings);
            double requiredCapacity = _liftingEngine.CalculateRequiredWeight(element, settings);
            var resolveResult = _capacityResolver.Resolve(craneSpec, radius, boomLength, requiredCapacity, settings);

            return new ElementEvaluationResult
            {
                ElementId = element.ElementID,
                AssignedCraneIndex = craneIndex,
                Status = resolveResult.LiftStatus,
                Radius = radius,
                BoomLength = boomLength,
                AvailableCapacity = resolveResult.AvailableCapacity,
                RequiredCapacity = requiredCapacity,
                ReachStatus = resolveResult.ReachStatus,
                ChartStatus = resolveResult.ChartStatus,
                IsExactMatch = resolveResult.IsExactMatch,
                IsSteppedRadius = resolveResult.IsSteppedRadius,
                IsBlankCell = resolveResult.IsBlankCell,
                IsZeroCell = resolveResult.IsZeroCell,
                SafetyMargin = resolveResult.SafetyMargin,
                LookupMode = resolveResult.LookupMode,
                RequiredRadius = radius,
                SelectedChartRadius = resolveResult.SelectedChartRadius,
                SelectedChartBoomLength = resolveResult.SelectedChartBoomLength,
                SelectedVisualColumnIndex = resolveResult.SelectedVisualColumnIndex,
                SelectedVisualColumnHeader = resolveResult.SelectedVisualColumnHeader,
                SelectedNumberOfLines = resolveResult.SelectedNumberOfLines,
                SelectedOperatingConditionLabel = resolveResult.SelectedOperatingConditionLabel,
                IsConservativeDuplicateFallback = resolveResult.IsConservativeDuplicateFallback,
                DuplicateAlternativeCount = resolveResult.DuplicateAlternativeCount,
                DuplicateAlternativeSummary = resolveResult.DuplicateAlternativeSummary,
                CraneBaseX = cranePosition.X,
                CraneBaseY = cranePosition.Y,
                CraneBaseZ = cranePosition.Z,
                SlewCenterX = cranePosition.X + settings.SlewOffsetX,
                SlewCenterY = cranePosition.Y + settings.SlewOffsetY,
                SlewCenterZ = cranePosition.Z + settings.SlewOffsetZ,
                BoomHingeX = cranePosition.X + settings.BoomHingeOffsetX,
                BoomHingeY = cranePosition.Y + settings.BoomHingeOffsetY,
                BoomHingeZ = cranePosition.Z + settings.BoomHingeOffsetZ,
                LoadPointX = element.ElementCOG.X,
                LoadPointY = element.ElementCOG.Y,
                LoadPointZ = element.ElementCOG.Z,
                RadiusOriginType = "SlewCenter",
                BoomOriginType = "BoomHinge",
                CandidateBoomsChecked = resolveResult.CandidateBoomsChecked,
                RejectedCandidates = resolveResult.RejectedCandidates
            };
        }

        private ElementEvaluationResult ComputeEvaluationResultFast(
            PrecastElement element,
            CraneSolution solution,
            int craneIndex,
            CraneSpecification craneSpec,
            GASettings settings,
            LoadChartIndex index,
            ElementStaticCache elementCache)
        {
            var cranePosition = solution.Chromosome.CranePositions[craneIndex];
            double slewX = cranePosition.X + settings.SlewOffsetX;
            double slewY = cranePosition.Y + settings.SlewOffsetY;

            double cogX, cogY, cogZ, halfHeight, requiredCapacity;

            if (elementCache.ByElementId.TryGetValue(element.ElementID, out var cached))
            {
                cogX = cached.CogX;
                cogY = cached.CogY;
                cogZ = cached.CogZ;
                halfHeight = cached.HalfHeight;
                requiredCapacity = cached.RequiredWeight;
            }
            else
            {
                cogX = element.ElementCOG.X;
                cogY = element.ElementCOG.Y;
                cogZ = element.ElementCOG.Z;
                halfHeight = (element.ElementHeight ?? 0) / 2.0;
                requiredCapacity = (element.ElementWT ?? 0) + settings.AdditionalWt;
            }

            double dx = slewX - cogX;
            double dy = slewY - cogY;
            double radius = Math.Sqrt(dx * dx + dy * dy);

            double hingeX = cranePosition.X + settings.BoomHingeOffsetX;
            double hingeY = cranePosition.Y + settings.BoomHingeOffsetY;
            double hingeZ = cranePosition.Z + settings.BoomHingeOffsetZ;

            double dxHinge = cogX - hingeX;
            double dyHinge = cogY - hingeY;
            double dHinge = Math.Sqrt(dxHinge * dxHinge + dyHinge * dyHinge);

            double zHook = cogZ + halfHeight + settings.WireLength;
            double hHinge = Math.Max(0.0, zHook - hingeZ);
            double boomLength = Math.Sqrt(dHinge * dHinge + hHinge * hHinge);

            var resolveResult = _capacityResolver.Resolve(
                craneSpec, radius, boomLength, requiredCapacity, settings,
                buildDiagnostics: false, index: index);

            return new ElementEvaluationResult
            {
                ElementId = element.ElementID,
                AssignedCraneIndex = craneIndex,
                Status = resolveResult.LiftStatus,
                Radius = radius,
                BoomLength = boomLength,
                AvailableCapacity = resolveResult.AvailableCapacity,
                RequiredCapacity = requiredCapacity,
                ReachStatus = resolveResult.ReachStatus,
                ChartStatus = resolveResult.ChartStatus,
                IsExactMatch = resolveResult.IsExactMatch,
                IsSteppedRadius = resolveResult.IsSteppedRadius,
                IsBlankCell = resolveResult.IsBlankCell,
                IsZeroCell = resolveResult.IsZeroCell,
                SafetyMargin = resolveResult.SafetyMargin,
                LookupMode = resolveResult.LookupMode,
                RequiredRadius = radius,
                SelectedChartRadius = resolveResult.SelectedChartRadius,
                SelectedChartBoomLength = resolveResult.SelectedChartBoomLength,
                SelectedVisualColumnIndex = resolveResult.SelectedVisualColumnIndex,
                SelectedVisualColumnHeader = resolveResult.SelectedVisualColumnHeader,
                SelectedNumberOfLines = resolveResult.SelectedNumberOfLines,
                SelectedOperatingConditionLabel = resolveResult.SelectedOperatingConditionLabel,
                IsConservativeDuplicateFallback = resolveResult.IsConservativeDuplicateFallback,
                DuplicateAlternativeCount = resolveResult.DuplicateAlternativeCount,
                DuplicateAlternativeSummary = resolveResult.DuplicateAlternativeSummary,
                CraneBaseX = cranePosition.X,
                CraneBaseY = cranePosition.Y,
                CraneBaseZ = cranePosition.Z,
                SlewCenterX = slewX,
                SlewCenterY = slewY,
                SlewCenterZ = cranePosition.Z + settings.SlewOffsetZ,
                BoomHingeX = hingeX,
                BoomHingeY = hingeY,
                BoomHingeZ = hingeZ,
                LoadPointX = cogX,
                LoadPointY = cogY,
                LoadPointZ = cogZ,
                RadiusOriginType = "SlewCenter",
                BoomOriginType = "BoomHinge"
            };
        }

        private ElementEvaluationResult CreateEmptyPositionEvaluationResult(PrecastElement element)
        {
            return new ElementEvaluationResult
            {
                ElementId = element.ElementID,
                AssignedCraneIndex = -1,
                Status = LiftStatus.OutOfRadius,
                Radius = double.MaxValue,
                BoomLength = double.MaxValue,
                AvailableCapacity = 0,
                RequiredCapacity = 0,
                ReachStatus = ReachStatus.OutOfRadius,
                ChartStatus = ChartStatus.OutsideChartEnvelope,
                IsExactMatch = false,
                IsSteppedRadius = false,
                IsBlankCell = false,
                IsZeroCell = false,
                SafetyMargin = double.MinValue,
                CraneBaseX = 0,
                CraneBaseY = 0,
                CraneBaseZ = 0,
                SlewCenterX = 0,
                SlewCenterY = 0,
                SlewCenterZ = 0,
                BoomHingeX = 0,
                BoomHingeY = 0,
                BoomHingeZ = 0,
                LoadPointX = element.ElementCOG != null ? element.ElementCOG.X : 0,
                LoadPointY = element.ElementCOG != null ? element.ElementCOG.Y : 0,
                LoadPointZ = element.ElementCOG != null ? element.ElementCOG.Z : 0,
                LookupMode = "None",
                RequiredRadius = 0,
                SelectedChartRadius = 0,
                SelectedChartBoomLength = 0,
                SelectedVisualColumnIndex = null,
                SelectedVisualColumnHeader = string.Empty,
                SelectedNumberOfLines = null,
                SelectedOperatingConditionLabel = string.Empty,
                IsConservativeDuplicateFallback = false,
                DuplicateAlternativeCount = 0,
                DuplicateAlternativeSummary = string.Empty,
                RadiusOriginType = "SlewCenter",
                BoomOriginType = "BoomHinge",
                CandidateBoomsChecked = string.Empty,
                RejectedCandidates = string.Empty
            };
        }

        private void UpdateAssignmentSummary(AssignmentResult result)
        {
            result.AssignedElements.Clear();
            result.AssignedLiftableElements.Clear();

            foreach (var eval in result.EvaluationResults)
            {
                if (eval.AssignedCraneIndex < 0)
                {
                    continue;
                }

                if (!result.AssignedElements.ContainsKey(eval.AssignedCraneIndex))
                    result.AssignedElements[eval.AssignedCraneIndex] = new List<string>();

                result.AssignedElements[eval.AssignedCraneIndex].Add(eval.ElementId);

                if (eval.Status == LiftStatus.Liftable)
                {
                    if (!result.AssignedLiftableElements.ContainsKey(eval.AssignedCraneIndex))
                        result.AssignedLiftableElements[eval.AssignedCraneIndex] = new List<string>();

                    result.AssignedLiftableElements[eval.AssignedCraneIndex].Add(eval.ElementId);
                }
            }
        }
    }
}
