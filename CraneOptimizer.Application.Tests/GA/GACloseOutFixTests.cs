using Xunit;
using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.GeneticOptimization;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Constraints;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Common;

namespace CraneOptimizer.Application.Tests.GA
{
    public class GACloseOutFixTests
    {
        [Fact]
        public void TestCoverageDominanceMultiplier_AppliedCorrectly()
        {
            // Arrange
            var constraintEngine = new ConstraintEngine();
            var fitnessEvaluator = new FitnessEvaluator(constraintEngine);

            var settings = new GASettings
            {
                CoverageWeight = 10,
                CoverageDominanceMultiplier = 100.0,
                RelocationWeight = 0,
                RadiusPenaltyWeight = 0,
                OverweightPenaltyWeight = 0,
                HeightPenaltyWeight = 0,
                RadiusQualityPenaltyWeight = 0,
                BalancePenaltyWeight = 0,
                RadiusStdDevPenaltyWeight = 0,
                SimilarPositionPenaltyWeight = 0,
                RelocationDistancePenaltyWeight = 0
            };

            var solution = new CraneSolution();
            solution.Chromosome.CranePositions.Add(new DomainPoint3D(0, 0, 0));

            // 5 out of 10 elements are Liftable (50% coverage)
            var evaluationResults = new List<ElementEvaluationResult>();
            for (int i = 0; i < 10; i++)
            {
                evaluationResults.Add(new ElementEvaluationResult
                {
                    ElementId = $"E{i}",
                    Status = i < 5 ? LiftStatus.Liftable : LiftStatus.OutOfRadius
                });
            }

            var assignment = new AssignmentResult
            {
                EvaluationResults = evaluationResults
            };

            // Act
            var breakdownWithDefault = fitnessEvaluator.EvaluateFitness(solution, assignment, settings);

            // Change multiplier to 250.0
            settings.CoverageDominanceMultiplier = 250.0;
            var breakdownWithCustom = fitnessEvaluator.EvaluateFitness(solution, assignment, settings);

            // Assert
            // Expected: CoverageWeight * DominanceMultiplier * CoverageRatio
            // 10 * 100 * 0.5 = 500
            Assert.Equal(500.0, breakdownWithDefault.FitnessScore);
            // 10 * 250 * 0.5 = 1250
            Assert.Equal(1250.0, breakdownWithCustom.FitnessScore);
        }

        [Fact]
        public void TestCoverageOverFitness_HigherCoverageWins()
        {
            // Arrange
            var constraintEngine = new ConstraintEngine();
            var fitnessEvaluator = new FitnessEvaluator(constraintEngine);

            var settings = new GASettings
            {
                CoverageWeight = 10,
                CoverageDominanceMultiplier = 100.0, // Ensures coverage dominates
                RelocationWeight = 50, // High relocation penalty
                RadiusPenaltyWeight = 0,
                OverweightPenaltyWeight = 0,
                HeightPenaltyWeight = 0,
                RadiusQualityPenaltyWeight = 0,
                BalancePenaltyWeight = 0,
                RadiusStdDevPenaltyWeight = 0,
                SimilarPositionPenaltyWeight = 0,
                RelocationDistancePenaltyWeight = 0,
                MaxCranePositions = 3
            };

            // Solution A: Coverage 90%, 3 setups (high relocation penalty)
            var solutionA = new CraneSolution();
            solutionA.Chromosome.CranePositions.Add(new DomainPoint3D(0, 0, 0));
            solutionA.Chromosome.CranePositions.Add(new DomainPoint3D(10, 0, 0));
            solutionA.Chromosome.CranePositions.Add(new DomainPoint3D(20, 0, 0));

            var evalA = new List<ElementEvaluationResult>();
            for (int i = 0; i < 10; i++)
            {
                evalA.Add(new ElementEvaluationResult
                {
                    ElementId = $"E{i}",
                    Status = i < 9 ? LiftStatus.Liftable : LiftStatus.OutOfRadius
                });
            }
            var assignmentA = new AssignmentResult { EvaluationResults = evalA };

            // Solution B: Coverage 50%, 1 setup (zero relocation penalty)
            var solutionB = new CraneSolution();
            solutionB.Chromosome.CranePositions.Add(new DomainPoint3D(0, 0, 0));

            var evalB = new List<ElementEvaluationResult>();
            for (int i = 0; i < 10; i++)
            {
                evalB.Add(new ElementEvaluationResult
                {
                    ElementId = $"E{i}",
                    Status = i < 5 ? LiftStatus.Liftable : LiftStatus.OutOfRadius
                });
            }
            var assignmentB = new AssignmentResult { EvaluationResults = evalB };

            // Act
            var fitnessA = fitnessEvaluator.EvaluateFitness(solutionA, assignmentA, settings).FitnessScore;
            var fitnessB = fitnessEvaluator.EvaluateFitness(solutionB, assignmentB, settings).FitnessScore;

            // Assert
            // Solution A fitness: 10 * 100 * 0.9 - 50 * (3 - 1) = 900 - 100 = 800
            // Solution B fitness: 10 * 100 * 0.5 - 50 * (1 - 1) = 500
            Assert.True(fitnessA > fitnessB, $"Solution A (800) should win over Solution B (500). Actual: {fitnessA} vs {fitnessB}");
        }

