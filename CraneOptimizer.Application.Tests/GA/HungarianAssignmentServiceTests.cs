using Xunit;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.GeneticOptimization;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.LiftingCalculation;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.CraneConfiguration;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CraneOptimizer.Application.Tests.GA
{
    public class HungarianAssignmentServiceTests
    {
        private readonly HungarianAssignmentService _service;
        private readonly ElementAssignmentService _greedyService;

        public HungarianAssignmentServiceTests()
        {
            var configEngine = new CraneConfigurationEngine();
            var capacityResolver = new LoadChartCapacityResolver();
            var liftingEngine = new LiftingCalculationEngine(configEngine, capacityResolver);
            
            _service = new HungarianAssignmentService(liftingEngine, configEngine, capacityResolver);
            _greedyService = new ElementAssignmentService(liftingEngine, configEngine, capacityResolver);
        }

        [Fact]
        public void TestSinglePosition_MatchesGreedy()
        {
            var elements = CreateTestElements(5);
            var solution = new CraneSolution
            {
                Chromosome = new CraneChromosome
                {
                    CranePositions = new List<DomainPoint3D>
                    {
                        new DomainPoint3D(0, 0, 0)
                    }
                }
            };

            var craneSpec = CreateStandardCraneSpec();
            var settings = new GASettings { MaxCranePositions = 1 };

            var resultHungarian = _service.EvaluateAssignment(solution, elements, null!, craneSpec, settings);
            var resultGreedy = _greedyService.EvaluateAssignment(solution, elements, null!, craneSpec, settings);

            Assert.Equal(resultGreedy.EvaluationResults.Count, resultHungarian.EvaluationResults.Count);
            for (int i = 0; i < elements.Count; i++)
            {
                Assert.Equal(resultGreedy.EvaluationResults[i].AssignedCraneIndex, resultHungarian.EvaluationResults[i].AssignedCraneIndex);
                Assert.Equal(resultGreedy.EvaluationResults[i].Status, resultHungarian.EvaluationResults[i].Status);
            }
        }

        [Fact]
        public void TestGlobalOptimality_WithCapacityConstraint()
        {
            // Scenario:
            // Element A is at (0, 0, 0).
            // Element B is at (1, 0, 0).
            // Crane 0 is at (0, 0, 0).
            // Crane 1 is at (10, 0, 0).
            // MaxElementsPerSetup = 1.
            // Both elements are closer to Crane 0 (radii 0 and 1) than to Crane 1 (radii 10 and 9).
            // Greedy would assign both to Crane 0, violating capacity.
            // Hungarian must assign A -> 0 (radius 0) and B -> 1 (radius 9) for total cost = 9.
            // (If it assigned A -> 1 and B -> 0, total cost would be 10 + 1 = 11, which is worse).

            var elements = new List<PrecastElement>
            {
                new PrecastElement
                {
                    ElementID = "A",
                    ElementCOG = new DomainPoint3D(0, 0, 0),
                    ElementHeight = 1.0,
                    ElementWT = 5.0
                },
                new PrecastElement
                {
                    ElementID = "B",
                    ElementCOG = new DomainPoint3D(1, 0, 0),
                    ElementHeight = 1.0,
                    ElementWT = 5.0
                }
            };

            var solution = new CraneSolution
            {
                Chromosome = new CraneChromosome
                {
                    CranePositions = new List<DomainPoint3D>
                    {
                        new DomainPoint3D(0, 0, 0),   // Crane 0
                        new DomainPoint3D(10, 0, 0)   // Crane 1
                    }
                }
            };

            var craneSpec = CreateStandardCraneSpec();
            var settings = new GASettings { MaxCranePositions = 2 };

            _service.MaxElementsPerSetup = 1;

            var result = _service.EvaluateAssignment(solution, elements, null!, craneSpec, settings);

            // Verify capacity constraint is respected
            Assert.Single(result.AssignedElements[0]);
            Assert.Single(result.AssignedElements[1]);

            // Verify optimal assignment: A assigned to 0, B assigned to 1
            var evalA = result.EvaluationResults.First(r => r.ElementId == "A");
            var evalB = result.EvaluationResults.First(r => r.ElementId == "B");

            Assert.Equal(0, evalA.AssignedCraneIndex);
            Assert.Equal(1, evalB.AssignedCraneIndex);
        }

        [Fact]
        public void TestInfeasibleElements_DoNotThrow()
        {
            // Element is far outside crane radius (1000m)
            var elements = new List<PrecastElement>
            {
                new PrecastElement
                {
                    ElementID = "FarElement",
                    ElementCOG = new DomainPoint3D(1000, 1000, 0),
                    ElementHeight = 1.0,
                    ElementWT = 5.0
                }
            };

            var solution = new CraneSolution
            {
                Chromosome = new CraneChromosome
                {
                    CranePositions = new List<DomainPoint3D>
                    {
                        new DomainPoint3D(0, 0, 0),
                        new DomainPoint3D(5, 5, 0)
                    }
                }
            };

            var craneSpec = CreateStandardCraneSpec();
            var settings = new GASettings { MaxCranePositions = 2 };

            var result = _service.EvaluateAssignment(solution, elements, null!, craneSpec, settings);

            Assert.Single(result.EvaluationResults);
            var eval = result.EvaluationResults[0];
            Assert.Equal(LiftStatus.OutOfRadius, eval.Status);
            Assert.True(eval.AssignedCraneIndex >= 0); // Still assigned to the best available index (which fails)
        }

        [Fact]
        public void TestUserOverride_ForcedAssignment()
        {
            var elements = CreateTestElements(3);
            var solution = new CraneSolution
            {
                Chromosome = new CraneChromosome
                {
                    CranePositions = new List<DomainPoint3D>
                    {
                        new DomainPoint3D(0, 0, 0),
                        new DomainPoint3D(10, 0, 0)
                    }
                }
            };

            var craneSpec = CreateStandardCraneSpec();
            var settings = new GASettings { MaxCranePositions = 2 };

            // Force element 0 to Crane 1 manually
            var overrides = new Dictionary<int, List<string>>
            {
                [1] = new List<string> { elements[0].ElementID }
            };

            var result = _service.EvaluateAssignment(solution, elements, overrides, craneSpec, settings);

            var eval0 = result.EvaluationResults.First(r => r.ElementId == elements[0].ElementID);
            Assert.Equal(1, eval0.AssignedCraneIndex);
        }

        [Fact]
        public void TestPerformanceBenchmark_OnLargeDataset()
        {
            // 3,940 elements (similar to try9).
            //
            // This is a unit-test guardrail, not a lab-grade benchmark. It should catch
            // large accidental regressions in the hot assignment path without failing
            // on ordinary Debug-build machine noise from JIT, scheduling, antivirus,
            // thermal throttling, or concurrent test-run activity.
            const int elementCount = 3940;
            const int WarmupIterations = 5;
            const int TimedIterations = 7;
            const long MedianGuardrailMs = 100;
            const long WorstRunGuardrailMs = 250;

            var elements = CreateTestElements(elementCount);

            var solution = new CraneSolution
            {
                Chromosome = new CraneChromosome
                {
                    CranePositions = new List<DomainPoint3D>
                    {
                        new DomainPoint3D(0, 0, 0),
                        new DomainPoint3D(20, 20, 0),
                        new DomainPoint3D(-20, -20, 0)
                    }
                }
            };

            var craneSpec = CreateStandardCraneSpec();
            var settings = new GASettings { MaxCranePositions = 3 };

            // Pre-build hot-path cache to simulate actual GA loop call
            var index = new LoadChartIndex(craneSpec);
            var cache = new ElementStaticCache(elements, settings);

            // Verify global load-balancing successive relocation runs (no fast path bypass)
            _service.MaxElementsPerSetup = int.MaxValue;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            for (int i = 0; i < WarmupIterations; i++)
            {
                var warmupResult = _service.EvaluateAssignment(solution, elements, null!, craneSpec, settings, index, cache);
                Assert.Equal(elementCount, warmupResult.EvaluationResults.Count);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var timings = new List<long>(TimedIterations);
            for (int i = 0; i < TimedIterations; i++)
            {
                var sw = Stopwatch.StartNew();
                var result = _service.EvaluateAssignment(solution, elements, null!, craneSpec, settings, index, cache);
                sw.Stop();

                Assert.Equal(elementCount, result.EvaluationResults.Count);
                timings.Add(sw.ElapsedMilliseconds);
            }

            var ordered = timings.OrderBy(t => t).ToList();
            long bestMs = ordered[0];
            long medianMs = ordered[ordered.Count / 2];
            long maxMs = ordered[ordered.Count - 1];

            Console.WriteLine(
                $"Hungarian Assignment Solver on {elementCount} elements timings: {string.Join(", ", timings)} ms. " +
                $"Best={bestMs} ms, Median={medianMs} ms, Max={maxMs} ms. " +
                $"Guardrails: Median<={MedianGuardrailMs} ms, Max<={WorstRunGuardrailMs} ms.");

            Assert.True(
                medianMs <= MedianGuardrailMs,
                $"Performance guardrail failed: Hungarian median took {medianMs} ms over {TimedIterations} runs " +
                $"({string.Join(", ", timings)} ms), which is > {MedianGuardrailMs} ms.");

            Assert.True(
                maxMs <= WorstRunGuardrailMs,
                $"Performance guardrail failed: Hungarian worst run took {maxMs} ms over {TimedIterations} runs " +
                $"({string.Join(", ", timings)} ms), which is > {WorstRunGuardrailMs} ms.");
        }

        // ══════════════════════════════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════════════════════════════

        private List<PrecastElement> CreateTestElements(int count)
        {
            var elements = new List<PrecastElement>(count);
            var rand = new Random(42);

            for (int i = 0; i < count; i++)
            {
                // Distribute elements randomly around (0,0)
                double x = (rand.NextDouble() - 0.5) * 40.0;
                double y = (rand.NextDouble() - 0.5) * 40.0;

                elements.Add(new PrecastElement
                {
                    ElementID = $"EL-{i}",
                    ElementCOG = new DomainPoint3D(x, y, 0),
                    ElementHeight = 1.2,
                    ElementWT = 3.0 + rand.NextDouble() * 5.0
                });
            }

            return elements;
        }

        private CraneSpecification CreateStandardCraneSpec()
        {
            return new CraneSpecification
            {
                Id = 1,
                Crane = new Crane { Name = "Standard Test Crane" },
                MaxRadius = 50.0,
                MaxBoomLength = 60.0,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 5.0, BoomLength = 10.0, Capacity = 50.0 },
                    new TargetPoint { Radius = 10.0, BoomLength = 15.0, Capacity = 40.0 },
                    new TargetPoint { Radius = 20.0, BoomLength = 25.0, Capacity = 25.0 },
                    new TargetPoint { Radius = 30.0, BoomLength = 35.0, Capacity = 15.0 },
                    // Extrapolate capacity out to max radius
                    new TargetPoint { Radius = 50.0, BoomLength = 55.0, Capacity = 5.0 }
                }
            };
        }
    }
}
