using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.Utilities;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.GeneticOptimization
{
    /// <summary>
    /// Default implementation of the GA population initializer.
    ///
    /// Initialization Strategy — 70% Random / 30% Heuristic:
    ///
    ///   70% → Pure random positions sampled inside crane placement zones.
    ///          Ensures population diversity and exploration of the search space.
    ///
    ///   30% → Heuristic positions guided by precast element centroids.
    ///          Elements are clustered by spatial proximity.
    ///          Each crane position is seeded near the centroid of a cluster.
    ///          This accelerates GA convergence by providing smarter starting points.
    /// </summary>
    public class GAInitializer : IGAInitializer
    {
        private readonly IConstraintEngine _constraintEngine;
        private Random Rng => GARandomManager.Instance;

        public GAInitializer(IConstraintEngine constraintEngine)
        {
            _constraintEngine = constraintEngine;
        }

        // ══════════════════════════════════════════════════════════════
        // PUBLIC ENTRY POINT
        // ══════════════════════════════════════════════════════════════

        public Population InitializePopulation(
            List<GreenAreaZone> zones,
            List<GreenAreaZone> forbiddenZones,
            List<PrecastElement> elements,
            int populationSize,
            int maxCranePositions,
            GASettings settings)
        {
            var population = new Population();

            // ── Split population into random vs heuristic ──────────
            int heuristicCount = (int)(populationSize * 0.30);
            int randomCount = populationSize - heuristicCount;

            // ── 70% Pure Random ────────────────────────────────────
            for (int i = 0; i < randomCount; i++)
            {
                var solution = CreateRandomSolution(zones, forbiddenZones, maxCranePositions, settings);
                population.Individuals.Add(solution);
            }

            // ── 30% Heuristic (element-centroid guided) ────────────
            for (int i = 0; i < heuristicCount; i++)
            {
                var solution = CreateHeuristicSolution(zones, forbiddenZones, elements, maxCranePositions, settings);
                population.Individuals.Add(solution);
            }

            return population;
        }

        // ══════════════════════════════════════════════════════════════
        // RANDOM SOLUTION (70%)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates a fully random solution.
        /// Crane positions are sampled uniformly inside random zones.
        /// </summary>
        private CraneSolution CreateRandomSolution(
            List<GreenAreaZone> zones,
            List<GreenAreaZone> forbiddenZones,
            int maxCranePositions,
            GASettings settings)
        {
            var chromosome = new CraneChromosome();

            // Variable number of crane setups per chromosome
            int craneCount = Rng.Next(1, maxCranePositions + 1);

            for (int j = 0; j < craneCount; j++)
            {
                var point = GenerateDiversePointInsideZones(
                    zones,
                    forbiddenZones,
                    chromosome.CranePositions,
                    settings);

                chromosome.CranePositions.Add(point);
            }

            return new CraneSolution
            {
                Chromosome = chromosome,
                Fitness = 0,
                Penalty = 0,
                CoverageRatio = 0
            };
        }

        // ══════════════════════════════════════════════════════════════
        // HEURISTIC SOLUTION (30%)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates a heuristic solution guided by element spatial distribution.
        ///
        /// Steps:
        ///   1. Decide how many crane positions to use (1 → maxCranePositions)
        ///   2. Cluster elements into that many groups using K-Means style split
        ///   3. Compute the centroid of each cluster
        ///   4. Find the nearest valid point to that centroid inside a zone
        ///   5. Use that point as the crane position for this cluster
        /// </summary>
        private CraneSolution CreateHeuristicSolution(
            List<GreenAreaZone> zones,
            List<GreenAreaZone> forbiddenZones,
            List<PrecastElement> elements,
            int maxCranePositions,
            GASettings settings)
        {
            var chromosome = new CraneChromosome();

            int craneCount = Rng.Next(1, maxCranePositions + 1);

            // Step 1 — Cluster elements into craneCount groups
            var clusters = ClusterElements(elements, craneCount);

            foreach (var cluster in clusters)
            {
                if (cluster.Count == 0)
                    continue;

                // Step 2 — Compute spatial centroid of the cluster
                var centroid = CalculateCentroid(cluster);

                // Step 3 — Find nearest valid crane position inside zones
                var point = FindNearestValidPoint(
                    centroid,
                    zones,
                    forbiddenZones,
                    chromosome.CranePositions,
                    settings);

                chromosome.CranePositions.Add(point);
            }

            // Fallback: if heuristic failed to produce positions, add one random
            if (chromosome.CranePositions.Count == 0)
            {
                var point = GenerateDiversePointInsideZones(
                    zones,
                    forbiddenZones,
                    chromosome.CranePositions,
                    settings);

                chromosome.CranePositions.Add(point);
            }

            return new CraneSolution
            {
                Chromosome = chromosome,
                Fitness = 0,
                Penalty = 0,
                CoverageRatio = 0
            };
        }

        /// <summary>
        /// Splits elements into k clusters based on 2D spatial proximity
        /// using a Morton (Z-order) curve sort.
        ///
        /// Issue 9 fix — replaces the previous X-only sort:
        ///   The old approach sorted by X then Y, which groups elements in
        ///   horizontal strips. For L-shaped, multi-wing, or rotated buildings
        ///   this produced poor centroids that are far from any actual element.
        ///
        /// Morton-order sort interleaves the bits of normalized X and Y integer
        /// coordinates so that elements that are close in 2D space also appear
        /// close in the sort order. Dividing this sorted sequence into k chunks
        /// therefore produces spatially compact clusters rather than horizontal
        /// bands, resulting in better crane position seeds for the GA.
        ///
        /// Implementation note:
        ///   Coordinates are normalized to [0, 1023] (10 bits each) so that
        ///   X-range and Y-range are on the same scale even if the building
        ///   footprint is not square. The resulting Morton key is a 20-bit value.
        /// </summary>
        private List<List<PrecastElement>> ClusterElements(
            List<PrecastElement> elements,
            int k)
        {
            if (elements.Count == 0)
                return Enumerable.Range(0, k).Select(_ => new List<PrecastElement>()).ToList();

            // Normalize COG coordinates to [0, 1023] integer range
            double minX = elements.Min(e => e.ElementCOG.X);
            double maxX = elements.Max(e => e.ElementCOG.X);
            double minY = elements.Min(e => e.ElementCOG.Y);
            double maxY = elements.Max(e => e.ElementCOG.Y);

            double rangeX = maxX - minX;
            double rangeY = maxY - minY;

            // Avoid division by zero for degenerate (single-point) cases
            if (rangeX < double.Epsilon) rangeX = 1;
            if (rangeY < double.Epsilon) rangeY = 1;

            var sorted = elements
                .OrderBy(e =>
                {
                    uint ix = (uint)Math.Round((e.ElementCOG.X - minX) / rangeX * 1023);
                    uint iy = (uint)Math.Round((e.ElementCOG.Y - minY) / rangeY * 1023);
                    return SpatialMath.MortonEncode(ix, iy);
                })
                .ToList();

            var clusters = new List<List<PrecastElement>>();
            int chunkSize = (int)Math.Ceiling((double)sorted.Count / k);

            for (int i = 0; i < k; i++)
            {
                var cluster = sorted
                    .Skip(i * chunkSize)
                    .Take(chunkSize)
                    .ToList();

                clusters.Add(cluster);
            }

            return clusters;
        }

        // ══════════════════════════════════════════════════════════════
        // CENTROID — Compute spatial center of a cluster
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Calculates the average X/Y position of all elements in a cluster.
        /// This centroid represents the ideal crane position for this group.
        /// </summary>
        private DomainPoint3D CalculateCentroid(List<PrecastElement> elements)
        {
            return SpatialMath.Centroid(elements);
        }

        // ══════════════════════════════════════════════════════════════
        // NEAREST VALID POINT — Map centroid to a zone-valid position
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Finds the nearest valid crane position inside any zone
        /// relative to the provided centroid.
        ///
        /// Strategy:
        ///   1. If centroid is already inside a zone → use it directly
        ///   2. Otherwise → find the zone whose centroid is closest
        ///      and sample a random point inside that zone
        /// </summary>
        private DomainPoint3D FindNearestValidPoint(
            DomainPoint3D centroid,
            List<GreenAreaZone> zones,
            List<GreenAreaZone> forbiddenZones,
            List<DomainPoint3D> existingPoints,
            GASettings settings)
        {
            var closestZone = zones
                .OrderBy(z => DistanceToZoneCentroid(centroid, z))
                .First();
            var groundedCentroid = new DomainPoint3D(
                centroid.X,
                centroid.Y,
                GetZoneElevation(closestZone));

            // Case 1: centroid falls inside one of the zones
            if (IsValidSetupPointWithTracking(groundedCentroid, zones, forbiddenZones, settings)
                && _constraintEngine.IsMeaningfullySeparated(groundedCentroid, existingPoints, settings))
            {
                var snappedCentroid = SnapPointToGrid(groundedCentroid, settings.GridStep);
                return IsValidSetupPointWithTracking(snappedCentroid, zones, forbiddenZones, settings)
                    ? snappedCentroid
                    : groundedCentroid;
            }

            // Case 2: Project centroid onto the closest zone boundary
            var projected = SnapPointToGrid(ProjectPointToZoneBoundary(groundedCentroid, closestZone), settings.GridStep);
            if (IsValidSetupPointWithTracking(projected, zones, forbiddenZones, settings)
                && _constraintEngine.IsMeaningfullySeparated(projected, existingPoints, settings))
            {
                return projected;
            }

            // Case 3: find the closest zone and sample inside it
            var sampled = GenerateRandomPointInsideZone(closestZone, forbiddenZones, settings);
            return _constraintEngine.IsMeaningfullySeparated(sampled, existingPoints, settings)
                ? sampled
                : GenerateDiversePointInsideZones(zones, forbiddenZones, existingPoints, settings);
        }

        private DomainPoint3D ProjectPointToZoneBoundary(DomainPoint3D point, GreenAreaZone zone)
        {
            if (zone.Vertices == null || zone.Vertices.Count == 0)
                return point;

            double minDistance = double.MaxValue;
            DomainPoint3D closestProjection = point;

            for (int i = 0; i < zone.Vertices.Count; i++)
            {
                var p1 = zone.Vertices[i];
                var p2 = zone.Vertices[(i + 1) % zone.Vertices.Count];

                var projection = ProjectPointOntoSegment2D(point, p1, p2);
                double dist = SpatialMath.Distance2D(point, projection);

                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestProjection = projection;
                }
            }

            return closestProjection;
        }

        private DomainPoint3D ProjectPointOntoSegment2D(DomainPoint3D point, DomainPoint3D a, DomainPoint3D b)
        {
            double vx = b.X - a.X;
            double vy = b.Y - a.Y;
            double wx = point.X - a.X;
            double wy = point.Y - a.Y;

            double c1 = vx * wx + vy * wy;
            if (c1 <= 0)
                return new DomainPoint3D(a.X, a.Y, point.Z);

            double c2 = vx * vx + vy * vy;
            if (c2 <= c1)
                return new DomainPoint3D(b.X, b.Y, point.Z);

            double t = c1 / c2;
            return new DomainPoint3D(a.X + t * vx, a.Y + t * vy, point.Z);
        }

        /// <summary>
        /// Computes distance from a point to the centroid of a zone's vertices.
        /// Used to find the closest zone when the element centroid falls outside all zones.
        /// </summary>
        private double DistanceToZoneCentroid(DomainPoint3D point, GreenAreaZone zone)
        {
            double zx = zone.Vertices.Average(v => v.X);
            double zy = zone.Vertices.Average(v => v.Y);

            return SpatialMath.Distance2D(point, new DomainPoint3D(zx, zy, 0));
        }

        // ══════════════════════════════════════════════════════════════
        // GEOMETRY HELPERS
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Samples a spatially diverse setup point from allowed zones.
        /// Inputs are allowed zones, forbidden zones, existing chromosome points, and GA settings.
        /// The output is a valid setup point that maximizes distance from existing points when
        /// the minimum relocation threshold cannot be satisfied immediately.
        /// </summary>
        private DomainPoint3D GenerateDiversePointInsideZones(
            List<GreenAreaZone> zones,
            List<GreenAreaZone> forbiddenZones,
            List<DomainPoint3D> existingPoints,
            GASettings settings)
        {
            DomainPoint3D? bestCandidate = null;
            double bestDistance = double.MinValue;
            int attempts = Math.Max(1, settings.PositionSamplingAttempts);

            for (int i = 0; i < attempts; i++)
            {
                var zone = zones[Rng.Next(zones.Count)];
                var candidate = GenerateRandomPointInsideZone(zone, forbiddenZones, settings);
                double nearestDistance = existingPoints.Count == 0
                    ? double.MaxValue
                    : existingPoints.Min(point => SpatialMath.Distance2D(point, candidate));

                if (_constraintEngine.IsMeaningfullySeparated(candidate, existingPoints, settings))
                    return candidate;

                if (nearestDistance > bestDistance)
                {
                    bestDistance = nearestDistance;
                    bestCandidate = candidate;
                }
            }

            return bestCandidate ?? GenerateRandomPointInsideZone(zones[Rng.Next(zones.Count)], forbiddenZones, settings);
        }

        /// <summary>
        /// Samples one valid crane setup point inside a specific allowed zone.
        /// The output respects the simplified crane footprint and forbidden zones.
        /// </summary>
        private DomainPoint3D GenerateRandomPointInsideZone(
            GreenAreaZone zone,
            List<GreenAreaZone> forbiddenZones,
            GASettings settings)
        {
            var bbox = SpatialMath.BoundingBox(zone.Vertices);

            int attempts = Math.Max(1, settings.PositionSamplingAttempts);
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                double x = bbox.minX + Rng.NextDouble() * (bbox.maxX - bbox.minX);
                double y = bbox.minY + Rng.NextDouble() * (bbox.maxY - bbox.minY);

                var point = SnapPointToGrid(new DomainPoint3D(x, y, GetZoneElevation(zone)), settings.GridStep);

                if (IsValidSetupPointWithTracking(point, new[] { zone }, forbiddenZones, settings))
                    return point;
            }

            var fallback = new DomainPoint3D(
                zone.Vertices.Average(v => v.X),
                zone.Vertices.Average(v => v.Y),
                GetZoneElevation(zone));

            return SnapPointToGrid(fallback, settings.GridStep);
        }

        private DomainPoint3D SnapPointToGrid(DomainPoint3D point, double gridStep)
        {
            if (gridStep <= 0)
                return point;

            return new DomainPoint3D(
                Math.Round(point.X / gridStep) * gridStep,
                Math.Round(point.Y / gridStep) * gridStep,
                point.Z);
        }

        private static double GetZoneElevation(GreenAreaZone zone)
        {
            return zone.Vertices.Count == 0
                ? 0d
                : zone.Vertices.Average(vertex => vertex.Z);
        }

        private bool IsValidSetupPointWithTracking(
            DomainPoint3D point,
            IReadOnlyList<GreenAreaZone> allowedZones,
            IReadOnlyList<GreenAreaZone> forbiddenZones,
            GASettings settings)
        {
            if (GAProfiler.Enabled)
            {
                System.Threading.Interlocked.Increment(ref GAProfiler.CandidatePointsGenerated);
            }

            bool isValid = _constraintEngine.IsValidSetupPoint(point, allowedZones, forbiddenZones, settings);

            if (isValid && GAProfiler.Enabled)
            {
                System.Threading.Interlocked.Increment(ref GAProfiler.ValidPointsGenerated);
            }

            return isValid;
        }
    }
}
