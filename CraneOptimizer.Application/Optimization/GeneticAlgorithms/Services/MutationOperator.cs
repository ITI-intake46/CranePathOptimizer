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

        /// <summary>
        /// Mutates one crane setup point while preserving engineering constraints.
        /// Inputs are the candidate solution, allowed zones, forbidden zones, and GA settings.
        /// The output is an in-place position update only when the mutated point is valid,
        /// outside forbidden zones, and meaningfully separated from the other setup points.
        /// </summary>
        public void Mutate(
            CraneSolution solution,
            List<GreenAreaZone> zones,
            List<GreenAreaZone> forbiddenZones,
            GASettings settings)
        {
            var positions = solution.Chromosome.CranePositions;

            if (positions == null || positions.Count == 0)
                return;

            int index = _random.Next(positions.Count);
            var point = positions[index];

            double sigma = ResolveMutationSigma(settings);
            var otherPositions = positions
                .Where((_, positionIndex) => positionIndex != index)
                .ToList();

            int attempts = Math.Max(1, settings.PositionSamplingAttempts / 4);
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                double deltaX = NextGaussian() * sigma;
                double deltaY = NextGaussian() * sigma;

                var mutated = new DomainPoint3D(
                    SnapToGrid(point.X + deltaX, settings.GridStep),
                    SnapToGrid(point.Y + deltaY, settings.GridStep),
                    point.Z
                );

                if (!CranePlacementConstraintService.IsValidSetupPoint(mutated, zones, forbiddenZones, settings))
                    continue;

                if (!CranePlacementConstraintService.IsMeaningfullySeparated(mutated, otherPositions, settings))
                    continue;

                positions[index] = mutated;
                return;
            }
        }

        private double ResolveMutationSigma(GASettings settings)
        {
            if (settings.MutationSigma <= 0)
                return 5.0;

            double sigma = settings.MutationSigma <= 1.0
                ? settings.MutationSigma * settings.MaxRadiusForNormalisation
                : settings.MutationSigma;

            return Math.Max(1.0, sigma * Math.Max(1.0, settings.MutationExplorationMultiplier));
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

    }
}
