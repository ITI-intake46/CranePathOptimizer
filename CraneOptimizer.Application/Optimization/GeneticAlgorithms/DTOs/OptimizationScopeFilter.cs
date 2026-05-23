using System.Collections.Generic;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs
{
    /// <summary>
    /// Criteria to filter target elements before running the GA optimization.
    /// Supports selection, exclusion, categories, and 2D bounding boxes.
    /// </summary>
    public class OptimizationScopeFilter
    {
        public HashSet<string>? SelectedElementIds { get; set; }
        public HashSet<string>? ExcludedElementIds { get; set; }
        public HashSet<string>? TargetCategories { get; set; }

        public double? MinX { get; set; }
        public double? MaxX { get; set; }
        public double? MinY { get; set; }
        public double? MaxY { get; set; }
    }
}
