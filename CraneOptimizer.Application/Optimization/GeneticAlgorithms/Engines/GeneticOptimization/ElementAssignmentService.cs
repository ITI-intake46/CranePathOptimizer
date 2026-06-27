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
    /// Evaluates whether building elements can be lifted
    /// by the crane positions defined in a GA solution.
    ///
    /// Assignment strategy — Best Position Selection:
    ///
    ///   For each element:
    ///     1. Check if the user has manually assigned it to a specific crane index.
    ///        If yes → evaluate that position only (user override).
    ///     2. If no assignment → try ALL crane positions in the chromosome.
    ///        Select the best feasible position using this priority:
    ///          Priority 1 → Liftable   (prefer any position that can lift)
    ///          Priority 2 → smallest radius among Liftable positions
    ///          Priority 3 → if no Liftable exists, return the least-bad failure
    /// </summary>
    public class ElementAssignmentService : IElementAssignmentService
    {
        private readonly ILiftingCalculationEngine _liftingEngine;
        private readonly ICraneConfigurationEngine _configEngine;
        private readonly ILoadChartCapacityResolver _capacityResolver;

        public ElementAssignmentService(
            ILiftingCalculationEngine liftingEngine,
            ICraneConfigurationEngine configEngine)
            : this(liftingEngine, configEngine, null)
        {
        }

        public ElementAssignmentService(
            ILiftingCalculationEngine liftingEngine,
            ICraneConfigurationEngine configEngine,
            ILoadChartCapacityResolver? capacityResolver)
        {
            _liftingEngine = liftingEngine;
            _configEngine = configEngine;
            _capacityResolver = capacityResolver ?? new LoadChartCapacityResolver();
        }

        // ══════════════════════════════════════════════════════════════
        // PUBLIC ENTRY POINT (original — full diagnostics)
        // ══════════════════════════════════════════════════════════════

        public AssignmentResult EvaluateAssignment(
            CraneSolution solution,
            List<PrecastElement> elements,
            Dictionary<int, List<string>> assignment,
            CraneSpecification craneSpec,
            GASettings settings)
        {
            using (new GAProfiler.ProfileScope("ElementAssignmentService.EvaluateAssignment"))
            {
                if (solution.Chromosome.CranePositions.Count == 0)
                {
                    System.Threading.Interlocked.Increment(ref GAProfiler.EmptyChromosomeCount);
                }

                var result = new AssignmentResult();

                // ── Optimization: pre-process assignment list into dictionary lookup ──
                Dictionary<string, int>? assignmentLookup = null;
                if (assignment != null && assignment.Count > 0)
                {
                    assignmentLookup = new Dictionary<string, int>(StringComparer.Ordinal);
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

                foreach (var element in elements)
                {
                    ElementEvaluationResult evaluation;

                    // ── Check for user-defined assignment ─────────────────
                    int? userAssignedIndex = ResolveUserAssignment(element, assignmentLookup);

                    if (userAssignedIndex.HasValue)
                    {
                        // User forced this element to a specific crane position
                        evaluation = EvaluateSinglePosition(
                            element,
                            solution,
                            userAssignedIndex.Value,
                            craneSpec,
                            settings,
                            assignmentLookup);
                    }
                    else
                    {
                        // No user override → try all positions and pick the best
                        evaluation = EvaluateBestPosition(
                            element,
                            solution,
                            craneSpec,
                            settings);
                    }

                    result.EvaluationResults.Add(evaluation);
                }

                UpdateAssignmentSummary(result);

                return result;
            }
        }

        // ══════════════════════════════════════════════════════════════
        // PUBLIC ENTRY POINT (hot-path — diagnostics disabled, indexed)
        // ══════════════════════════════════════════════════════════════

        public AssignmentResult EvaluateAssignment(
            CraneSolution solution,
            List<PrecastElement> elements,
            Dictionary<int, List<string>> assignment,
            CraneSpecification craneSpec,
            GASettings settings,
            LoadChartIndex index,
            ElementStaticCache elementCache)
        {
            using (new GAProfiler.ProfileScope("ElementAssignmentService.EvaluateAssignment"))
            {
                if (solution.Chromosome.CranePositions.Count == 0)
                {
                    System.Threading.Interlocked.Increment(ref GAProfiler.EmptyChromosomeCount);
                }

                var result = new AssignmentResult();

                // ── Optimization: pre-process assignment list into dictionary lookup ──
                Dictionary<string, int>? assignmentLookup = null;
                if (assignment != null && assignment.Count > 0)
                {
                    assignmentLookup = new Dictionary<string, int>(StringComparer.Ordinal);
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

                foreach (var element in elements)
                {
                    ElementEvaluationResult evaluation;

                    // ── Check for user-defined assignment ─────────────────
                    int? userAssignedIndex = ResolveUserAssignment(element, assignmentLookup);

                    if (userAssignedIndex.HasValue)
                    {
                        // User forced this element to a specific crane position
                        evaluation = EvaluateSinglePositionFast(
                            element,
                            solution,
                            userAssignedIndex.Value,
                            craneSpec,
                            settings,
                            assignmentLookup,
                            index,
                            elementCache);
                    }
                    else
                    {
                        // No user override → try all positions and pick the best
                        evaluation = EvaluateBestPositionFast(
                            element,
                            solution,
                            craneSpec,
                            settings,
                            index,
                            elementCache);
                    }

                    result.EvaluationResults.Add(evaluation);
                }

                UpdateAssignmentSummary(result);

                return result;
            }
        }

        public IReadOnlyList<ElementEvaluationResult> EvaluatePosition(
            CraneSolution solution,
            int craneIndex,
            List<PrecastElement> elements,
            CraneSpecification craneSpec,
            GASettings settings)
        {
            using (new GAProfiler.ProfileScope("ElementAssignmentService.EvaluatePosition"))
            {
                if (solution.Chromosome.CranePositions.Count == 0 || craneIndex < 0 || craneIndex >= solution.Chromosome.CranePositions.Count)
                    return Array.Empty<ElementEvaluationResult>();

                return elements
                    .Select(element => ComputeEvaluationResult(element, solution, craneIndex, craneSpec, settings))
                    .ToList();
            }
        }

        // ══════════════════════════════════════════════════════════════
        // USER ASSIGNMENT RESOLUTION
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Checks whether the user has manually assigned this element
        /// to a specific crane index.
        ///
        /// Returns the crane index if found, null if no assignment exists.
        /// </summary>
        private int? ResolveUserAssignment(
            PrecastElement element,
            Dictionary<string, int>? assignmentLookup)
        {
            if (assignmentLookup != null && assignmentLookup.TryGetValue(element.ElementID, out int index))
                return index;

            return null;
        }

        // ══════════════════════════════════════════════════════════════
        // SINGLE POSITION EVALUATION (user override — original path)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Evaluates the lift feasibility of one element
        /// against one specific crane position.
        ///
        /// Used when the user has manually assigned the element
        /// to a specific crane index.
        /// </summary>
        private ElementEvaluationResult EvaluateSinglePosition(
            PrecastElement element,
            CraneSolution solution,
            int craneIndex,
            CraneSpecification craneSpec,
            GASettings settings,
            Dictionary<string, int>? assignmentLookup)
        {
            // Guard: if craneIndex is out of range, fall back to best-position
            if (craneIndex >= solution.Chromosome.CranePositions.Count)
                return EvaluateBestPosition(element, solution, craneSpec, settings);

            return ComputeEvaluationResult(element, solution, craneIndex, craneSpec, settings);
        }

        /// <summary>
        /// Hot-path version: evaluates single position with index + cache, diagnostics disabled.
        /// </summary>
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
            // Guard: if craneIndex is out of range, fall back to best-position
            if (craneIndex >= solution.Chromosome.CranePositions.Count)
                return EvaluateBestPositionFast(element, solution, craneSpec, settings, index, elementCache);

            return ComputeEvaluationResultFast(element, solution, craneIndex, craneSpec, settings, index, elementCache);
        }

        // ══════════════════════════════════════════════════════════════
        // BEST POSITION SELECTION (automatic — main logic)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Tries all crane positions in the chromosome for this element
        /// and selects the best feasible one.
        /// </summary>
        private ElementEvaluationResult EvaluateBestPosition(
            PrecastElement element,
            CraneSolution solution,
            CraneSpecification craneSpec,
            GASettings settings)
        {
            if (solution.Chromosome.CranePositions.Count == 0)
            {
                return CreateEmptyPositionEvaluationResult(element);
            }

            ElementEvaluationResult? best = null;

            // ── Evaluate all crane positions ───────────────────────
            for (int i = 0; i < solution.Chromosome.CranePositions.Count; i++)
            {
                var candidate = ComputeEvaluationResult(
                    element, solution, i, craneSpec, settings);

                if (best == null || IsBetterThan(candidate, best))
                {
                    best = candidate;
                }
            }

            return best!;
        }

        /// <summary>
        /// Hot-path version: tries all positions with index + cache, diagnostics disabled.
        /// </summary>
        private ElementEvaluationResult EvaluateBestPositionFast(
            PrecastElement element,
            CraneSolution solution,
            CraneSpecification craneSpec,
            GASettings settings,
            LoadChartIndex index,
            ElementStaticCache elementCache)
        {
            if (solution.Chromosome.CranePositions.Count == 0)
            {
                return CreateEmptyPositionEvaluationResult(element);
            }

            ElementEvaluationResult? best = null;

            // ── Evaluate all crane positions ───────────────────────
            for (int i = 0; i < solution.Chromosome.CranePositions.Count; i++)
            {
                var candidate = ComputeEvaluationResultFast(
                    element, solution, i, craneSpec, settings, index, elementCache);

                if (best == null || IsBetterThan(candidate, best))
                {
                    best = candidate;
                }
            }

            return best!;
        }

        private bool IsBetterThan(ElementEvaluationResult candidate, ElementEvaluationResult best)
        {
            if (candidate.Status == LiftStatus.Liftable && best.Status != LiftStatus.Liftable)
                return true;

            if (candidate.Status != LiftStatus.Liftable && best.Status == LiftStatus.Liftable)
                return false;

            if (candidate.Status == LiftStatus.Liftable && best.Status == LiftStatus.Liftable)
            {
                // Both are liftable: prefer smaller radius
                if (candidate.Radius < best.Radius)
                    return true;
                if (candidate.Radius > best.Radius)
                    return false;
                
                // Tie breaker: smaller crane index
                return candidate.AssignedCraneIndex < best.AssignedCraneIndex;
            }

            // Both are failures: prefer higher failure rank
            int rankCand = GetFailureRank(candidate.Status);
            int rankBest = GetFailureRank(best.Status);
            if (rankCand > rankBest)
                return true;
            if (rankCand < rankBest)
                return false;

            // Same failure rank: prefer smaller radius
            if (candidate.Radius < best.Radius)
                return true;
            if (candidate.Radius > best.Radius)
                return false;

            // Tie breaker: smaller crane index
            return candidate.AssignedCraneIndex < best.AssignedCraneIndex;
        }

        /// <summary>
        /// Returns a numeric rank for non-Liftable statuses.
        /// Higher rank = closer to being liftable = preferred fallback.
        /// </summary>
        private int GetFailureRank(LiftStatus status)
        {
            return LiftClassificationPolicy.GetFailureRank(status);
        }

        // ══════════════════════════════════════════════════════════════
        // CORE LIFT COMPUTATION (original — full diagnostics)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Computes the full lift evaluation for one element
        /// against one crane position index.
        ///
        /// Steps:
        ///   1. Retrieve crane position from chromosome
        ///   2. Compute working radius       → ILiftingCalculationEngine.CalculateRadius
        ///   3. Compute lift height          → ILiftingCalculationEngine.CalculateLiftHeight
        ///   4. Compute required boom length → ICraneConfigurationEngine.CalculateRequiredBoomLength
        ///   5. Determine lift status        → ILiftingCalculationEngine.DetermineLiftStatus
        /// </summary>
        private ElementEvaluationResult ComputeEvaluationResult(
            PrecastElement element,
            CraneSolution solution,
            int craneIndex,
            CraneSpecification craneSpec,
            GASettings settings)
        {
            // Step 1 — Get crane position
            var cranePosition = solution.Chromosome.CranePositions[craneIndex];

            // Step 2 — Working radius (orientation-aware and slew-offset-aware)
            double radius = _liftingEngine.CalculateRadius(cranePosition, element, settings);

            // Step 3 — Vertical lift height
            double height = _liftingEngine.CalculateLiftHeight(element, cranePosition, settings);

            // Step 4 — Required boom length (hinge-offset-aware)
            double boomLength = _configEngine.CalculateRequiredBoomLength(cranePosition, element, settings);

            // Step 5 — Required capacity = element weight + lifting accessories
            double requiredCapacity = _liftingEngine.CalculateRequiredWeight(element, settings);

            // Step 6 — Resolve via ILoadChartCapacityResolver
            var resolveResult = _capacityResolver.Resolve(craneSpec, radius, boomLength, requiredCapacity, settings);

            return new ElementEvaluationResult
            {
                ElementId = element.ElementID,
                AssignedCraneIndex = craneIndex,
                Status = resolveResult.LiftStatus,
                Radius = radius,
                BoomLength = boomLength,
                AvailableCapacity = resolveResult.AvailableCapacity,
                RawChartCapacity = resolveResult.RawChartCapacity,
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

        // ══════════════════════════════════════════════════════════════
        // CORE LIFT COMPUTATION (hot-path — no diagnostics, indexed)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Hot-path version of ComputeEvaluationResult.
        /// Uses pre-built LoadChartIndex and ElementStaticCache.
        /// Passes buildDiagnostics=false to the resolver.
        /// </summary>
        private ElementEvaluationResult ComputeEvaluationResultFast(
            PrecastElement element,
            CraneSolution solution,
            int craneIndex,
            CraneSpecification craneSpec,
            GASettings settings,
            LoadChartIndex index,
            ElementStaticCache elementCache)
        {
            // Step 1 — Get crane position
            var cranePosition = solution.Chromosome.CranePositions[craneIndex];

            // Step 2 — Working radius using cached COG (avoid nullable property access)
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
                // Fallback: compute directly (should not happen in normal flow)
                cogX = element.ElementCOG.X;
                cogY = element.ElementCOG.Y;
                cogZ = element.ElementCOG.Z;
                halfHeight = (element.ElementHeight ?? 0) / 2.0;
                requiredCapacity = (element.ElementWT ?? 0) + settings.AdditionalWt;
            }

            double dx = slewX - cogX;
            double dy = slewY - cogY;
            double radius = Math.Sqrt(dx * dx + dy * dy);

            // Step 3 — Required boom length (hinge-offset-aware) using cached values
            double hingeX = cranePosition.X + settings.BoomHingeOffsetX;
            double hingeY = cranePosition.Y + settings.BoomHingeOffsetY;
            double hingeZ = cranePosition.Z + settings.BoomHingeOffsetZ;

            double dxHinge = cogX - hingeX;
            double dyHinge = cogY - hingeY;
            double dHinge = Math.Sqrt(dxHinge * dxHinge + dyHinge * dyHinge);

            double zHook = cogZ + halfHeight + settings.WireLength;
            double hHinge = Math.Max(0.0, zHook - hingeZ);
            double boomLength = Math.Sqrt(dHinge * dHinge + hHinge * hHinge);

            // Step 4 — Resolve via ILoadChartCapacityResolver (indexed, no diagnostics)
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
                RawChartCapacity = resolveResult.RawChartCapacity,
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

        // ══════════════════════════════════════════════════════════════
        // ASSIGNMENT SUMMARY
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Computes summary statistics from all element evaluation results.
        ///
        /// Populates AssignedElements (all assigned, including failures) and
        /// AssignedLiftableElements (only confirmed Liftable results).
        ///
        ///   Key   = crane position index
        ///   Value = list of element IDs assigned to that crane
        ///
        /// Keeping both dictionaries allows the FitnessEvaluator to use
        /// AssignedLiftableElements for balance scoring, avoiding penalising
        /// cranes that merely attract nearby elements they cannot lift.
        /// </summary>
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

                // All assigned elements (regardless of status)
                if (!result.AssignedElements.ContainsKey(eval.AssignedCraneIndex))
                    result.AssignedElements[eval.AssignedCraneIndex] = new List<string>();

                result.AssignedElements[eval.AssignedCraneIndex].Add(eval.ElementId);

                // Liftable-only elements for balance penalty (Issue 10 fix)
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