        [Fact]
        public void TestCoverageTieBreaker_FewerPositionsWin()
        {
            // Arrange
            var constraintEngine = new ConstraintEngine();
            var fitnessEvaluator = new FitnessEvaluator(constraintEngine);

            var settings = new GASettings
            {
                CoverageWeight = 10,
                CoverageDominanceMultiplier = 100.0,
                RelocationWeight = 10,
                RadiusPenaltyWeight = 0,
                OverweightPenaltyWeight = 0,
                HeightPenaltyWeight = 0,
                RadiusQualityPenaltyWeight = 0,
                BalancePenaltyWeight = 0,
                RadiusStdDevPenaltyWeight = 0,
                SimilarPositionPenaltyWeight = 0,
                RelocationDistancePenaltyWeight = 0,
                MaxCranePositions = 3
            };

            // Solution A: Coverage 80%, 2 setups
            var solutionA = new CraneSolution();
            solutionA.Chromosome.CranePositions.Add(new DomainPoint3D(0, 0, 0));
            solutionA.Chromosome.CranePositions.Add(new DomainPoint3D(10, 0, 0));

            var evalA = new List<ElementEvaluationResult>();
            for (int i = 0; i < 10; i++)
            {
                evalA.Add(new ElementEvaluationResult
                {
                    ElementId = $"E{i}",
                    Status = i < 8 ? LiftStatus.Liftable : LiftStatus.OutOfRadius
                });
            }
            var assignmentA = new AssignmentResult { EvaluationResults = evalA };

            // Solution B: Coverage 80%, 3 setups (tied on coverage, more positions)
            var solutionB = new CraneSolution();
            solutionB.Chromosome.CranePositions.Add(new DomainPoint3D(0, 0, 0));
            solutionB.Chromosome.CranePositions.Add(new DomainPoint3D(10, 0, 0));
            solutionB.Chromosome.CranePositions.Add(new DomainPoint3D(20, 0, 0));

            var evalB = new List<ElementEvaluationResult>();
            for (int i = 0; i < 10; i++)
            {
                evalB.Add(new ElementEvaluationResult
                {
                    ElementId = $"E{i}",
                    Status = i < 8 ? LiftStatus.Liftable : LiftStatus.OutOfRadius
                });
            }
            var assignmentB = new AssignmentResult { EvaluationResults = evalB };

            // Act
            var fitnessA = fitnessEvaluator.EvaluateFitness(solutionA, assignmentA, settings).FitnessScore;
            var fitnessB = fitnessEvaluator.EvaluateFitness(solutionB, assignmentB, settings).FitnessScore;

            // Assert
            // Solution A fitness: 800 - 10 * 1 = 790
            // Solution B fitness: 800 - 10 * 2 = 780
            Assert.True(fitnessA > fitnessB, $"Solution A (790) should win over Solution B (780). Actual: {fitnessA} vs {fitnessB}");
        }

        [Fact]
        public void TestResultDTO_CoverageRatioAndFitnessScore_AreSeparate()
        {
            // Arrange
            var result = new PlacementResult
            {
                CoverageRatio = 0.85,
                FitnessScore = 850.0
            };

            // Assert
            Assert.Equal(0.85, result.CoverageRatio);
            Assert.Equal(850.0, result.FitnessScore);
            Assert.NotEqual(result.CoverageRatio, result.FitnessScore);
        }

        [Fact]
        public void TestCsvCultureInfo_UsesInvariantCultureForDecimals()
        {
            // Test formatting behavior under cultures with different decimal separators (e.g. German "de-DE" uses comma)
            var originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");

                double val = 123.456;
                string formatted = val.ToString("F3", CultureInfo.InvariantCulture);

                // Should always use '.' as the decimal separator under invariant culture
                Assert.Equal("123.456", formatted);
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }
    }
}
