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
        private readonly IReachabilityMapService? _reachabilityMapService;
        private Random Rng => GARandomManager.Instance;

        public GAInitializer(IConstraintEngine constraintEngine)
            : this(constraintEngine, null)
        {
        }

        public GAInitializer(IConstraintEngine constraintEngine, IReachabilityMapService? reachabilityMapService)
        {
            _constraintEngine = constraintEngine;
            _reachabilityMapService = reachabilityMapService;
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
            GASettings settings,
            CraneSpecification craneSpec)
        {
            var population = new Population();

            bool useReachability = settings.EnableReachabilitySeeding 
                                   && _reachabilityMapService != null 
                                   && settings.HotSpotSeedingRatio > 0;

            IReadOnlyList<ScoredCoordinate> hotSpots = Array.Empty<ScoredCoordinate>();
            if (useReachability)
            {
                int topN = Math.Max(100, (int)(populationSize * maxCranePositions * 1.5));
                hotSpots = _reachabilityMapService!.ComputeHotSpots(elements, zones, forbiddenZones, craneSpec, settings, topN);
            }

            if (useReachability && hotSpots.Count > 0)
            {
                // Seed HotSpotSeedingRatio% of the population from hot spots
                int hotSpotCount = (int)(populationSize * settings.HotSpotSeedingRatio);
                int randomCount = populationSize - hotSpotCount;

                for (int i = 0; i < hotSpotCount; i++)
                {
                    var solution = CreateHotSpotSolution(hotSpots.ToList(), zones, forbiddenZones, maxCranePositions, settings);
                    population.Individuals.Add(solution);
                }

                for (int i = 0; i < randomCount; i++)
                {
                    var solution = CreateRandomSolution(zones, forbiddenZones, maxCranePositions, settings);
                    population.Individuals.Add(solution);
                }
            }
            else
            {
                // Legacy Strategy: 30% heuristic / 70% random
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
            }

            return population;
        }

        private CraneSolution CreateHotSpotSolution(
            List<ScoredCoordinate> hotSpots,
            List<GreenAreaZone> zones,
            List<GreenAreaZone> forbiddenZones,
            int maxCranePositions,
            GASettings settings)
        {
            var chromosome = new CraneChromosome();
            int craneCount = Rng.Next(1, maxCranePositions + 1);

            var selectedIndices = new HashSet<int>();
            var selectedZones = new HashSet<Guid>();
            int attempts = 0;

            while (chromosome.CranePositions.Count < craneCount && attempts < craneCount * 5 && hotSpots.Count > 0)
            {
                attempts++;

                int idx = -1;
                var unselectedZoneIndices = new List<int>();
                for (int j = 0; j < hotSpots.Count; j++)
                {
                    if (!selectedIndices.Contains(j) && !selectedZones.Contains(hotSpots[j].ZoneId))
                    {
                        unselectedZoneIndices.Add(j);
                    }
                }

                if (unselectedZoneIndices.Count > 0)
                {
                    idx = unselectedZoneIndices[Rng.Next(unselectedZoneIndices.Count)];
                }
                else
                {
                    var eligible = Enumerable.Range(0, hotSpots.Count).Where(j => !selectedIndices.Contains(j)).ToList();
                    if (eligible.Count > 0)
                    {
                        idx = eligible[Rng.Next(eligible.Count)];
                    }
                }

                if (idx == -1)
                    break;

                selectedIndices.Add(idx);
                var spot = hotSpots[idx];
                var point = new DomainPoint3D(spot.X, spot.Y, GetZoneElevationForPoint(spot.X, spot.Y, zones));

                if (_constraintEngine.IsMeaningfullySeparated(point, chromosome.CranePositions, settings))
                {
                    chromosome.CranePositions.Add(point);
                    selectedZones.Add(spot.ZoneId);
                    _reachabilityMapService?.RecordSelectedSeed(spot);
                }
            }

            // Fallback: if we couldn't get enough distinct hot spot positions, fill the rest as random
            while (chromosome.CranePositions.Count < craneCount)
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

        private double GetZoneElevationForPoint(double x, double y, List<GreenAreaZone> zones)
        {
            foreach (var zone in zones)
            {
                if (zone.Vertices == null)
                    continue;

                if (SpatialMath.PointInPolygon(new DomainPoint3D(x, y, 0), zone.Vertices))
                {
                    return GetZoneElevation(zone);
                }
            }

            if (zones.Count > 0)
                return GetZoneElevation(zones[0]);

            return 0d;
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
            if (TryGenerateRandomPointInsideZone(closestZone, forbiddenZones, settings, out var sampled)
                && _constraintEngine.IsMeaningfullySeparated(sampled, existingPoints, settings))
            {
                return sampled;
            }

            return GenerateDiversePointInsideZones(zones, forbiddenZones, existingPoints, settings);
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
                if (!TryGenerateRandomPointInsideZone(zone, forbiddenZones, settings, out var candidate))
                    continue;

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

            if (bestCandidate != null)
                return bestCandidate;

            foreach (var zone in zones)
            {
                if (TryGenerateRandomPointInsideZone(zone, forbiddenZones, settings, out var candidate))
                    return candidate;
            }

            throw new InvalidOperationException("No valid crane setup point has a complete physical ground footprint inside an allowed zone without overlapping forbidden zones.");
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
            return TryGenerateRandomPointInsideZone(zone, forbiddenZones, settings, out var point)
                ? point
                : throw new InvalidOperationException($"No valid crane setup point fits inside allowed zone '{zone.Name}'.");
        }

        private bool TryGenerateRandomPointInsideZone(
            GreenAreaZone zone,
            List<GreenAreaZone> forbiddenZones,
            GASettings settings,
            out DomainPoint3D point)
        {
            var bbox = SpatialMath.BoundingBox(zone.Vertices);

            int attempts = Math.Max(1, settings.PositionSamplingAttempts);
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                double x = bbox.minX + Rng.NextDouble() * (bbox.maxX - bbox.minX);
                double y = bbox.minY + Rng.NextDouble() * (bbox.maxY - bbox.minY);

                var candidatePoint = SnapPointToGrid(new DomainPoint3D(x, y, GetZoneElevation(zone)), settings.GridStep);

                if (IsValidSetupPointWithTracking(candidatePoint, new[] { zone }, forbiddenZones, settings))
                {
                    point = candidatePoint;
                    return true;
                }
            }

            if (TryFindFirstValidGridPoint(zone, forbiddenZones, settings, out point))
                return true;

            point = new DomainPoint3D();
            return false;
        }

        private bool TryFindFirstValidGridPoint(
            GreenAreaZone zone,
            List<GreenAreaZone> forbiddenZones,
            GASettings settings,
            out DomainPoint3D point)
        {
            var bbox = SpatialMath.BoundingBox(zone.Vertices);
            int divisions = Math.Max(4, settings.PositionSamplingAttempts);
            double stepX = (bbox.maxX - bbox.minX) / divisions;
            double stepY = (bbox.maxY - bbox.minY) / divisions;

            if (stepX <= 0 || stepY <= 0)
            {
                point = new DomainPoint3D();
                return false;
            }

            for (int ix = 0; ix <= divisions; ix++)
            {
                for (int iy = 0; iy <= divisions; iy++)
                {
                    var candidate = SnapPointToGrid(
                        new DomainPoint3D(bbox.minX + (ix * stepX), bbox.minY + (iy * stepY), GetZoneElevation(zone)),
                        settings.GridStep);

                    if (IsValidSetupPointWithTracking(candidate, new[] { zone }, forbiddenZones, settings))
                    {
                        point = candidate;
                        return true;
                    }
                }
            }

            point = new DomainPoint3D();
            return false;
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
