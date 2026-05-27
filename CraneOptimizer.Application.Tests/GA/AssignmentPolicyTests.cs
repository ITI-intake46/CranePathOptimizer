using Xunit;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.Coverage;
using System.Collections.Generic;
using System.Linq;

namespace CraneOptimizer.Application.Tests
{
    public class AssignmentPolicyTests
    {
        [Fact]
        public void TestElementFeasibleFromTwoPointsIsFinallyAssignedToOne()
        {
            var elements = new List<PrecastElement>
            {
                new PrecastElement { ElementID = "E1" }
            };

            var positions = new List<CranePositionResult>
            {
                new CranePositionResult
                {
                    ElementResults = new List<ElementLiftResult>
                    {
                        new ElementLiftResult { ElementId = "E1", Status = LiftStatus.Liftable, SafetyMargin = 0.5, RequiredRadius = 10, BoomLength = 12 }
                    }
                },
                new CranePositionResult
                {
                    ElementResults = new List<ElementLiftResult>
                    {
                        new ElementLiftResult { ElementId = "E1", Status = LiftStatus.Liftable, SafetyMargin = 0.8, RequiredRadius = 8, BoomLength = 10 }
                    }
                }
            };

            AssignmentPolicy.ResolveFinalAssignments(positions, elements, new CraneSpecification(), new GASettings());

            // Unique assignment verification
            Assert.True(positions[1].ElementResults[0].IsFinalAssignedToPoint);
            Assert.False(positions[0].ElementResults[0].IsFinalAssignedToPoint);

            Assert.Equal("Point_1", positions[0].ElementResults[0].AssignedPointId);
            Assert.Equal("Point_1", positions[1].ElementResults[0].AssignedPointId);
            Assert.Equal("Point_1", positions[0].ElementResults[0].FinalOwnerPointId);
            Assert.True(positions[0].ElementResults[0].IsFinalAssigned);
            Assert.True(positions[0].ElementResults[0].DuplicateAssignmentResolved);
            Assert.Equal("Point_0,Point_1", positions[0].ElementResults[0].FeasiblePointIds);
        }

        [Fact]
        public void TestStableTieBreakerPriority()
        {
            var elements = new List<PrecastElement>
            {
                new PrecastElement { ElementID = "E1" }
            };

            // Two points with identical metrics -> tie broken by CandidatePointIndex (prefer index 0)
            var positions = new List<CranePositionResult>
            {
                new CranePositionResult
                {
                    ElementResults = new List<ElementLiftResult>
                    {
                        new ElementLiftResult { ElementId = "E1", Status = LiftStatus.Liftable, SafetyMargin = 0.5, RequiredRadius = 10, BoomLength = 12 }
                    }
                },
                new CranePositionResult
                {
                    ElementResults = new List<ElementLiftResult>
                    {
                        new ElementLiftResult { ElementId = "E1", Status = LiftStatus.Liftable, SafetyMargin = 0.5, RequiredRadius = 10, BoomLength = 12 }
                    }
                }
            };

            AssignmentPolicy.ResolveFinalAssignments(positions, elements, new CraneSpecification(), new GASettings());

            Assert.True(positions[0].ElementResults[0].IsFinalAssignedToPoint);
            Assert.False(positions[1].ElementResults[0].IsFinalAssignedToPoint);
            Assert.Equal("Point_0", positions[0].ElementResults[0].AssignedPointId);
        }

