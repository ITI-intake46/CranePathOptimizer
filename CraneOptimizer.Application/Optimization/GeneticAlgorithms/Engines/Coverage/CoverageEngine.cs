using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.Utilities;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Coverage
{
    public class CoverageEngine : ICoverageEngine
    {
        public void RecalculateCoverage(PlacementResult result, int totalElementCount)
        {
            using (new GAProfiler.ProfileScope("CoverageEngine.RecalculateCoverage"))
            {
                var covered = new HashSet<string>(StringComparer.Ordinal);

                // Check if final assignments are resolved
                bool hasFinalAssignments = result.Positions.Any(p => p.ElementResults.Any(er => er.IsFinalAssignedToPoint));

                foreach (var position in result.Positions)
                {
                    int newlyCovered;
                    if (hasFinalAssignments)
                    {
                        newlyCovered = position.ElementResults
                            .Where(item => item.IsFinalAssignedToPoint && item.Status == LiftStatus.Liftable && covered.Add(item.ElementId))
                            .Count();
                    }
                    else
                    {
                        newlyCovered = position.ElementResults
                            .Where(item => item.Status == LiftStatus.Liftable && covered.Add(item.ElementId))
                            .Count();
                    }

                    position.NewLiftableCount = newlyCovered;
                }

                result.LiftedElementCount = covered.Count;
                result.TotalElementCount = totalElementCount;
                result.CoverageRatio = totalElementCount == 0
                    ? 0
                    : (double)covered.Count / totalElementCount;
            }
        }

        public void DeduplicateFinalPositions(
            PlacementResult result,
            GASettings settings)
        {
            using (new GAProfiler.ProfileScope("CoverageEngine.DeduplicateFinalPositions"))
            {
                if (result.Positions.Count < 2)
                    return;

                double minimumDistanceMeters = Math.Max(
                    settings.DuplicatePositionDistance,
                    settings.MinimumRelocationDistance);

                if (minimumDistanceMeters <= 0)
                    return;

                double minimumDistance = minimumDistanceMeters;

                var filtered = new List<CranePositionResult>();

                foreach (var candidate in result.Positions)
                {
                    int nearIndex = filtered.FindIndex(existing =>
                    {
                        var pExisting = new DomainPoint3D(existing.X, existing.Y, existing.Z);
                        var pCandidate = new DomainPoint3D(candidate.X, candidate.Y, candidate.Z);
                        return SpatialMath.Distance2D(pExisting, pCandidate) < minimumDistance;
                    });

                    if (nearIndex < 0)
                    {
                        filtered.Add(candidate);
                        continue;
                    }

                    var existing = filtered[nearIndex];
                    if (candidate.NewLiftableCount > existing.NewLiftableCount
                        || (candidate.NewLiftableCount == existing.NewLiftableCount
                            && candidate.LiftableCount > existing.LiftableCount))
                    {
                        filtered[nearIndex] = candidate;
                    }
                }

                result.Positions = filtered;
            }
        }

        public List<PrecastElement> FilterRemainingElements(
            IReadOnlyList<PrecastElement> allElements,
            HashSet<string> coveredElements)
        {
            using (new GAProfiler.ProfileScope("CoverageEngine.FilterRemainingElements"))
            {
                if (allElements == null)
                    return new List<PrecastElement>();

                if (coveredElements == null || coveredElements.Count == 0)
                    return allElements.ToList();

                return allElements
                    .Where(element => !coveredElements.Contains(element.ElementID))
                    .ToList();
            }
        }

        public HashSet<string> GetCoveredElementIds(PlacementResult result)
        {
            using (new GAProfiler.ProfileScope("CoverageEngine.GetCoveredElementIds"))
            {
                var covered = new HashSet<string>(StringComparer.Ordinal);
                if (result == null || result.Positions == null)
                    return covered;

                foreach (var position in result.Positions)
                {
                    foreach (var item in position.ElementResults)
                    {
                        if (item.Status == LiftStatus.Liftable)
                        {
                            covered.Add(item.ElementId);
                        }
                    }
                }

                return covered;
            }
        }
    }
}
