using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Calculations;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Assignment;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CraneOptimizer.Application.Services;

/// <summary>
/// Unified assignment engine that maps elements to cranes using a greedy, spatial-indexed strategy.
/// Supports both manual user overrides and automatic nearest-neighbor assignment.
/// </summary>
public sealed class GreedyAssignmentEngine
{
    private readonly ICraneConstraintService _constraintService;

    public GreedyAssignmentEngine(ICraneConstraintService constraintService)
    {
        _constraintService = constraintService;
    }

    /// <summary>
    /// Executes the element-to-crane assignment logic.
    /// 1. Validates all crane positions for zone/physical penalties.
    /// 2. Respects user-defined manual assignments.
    /// 3. Automatically assigns remaining elements to the nearest feasible crane.
    /// </summary>
    public CraneAssignment Evaluate(CraneSolution solution, PlacementInput input)
    {
        var placements = solution.Chromosome.CranePositions;
        var result = new CraneAssignment();

        // 1. Validate Crane Positions (Soft Penalties)
        for (int cIdx = 0; cIdx < placements.Count; cIdx++)
        {
            var validation = _constraintService.Validate(placements[cIdx], input);
            if (!validation.IsOutsideForbiddenZone)
                result.ForbiddenZoneViolations++;

            result.TotalConstraintPenalty += validation.NormalizedPenalty;
            result.CraneToElements[cIdx] = new List<int>();
            result.AssignedElements[cIdx] = new List<string>();
        }

        // 2. Element Assignment
        var elementCount = input.ElementCenters.Count;
        var bestCranes = new int[elementCount];
        var bestDistancesSq = new double[elementCount];
        var finalEvaluations = new ElementEvaluationResult[elementCount];

        for (int i = 0; i < elementCount; i++)
        {
            bestCranes[i] = -1;
            bestDistancesSq[i] = double.MaxValue;
        }

        // ── Phase A: User Manual Overrides ──────────────────────────────
        var processedByOverride = new bool[elementCount];
        if (input.Assignment != null && input.Assignment.Count > 0)
        {
            foreach (var entry in input.Assignment)
            {
                int cIdx = entry.Key;
                if (cIdx >= placements.Count) continue;

                foreach (var elementId in entry.Value)
                {
                    int eIdx = FindElementIndex(elementId, input.Elements);
                    if (eIdx < 0) continue;

                    var eval = EvaluatePosition(eIdx, cIdx, solution, input);
                    finalEvaluations[eIdx] = eval;
                    bestCranes[eIdx] = cIdx;
                    processedByOverride[eIdx] = true;
                }
            }
        }

        // ── Phase B: Greedy Spatial Assignment ──────────────────────────
        for (int cIdx = 0; cIdx < placements.Count; cIdx++)
        {
            var cranePos = placements[cIdx];
            var validation = _constraintService.Validate(cranePos, input);
            if (!validation.IsValid) continue;

            double maxR2 = input.CraneSpec.MaxRadius * input.CraneSpec.MaxRadius;
            var nearbyIndices = input.SpatialIndex.Query(cranePos, input.CraneSpec.MaxRadius);

            foreach (var eIdx in nearbyIndices)
            {
                if (processedByOverride[eIdx]) continue;

                var center = input.ElementCenters[eIdx];
                double dx = cranePos.X - center.X;
                double dy = cranePos.Y - center.Y;
                double dist2 = dx * dx + dy * dy;

                if (dist2 > maxR2) continue;

                if (dist2 < bestDistancesSq[eIdx])
                {
                    var eval = EvaluatePosition(eIdx, cIdx, solution, input);
                    if (eval.Status == LiftStatus.Liftable)
                    {
                        bestDistancesSq[eIdx] = dist2;
                        bestCranes[eIdx] = cIdx;
                        finalEvaluations[eIdx] = eval;
                    }
                }
            }
        }

        // 3. Finalize Mapping
        for (int eIdx = 0; eIdx < elementCount; eIdx++)
        {
            var elementId = input.Elements[eIdx].ElementID;
            if (bestCranes[eIdx] >= 0)
            {
                int cIdx = bestCranes[eIdx];
                result.CraneToElements[cIdx].Add(eIdx);
                result.AssignedElements[cIdx].Add(elementId);
                result.EvaluationResults.Add(finalEvaluations[eIdx]);
            }
            else
            {
                result.UnassignedElements.Add(eIdx);
                // Create a "failed" evaluation for the unassigned element
                result.EvaluationResults.Add(new ElementEvaluationResult
                {
                    ElementId = elementId,
                    Status = LiftStatus.OutOfRadius,
                    AssignedCraneIndex = -1
                });
            }
        }

        return result;
    }

    private ElementEvaluationResult EvaluatePosition(int eIdx, int cIdx, CraneSolution solution, PlacementInput input)
    {
        var element = input.Elements[eIdx];
        var cranePos = solution.Chromosome.CranePositions[cIdx];
        var center = input.ElementCenters[eIdx];
        var weight = input.ElementWeights[eIdx];

        double dx = cranePos.X - center.X;
        double dy = cranePos.Y - center.Y;
        double radius = Math.Sqrt(dx * dx + dy * dy);
        double height = center.Z + input.Settings.RiggingLength;
        double boomLength = Math.Sqrt(radius * radius + height * height);

        var status = LiftCalculation.DetermineLiftStatus(radius, boomLength, weight, input.CraneSpec, input.Settings);
        double capacity = LiftCalculation.CraneCapacity(input.CraneSpec, radius, boomLength, input.Settings);

        return new ElementEvaluationResult
        {
            ElementId = element.ElementID,
            AssignedCraneIndex = cIdx,
            Status = status,
            Radius = radius,
            BoomLength = boomLength,
            RequiredCapacity = weight
        };
    }

    private int FindElementIndex(string id, IReadOnlyList<CraneOptimizer.Domain.Schemas.PrecastElement> elements)
    {
        for (int i = 0; i < elements.Count; i++)
        {
            if (elements[i].ElementID == id) return i;
        }
        return -1;
    }
}
