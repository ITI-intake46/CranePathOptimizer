using Xunit;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Coverage;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Schemas;
using System.Collections.Generic;

namespace CraneOptimizer.Application.Tests
{
    public class CoverageEngineTests
    {
        [Fact]
        public void TestRecalculateCoverageAndGetCoveredElementIds()
        {
            var engine = new CoverageEngine();
            
            var result = new PlacementResult
            {
                Positions = new List<CranePositionResult>
                {
                    new CranePositionResult
                    {
                        ElementResults = new List<ElementLiftResult>
                        {
                            new ElementLiftResult { ElementId = "E1", Status = LiftStatus.Liftable },
                            new ElementLiftResult { ElementId = "E2", Status = LiftStatus.Liftable },
                            new ElementLiftResult { ElementId = "E3", Status = LiftStatus.Overweight }
                        }
                    },
                    new CranePositionResult
                    {
                        ElementResults = new List<ElementLiftResult>
                        {
                            new ElementLiftResult { ElementId = "E2", Status = LiftStatus.Liftable }, // Duplicate
                            new ElementLiftResult { ElementId = "E4", Status = LiftStatus.Liftable }
                        }
                    }
                }
            };

            engine.RecalculateCoverage(result, 4);

            // Unique Liftable elements: E1, E2, E4 (count = 3)
            Assert.Equal(3, result.LiftedElementCount);
            Assert.Equal(4, result.TotalElementCount);
            Assert.Equal(0.75, result.CoverageRatio, 5);

            // Position 1: Newly covered E1, E2 -> count = 2
            Assert.Equal(2, result.Positions[0].NewLiftableCount);
            // Position 2: Newly covered E4 -> count = 1 (E2 already covered in previous position)
            Assert.Equal(1, result.Positions[1].NewLiftableCount);

            var coveredIds = engine.GetCoveredElementIds(result);
            Assert.Equal(3, coveredIds.Count);
            Assert.Contains("E1", coveredIds);
            Assert.Contains("E2", coveredIds);
            Assert.Contains("E4", coveredIds);
            Assert.DoesNotContain("E3", coveredIds);
        }

        [Fact]
        public void TestFilterRemainingElements()
        {
            var engine = new CoverageEngine();

            var all = new List<PrecastElement>
            {
                new PrecastElement { ElementID = "E1" },
                new PrecastElement { ElementID = "E2" },
                new PrecastElement { ElementID = "E3" }
            };

            var covered = new HashSet<string> { "E1", "E3" };

            var remaining = engine.FilterRemainingElements(all, covered);

            Assert.Single(remaining);
            Assert.Equal("E2", remaining[0].ElementID);
        }

        [Fact]
        public void TestDeduplicateFinalPositions()
        {
            var engine = new CoverageEngine();
            var settings = new GASettings
            {
                DuplicatePositionDistance = 5.0,
                MinimumRelocationDistance = 3.0
            };

            // minimum distance = Max(5, 3) = 5.0

            var result = new PlacementResult
            {
                Positions = new List<CranePositionResult>
                {
                    // Candidate 1: (0,0) - 3 elements
                    new CranePositionResult 
                    { 
                        X = 0, Y = 0, Z = 0, 
                        NewLiftableCount = 3, 
                        ElementResults = new List<ElementLiftResult> { new ElementLiftResult(), new ElementLiftResult(), new ElementLiftResult() } 
                    },
                    // Candidate 2: (1,1) - 4 elements (distance to C1 = sqrt(2) = 1.414 < 5.0, C2 has more new liftable elements so C2 should replace C1)
                    new CranePositionResult 
                    { 
                        X = 1, Y = 1, Z = 0, 
                        NewLiftableCount = 4, 
                        ElementResults = new List<ElementLiftResult> { new ElementLiftResult(), new ElementLiftResult(), new ElementLiftResult(), new ElementLiftResult() } 
                    },
                    // Candidate 3: (10,10) - far away, should remain
                    new CranePositionResult 
                    { 
                        X = 10, Y = 10, Z = 0, 
                        NewLiftableCount = 2, 
                        ElementResults = new List<ElementLiftResult> { new ElementLiftResult(), new ElementLiftResult() } 
                    }
                }
            };

            engine.DeduplicateFinalPositions(result, settings);

            // C1 is replaced by C2. C3 is kept. Total = 2 positions.
            Assert.Equal(2, result.Positions.Count);
            Assert.Equal(1, result.Positions[0].X);
            Assert.Equal(1, result.Positions[0].Y);
            Assert.Equal(10, result.Positions[1].X);
            Assert.Equal(10, result.Positions[1].Y);
        }
    }
}
