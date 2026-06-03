using Xunit;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.GeneticOptimization;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.LiftingCalculation;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.CraneConfiguration;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Constraints;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Optimization.Utilities;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using System.Collections.Generic;

namespace CraneOptimizer.Application.Tests
{
    public class EmptyChromosomeRegressionTests
    {
        [Fact]
        public void TestEmptyChromosomeEvaluation_NoExceptionAndCorrectDefaults()
        {
            // Reset GAProfiler & enable it
            GAProfiler.Reset();
            GAProfiler.Enabled = true;

            var configEngine = new CraneConfigurationEngine();
            var liftingEngine = new LiftingCalculationEngine(configEngine);
            var assignmentService = new ElementAssignmentService(liftingEngine, configEngine);

            var settings = new GASettings();
            var craneSpec = new CraneSpecification();
            var elements = new List<PrecastElement>
            {
                new PrecastElement
                {
                    ElementID = "EL-REG-1",
                    ElementCOG = new DomainPoint3D(10, 10, 2),
                    ElementWT = 5.0
                }
            };

            // Crane solution has 0 positions
            var solution = new CraneSolution
            {
                Chromosome = new CraneChromosome
                {
                    CranePositions = new List<DomainPoint3D>()
                }
            };

            // 1. Evaluate assignment (slow path)
            var slowResult = assignmentService.EvaluateAssignment(
                solution,
                elements,
                new Dictionary<int, List<string>>(),
                craneSpec,
                settings);

            Assert.NotNull(slowResult);
            Assert.Single(slowResult.EvaluationResults);
            
            var slowEval = slowResult.EvaluationResults[0];
            Assert.NotNull(slowEval);
            Assert.Equal("EL-REG-1", slowEval.ElementId);
            Assert.Equal(-1, slowEval.AssignedCraneIndex);
            Assert.Equal(LiftStatus.OutOfRadius, slowEval.Status);
            Assert.Equal(ReachStatus.OutOfRadius, slowEval.ReachStatus);
            Assert.Equal(ChartStatus.OutsideChartEnvelope, slowEval.ChartStatus);
            Assert.Equal(0, slowEval.AvailableCapacity);
            Assert.Equal(double.MinValue, slowEval.SafetyMargin);
            Assert.Empty(slowResult.AssignedElements);
            Assert.Empty(slowResult.AssignedLiftableElements);

            // 2. Evaluate assignment (fast path)
            var index = new LoadChartIndex(craneSpec);
            var elementCache = new ElementStaticCache(elements, settings);
            var fastResult = assignmentService.EvaluateAssignment(
                solution,
                elements,
                new Dictionary<int, List<string>>(),
                craneSpec,
                settings,
                index,
                elementCache);

            Assert.NotNull(fastResult);
            Assert.Single(fastResult.EvaluationResults);

            var fastEval = fastResult.EvaluationResults[0];
            Assert.NotNull(fastEval);
            Assert.Equal("EL-REG-1", fastEval.ElementId);
            Assert.Equal(-1, fastEval.AssignedCraneIndex);
            Assert.Equal(LiftStatus.OutOfRadius, fastEval.Status);
            Assert.Equal(ReachStatus.OutOfRadius, fastEval.ReachStatus);
            Assert.Equal(ChartStatus.OutsideChartEnvelope, fastEval.ChartStatus);
            Assert.Equal(0, fastEval.AvailableCapacity);
            Assert.Equal(double.MinValue, fastEval.SafetyMargin);
            Assert.Empty(fastResult.AssignedElements);
            Assert.Empty(fastResult.AssignedLiftableElements);

            // 3. Compute Fitness (should be 0)
            var constraintEngine = new ConstraintEngine();
            var fitnessEvaluator = new FitnessEvaluator(constraintEngine);
            var fitnessBreakdown = fitnessEvaluator.EvaluateFitness(solution, fastResult, settings);

            Assert.Equal(0, fitnessBreakdown.FitnessScore);
            Assert.Equal(0, fitnessBreakdown.CoverageRatio);

            // 4. Verify Diagnostics counter incremented (called once for slow, once for fast path)
            Assert.Equal(2, GAProfiler.EmptyChromosomeCount);
        }
    }
}
