using Xunit;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Interfaces;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CraneOptimizer.Application.Tests.GA
{
    public class MultiStageGAOrchestratorTests
    {
        private class MockPlacementEngine : ICranePlacementEngine
        {
            public List<PlacementInput> InputsCaptured { get; } = new List<PlacementInput>();
            public Func<PlacementInput, CancellationToken, PlacementResult>? OptimizationMock { get; set; }

            public PlacementResult RunOptimization(PlacementInput input, CancellationToken cancellationToken)
            {
                InputsCaptured.Add(input);
                if (OptimizationMock != null)
                {
                    return OptimizationMock(input, cancellationToken);
                }
                return new PlacementResult
                {
                    CoverageRatio = 0.5,
                    Positions = new List<CranePositionResult>()
                };
            }
        }

        [Fact]
        public async Task TestSingleStageTermination_WhenTargetMet()
        {
            var mockEngine = new MockPlacementEngine();
            mockEngine.OptimizationMock = (input, token) => new PlacementResult
            {
                CoverageRatio = 0.95,
                Positions = new List<CranePositionResult> { new CranePositionResult() }
            };

            var orchestrator = new MultiStageGAOrchestrator(mockEngine);
            var baseSettings = new GASettings
            {
                MaxCranePositions = 3,
                CoverageWeight = 10,
                AdaptiveRelocationScaleFactor = 0.4
            };

            var input = new PlacementInput { Settings = baseSettings };
            var crane = new Crane();

            var result = await orchestrator.RunAsync(input, crane, baseSettings, 0.90, CancellationToken.None);

            Assert.Equal(1, result.StagesExecuted);
            Assert.Equal("CoverageTargetMet", result.TerminationReason);
            Assert.Single(mockEngine.InputsCaptured);
            Assert.Equal(1, mockEngine.InputsCaptured[0].Settings.MaxCranePositions);
        }

        [Fact]
        public async Task TestTwoStageEscalation_WhenTargetMissedInStage1()
        {
            var mockEngine = new MockPlacementEngine();
            int callCount = 0;
            mockEngine.OptimizationMock = (input, token) =>
            {
                callCount++;
                return new PlacementResult
                {
                    CoverageRatio = callCount == 1 ? 0.70 : 0.92,
                    Positions = new List<CranePositionResult> { new CranePositionResult() }
                };
            };

            var orchestrator = new MultiStageGAOrchestrator(mockEngine);
            var baseSettings = new GASettings
            {
                MaxCranePositions = 3,
                CoverageWeight = 10,
                AdaptiveRelocationScaleFactor = 0.4
            };

            var input = new PlacementInput { Settings = baseSettings };
            var crane = new Crane();

            var result = await orchestrator.RunAsync(input, crane, baseSettings, 0.90, CancellationToken.None);

            Assert.Equal(2, result.StagesExecuted);
            Assert.Equal("CoverageTargetMet", result.TerminationReason);
            Assert.Equal(2, mockEngine.InputsCaptured.Count);
            Assert.Equal(1, mockEngine.InputsCaptured[0].Settings.MaxCranePositions);
            Assert.Equal(2, mockEngine.InputsCaptured[1].Settings.MaxCranePositions);
        }

        [Fact]
        public void TestAdaptiveWeightFormula()
        {
            // Formula: weight = (1.0 - previousCoverage) * coverageWeight * scaleFactor
            // Guard: Assert adaptiveWeight is always strictly less than CoverageWeight
            double w1 = MultiStageGAOrchestrator.ComputeAdaptiveRelocationWeight(0.0, 10.0, 0.4);
            Assert.Equal(4.0, w1);
            Assert.True(w1 < 10.0);

            // Test extreme case where previousCoverage is 0, scaleFactor is 1.5 (should trigger guard)
            double w2 = MultiStageGAOrchestrator.ComputeAdaptiveRelocationWeight(0.0, 10.0, 1.5);
            Assert.Equal(9.9, w2);
            Assert.True(w2 < 10.0);
        }

        [Fact]
        public async Task TestCancellation_MidStageReturnsCompletedStages()
        {
            var mockEngine = new MockPlacementEngine();
            int callCount = 0;
            var cts = new CancellationTokenSource();

            mockEngine.OptimizationMock = (input, token) =>
            {
                callCount++;
                if (callCount == 2)
                {
                    cts.Cancel();
                    throw new OperationCanceledException(token);
                }
                return new PlacementResult
                {
                    CoverageRatio = 0.60,
                    Positions = new List<CranePositionResult> { new CranePositionResult() }
                };
            };

            var orchestrator = new MultiStageGAOrchestrator(mockEngine);
            var baseSettings = new GASettings
            {
                MaxCranePositions = 3,
                CoverageWeight = 10,
                AdaptiveRelocationScaleFactor = 0.4
            };

            var input = new PlacementInput { Settings = baseSettings };
            var crane = new Crane();

            var result = await orchestrator.RunAsync(input, crane, baseSettings, 0.90, cts.Token);

            Assert.Equal(2, result.StagesExecuted);
            Assert.Equal("Cancelled", result.TerminationReason);
            Assert.Equal(0.60, result.BestResult.CoverageRatio);
        }
    }
}
