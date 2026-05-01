using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    /// <summary>
    /// Mutation operator that slightly modifies crane positions
    /// to maintain population diversity.
    /// </summary>
    public class MutationOperator : IMutationOperator
    {
        private Random _random => GARandomManager.Instance;

        public void Mutate(CraneSolution solution, List<GreenAreaZone> zones, GASettings settings)
        {
            var positions = solution.Chromosome.CranePositions;

            if (positions == null || positions.Count == 0)
                return;

            int index = _random.Next(positions.Count);
            var point = positions[index];

            double sigma = ResolveMutationSigma(settings);
            double deltaX = NextGaussian() * sigma;
            double deltaY = NextGaussian() * sigma;

            var mutated = new DomainPoint3D(
                SnapToGrid(point.X + deltaX, settings.GridStep),
                SnapToGrid(point.Y + deltaY, settings.GridStep),
                point.Z
            );

            if (IsInsideAnyZone(mutated, zones))
                positions[index] = mutated;
        }

        private double ResolveMutationSigma(GASettings settings)
        {
            if (settings.MutationSigma <= 0)
                return 5.0;

            if (settings.MutationSigma <= 1.0)
                return settings.MutationSigma * settings.MaxRadiusForNormalisation;

            return settings.MutationSigma;
        }

        private double NextGaussian()
        {
            double u1 = 1.0 - _random.NextDouble();
            double u2 = 1.0 - _random.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        private double SnapToGrid(double value, double gridStep)
        {
            if (gridStep <= 0)
                return value;

            return Math.Round(value / gridStep) * gridStep;
        }

        private bool IsInsideAnyZone(DomainPoint3D point, List<GreenAreaZone> zones)
        {
            return zones.Any(zone => IsPointInsidePolygon(point, zone.Vertices));
        }

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
