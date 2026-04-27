using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
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
        private Random Rng => GARandomManager.Instance;

        public GAInitializer(GASettings settings)
        {
        }

        // ══════════════════════════════════════════════════════════════
        // PUBLIC ENTRY POINT
        // ══════════════════════════════════════════════════════════════

        public Population InitializePopulation(
            List<GreenAreaZone> zones,
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
                var solution = CreateRandomSolution(zones, maxCranePositions, settings);
                population.Individuals.Add(solution);
            }

            // ── 30% Heuristic (element-centroid guided) ────────────
            for (int i = 0; i < heuristicCount; i++)
            {
                var solution = CreateHeuristicSolution(zones, elements, maxCranePositions, settings);
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
            int maxCranePositions,
            GASettings settings)
        {
            var chromosome = new CraneChromosome();

            // Variable number of crane setups per chromosome
            int craneCount = Rng.Next(1, maxCranePositions + 1);

            for (int j = 0; j < craneCount; j++)
            {
                var zone = zones[Rng.Next(zones.Count)];
                var point = GenerateRandomPointInsideZone(zone, settings);
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
                var point = FindNearestValidPoint(centroid, zones, settings);

                chromosome.CranePositions.Add(point);
            }

            // Fallback: if heuristic failed to produce positions, add one random
            if (chromosome.CranePositions.Count == 0)
            {
                var zone = zones[Rng.Next(zones.Count)];
                var point = GenerateRandomPointInsideZone(zone, settings);
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
        // CLUSTERING — Split elements into spatial groups
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Splits elements into k clusters based on X-axis spatial ordering.
        ///
        /// Simple approach: sort elements by X coordinate, divide evenly.
        /// This is a fast approximation — not full K-Means — but sufficient
        /// to produce spatially meaningful cluster centroids for seeding.
        /// </summary>
        private List<List<PrecastElement>> ClusterElements(
            List<PrecastElement> elements,
            int k)
        {
            // Sort by X coordinate for a simple spatial split
            var sorted = elements
                .OrderBy(e => e.ElementCOG.X)
                .ThenBy(e => e.ElementCOG.Y)
                .ToList();

            var clusters = new List<List<PrecastElement>>();

            // Divide sorted elements evenly into k groups
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
            double x = elements.Average(e => e.ElementCOG.X);
            double y = elements.Average(e => e.ElementCOG.Y);

            return new DomainPoint3D(x, y, 0);
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
            GASettings settings)
        {
            // Case 1: centroid falls inside one of the zones
            foreach (var zone in zones)
            {
                if (IsPointInsidePolygon(centroid, zone.Vertices))
                {
                    var snappedCentroid = SnapPointToGrid(centroid, settings.GridStep);
                    return IsPointInsidePolygon(snappedCentroid, zone.Vertices)
                        ? snappedCentroid
                        : centroid;
                }
            }

            // Case 2: find the closest zone and sample inside it
            var closestZone = zones
                .OrderBy(z => DistanceToZoneCentroid(centroid, z))
                .First();

            return GenerateRandomPointInsideZone(closestZone, settings);
        }

        /// <summary>
        /// Computes distance from a point to the centroid of a zone's vertices.
        /// Used to find the closest zone when the element centroid falls outside all zones.
        /// </summary>
        private double DistanceToZoneCentroid(DomainPoint3D point, GreenAreaZone zone)
        {
            double zx = zone.Vertices.Average(v => v.X);
            double zy = zone.Vertices.Average(v => v.Y);

            double dx = point.X - zx;
            double dy = point.Y - zy;

            return Math.Sqrt(dx * dx + dy * dy);
        }

        // ══════════════════════════════════════════════════════════════
        // GEOMETRY HELPERS
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Generates a random point inside a polygon zone.
        /// Uses bounding box rejection sampling with ray-casting validation.
        /// </summary>
        private DomainPoint3D GenerateRandomPointInsideZone(GreenAreaZone zone, GASettings settings)
        {
            var bbox = GetBoundingBox(zone.Vertices);

            while (true)
            {
                double x = bbox.minX + Rng.NextDouble() * (bbox.maxX - bbox.minX);
                double y = bbox.minY + Rng.NextDouble() * (bbox.maxY - bbox.minY);

                var point = SnapPointToGrid(new DomainPoint3D(x, y, 0), settings.GridStep);

                if (IsPointInsidePolygon(point, zone.Vertices))
                    return point;
            }
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

        /// <summary>
        /// Calculates the bounding box (min/max X and Y) of a polygon.
        /// </summary>
        private (double minX, double maxX, double minY, double maxY)
            GetBoundingBox(List<DomainPoint3D> vertices)
        {
            return (
                vertices.Min(v => v.X),
                vertices.Max(v => v.X),
                vertices.Min(v => v.Y),
                vertices.Max(v => v.Y)
            );
        }

        /// <summary>
        /// Ray-casting algorithm to determine if a point lies inside a polygon.
        /// Casts a horizontal ray from the point to the right and counts
        /// how many polygon edges it crosses — odd = inside, even = outside.
        /// </summary>
        private bool IsPointInsidePolygon(DomainPoint3D point, List<DomainPoint3D> polygon)
        {
            bool inside = false;

            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var pi = polygon[i];
                var pj = polygon[j];

                bool intersect =
                    ((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                    (point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y) + pi.X);

                if (intersect)
                    inside = !inside;
            }

            return inside;
        }
    }
}
