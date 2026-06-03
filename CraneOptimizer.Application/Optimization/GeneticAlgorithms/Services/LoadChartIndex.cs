using CraneOptimizer.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    /// <summary>
    /// Pre-indexed load chart data for a single CraneSpecification.
    /// Built once before the GA run to replace per-call LINQ operations
    /// with O(log N) binary search and O(1) dictionary lookups.
    /// </summary>
    public class LoadChartIndex
    {
        /// <summary>
        /// Precomputed maximum radius from the load chart.
        /// </summary>
        public double MaxRadius { get; }

        /// <summary>
        /// Precomputed maximum boom length from the load chart.
        /// </summary>
        public double MaxBoomLength { get; }

        /// <summary>
        /// Sorted distinct chart radii for binary search (ascending order).
        /// </summary>
        public double[] SortedRadii { get; }

        /// <summary>
        /// Pre-grouped boom configurations keyed by chart radius.
        /// Each value is a list of BoomGroup sorted by ascending boom length.
        /// </summary>
        public Dictionary<double, List<BoomGroup>> BoomGroupsByRadius { get; }

        /// <summary>
        /// Indicates whether the index was built from a valid, non-empty load chart.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Builds a pre-indexed representation of a CraneSpecification's load chart.
        /// </summary>
        public LoadChartIndex(CraneSpecification craneSpec)
        {
            if (craneSpec?.LoadChart == null || craneSpec.LoadChart.Count == 0)
            {
                IsValid = false;
                MaxRadius = 0;
                MaxBoomLength = 0;
                SortedRadii = Array.Empty<double>();
                BoomGroupsByRadius = new Dictionary<double, List<BoomGroup>>();
                return;
            }

            IsValid = true;
            var chart = craneSpec.LoadChart;

            // Precompute envelope limits
            MaxRadius = craneSpec.MaxRadius > 0
                ? craneSpec.MaxRadius
                : chart.Max(tp => tp.Radius);

            MaxBoomLength = craneSpec.MaxBoomLength > 0
                ? craneSpec.MaxBoomLength
                : chart.Max(tp => tp.BoomLength);

            // Build sorted distinct radii
            SortedRadii = chart
                .Select(tp => tp.Radius)
                .Distinct()
                .OrderBy(r => r)
                .ToArray();

            // Build boom groups per radius
            BoomGroupsByRadius = new Dictionary<double, List<BoomGroup>>();

            var byRadius = chart.GroupBy(tp => tp.Radius);

            foreach (var radiusGroup in byRadius)
            {
                double radius = radiusGroup.Key;

                // Group by rounded boom length (same logic as LoadChartCapacityResolver)
                var boomGroups = radiusGroup
                    .GroupBy(tp => Math.Round(tp.BoomLength, 4))
                    .OrderBy(g => g.Min(tp => tp.BoomLength))
                    .Select(g =>
                    {
                        var groupEntries = g
                            .OrderBy(tp => tp.VisualColumnIndex ?? int.MaxValue)
                            .ThenBy(tp => tp.Capacity)
                            .ToList();

                        double boomLength = groupEntries.Min(tp => tp.BoomLength);

                        var positiveEntries = groupEntries
                            .Where(tp => tp.Capacity > 0.0)
                            .OrderBy(tp => tp.Capacity)
                            .ThenBy(tp => tp.VisualColumnIndex ?? int.MaxValue)
                            .ToList();

                        return new BoomGroup
                        {
                            BoomLength = boomLength,
                            AllEntries = groupEntries,
                            PositiveEntries = positiveEntries,
                            MinPositiveEntry = positiveEntries.Count > 0 ? positiveEntries[0] : null,
                            HasPositiveCapacity = positiveEntries.Count > 0,
                            FirstInvalidEntry = groupEntries.FirstOrDefault(tp => tp.Capacity == 0.0)
                                ?? (groupEntries.Count > 0 ? groupEntries.First() : null)
                        };
                    })
                    .ToList();

                BoomGroupsByRadius[radius] = boomGroups;
            }
        }

        /// <summary>
        /// Finds the smallest chart radius >= requiredRadius using binary search.
        /// Returns -1 if no chart radius is large enough.
        /// </summary>
        public int FindFirstRadiusIndex(double requiredRadius)
        {
            // Binary search for first element >= requiredRadius
            int lo = 0, hi = SortedRadii.Length - 1;
            int result = -1;

            while (lo <= hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (SortedRadii[mid] >= requiredRadius)
                {
                    result = mid;
                    hi = mid - 1;
                }
                else
                {
                    lo = mid + 1;
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Pre-sorted, pre-grouped boom configuration for one boom-length group at one chart radius.
    /// </summary>
    public class BoomGroup
    {
        /// <summary>
        /// The minimum boom length in this group.
        /// </summary>
        public double BoomLength { get; init; }

        /// <summary>
        /// All entries in this group, sorted by VisualColumnIndex then Capacity.
        /// </summary>
        public List<TargetPoint> AllEntries { get; init; } = new();

        /// <summary>
        /// Entries with positive capacity, sorted by ascending Capacity then VisualColumnIndex.
        /// </summary>
        public List<TargetPoint> PositiveEntries { get; init; } = new();

        /// <summary>
        /// The minimum positive capacity entry (conservative selection).
        /// Null if no positive entries exist.
        /// </summary>
        public TargetPoint? MinPositiveEntry { get; init; }

        /// <summary>
        /// Whether this group has any positive capacity entries.
        /// </summary>
        public bool HasPositiveCapacity { get; init; }

        /// <summary>
        /// First invalid (zero or blank) entry, used for diagnostics reporting.
        /// </summary>
        public TargetPoint? FirstInvalidEntry { get; init; }
    }
}
