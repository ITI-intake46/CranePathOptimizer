using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.GreenAreaEntities;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services
{
    /// <summary>
    /// Mutation operator that slightly modifies crane positions
    /// to maintain population diversity.
    /// </summary>
    public class MutationOperator : IMutationOperator
    {
        private readonly Random _random = new();

        /// <summary>
        /// Applies mutation to a solution.
        /// </summary>
        public void Mutate(CraneSolution solution , List<CranePlacementZone> zones)
        {
            // TODO:
            // 1️⃣ Select random crane position from chromosome

            // TODO:
            // 2️⃣ Slightly modify X or Y coordinate

            // Example:
            // position.X += random small value
            // position.Y += random small value

            // TODO:
            // 3️⃣ Ensure position remains valid
            // (inside crane placement zone)
            var positions = solution.Chromosome.CranePositions;

            if (positions == null || positions.Count == 0)
                return;

            // 1️⃣ pick random position
            int index = _random.Next(positions.Count);
            var p = positions[index];

            // 2️⃣ perturb
            double deltaX = (_random.NextDouble() * 10) - 5;
            double deltaY = (_random.NextDouble() * 10) - 5;

            var mutated = new DomainPoint3D(
                p.X + deltaX,
                p.Y + deltaY,
                p.Z
            );

            // 3️⃣ validate (Reject strategy)
            if (IsInsideAnyZone(mutated, zones))
            {
                positions[index] = mutated;
            }
            // else: ignore mutation (keep original)
        }
        private bool IsInsideAnyZone(DomainPoint3D point, List<CranePlacementZone> zones)
        {
            return zones.Any(z => IsPointInsidePolygon(point, z.Vertices));
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