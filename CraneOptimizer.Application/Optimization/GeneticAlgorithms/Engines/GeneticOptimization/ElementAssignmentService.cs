using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Calculations;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using System;
using System.Collections.Generic;
using System.Linq;

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

        public ElementAssignmentService(
            ILiftingCalculationEngine liftingEngine,
            ICraneConfigurationEngine configEngine)
        {
            _liftingEngine = liftingEngine;
            _configEngine = configEngine;
        }

        // ══════════════════════════════════════════════════════════════
        // PUBLIC ENTRY POINT
        // ══════════════════════════════════════════════════════════════

        public AssignmentResult EvaluateAssignment(
            CraneSolution solution,
            List<PrecastElement> elements,
            Dictionary<int, List<string>> assignment,
            CraneSpecification craneSpec,
            GASettings settings)
        {
            var result = new AssignmentResult();

            foreach (var element in elements)
            {
                ElementEvaluationResult evaluation;

                // ── Check for user-defined assignment ─────────────────
                int? userAssignedIndex = ResolveUserAssignment(element, assignment);

                if (userAssignedIndex.HasValue)
                {
                    // User forced this element to a specific crane position
                    evaluation = EvaluateSinglePosition(
                        element,
                        solution,
                        userAssignedIndex.Value,
                        craneSpec,
                        settings);
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

        public IReadOnlyList<ElementEvaluationResult> EvaluatePosition(
            CraneSolution solution,
            int craneIndex,
            List<PrecastElement> elements,
            CraneSpecification craneSpec,
            GASettings settings)
        {
            if (solution.Chromosome.CranePositions.Count == 0 || craneIndex < 0 || craneIndex >= solution.Chromosome.CranePositions.Count)
                return Array.Empty<ElementEvaluationResult>();

            return elements
                .Select(element => ComputeEvaluationResult(element, solution, craneIndex, craneSpec, settings))
                .ToList();
        }

        // ══════════════════════════════════════════════════════════════
        // USER ASSIGNMENT RESOLUTION
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Checks whether the user has manually assigned this element
        /// to a specific crane index.
        ///
        /// The assignment dictionary maps:
        ///   Key   = crane position index in the chromosome
        ///   Value = list of ElementIDs assigned to that crane
        ///
        /// Returns the crane index if found, null if no assignment exists.
        /// </summary>
        private int? ResolveUserAssignment(
            PrecastElement element,
            Dictionary<int, List<string>> assignment)
        {
            if (assignment == null || assignment.Count == 0)
                return null;

            foreach (var entry in assignment)
            {
                if (entry.Value.Contains(element.ElementID))
                    return entry.Key;
            }

            return null;
        }

        // ══════════════════════════════════════════════════════════════
        // SINGLE POSITION EVALUATION (user override)
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
            GASettings settings)
        {
            // Guard: if craneIndex is out of range, fall back to best-position
            if (craneIndex >= solution.Chromosome.CranePositions.Count)
                return EvaluateBestPosition(element, solution, craneSpec, settings);

            return ComputeEvaluationResult(element, solution, craneIndex, craneSpec, settings);
        }

        // ══════════════════════════════════════════════════════════════
        // BEST POSITION SELECTION (automatic — main logic)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Tries all crane positions in the chromosome for this element
        /// and selects the best feasible one.
        ///
        /// Selection priority:
        ///   1. Prefer Liftable over any failure status
        ///   2. Among Liftable positions, prefer the smallest radius
        ///      (smaller radius = safer lift, less stress on crane)
        ///   3. If no Liftable position exists, return the least-bad failure:
        ///      Overweight > HeightFail > SpecialCase > OutOfRadius
        ///      (Overweight is closest to liftable — just too heavy at this position)
        /// </summary>
        private ElementEvaluationResult EvaluateBestPosition(
            PrecastElement element,
            CraneSolution solution,
            CraneSpecification craneSpec,
            GASettings settings)
        {
            var candidates = new List<ElementEvaluationResult>();

            // ── Evaluate all crane positions ───────────────────────
            for (int i = 0; i < solution.Chromosome.CranePositions.Count; i++)
            {
                var candidate = ComputeEvaluationResult(
                    element, solution, i, craneSpec, settings);

                candidates.Add(candidate);
            }

            // ── Pick the best result ───────────────────────────────
            return SelectBestCandidate(candidates);
        }

        /// <summary>
        /// Selects the best evaluation result from a list of candidates.
        ///
        /// Step 1: Find all Liftable candidates.
        ///         If any exist → return the one with the smallest radius.
        ///
        /// Step 2: No Liftable candidates → return the least-bad failure.
        ///         Failure ranking (higher = closer to liftable):
        ///           Overweight  = 3   (can reach, just too heavy here)
        ///           HeightFail  = 2   (can reach horizontally, boom too short)
        ///           SpecialCase = 1   (no chart data, unknown)
        ///           OutOfRadius = 0   (geometrically unreachable)
        /// </summary>
        private ElementEvaluationResult SelectBestCandidate(
            List<ElementEvaluationResult> candidates)
        {
            // ── Priority 1: any Liftable position ─────────────────
            var liftable = candidates
                .Where(c => c.Status == LiftStatus.Liftable)
                .OrderBy(c => c.Radius)
                .ThenBy(c => c.AssignedCraneIndex) // Deterministic tie-breaking
                .FirstOrDefault();

            if (liftable != null)
                return liftable;

            // ── Priority 2: least-bad failure ─────────────────────
            return candidates
                .OrderByDescending(c => GetFailureRank(c.Status))
                .ThenBy(c => c.Radius)
                .ThenBy(c => c.AssignedCraneIndex) // Deterministic tie-breaking
                .First();
        }

        /// <summary>
        /// Returns a numeric rank for non-Liftable statuses.
        /// Higher rank = closer to being liftable = preferred fallback.
        /// </summary>
        private int GetFailureRank(LiftStatus status)
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

        // ══════════════════════════════════════════════════════════════
        // CORE LIFT COMPUTATION
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Computes the full lift evaluation for one element
        /// against one crane position index.
        ///
        /// Steps:
        ///   1. Retrieve crane position from chromosome
        ///   2. Compute working radius       → LiftCalculation.CalculateRadius
        ///   3. Compute lift height          → LiftCalculation.CalculateLiftHeight
        ///   4. Compute required boom length → LiftCalculation.CalculateRequiredBoomLength
        ///   5. Determine lift status        → LiftCalculation.DetermineLiftStatus
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

            // Step 2 — Working radius (orientation-aware)
            double radius = _liftingEngine.CalculateRadius(cranePosition, element);

            // Step 3 — Vertical lift height
            double height = _liftingEngine.CalculateLiftHeight(element, cranePosition, settings);

            // Step 4 — Required boom length
            double boomLength = _configEngine.CalculateRequiredBoomLength(radius, height);

            // Step 5 — Required capacity = element weight + lifting accessories
            double requiredCapacity = _liftingEngine.CalculateRequiredWeight(element, settings);

            // Step 6 — Lookup chart capacity for debug/reporting
            double availableCapacity = _configEngine.CraneCapacity(
                craneSpec,
                radius,
                boomLength,
                settings);

            // Step 7 — Determine full lift status
            LiftStatus status = _liftingEngine.DetermineLiftStatus(
                radius, boomLength, requiredCapacity, craneSpec, settings);

            // ══════════════════════════════════════════════════════════════
            // GOLDEN MASTER COMPARISON (Phase 2 Verification)
            // ══════════════════════════════════════════════════════════════
            double legacyRadius = LiftCalculationLegacy.CalculateRadius(cranePosition, element);
            double legacyHeight = LiftCalculationLegacy.CalculateLiftHeight(element, cranePosition, settings);
            double legacyBoomLength = LiftCalculationLegacy.CalculateRequiredBoomLength(legacyRadius, legacyHeight);
            double legacyCapacity = LiftCalculationLegacy.CraneCapacity(craneSpec, legacyRadius, legacyBoomLength, settings);
            LiftStatus legacyStatus = LiftCalculationLegacy.DetermineLiftStatus(legacyRadius, legacyBoomLength, requiredCapacity, craneSpec, settings);

            if (Math.Abs(radius - legacyRadius) > 1e-9)
                throw new InvalidOperationException($"Radius mismatch! New: {radius}, Legacy: {legacyRadius}");

            if (Math.Abs(height - legacyHeight) > 1e-9)
                throw new InvalidOperationException($"Height mismatch! New: {height}, Legacy: {legacyHeight}");

            if (Math.Abs(boomLength - legacyBoomLength) > 1e-9)
                throw new InvalidOperationException($"Boom length mismatch! New: {boomLength}, Legacy: {legacyBoomLength}");

            if (Math.Abs(availableCapacity - legacyCapacity) > 1e-9)
                throw new InvalidOperationException($"Capacity mismatch! New: {availableCapacity}, Legacy: {legacyCapacity}");

            if (status != legacyStatus)
                throw new InvalidOperationException($"Status mismatch! New: {status}, Legacy: {legacyStatus}");
            // ══════════════════════════════════════════════════════════════

            return new ElementEvaluationResult
            {
                ElementId = element.ElementID,
                AssignedCraneIndex = craneIndex,
                Status = status,
                Radius = radius,
                BoomLength = boomLength,
                AvailableCapacity = availableCapacity,
                RequiredCapacity = requiredCapacity
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