        [Fact]
        public void TestNonLiftableElementRemainsUnassigned()
        {
            var elements = new List<PrecastElement>
            {
                new PrecastElement { ElementID = "E1" }
            };

            var positions = new List<CranePositionResult>
            {
                new CranePositionResult
                {
                    ElementResults = new List<ElementLiftResult>
                    {
                        new ElementLiftResult { ElementId = "E1", Status = LiftStatus.OutOfRadius, SafetyMargin = -0.5, RequiredRadius = 35, BoomLength = 40 }
                    }
                },
                new CranePositionResult
                {
                    ElementResults = new List<ElementLiftResult>
                    {
                        new ElementLiftResult { ElementId = "E1", Status = LiftStatus.Overweight, SafetyMargin = -0.2, RequiredRadius = 15, BoomLength = 20 }
                    }
                }
            };

            AssignmentPolicy.ResolveFinalAssignments(positions, elements, new CraneSpecification(), new GASettings());

            // Both points should be unassigned
            Assert.False(positions[0].ElementResults[0].IsFinalAssignedToPoint);
            Assert.False(positions[1].ElementResults[0].IsFinalAssignedToPoint);
            Assert.False(positions[0].ElementResults[0].IsFinalAssigned);
            Assert.Equal(string.Empty, positions[0].ElementResults[0].AssignedPointId);

            // Diagnostics must store best failed point (Point 1 has higher rank/better safety margin than Point 0)
            Assert.Equal("Point_1", positions[0].ElementResults[0].BestFailedPointId);
            Assert.Equal("Overweight", positions[0].ElementResults[0].BestFailureReason);
        }

        [Fact]
        public void TestCoverageCountsUniqueElementsOnlyOnce()
        {
            var coverageEngine = new CoverageEngine();
            var elements = new List<PrecastElement>
            {
                new PrecastElement { ElementID = "E1" },
                new PrecastElement { ElementID = "E2" }
            };

            var result = new PlacementResult
            {
                Positions = new List<CranePositionResult>
                {
                    new CranePositionResult
                    {
                        ElementResults = new List<ElementLiftResult>
                        {
                            new ElementLiftResult { ElementId = "E1", Status = LiftStatus.Liftable, SafetyMargin = 0.8, RequiredRadius = 10, BoomLength = 12 },
                            new ElementLiftResult { ElementId = "E2", Status = LiftStatus.Liftable, SafetyMargin = 0.5, RequiredRadius = 15, BoomLength = 18 }
                        }
                    },
                    new CranePositionResult
                    {
                        ElementResults = new List<ElementLiftResult>
                        {
                            new ElementLiftResult { ElementId = "E1", Status = LiftStatus.Liftable, SafetyMargin = 0.5, RequiredRadius = 15, BoomLength = 18 },
                            new ElementLiftResult { ElementId = "E2", Status = LiftStatus.Liftable, SafetyMargin = 0.8, RequiredRadius = 10, BoomLength = 12 }
                        }
                    }
                }
            };

            // Resolve final unique assignments first
            AssignmentPolicy.ResolveFinalAssignments(result.Positions, elements, new CraneSpecification(), new GASettings());

            // Verify unique assignment validation
            int finalAssignedCount0 = result.Positions[0].ElementResults.Count(r => r.IsFinalAssignedToPoint);
            int finalAssignedCount1 = result.Positions[1].ElementResults.Count(r => r.IsFinalAssignedToPoint);
            Assert.Equal(1, finalAssignedCount0);
            Assert.Equal(1, finalAssignedCount1);

            // Recalculate coverage and ensure unique lift count is exactly 2, not 4!
            coverageEngine.RecalculateCoverage(result, 2);
            Assert.Equal(2, result.LiftedElementCount);
            Assert.Equal(1.0, result.CoverageRatio);
            Assert.Equal(1, result.Positions[0].NewLiftableCount);
            Assert.Equal(1, result.Positions[1].NewLiftableCount);
        }

