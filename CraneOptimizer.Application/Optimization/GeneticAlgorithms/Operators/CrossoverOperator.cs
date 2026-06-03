using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.GeneticOptimization;
using CraneOptimizer.Application.Optimization.Utilities;
using System;
using System.Linq;

namespace CraneOptimizer.Application.Optimization.GeneticAlgorithms.Operators
{
    /// <summary>
    /// Default crossover operator for crane chromosomes.
    /// Combines crane positions from two parents to create a child solution.
    /// </summary>
    public class CrossoverOperator : ICrossoverOperator
    {
        private Random _random => GARandomManager.Instance;

        /// <summary>
        /// Generates a child solution by combining positions from two parents.
        /// </summary>
        public CraneSolution Crossover(CraneSolution parent1, CraneSolution parent2, GASettings settings)
        {
            var child = new CraneSolution
            {
                Chromosome = new CraneChromosome()
            };

            var p1 = parent1.Chromosome.CranePositions;
            var p2 = parent2.Chromosome.CranePositions;

            int maxCount = Math.Max(p1.Count, p2.Count);

            // Dynamic footprint guard: use 2 * settings.CraneFootprintRadius as duplicate threshold
            double duplicateThreshold = settings.CraneFootprintRadius > 0
                ? 2.0 * settings.CraneFootprintRadius
                : 3.0; // safe fallback

            for (int i = 0; i < maxCount; i++)
            {
                Domain.Common.DomainPoint3D? inheritedPoint = null;

                if (i < p1.Count && i < p2.Count)
                {
                    inheritedPoint = _random.NextDouble() < 0.5 ? p1[i] : p2[i];
                }
                else if (i < p1.Count)
                {
                    inheritedPoint = p1[i];
                }
                else if (i < p2.Count)
                {
                    inheritedPoint = p2[i];
                }

                if (inheritedPoint != null)
                {
                    // Ensure no duplicate points are inherited close to existing child points
                    bool isDuplicate = child.Chromosome.CranePositions.Any(p =>
                        SpatialMath.Distance2D(p, inheritedPoint) < duplicateThreshold);

                    if (!isDuplicate)
                    {
                        child.Chromosome.CranePositions.Add(inheritedPoint);
                    }
                }
            }

            // Ensure child has at least one setup point
            if (child.Chromosome.CranePositions.Count == 0)
            {
                if (p1.Count > 0) child.Chromosome.CranePositions.Add(p1[0]);
                else if (p2.Count > 0) child.Chromosome.CranePositions.Add(p2[0]);
            }

            return child;
        }
    }
}
