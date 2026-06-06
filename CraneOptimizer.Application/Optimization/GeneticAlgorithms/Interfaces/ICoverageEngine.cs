using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Schemas;
using System.Collections.Generic;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces
{
    public interface ICoverageEngine
    {
        /// <summary>
        /// Recomputes total coverage and per-point new coverage after final de-duplication.
        /// </summary>
        void RecalculateCoverage(PlacementResult result, int totalElementCount);

        /// <summary>
        /// Filters final crane setup points so small adjustments are not presented as separate relocations.
        /// </summary>
        void DeduplicateFinalPositions(PlacementResult result, GASettings settings);

        /// <summary>
        /// Returns only the elements that are not currently covered (i.e. not successfully liftable).
        /// </summary>
        List<PrecastElement> FilterRemainingElements(IReadOnlyList<PrecastElement> allElements, HashSet<string> coveredElements);

        /// <summary>
        /// Returns the IDs of all successfully liftable elements in the result.
        /// </summary>
        HashSet<string> GetCoveredElementIds(PlacementResult result);
    }
}