        [Fact]
        public void TestUnassignedElementsPopulateBestFailedDetails()
        {
            var elements = new List<PrecastElement> { new PrecastElement { ElementID = "E1" } };
            var positions = new List<CranePositionResult>
            {
                new CranePositionResult
                {
                    ElementResults = new List<ElementLiftResult>
                    {
                        new ElementLiftResult
                        {
                            ElementId = "E1",
                            Status = LiftStatus.OutOfRadius,
                            ReachStatus = ReachStatus.OutOfRadius,
                            ChartStatus = ChartStatus.OutsideChartEnvelope,
                            SafetyMargin = -0.5,
                            RequiredRadius = 35,
                            BoomLength = 40
                        }
                    }
                }
            };

            AssignmentPolicy.ResolveFinalAssignments(positions, elements, new CraneSpecification(), new GASettings());

            var res = positions[0].ElementResults[0];
            Assert.False(res.IsFinalAssigned);
            Assert.True(res.HasBestFailedCandidate);
            Assert.Equal(LiftStatus.OutOfRadius, res.BestFailedLiftStatus);
            Assert.Equal(ReachStatus.OutOfRadius, res.BestFailedReachStatus);
            Assert.Equal(ChartStatus.OutsideChartEnvelope, res.BestFailedChartStatus);
            Assert.Equal(-0.5, res.BestFailedSafetyMargin);
            Assert.Equal(35, res.BestFailedRequiredRadius);
            Assert.Equal(40, res.BestFailedRequiredBoomLength);
        }

        [Fact]
        public void TestAssignedElementsClearBestFailedDetails()
        {
            var elements = new List<PrecastElement> { new PrecastElement { ElementID = "E1" } };
            var positions = new List<CranePositionResult>
            {
                new CranePositionResult
                {
                    ElementResults = new List<ElementLiftResult>
                    {
                        new ElementLiftResult
                        {
                            ElementId = "E1",
                            Status = LiftStatus.Liftable,
                            ReachStatus = ReachStatus.Reachable,
                            ChartStatus = ChartStatus.ListedCapacityFound,
                            SafetyMargin = 0.5,
                            RequiredRadius = 15,
                            BoomLength = 18
                        }
                    }
                }
            };

            AssignmentPolicy.ResolveFinalAssignments(positions, elements, new CraneSpecification(), new GASettings());

            var res = positions[0].ElementResults[0];
            Assert.True(res.IsFinalAssigned);
            Assert.False(res.HasBestFailedCandidate);
            Assert.Equal(0.0, res.BestFailedSafetyMargin);
            Assert.Equal(0.0, res.BestFailedRequiredRadius);
            Assert.Equal(0.0, res.BestFailedRequiredBoomLength);
        }

