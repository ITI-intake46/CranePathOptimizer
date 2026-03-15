using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.GreenAreaEntities;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    /// <summary>
    /// Default implementation of the GA population initializer.
    /// Generates random chromosomes inside crane placement zones.
    /// </summary>
    public class GAInitializer : IGAInitializer
    {
        private readonly Random _random = new();

        public Population InitializePopulation(
            List<CranePlacementZone> zones,
            int populationSize,
            int maxCranePositions)
        {
            var population = new Population();

            for (int i = 0; i < populationSize; i++)
            {
                var chromosome = new CraneChromosome();

                // Variable number of crane setups
                int craneCount = _random.Next(1, maxCranePositions + 1);

                for (int j = 0; j < craneCount; j++)
                {
                    var zone = zones[_random.Next(zones.Count)];

                    var point = GenerateRandomPointInsideZone(zone);

                    chromosome.CranePositions.Add(point);
                }

                var solution = new CraneSolution
                {
                    Chromosome = chromosome,
                    Fitness = 0,
                    Penalty = 0,
                    CoverageRatio = 0
                };

                population.Individuals.Add(solution);
            }

            return population;
        }

        /// <summary>
        /// Generates a random point inside a polygon using
        /// bounding box sampling and point-in-polygon testing.
        /// </summary>
        private Point3D GenerateRandomPointInsideZone(CranePlacementZone zone)
        {
            var bbox = GetBoundingBox(zone.Vertices);

            while (true)
            {
                double x = bbox.minX + _random.NextDouble() * (bbox.maxX - bbox.minX);
                double y = bbox.minY + _random.NextDouble() * (bbox.maxY - bbox.minY);

                var point = new Point3D(x, y, 0);

                if (IsPointInsidePolygon(point, zone.Vertices))
                    return point;
            }
        }

        /// <summary>
        /// Calculates the bounding box of a polygon.
        /// </summary>
        private (double minX, double maxX, double minY, double maxY)
            GetBoundingBox(List<Point3D> vertices)
        {
            double minX = vertices.Min(v => v.X);
            double maxX = vertices.Max(v => v.X);
            double minY = vertices.Min(v => v.Y);
            double maxY = vertices.Max(v => v.Y);

            return (minX, maxX, minY, maxY);
        }

        /// <summary>
        /// Ray-casting algorithm to determine if a point
        /// is inside a polygon.
        /// </summary>
        private bool IsPointInsidePolygon(Point3D point, List<Point3D> polygon)
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