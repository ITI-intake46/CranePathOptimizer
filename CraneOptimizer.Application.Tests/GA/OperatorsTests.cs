using Xunit;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Operators;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.GeneticOptimization;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Constraints;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using System.Collections.Generic;

namespace CraneOptimizer.Application.Tests
{
    public class OperatorsTests
    {
        [Fact]
        public void TestCrossoverOperator()
        {
            var op = new CrossoverOperator();

            var parent1 = new CraneSolution
            {
                Chromosome = new CraneChromosome
                {
                    CranePositions = new List<DomainPoint3D>
                    {
                        new DomainPoint3D(1, 1, 0),
                        new DomainPoint3D(2, 2, 0)
                    }
                }
            };

            var parent2 = new CraneSolution
            {
                Chromosome = new CraneChromosome
                {
                    CranePositions = new List<DomainPoint3D>
                    {
                        new DomainPoint3D(3, 3, 0),
                        new DomainPoint3D(4, 4, 0)
                    }
                }
            };

            GARandomManager.Initialize(42); // Seed to guarantee determinism

            var child = op.Crossover(parent1, parent2);

            Assert.NotNull(child);
            Assert.NotEmpty(child.Chromosome.CranePositions);
            // Positions should come from parents
            foreach (var pos in child.Chromosome.CranePositions)
            {
                bool matchesP1 = pos.X == 1 || pos.X == 2;
                bool matchesP2 = pos.X == 3 || pos.X == 4;
                Assert.True(matchesP1 || matchesP2);
            }
        }

        [Fact]
        public void TestMutationOperator()
        {
            var constraintEngine = new ConstraintEngine();
            var op = new MutationOperator(constraintEngine);

            var settings = new GASettings
            {
                MutationSigma = 1.0,
                PositionSamplingAttempts = 100,
                GridStep = 0.5
            };

            // Setup mock allowed area: extremely large to avoid validation failures
            var allowedZone = new GreenAreaZone
            {
                Vertices = new List<DomainPoint3D>
                {
                    new DomainPoint3D(-100, -100, 0),
                    new DomainPoint3D(100, -100, 0),
                    new DomainPoint3D(100, 100, 0),
                    new DomainPoint3D(-100, 100, 0)
                }
            };

            var zones = new List<GreenAreaZone> { allowedZone };
            var forbiddenZones = new List<GreenAreaZone>();

            var solution = new CraneSolution
            {
                Chromosome = new CraneChromosome
                {
                    CranePositions = new List<DomainPoint3D>
                    {
                        new DomainPoint3D(10, 10, 0)
                    }
                }
            };

            GARandomManager.Initialize(12345); // Seed to guarantee determinism

            // Run mutation multiple times to ensure we get a change
            bool modified = false;
            for (int i = 0; i < 10; i++)
            {
                op.Mutate(solution, zones, forbiddenZones, settings, 0);
                if (solution.Chromosome.CranePositions[0].X != 10.0 || solution.Chromosome.CranePositions[0].Y != 10.0)
                {
                    modified = true;
                    break;
                }
            }

            Assert.True(modified, "Point should have mutated within the allowed zone boundaries");
        }
    }
}