        [Fact]
        public void TestSceneRenderingRules()
        {
            // Mocks the exact dictionary mapping of SceneRenderingManager
            var selectedPointId = "Point_0";

            // Helper rendering map function matching SceneRenderingManager logic exactly
            Func<ElementLiftResult, bool, string, LiftStatus?> mapStatus = (item, hasFinalAssignments, selectedPtId) =>
            {
                if (hasFinalAssignments)
                {
                    if (item.IsFinalAssignedToPoint || (!string.IsNullOrEmpty(item.AssignedPointId) && item.AssignedPointId == selectedPtId))
                    {
                        return (LiftStatus?)item.Status;
                    }
                    else if (item.IsFinalAssigned && !string.IsNullOrEmpty(item.AssignedPointId) && item.AssignedPointId != selectedPtId)
                    {
                        return null; // Dimmed/Blueprint Gray
                    }
                    else if (!item.IsFinalAssigned)
                    {
                        return (LiftStatus?)item.BestFailedLiftStatus;
                    }
                    else
                    {
                        return null; // Safe neutral fallback
                    }
                }
                return (LiftStatus?)item.Status;
            };

            // 1. Assigned to selected point
            var assignedToSelected = new ElementLiftResult
            {
                ElementId = "E1",
                Status = LiftStatus.Liftable,
                IsFinalAssigned = true,
                IsFinalAssignedToPoint = true,
                AssignedPointId = "Point_0"
            };
            Assert.Equal(LiftStatus.Liftable, mapStatus(assignedToSelected, true, selectedPointId));

            // 2. Assigned to another point
            var assignedToOther = new ElementLiftResult
            {
                ElementId = "E2",
                Status = LiftStatus.Liftable,
                IsFinalAssigned = true,
                IsFinalAssignedToPoint = false,
                AssignedPointId = "Point_1"
            };
            Assert.Null(mapStatus(assignedToOther, true, selectedPointId));

            // 3. Unassigned Overweight
            var unassignedOverweight = new ElementLiftResult
            {
                ElementId = "E3",
                Status = LiftStatus.Overweight,
                IsFinalAssigned = false,
                BestFailedLiftStatus = LiftStatus.Overweight
            };
            Assert.Equal(LiftStatus.Overweight, mapStatus(unassignedOverweight, true, selectedPointId));

            // 4. Unassigned OutOfRadius
            var unassignedOutOfRadius = new ElementLiftResult
            {
                ElementId = "E4",
                Status = LiftStatus.OutOfRadius,
                IsFinalAssigned = false,
                BestFailedLiftStatus = LiftStatus.OutOfRadius
            };
            Assert.Equal(LiftStatus.OutOfRadius, mapStatus(unassignedOutOfRadius, true, selectedPointId));

            // 5. Unassigned NotCharted
            var unassignedNotCharted = new ElementLiftResult
            {
                ElementId = "E5",
                Status = LiftStatus.NotCharted,
                IsFinalAssigned = false,
                BestFailedLiftStatus = LiftStatus.NotCharted
            };
            Assert.Equal(LiftStatus.NotCharted, mapStatus(unassignedNotCharted, true, selectedPointId));

            // 6. Unassigned SpecialCase
            var unassignedSpecialCase = new ElementLiftResult
            {
                ElementId = "E6",
                Status = LiftStatus.SpecialCase,
                IsFinalAssigned = false,
                BestFailedLiftStatus = LiftStatus.SpecialCase
            };
            Assert.Equal(LiftStatus.SpecialCase, mapStatus(unassignedSpecialCase, true, selectedPointId));
        }

        [Fact]
        public void TestRendererDoesNotDependOnGroupLastListOrdering()
        {
            var selectedPointId = "Point_0";
            
            // Multiple records for the same element in the group
            var results = new List<ElementLiftResult>
            {
                new ElementLiftResult { ElementId = "E1", CandidatePointId = "Point_1", Status = LiftStatus.Liftable, IsFinalAssigned = true, AssignedPointId = "Point_1", IsFinalAssignedToPoint = false },
                new ElementLiftResult { ElementId = "E1", CandidatePointId = "Point_0", Status = LiftStatus.Liftable, IsFinalAssigned = true, AssignedPointId = "Point_1", IsFinalAssignedToPoint = false }
            };

            // Order-independent resolution inside SceneRenderingManager:
            var grouped = results.GroupBy(r => r.ElementId).First();
            var item = grouped.FirstOrDefault(r => r.CandidatePointId == selectedPointId)
                       ?? grouped.FirstOrDefault(r => r.IsFinalAssignedToPoint)
                       ?? grouped.Last();

            // We expect it to successfully pick the record matching the selected point ("Point_0") even though it's not the first or last necessarily in all collections.
            Assert.Equal("Point_0", item.CandidatePointId);
        }

        [Fact]
        public void TestLiftStatusPurity()
        {
            // Pure lift status check
            var names = System.Enum.GetNames(typeof(LiftStatus));
            Assert.Equal(6, names.Length);
            Assert.Contains("Liftable", names);
            Assert.Contains("Overweight", names);
            Assert.Contains("NotCharted", names);
            Assert.Contains("OutOfRadius", names);
            Assert.Contains("HeightFail", names);
            Assert.Contains("SpecialCase", names);
            
            // Explicitly assert that no UI-only visual states are added
            Assert.DoesNotContain("AssignedToPoint", names);
        }
    }
}
