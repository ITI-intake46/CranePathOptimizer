using System;
using System.Collections.Generic;
using System.Linq;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    /// <summary>
    /// Single source of truth for Genetic Algorithm final element assignment and unique ownership.
    /// </summary>
    public static class AssignmentPolicy
    {
        /// <summary>
        /// Calculates the unique assignment/suitability score for an element-to-point combination.
        /// Prioritizes: Liftable status, higher safety margin, shorter radius, shorter boom, stable tie-break.
        /// </summary>
        public static double CalculateAssignmentScore(
            LiftStatus status,
            double safetyMargin,
            double requiredRadius,
            double requiredBoomLength,
            int candidateIndex)
        {
            double isLiftableScore = (status == LiftStatus.Liftable) ? 1000000.0 : 0.0;
            double failureRankScore = LiftClassificationPolicy.GetFailureRank(status) * 100000.0;
            double safetyMarginScore = safetyMargin * 10000.0;
            double radiusPenalty = requiredRadius * 100.0;
            double boomPenalty = requiredBoomLength * 1.0;
            double indexPenalty = candidateIndex * 0.01;

            return isLiftableScore + failureRankScore + safetyMarginScore - radiusPenalty - boomPenalty - indexPenalty;
        }

        /// <summary>
        /// Resolves unique assignments for all project elements across the final selected setup positions.
        /// Guarantees that each element is assigned to at most one final setup position.
        /// </summary>
        public static void ResolveFinalAssignments(
            List<CranePositionResult> positions,
            List<PrecastElement> elements,
            CraneSpecification craneSpec,
            GASettings settings)
        {
            if (positions == null || positions.Count == 0 || elements == null || elements.Count == 0)
                return;

            // 1. Group all elements by ID for fast lookup
            var elementIds = elements
                .Where(e => !string.IsNullOrWhiteSpace(e.ElementID))
                .Select(e => e.ElementID)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            foreach (var elementId in elementIds)
            {
                // 2. Gather candidate results for this element across all setups
                var candidates = new List<(int SetupIndex, ElementLiftResult Result)>();
                for (int i = 0; i < positions.Count; i++)
                {
                    var pos = positions[i];
                    var elemResult = pos.ElementResults.FirstOrDefault(r => string.Equals(r.ElementId, elementId, StringComparison.Ordinal));
                    if (elemResult != null)
                    {
                        candidates.Add((i, elemResult));
                    }
                }

                if (candidates.Count == 0)
                    continue;

                // 3. Compute scores for all candidates
                var scoredCandidates = candidates.Select(c => new
                {
                    c.SetupIndex,
                    c.Result,
                    Score = CalculateAssignmentScore(c.Result.Status, c.Result.SafetyMargin, c.Result.RequiredRadius, c.Result.BoomLength, c.SetupIndex)
                }).ToList();

                var feasibleCandidates = scoredCandidates.Where(c => c.Result.Status == LiftStatus.Liftable).ToList();

                if (feasibleCandidates.Count > 0)
                {
                    // Case A: Element is feasible from at least one setup point
                    // Pick the highest scoring feasible candidate as the unique owner
                    var bestFeasible = feasibleCandidates.OrderByDescending(c => c.Score).First();
                    int ownerIndex = bestFeasible.SetupIndex;
                    string ownerPointId = $"Point_{ownerIndex}";

                    var feasiblePointIdsList = feasibleCandidates.Select(c => $"Point_{c.SetupIndex}").ToList();
                    string feasiblePointIds = string.Join(",", feasiblePointIdsList);

                    foreach (var c in scoredCandidates)
                    {
                        c.Result.CandidatePointId = $"Point_{c.SetupIndex}";
                        c.Result.CandidatePointIndex = c.SetupIndex;
                        c.Result.IsFeasibleFromPoint = (c.Result.Status == LiftStatus.Liftable);
                        c.Result.IsFinalAssignedToPoint = (c.SetupIndex == ownerIndex);
                        c.Result.AssignedPointId = ownerPointId;
                        c.Result.AssignedPointIndex = ownerIndex;
                        c.Result.FinalOwnerPointId = ownerPointId;
                        c.Result.IsFinalAssigned = true;
                        c.Result.AssignmentScore = c.Score;
                        c.Result.AssignmentReason = (feasibleCandidates.Count > 1) ? "BestSafetyMargin" : "UniqueFeasible";
                        c.Result.DuplicateAssignmentResolved = (feasibleCandidates.Count > 1);
                        c.Result.FeasiblePointIds = feasiblePointIds;
                        c.Result.BestFailedPointId = string.Empty;
                        c.Result.BestFailureReason = string.Empty;

                        c.Result.HasBestFailedCandidate = false;
                        c.Result.BestFailedLiftStatus = LiftStatus.OutOfRadius;
                        c.Result.BestFailedReachStatus = ReachStatus.OutOfRadius;
                        c.Result.BestFailedChartStatus = ChartStatus.NoListedCapacity;
                        c.Result.BestFailedSafetyMargin = 0.0;
                        c.Result.BestFailedRequiredRadius = 0.0;
                        c.Result.BestFailedRequiredBoomLength = 0.0;
                    }
                }
                else
                {
                    // Case B: Element is not feasible from any setup point (Unassigned)
                    // Pick the best failed candidate for diagnostics only, but do not assign
                    var bestFailed = scoredCandidates.OrderByDescending(c => c.Score).First();
                    string bestFailedPointId = $"Point_{bestFailed.SetupIndex}";
                    string bestFailureReason = bestFailed.Result.Status.ToString();

                    foreach (var c in scoredCandidates)
                    {
                        c.Result.CandidatePointId = $"Point_{c.SetupIndex}";
                        c.Result.CandidatePointIndex = c.SetupIndex;
                        c.Result.IsFeasibleFromPoint = false;
                        c.Result.IsFinalAssignedToPoint = false;
                        c.Result.AssignedPointId = string.Empty;
                        c.Result.AssignedPointIndex = -1;
                        c.Result.FinalOwnerPointId = string.Empty;
                        c.Result.IsFinalAssigned = false;
                        c.Result.AssignmentScore = c.Score;
                        c.Result.AssignmentReason = "Unassigned";
                        c.Result.DuplicateAssignmentResolved = false;
                        c.Result.FeasiblePointIds = string.Empty;
                        c.Result.BestFailedPointId = bestFailedPointId;
                        c.Result.BestFailureReason = bestFailureReason;

                        c.Result.HasBestFailedCandidate = true;
                        c.Result.BestFailedLiftStatus = bestFailed.Result.Status;
                        c.Result.BestFailedReachStatus = bestFailed.Result.ReachStatus;
                        c.Result.BestFailedChartStatus = bestFailed.Result.ChartStatus;
                        c.Result.BestFailedSafetyMargin = bestFailed.Result.SafetyMargin;
                        c.Result.BestFailedRequiredRadius = bestFailed.Result.RequiredRadius;
                        c.Result.BestFailedRequiredBoomLength = bestFailed.Result.BoomLength;
                    }
                }
            }
        }
    }
}
