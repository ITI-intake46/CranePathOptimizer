using Xunit;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Entities;
using System.Collections.Generic;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.CraneConfiguration;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.LiftingCalculation;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Application.Tests
{
    public class LoadChartCapacityResolverTests
    {
        private readonly LoadChartCapacityResolver _resolver;
        private readonly GASettings _settings;

        public LoadChartCapacityResolverTests()
        {
            _resolver = new LoadChartCapacityResolver();
            _settings = new GASettings
            {
                DeratingRatio = 1.0,
                SafetyFactor = 1.0
            };
        }

        [Fact]
        public void TestBlankCellReturnsNotCharted()
        {
            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 20, Capacity = -1.0 } // blank sentinel
                }
            };

            var result = _resolver.Resolve(craneSpec, 10, 20, 5, _settings);

            Assert.Equal(LiftStatus.NotCharted, result.LiftStatus);
            Assert.Equal(ChartStatus.InvalidBlankCell, result.ChartStatus);
            Assert.True(result.IsBlankCell);
            Assert.False(result.IsZeroCell);
        }

        [Fact]
        public void TestZeroCellReturnsNotCharted()
        {
            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 20, Capacity = 0.0 } // explicit zero
                }
            };

            var result = _resolver.Resolve(craneSpec, 10, 20, 5, _settings);

            Assert.Equal(LiftStatus.NotCharted, result.LiftStatus);
            Assert.Equal(ChartStatus.InvalidZeroCell, result.ChartStatus);
            Assert.True(result.IsZeroCell);
            Assert.False(result.IsBlankCell);
        }

        [Fact]
        public void TestConservativeSteppedRadiusLookup()
        {
            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 20, Capacity = 10.0 },
                    new TargetPoint { Radius = 15, BoomLength = 20, Capacity = 8.0 }
                }
            };

            // Required radius is 12, next listed >= 12 is 15.
            var result = _resolver.Resolve(craneSpec, 12, 20, 5, _settings);

            Assert.Equal(LiftStatus.Liftable, result.LiftStatus);
            Assert.Equal(ChartStatus.ListedCapacityFound, result.ChartStatus);
            Assert.Equal(15.0, result.SelectedChartRadius);
            Assert.Equal(20.0, result.SelectedChartBoomLength);
            Assert.True(result.IsSteppedRadius);
            Assert.False(result.IsExactMatch);
            Assert.Equal(8.0, result.AvailableCapacity);
        }

        [Fact]
        public void TestConservativeSteppedBoomLengthLookup()
        {
            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 20, Capacity = 10.0 },
                    new TargetPoint { Radius = 10, BoomLength = 30, Capacity = 9.0 }
                }
            };

            // Required boom is 25, next listed >= 25 is 30.
            var result = _resolver.Resolve(craneSpec, 10, 25, 5, _settings);

            Assert.Equal(LiftStatus.Liftable, result.LiftStatus);
            Assert.Equal(ChartStatus.ListedCapacityFound, result.ChartStatus);
            Assert.Equal(10.0, result.SelectedChartRadius);
            Assert.Equal(30.0, result.SelectedChartBoomLength);
            Assert.Equal(9.0, result.AvailableCapacity);
        }

        [Fact]
        public void TestOutsideChartEnvelope()
        {
            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 20, Capacity = 10.0 },
                    new TargetPoint { Radius = 20, BoomLength = 20, Capacity = 8.0 }
                }
            };

            // Exceeds max radius in chart (max radius is 20)
            var result = _resolver.Resolve(craneSpec, 25, 20, 5, _settings);

            Assert.Equal(LiftStatus.NotCharted, result.LiftStatus);
            Assert.Equal(ChartStatus.OutsideChartEnvelope, result.ChartStatus);
        }

        [Fact]
        public void TestOverweightCapacityLimit()
        {
            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 20, Capacity = 10.0 }
                }
            };

            // Weight is 12, capacity is 10
            var result = _resolver.Resolve(craneSpec, 10, 20, 12, _settings);

            Assert.Equal(LiftStatus.Overweight, result.LiftStatus);
            Assert.Equal(ChartStatus.ListedCapacityFound, result.ChartStatus);
            Assert.Equal(10.0, result.AvailableCapacity);
            Assert.Equal(-2.0, result.SafetyMargin);
        }

        [Fact]
        public void TestValidExactMatchCapacity()
        {
            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 20, Capacity = 10.0 }
                }
            };

            // Exact match for 10m radius, 20m boom
            var result = _resolver.Resolve(craneSpec, 10, 20, 5, _settings);

            Assert.Equal(LiftStatus.Liftable, result.LiftStatus);
            Assert.Equal(ChartStatus.ListedCapacityFound, result.ChartStatus);
            Assert.True(result.IsExactMatch);
            Assert.False(result.IsSteppedRadius);
            Assert.Equal(10.0, result.AvailableCapacity);
            Assert.Equal(5.0, result.SafetyMargin);
        }

        [Fact]
        public void TestProblematicElementsResolveToNotChartedZeroCell()
        {
            // Problematic columns: RequiredRadius = 18.734m, SelectedChartRadius = 20.0m, SelectedChartBoom = 20.1m, Capacity = 0.0
            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 20.1, Capacity = 10.0 },
                    new TargetPoint { Radius = 20, BoomLength = 20.1, Capacity = 0.0 } // explicit zero cell
                }
            };

            var result = _resolver.Resolve(craneSpec, 18.734, 19.829, 1.84, _settings);

            Assert.Equal(LiftStatus.NotCharted, result.LiftStatus);
            Assert.Equal(ChartStatus.InvalidZeroCell, result.ChartStatus);
            Assert.True(result.IsZeroCell);
            Assert.False(result.IsBlankCell);
            Assert.Equal(20.0, result.SelectedChartRadius);
            Assert.Equal(20.1, result.SelectedChartBoomLength);
        }

        [Fact]
        public void TestGreenReferenceElementsResolveToLiftable()
        {
            // Green references: RequiredRadius = 17.169m, SelectedChartRadius = 18.0m/20.0m, SelectedChartBoom = 20.1m, Capacity = 3.249
            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 20, BoomLength = 20.1, Capacity = 3.249 }
                }
            };

            var result = _resolver.Resolve(craneSpec, 17.169, 18.358, 1.84, _settings);

            Assert.Equal(LiftStatus.Liftable, result.LiftStatus);
            Assert.Equal(ChartStatus.ListedCapacityFound, result.ChartStatus);
            Assert.Equal(3.249, result.AvailableCapacity);
            Assert.Equal(3.249 - 1.84, result.SafetyMargin);
        }

        [Fact]
        public void TestCentralizedPolicyClassificationDoesNotFallToUnknown()
        {
            var classification = LiftClassificationPolicy.Classify(
                LiftStatus.NotCharted,
                ChartStatus.InvalidZeroCell,
                1.84,
                19.829,
                40.0,
                true);

            Assert.Equal("InvalidZeroCell", classification.FailureReason);
            Assert.Equal("InvalidZeroCell", classification.FailureCategory);
            Assert.Contains("suggest review crane load chart zero cell rating.", classification.SuggestedAction);
        }

        [Fact]
        public void TestCentralizedFailureRankDeterminisitc()
        {
            Assert.Equal(5, LiftClassificationPolicy.GetFailureRank(LiftStatus.Liftable));
            Assert.Equal(4, LiftClassificationPolicy.GetFailureRank(LiftStatus.Overweight));
            Assert.Equal(3, LiftClassificationPolicy.GetFailureRank(LiftStatus.NotCharted));
            Assert.Equal(2, LiftClassificationPolicy.GetFailureRank(LiftStatus.HeightFail));
            Assert.Equal(1, LiftClassificationPolicy.GetFailureRank(LiftStatus.OutOfRadius));
            Assert.Equal(0, LiftClassificationPolicy.GetFailureRank(LiftStatus.SpecialCase));
        }

        [Fact]
        public void TestCentralizedStatisticsIncludeNotChartedExplicitly()
        {
            var stats = new LiftStatistics();
            LiftClassificationPolicy.UpdateStatistics(stats, LiftStatus.NotCharted);

            Assert.Equal(1, stats.NotCharted);
            Assert.Equal(0, stats.SpecialCase);
        }

        [Fact]
        public void TestFirstBoomZeroLaterBoomValidAndLiftable()
        {
            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 20, Capacity = 0.0 }, // First candidate is zero capacity
                    new TargetPoint { Radius = 10, BoomLength = 30, Capacity = 5.0 }  // Later is valid and liftable
                }
            };

            var result = _resolver.Resolve(craneSpec, 10, 20, 2.0, _settings);

            Assert.Equal(LiftStatus.Liftable, result.LiftStatus);
            Assert.Equal(ChartStatus.ListedCapacityFound, result.ChartStatus);
            Assert.Equal(30.0, result.SelectedChartBoomLength);
            Assert.Equal(5.0, result.AvailableCapacity);
            Assert.Contains("20 (InvalidZeroCell)", result.RejectedCandidates);
        }

        [Fact]
        public void TestFirstBoomBlankLaterBoomValidAndLiftable()
        {
            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 20, Capacity = -1.0 }, // First candidate is blank capacity
                    new TargetPoint { Radius = 10, BoomLength = 30, Capacity = 5.0 }   // Later is valid and liftable
                }
            };

            var result = _resolver.Resolve(craneSpec, 10, 20, 2.0, _settings);

            Assert.Equal(LiftStatus.Liftable, result.LiftStatus);
            Assert.Equal(ChartStatus.ListedCapacityFound, result.ChartStatus);
            Assert.Equal(30.0, result.SelectedChartBoomLength);
            Assert.Equal(5.0, result.AvailableCapacity);
            Assert.Contains("20 (InvalidBlankCell)", result.RejectedCandidates);
        }

        [Fact]
        public void TestAllBoomCandidatesFailedReturnsNotCharted()
        {
            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 20, Capacity = 0.0 },
                    new TargetPoint { Radius = 10, BoomLength = 30, Capacity = -1.0 }
                }
            };

            var result = _resolver.Resolve(craneSpec, 10, 20, 2.0, _settings);

            Assert.Equal(LiftStatus.NotCharted, result.LiftStatus);
            Assert.Equal(ChartStatus.InvalidZeroCell, result.ChartStatus);
            Assert.Equal(20.0, result.SelectedChartBoomLength); // shortest fallback
        }

        [Fact]
        public void TestPositiveCandidatesBelowWeightReturnsOverweight()
        {
            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 20, Capacity = 0.0 },
                    new TargetPoint { Radius = 10, BoomLength = 30, Capacity = 2.0 }, // Overweight but best
                    new TargetPoint { Radius = 10, BoomLength = 40, Capacity = 1.0 }
                }
            };

            var result = _resolver.Resolve(craneSpec, 10, 20, 5.0, _settings);

            Assert.Equal(LiftStatus.Overweight, result.LiftStatus);
            Assert.Equal(ChartStatus.ListedCapacityFound, result.ChartStatus);
            Assert.Equal(30.0, result.SelectedChartBoomLength);
            Assert.Equal(2.0, result.AvailableCapacity);
        }


        [Fact]
        public void TestDuplicateVisualColumnsUseConservativeMinimumCapacity()
        {
            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 70,
                LoadChartStorageMode = "ExactPdfVisual",
                LoadChartCapacityPolicy = "ConservativeMinimumAcrossDuplicateVisualColumns",
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 39.1, Capacity = 38.3, VisualColumnIndex = 1, VisualColumnHeader = "39.1" },
                    new TargetPoint { Radius = 10, BoomLength = 39.1, Capacity = 35.1, VisualColumnIndex = 2, VisualColumnHeader = "39.1 (2)" },
                    new TargetPoint { Radius = 10, BoomLength = 39.1, Capacity = 31.5, VisualColumnIndex = 3, VisualColumnHeader = "39.1 (3)" }
                }
            };

            var result = _resolver.Resolve(craneSpec, 10, 39.1, 30.0, _settings);

            Assert.Equal(LiftStatus.Liftable, result.LiftStatus);
            Assert.Equal(ChartStatus.ListedCapacityFound, result.ChartStatus);
            Assert.Equal(39.1, result.SelectedChartBoomLength, 1);
            Assert.Equal(31.5, result.AvailableCapacity, 3);
            Assert.Equal(1.5, result.SafetyMargin, 3);
        }

        [Fact]
        public void TestSelectedRadiusUsesNextRadiusGteRequiredRadiusNotMinimum()
        {
            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 50,
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 5, BoomLength = 20, Capacity = 10.0 },
                    new TargetPoint { Radius = 10, BoomLength = 20, Capacity = 8.0 },
                    new TargetPoint { Radius = 15, BoomLength = 20, Capacity = 6.0 }
                }
            };

            var result = _resolver.Resolve(craneSpec, 11.0, 20, 2.0, _settings);

            Assert.Equal(15.0, result.SelectedChartRadius);
        }

        [Fact]
        public void TestOffsetGeometryCalculations()
        {
            var settings = new GASettings
            {
                SlewOffsetX = 2.0,
                SlewOffsetY = 3.0,
                BoomHingeOffsetX = 1.0,
                BoomHingeOffsetY = 2.0,
                BoomHingeOffsetZ = 4.0,
                WireLength = 2.0
            };

            var cranePosition = new Domain.Common.DomainPoint3D(10.0, 20.0, 5.0);
            var element = new PrecastElement
            {
                ElementCOG = new Domain.Common.DomainPoint3D(30.0, 40.0, 8.0),
                ElementHeight = 4.0
            };

            var liftingEngine = new LiftingCalculationEngine(new CraneConfigurationEngine());
            var configEngine = new CraneConfigurationEngine();

            double radius = liftingEngine.CalculateRadius(cranePosition, element, settings);
            double expectedRadius = Math.Sqrt(Math.Pow(30.0 - 12.0, 2) + Math.Pow(40.0 - 23.0, 2));

            Assert.Equal(expectedRadius, radius, 5);

            double boomLength = configEngine.CalculateRequiredBoomLength(cranePosition, element, settings);
            double dHinge = Math.Sqrt(Math.Pow(30.0 - 11.0, 2) + Math.Pow(40.0 - 22.0, 2));
            double hHinge = 8.0 + 2.0 + 2.0 - 9.0;
            double expectedBoomLength = Math.Sqrt(dHinge * dHinge + hHinge * hHinge);

            Assert.Equal(expectedBoomLength, boomLength, 5);
        }

        [Fact]
        public void TestOffsetsDefaultToZeroPreserveOldBehavior()
        {
            var settings = new GASettings(); // offsets all default to 0.0
            var cranePosition = new Domain.Common.DomainPoint3D(10.0, 20.0, 5.0);
            var element = new PrecastElement
            {
                ElementCOG = new Domain.Common.DomainPoint3D(30.0, 40.0, 8.0),
                ElementHeight = 4.0
            };

            var liftingEngine = new LiftingCalculationEngine(new CraneConfigurationEngine());
            var configEngine = new CraneConfigurationEngine();

            double radiusWithOffsets = liftingEngine.CalculateRadius(cranePosition, element, settings);
            double radiusLegacy = liftingEngine.CalculateRadius(cranePosition, element);

            Assert.Equal(radiusLegacy, radiusWithOffsets, 5);

            double boomWithOffsets = configEngine.CalculateRequiredBoomLength(cranePosition, element, settings);
            double height = liftingEngine.CalculateLiftHeight(element, cranePosition, settings);
            double boomLegacy = configEngine.CalculateRequiredBoomLength(radiusLegacy, height);

            Assert.Equal(boomLegacy, boomWithOffsets, 5);
        }

        [Fact]
        public void TestDuplicateVisualColumnsExposeConservativeSelectionMetadata()
        {
            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 70,
                LoadChartStorageMode = "ExactPdfVisual",
                LoadChartCapacityPolicy = "ConservativeMinimumAcrossDuplicateVisualColumns",
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 39.1, Capacity = 38.3, VisualColumnIndex = 1, VisualColumnHeader = "39.1 360", NumberOfLines = 10, OperatingConditionLabel = "39.1 360 / 10 lines" },
                    new TargetPoint { Radius = 10, BoomLength = 39.1, Capacity = 35.1, VisualColumnIndex = 2, VisualColumnHeader = "39.1 rear", NumberOfLines = 8, OperatingConditionLabel = "39.1 rear / 8 lines" },
                    new TargetPoint { Radius = 10, BoomLength = 39.1, Capacity = 31.5, VisualColumnIndex = 3, VisualColumnHeader = "39.1 side", NumberOfLines = 6, OperatingConditionLabel = "39.1 side / 6 lines" }
                }
            };

            var result = _resolver.Resolve(craneSpec, 10, 39.1, 30.0, _settings);

            Assert.Equal(LiftStatus.Liftable, result.LiftStatus);
            Assert.Equal(31.5, result.AvailableCapacity, 3);
            Assert.Equal(3, result.SelectedVisualColumnIndex);
            Assert.Equal("39.1 side", result.SelectedVisualColumnHeader);
            Assert.Equal(6, result.SelectedNumberOfLines);
            Assert.Equal("39.1 side / 6 lines", result.SelectedOperatingConditionLabel);
            Assert.True(result.IsConservativeDuplicateFallback);
            Assert.Equal(3, result.DuplicateAlternativeCount);
            Assert.Contains("col 3", result.DuplicateAlternativeSummary);
            Assert.Contains("lines 6", result.DuplicateAlternativeSummary);
            Assert.Contains("capacity 31.5", result.DuplicateAlternativeSummary);
        }

        [Fact]
        public void TestDuplicateVisualColumnMetadataMatchesIndexedResolverPath()
        {
            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 70,
                LoadChartStorageMode = "ExactPdfVisual",
                LoadChartCapacityPolicy = "ConservativeMinimumAcrossDuplicateVisualColumns",
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 39.1, Capacity = 38.3, VisualColumnIndex = 1, VisualColumnHeader = "39.1 360", NumberOfLines = 10, OperatingConditionLabel = "39.1 360 / 10 lines" },
                    new TargetPoint { Radius = 10, BoomLength = 39.1, Capacity = 35.1, VisualColumnIndex = 2, VisualColumnHeader = "39.1 rear", NumberOfLines = 8, OperatingConditionLabel = "39.1 rear / 8 lines" },
                    new TargetPoint { Radius = 10, BoomLength = 39.1, Capacity = 31.5, VisualColumnIndex = 3, VisualColumnHeader = "39.1 side", NumberOfLines = 6, OperatingConditionLabel = "39.1 side / 6 lines" }
                }
            };

            var legacyResult = _resolver.Resolve(craneSpec, 10, 39.1, 30.0, _settings);
            var indexedResult = _resolver.Resolve(craneSpec, 10, 39.1, 30.0, _settings, index: new LoadChartIndex(craneSpec));

            Assert.Equal(legacyResult.AvailableCapacity, indexedResult.AvailableCapacity, 3);
            Assert.Equal(legacyResult.SelectedVisualColumnIndex, indexedResult.SelectedVisualColumnIndex);
            Assert.Equal(legacyResult.SelectedVisualColumnHeader, indexedResult.SelectedVisualColumnHeader);
            Assert.Equal(legacyResult.SelectedNumberOfLines, indexedResult.SelectedNumberOfLines);
            Assert.Equal(legacyResult.SelectedOperatingConditionLabel, indexedResult.SelectedOperatingConditionLabel);
            Assert.Equal(legacyResult.IsConservativeDuplicateFallback, indexedResult.IsConservativeDuplicateFallback);
            Assert.Equal(legacyResult.DuplicateAlternativeCount, indexedResult.DuplicateAlternativeCount);
        }

        [Fact]
        public void TestMaximumNumberOfLinesConstraintIncludesLowerLineVisualColumnMetadata()
        {
            var settings = new GASettings
            {
                DeratingRatio = 1.0,
                SafetyFactor = 1.0,
                LoadChartNumberOfLinesConstraint = 8
            };

            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 70,
                LoadChartStorageMode = "ExactPdfVisual",
                LoadChartCapacityPolicy = "ConservativeMinimumAcrossDuplicateVisualColumns",
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 39.1, Capacity = 38.3, VisualColumnIndex = 1, VisualColumnHeader = "39.1 360", NumberOfLines = 10, OperatingConditionLabel = "39.1 360 / 10 lines" },
                    new TargetPoint { Radius = 10, BoomLength = 39.1, Capacity = 35.1, VisualColumnIndex = 2, VisualColumnHeader = "39.1 rear", NumberOfLines = 8, OperatingConditionLabel = "39.1 rear / 8 lines" },
                    new TargetPoint { Radius = 10, BoomLength = 39.1, Capacity = 31.5, VisualColumnIndex = 3, VisualColumnHeader = "39.1 side", NumberOfLines = 6, OperatingConditionLabel = "39.1 side / 6 lines" }
                }
            };

            var legacyResult = _resolver.Resolve(craneSpec, 10, 39.1, 30.0, settings);
            var indexedResult = _resolver.Resolve(craneSpec, 10, 39.1, 30.0, settings, index: new LoadChartIndex(craneSpec));

            Assert.Equal(LiftStatus.Liftable, legacyResult.LiftStatus);
            Assert.Equal(31.5, legacyResult.AvailableCapacity, 3);
            Assert.Equal(3, legacyResult.SelectedVisualColumnIndex);
            Assert.Equal("39.1 side", legacyResult.SelectedVisualColumnHeader);
            Assert.Equal(6, legacyResult.SelectedNumberOfLines);
            Assert.Equal("39.1 side / 6 lines", legacyResult.SelectedOperatingConditionLabel);
            Assert.True(legacyResult.IsConservativeDuplicateFallback);
            Assert.Equal(3, legacyResult.DuplicateAlternativeCount);
            Assert.Contains("39.1 side", legacyResult.DuplicateAlternativeSummary);

            Assert.Equal(legacyResult.AvailableCapacity, indexedResult.AvailableCapacity, 3);
            Assert.Equal(legacyResult.SelectedVisualColumnIndex, indexedResult.SelectedVisualColumnIndex);
            Assert.Equal(legacyResult.SelectedNumberOfLines, indexedResult.SelectedNumberOfLines);
            Assert.Equal(legacyResult.SelectedOperatingConditionLabel, indexedResult.SelectedOperatingConditionLabel);
        }

        [Fact]
        public void TestMaximumNumberOfLinesConstraintBelowAvailableLineCountsFailsWithoutInventingSelectedMetadata()
        {
            var settings = new GASettings
            {
                DeratingRatio = 1.0,
                SafetyFactor = 1.0,
                LoadChartNumberOfLinesConstraint = 5
            };

            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 70,
                LoadChartStorageMode = "ExactPdfVisual",
                LoadChartCapacityPolicy = "ConservativeMinimumAcrossDuplicateVisualColumns",
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 39.1, Capacity = 38.3, VisualColumnIndex = 1, VisualColumnHeader = "39.1 360", NumberOfLines = 10, OperatingConditionLabel = "39.1 360 / 10 lines" },
                    new TargetPoint { Radius = 10, BoomLength = 39.1, Capacity = 35.1, VisualColumnIndex = 2, VisualColumnHeader = "39.1 rear", NumberOfLines = 8, OperatingConditionLabel = "39.1 rear / 8 lines" }
                }
            };

            var result = _resolver.Resolve(craneSpec, 10, 39.1, 30.0, settings);

            Assert.Equal(LiftStatus.NotCharted, result.LiftStatus);
            Assert.Equal(ChartStatus.NoListedCapacity, result.ChartStatus);
            Assert.Equal(CapacityFailureReason.LoadChartConditionMismatch.ToString(), result.FailureReason);
            Assert.Null(result.SelectedNumberOfLines);
            Assert.Equal(string.Empty, result.SelectedVisualColumnHeader);
            Assert.Equal(string.Empty, result.SelectedOperatingConditionLabel);
            Assert.Contains("ConditionMismatch", result.RejectedCandidates);
        }

        [Fact]
        public void TestMaximumNumberOfLinesConstraintExcludesHigherLineCountEvenWhenHigherLineEntryIsMoreConservative()
        {
            var settings = new GASettings
            {
                DeratingRatio = 1.0,
                SafetyFactor = 1.0,
                LoadChartNumberOfLinesConstraint = 8
            };

            var craneSpec = new CraneSpecification
            {
                MaxRadius = 40,
                MaxBoomLength = 70,
                LoadChartStorageMode = "ExactPdfVisual",
                LoadChartCapacityPolicy = "ConservativeMinimumAcrossDuplicateVisualColumns",
                LoadChart = new List<TargetPoint>
                {
                    new TargetPoint { Radius = 10, BoomLength = 39.1, Capacity = 20.0, VisualColumnIndex = 1, VisualColumnHeader = "39.1 360", NumberOfLines = 10, OperatingConditionLabel = "39.1 360 / 10 lines" },
                    new TargetPoint { Radius = 10, BoomLength = 39.1, Capacity = 31.5, VisualColumnIndex = 2, VisualColumnHeader = "39.1 side", NumberOfLines = 6, OperatingConditionLabel = "39.1 side / 6 lines" }
                }
            };

            var result = _resolver.Resolve(craneSpec, 10, 39.1, 30.0, settings);

            Assert.Equal(LiftStatus.Liftable, result.LiftStatus);
            Assert.Equal(31.5, result.AvailableCapacity, 3);
            Assert.Equal(2, result.SelectedVisualColumnIndex);
            Assert.Equal(6, result.SelectedNumberOfLines);
            Assert.Equal("39.1 side / 6 lines", result.SelectedOperatingConditionLabel);
        }
    }
}
