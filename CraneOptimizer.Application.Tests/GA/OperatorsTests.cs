using Xunit;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Operators;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.GeneticOptimization;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Constraints;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
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

            var settings = new GASettings
            {
                CraneFootprintRadius = 3.0
            };

            var child = op.Crossover(parent1, parent2, settings);

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
            Assert.All(solution.Chromosome.CranePositions, point =>
                Assert.True(constraintEngine.IsValidSetupPoint(point, zones, forbiddenZones, settings)));
        }

        [Fact]
        public void Initializer_ReturnsOnlyFootprintValidSetupPoints()
        {
            var constraintEngine = new ConstraintEngine();
            var initializer = new GAInitializer(constraintEngine);
            var settings = new GASettings
            {
                RandomSeed = 42,
                CraneFootprintRadius = 2.0,
                RenderedOperatingEnvelopeRadius = 2.0,
                PositionSamplingAttempts = 20,
                MinimumRelocationDistance = 0,
                GridStep = 0
            };
            var zones = new List<GreenAreaZone>
            {
                new GreenAreaZone
                {
                    Vertices = new List<DomainPoint3D>
                    {
                        new DomainPoint3D(0, 0, 0),
                        new DomainPoint3D(10, 0, 0),
                        new DomainPoint3D(10, 10, 0),
                        new DomainPoint3D(0, 10, 0)
                    }
                }
            };
            var forbiddenZones = new List<GreenAreaZone>
            {
                new GreenAreaZone
                {
                    Vertices = new List<DomainPoint3D>
                    {
                        new DomainPoint3D(4, 4, 0),
                        new DomainPoint3D(6, 4, 0),
                        new DomainPoint3D(6, 6, 0),
                        new DomainPoint3D(4, 6, 0)
                    }
                }
            };

            GARandomManager.Initialize(settings.RandomSeed);

            var population = initializer.InitializePopulation(
                zones,
                forbiddenZones,
                new List<PrecastElement>
                {
                    new PrecastElement { ElementID = "EL-1", ElementCOG = new DomainPoint3D(2, 2, 0), ElementWT = 1 },
                    new PrecastElement { ElementID = "EL-2", ElementCOG = new DomainPoint3D(8, 8, 0), ElementWT = 1 }
                },
                populationSize: 10,
                maxCranePositions: 2,
                settings,
                new CraneSpecification());

            Assert.NotEmpty(population.Individuals);
            foreach (var individual in population.Individuals)
            {
                Assert.NotEmpty(individual.Chromosome.CranePositions);
                Assert.All(individual.Chromosome.CranePositions, point =>
                    Assert.True(constraintEngine.IsValidSetupPoint(point, zones, forbiddenZones, settings)));
            }
        }

        [Fact]
        public void Initializer_ReturnsOnlyGroundFootprintValidSetupPoints()
        {
            var constraintEngine = new ConstraintEngine();
            var initializer = new GAInitializer(constraintEngine);
            var settings = new GASettings
            {
                RandomSeed = 7,
                CraneFootprintRadius = 0.5,
                RenderedOperatingEnvelopeRadius = 3.0,
                PositionSamplingAttempts = 40,
                MinimumRelocationDistance = 0,
                GridStep = 0
            };
            var zones = new List<GreenAreaZone>
            {
                new GreenAreaZone
                {
                    Vertices = new List<DomainPoint3D>
                    {
                        new DomainPoint3D(0, 0, 0),
                        new DomainPoint3D(12, 0, 0),
                        new DomainPoint3D(12, 12, 0),
                        new DomainPoint3D(0, 12, 0)
                    }
                }
            };

            GARandomManager.Initialize(settings.RandomSeed);

            var population = initializer.InitializePopulation(
                zones,
                new List<GreenAreaZone>(),
                new List<PrecastElement>
                {
                    new PrecastElement { ElementID = "EL-1", ElementCOG = new DomainPoint3D(2, 2, 0), ElementWT = 1 },
                    new PrecastElement { ElementID = "EL-2", ElementCOG = new DomainPoint3D(10, 10, 0), ElementWT = 1 }
                },
                populationSize: 12,
                maxCranePositions: 2,
                settings,
                new CraneSpecification());

            Assert.NotEmpty(population.Individuals);
            foreach (var individual in population.Individuals)
            {
                Assert.NotEmpty(individual.Chromosome.CranePositions);
                Assert.All(individual.Chromosome.CranePositions, point =>
                    Assert.True(constraintEngine.IsValidSetupPoint(point, zones, new List<GreenAreaZone>(), settings)));
            }
        }

        [Fact]
        public void Mutation_ReturnsOnlyGroundFootprintValidSetupPoints()
        {
            var constraintEngine = new ConstraintEngine();
            var op = new MutationOperator(constraintEngine);
            var settings = new GASettings
            {
                MutationSigma = 1.0,
                PositionSamplingAttempts = 80,
                GridStep = 0.5,
                MinimumRelocationDistance = 0,
                CraneFootprintRadius = 0.5,
                RenderedOperatingEnvelopeRadius = 3.0
            };
            var zones = new List<GreenAreaZone>
            {
                new GreenAreaZone
                {
                    Vertices = new List<DomainPoint3D>
                    {
                        new DomainPoint3D(0, 0, 0),
                        new DomainPoint3D(12, 0, 0),
                        new DomainPoint3D(12, 12, 0),
                        new DomainPoint3D(0, 12, 0)
                    }
                }
            };
            var solution = new CraneSolution
            {
                Chromosome = new CraneChromosome
                {
                    CranePositions = new List<DomainPoint3D>
                    {
                        new DomainPoint3D(6, 6, 0)
                    }
                }
            };

            GARandomManager.Initialize(99);

            for (int i = 0; i < 20; i++)
                op.Mutate(solution, zones, new List<GreenAreaZone>(), settings, i);

            Assert.All(solution.Chromosome.CranePositions, point =>
                Assert.True(constraintEngine.IsValidSetupPoint(point, zones, new List<GreenAreaZone>(), settings)));
        }
    }
}
