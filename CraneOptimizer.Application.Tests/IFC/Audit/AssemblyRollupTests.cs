using System;
using System.Collections.Generic;
using System.Linq;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.Domain.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.OptimizationScope;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using Xunit;

namespace CraneOptimizer.Application.Tests.IFC.Audit
{
    public class AssemblyRollupTests
    {
        [Fact]
        public void TestRollupMath_WithParentExplicitWeight_ReconciliationWarning()
        {
            // Arrange: Setup parent assembly and children simulating testBuilding2x3withMarks.ifc
            var parent = new PrecastElement
            {
                ElementID = "2KFTYALGT0ghARgUhfEqjI",
                Role = IfcElementRole.LiftAssembly,
                WeightTons = 9.2802,
                WeightSource = "ParentExplicitWeight",
                WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
                ElementVolume = 3.8,
                VolumeM3 = 3.8
            };

            var mainPart = new PrecastElement
            {
                ElementID = "2K1eRGMijEj9PRqHskPwYb",
                Role = IfcElementRole.LiftableElement,
                WeightTons = 5.87,
                WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
                ElementCOG = new DomainPoint3D(90.4492, 109.0853, 1.9401),
                PhysicalCOGAvailable = true,
                ParentGlobalId = "2KFTYALGT0ghARgUhfEqjI",
                IncludeInGa = false
            };

            var secondaryBeam1 = new PrecastElement
            {
                ElementID = "0LQDWLQ954Q9QwwTtOHEV0",
                Role = IfcElementRole.LiftableElement,
                WeightTons = 1.70,
                WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
                ElementCOG = new DomainPoint3D(90.0, 109.0, 1.8),
                PhysicalCOGAvailable = true,
                ParentGlobalId = "2KFTYALGT0ghARgUhfEqjI",
                IncludeInGa = false
            };

            var secondaryBeam2 = new PrecastElement
            {
                ElementID = "1hiQd8Xon3rRRv$q2e7P3T",
                Role = IfcElementRole.LiftableElement,
                WeightTons = 1.70,
                WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
                ElementCOG = new DomainPoint3D(91.0, 110.0, 2.1),
                PhysicalCOGAvailable = true,
                ParentGlobalId = "2KFTYALGT0ghARgUhfEqjI",
                IncludeInGa = false
            };

            parent.MainPartGlobalId = mainPart.ElementID;
            var children = new List<PrecastElement> { mainPart, secondaryBeam1, secondaryBeam2 };

            // Act
            var liftUnit = AssemblyRollupService.BuildAssemblyLiftUnit(parent, children, out var warnings);

            // Assert
            Assert.Equal(mainPart.ElementID, liftUnit.ElementID); // Option E: LiftUnitId = MainPartGlobalId
            Assert.Equal(parent.ElementID, liftUnit.ParentGlobalId);
            Assert.Equal(9.2802, liftUnit.WeightTons); // Weight is parent explicit weight

            // COG weighted centroid calculation:
            // totalWeight = 5.87 + 1.70 + 1.70 = 9.27
            // x = (90.4492 * 5.87 + 90.0 * 1.70 + 91.0 * 1.70) / 9.27 = 90.46876
            // y = (109.0853 * 5.87 + 109.0 * 1.70 + 110.0 * 1.70) / 9.27 = 109.2378
            // z = (1.9401 * 5.87 + 1.8 * 1.70 + 2.1 * 1.70) / 9.27 = 1.9439
            Assert.NotNull(liftUnit.ElementCOG);
            Assert.Equal(90.4678, liftUnit.ElementCOG.X, 4);
            Assert.Equal(109.2374, liftUnit.ElementCOG.Y, 4);
            Assert.Equal(1.9437, liftUnit.ElementCOG.Z, 4);

            // Reconciliation warning should not trigger for negligible diff (9.2802 vs 9.27 is 0.1% diff)
            Assert.Empty(warnings);
        }

        [Fact]
        public void TestRollupMath_WithMismatchParentWeight_TriggersWarning()
        {
            var parent = new PrecastElement
            {
                ElementID = "2KFTYALGT0ghARgUhfEqjI",
                Role = IfcElementRole.LiftAssembly,
                WeightTons = 12.0, // Parent weight is 12t, children sum is 9.27t (diff > 5%)
                WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
                ElementVolume = 3.8
            };

            var mainPart = new PrecastElement
            {
                ElementID = "2K1eRGMijEj9PRqHskPwYb",
                WeightTons = 5.87,
                WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
                ElementCOG = new DomainPoint3D(90.0, 109.0, 2.0),
                PhysicalCOGAvailable = true,
                ParentGlobalId = "2KFTYALGT0ghARgUhfEqjI"
            };

            var secondaryBeam = new PrecastElement
            {
                ElementID = "0LQDWLQ954Q9QwwTtOHEV0",
                WeightTons = 3.40,
                WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
                ElementCOG = new DomainPoint3D(91.0, 110.0, 2.0),
                PhysicalCOGAvailable = true,
                ParentGlobalId = "2KFTYALGT0ghARgUhfEqjI"
            };

            parent.MainPartGlobalId = mainPart.ElementID;
            var children = new List<PrecastElement> { mainPart, secondaryBeam };

            // Act
            var liftUnit = AssemblyRollupService.BuildAssemblyLiftUnit(parent, children, out var warnings);

            // Assert
            Assert.NotEmpty(warnings);
            Assert.Contains(warnings, w => w.Contains("SevereWeightMismatch"));
        }

        [Fact]
        public void TestRollupMath_HiddenAccessoryMinorChild_FallbackCog()
        {
            var parent = new PrecastElement
            {
                ElementID = "PARENT_1",
                Role = IfcElementRole.LiftAssembly,
                WeightTons = 0.0,
                WeightTrustLevel = IfcWeightTrustLevel.Missing
            };

            var mainPart = new PrecastElement
            {
                ElementID = "MAIN_PART",
                WeightTons = 5.0,
                WeightTrustLevel = IfcWeightTrustLevel.ExplicitProperty,
                ElementCOG = new DomainPoint3D(10, 20, 30),
                PhysicalCOGAvailable = true,
                ParentGlobalId = "PARENT_1"
            };

            var rebar = new PrecastElement
            {
                ElementID = "REBAR_1",
                RawIfcType = "IfcReinforcingBar",
                WeightTons = 0.05, // 1% of total weight (minor)
                WeightTrustLevel = IfcWeightTrustLevel.ExplicitProperty,
                PhysicalCOGAvailable = false, // missing COG
                ParentGlobalId = "PARENT_1"
            };

            parent.MainPartGlobalId = mainPart.ElementID;
            var children = new List<PrecastElement> { mainPart, rebar };

            // Act
            var liftUnit = AssemblyRollupService.BuildAssemblyLiftUnit(parent, children, out var warnings);

            // Assert
            Assert.Equal(5.05, liftUnit.WeightTons);
            Assert.NotNull(liftUnit.ElementCOG);
            Assert.Equal(10, liftUnit.ElementCOG.X); // Fell back to main part COG
            Assert.Contains(warnings, w => w.Contains("HiddenAccessoryMissingCogFallback"));
        }

        [Fact]
        public void TestRollupMath_MajorChildMissingCog_UnsafeWarning()
        {
            var parent = new PrecastElement
            {
                ElementID = "PARENT_1",
                Role = IfcElementRole.LiftAssembly,
                WeightTons = 0.0,
                WeightTrustLevel = IfcWeightTrustLevel.Missing
            };

            var mainPart = new PrecastElement
            {
                ElementID = "MAIN_PART",
                WeightTons = 5.0,
                WeightTrustLevel = IfcWeightTrustLevel.ExplicitProperty,
                ElementCOG = new DomainPoint3D(10, 20, 30),
                PhysicalCOGAvailable = true,
                ParentGlobalId = "PARENT_1"
            };

            var secondaryBeam = new PrecastElement
            {
                ElementID = "SEC_BEAM_1",
                WeightTons = 2.0, // 28.5% of total weight (major!)
                WeightTrustLevel = IfcWeightTrustLevel.ExplicitProperty,
                PhysicalCOGAvailable = false, // missing COG!
                ParentGlobalId = "PARENT_1"
            };

            parent.MainPartGlobalId = mainPart.ElementID;
            var children = new List<PrecastElement> { mainPart, secondaryBeam };

            // Act
            var liftUnit = AssemblyRollupService.BuildAssemblyLiftUnit(parent, children, out var warnings);

            // Assert
            Assert.Contains(warnings, w => w.Contains("UnsafeCogRollup"));
        }

        [Fact]
        public void TestOptimizationInputBuilder_GroupedLiftsEnabled_OptionE()
        {
            var settings = new GASettings { EnableGroupedLifts = true };
            var crane = CreateTestCrane();

            var parent = new PrecastElement
            {
                ElementID = "PARENT_1",
                Role = IfcElementRole.LiftAssembly,
                IncludeInGa = true,
                WeightTons = 10.0,
                WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
                MainPartGlobalId = "MAIN_1"
            };

            var mainPart = new PrecastElement
            {
                ElementID = "MAIN_1",
                ParentGlobalId = "PARENT_1",
                ParentGlobalIdSource = "IfcDecomposition",
                WeightTons = 6.0,
                WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
                ElementCOG = new DomainPoint3D(1, 2, 3),
                PhysicalCOGAvailable = true,
                IncludeInGa = false
            };

            var secondary = new PrecastElement
            {
                ElementID = "SEC_1",
                ParentGlobalId = "PARENT_1",
                ParentGlobalIdSource = "IfcDecomposition",
                WeightTons = 4.0,
                WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
                ElementCOG = new DomainPoint3D(2, 4, 6),
                PhysicalCOGAvailable = true,
                IncludeInGa = false
            };

            var independent = new PrecastElement
            {
                ElementID = "IND_1",
                WeightTons = 5.0,
                WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
                ElementCOG = new DomainPoint3D(10, 10, 10),
                PhysicalCOGAvailable = true,
                IncludeInGa = true
            };

            var elements = new List<PrecastElement> { parent, mainPart, secondary, independent };
            var zones = new List<GreenAreaZone> { CreateAllowedZone() };

            var builder = new OptimizationInputBuilder();

            // Act
            var result = builder.Build(settings, crane, elements, zones);

            // Assert
            Assert.NotNull(result);
            var liftUnits = result.Elements;

            // Should have 2 lift units: the representative assembly lift unit (MAIN_1) and independent element (IND_1)
            Assert.Equal(2, liftUnits.Count);

            var assemblyLift = liftUnits.FirstOrDefault(u => u.ElementID == "MAIN_1");
            Assert.NotNull(assemblyLift);
            Assert.Equal(10.0, assemblyLift.WeightTons); // Weight is parent explicit weight
            Assert.Equal("PARENT_1", assemblyLift.ParentGlobalId);
            Assert.Contains("MAIN_1", assemblyLift.ChildGlobalIds);
            Assert.Contains("SEC_1", assemblyLift.ChildGlobalIds);

            var indLift = liftUnits.FirstOrDefault(u => u.ElementID == "IND_1");
            Assert.NotNull(indLift);

            // Make sure the parent container PARENT_1 did not enter GA
            Assert.Null(liftUnits.FirstOrDefault(u => u.ElementID == "PARENT_1"));
        }

        [Fact]
        public void TestOptimizationInputBuilder_GroupedLiftsDisabled()
        {
            var settings = new GASettings { EnableGroupedLifts = false };
            var crane = CreateTestCrane();

            var parent = new PrecastElement
            {
                ElementID = "PARENT_1",
                Role = IfcElementRole.LiftAssembly,
                IncludeInGa = true,
                WeightTons = 10.0,
                WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
                MainPartGlobalId = "MAIN_1"
            };

            var mainPart = new PrecastElement
            {
                ElementID = "MAIN_1",
                ParentGlobalId = "PARENT_1",
                ParentGlobalIdSource = "IfcDecomposition",
                WeightTons = 6.0,
                WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
                ElementCOG = new DomainPoint3D(1, 2, 3),
                PhysicalCOGAvailable = true,
                IncludeInGa = true
            };

            var elements = new List<PrecastElement> { parent, mainPart };
            var zones = new List<GreenAreaZone> { CreateAllowedZone() };

            var builder = new OptimizationInputBuilder();

            // Act
            var result = builder.Build(settings, crane, elements, zones);

            // Assert
            Assert.NotNull(result);
            var liftUnits = result.Elements;

            // When disabled, individual elements are not grouped and are kept unchanged.
            // parent assembly (PARENT_1) and main part (MAIN_1) should both be treated independently
            Assert.Equal(2, liftUnits.Count);
            Assert.Contains(liftUnits, u => u.ElementID == "PARENT_1");
            Assert.Contains(liftUnits, u => u.ElementID == "MAIN_1");
        }

        [Fact]
        public void TestReconstructAssemblyClassifications_SaveLoadRebuild()
        {
            // Arrange: Simulate reloaded elements from SQLite DB (without memory classifications)
            var parent = new PrecastElement
            {
                ElementID = "PARENT_1",
                Role = IfcElementRole.LiftAssembly,
                ChildGlobalIds = new List<string> { "MAIN_1", "SEC_1", "REBAR_1" }
            };

            var mainPart = new PrecastElement
            {
                ElementID = "MAIN_1",
                ElementName = "Wall Panel Main",
                ResolvedCategory = "Wall",
                ParentGlobalId = "PARENT_1",
                WeightTons = 5.87
            };

            var secondary = new PrecastElement
            {
                ElementID = "SEC_1",
                ElementName = "Accessory Bracket",
                ResolvedCategory = "Bracket",
                ParentGlobalId = "PARENT_1",
                WeightTons = 1.70
            };

            var rebar = new PrecastElement
            {
                ElementID = "REBAR_1",
                RawIfcType = "IfcReinforcingBar",
                ParentGlobalId = "PARENT_1",
                WeightTons = 0.1
            };

            var elements = new List<PrecastElement> { parent, mainPart, secondary, rebar };

            // Act
            AssemblyRollupService.ReconstructAssemblyClassifications(elements);

            // Assert
            Assert.Equal("MAIN_1", parent.MainPartGlobalId);
            Assert.Equal("Wall Panel Main", parent.MainPartName);
            Assert.Equal("Wall", parent.MainPartCategory);
            Assert.Equal(3, parent.ChildCount);
            Assert.Equal(1, parent.VisibleSecondaryPartCount); // SEC_1
            Assert.Equal(1, parent.HiddenWeightContributorCount); // REBAR_1
            Assert.True(parent.HasSecondaryParts);
            Assert.True(parent.HasHiddenContributors);
        }

        private static Crane CreateTestCrane()
        {
            return new Crane
            {
                Specifications = new List<CraneSpecification>
                {
                    new()
                    {
                        MaxRadius = 40,
                        MaxBoomLength = 50,
                        LoadChart = new List<TargetPoint>
                        {
                            new() { Radius = 10, BoomLength = 20, Capacity = 50 }
                        }
                    }
                }
            };
        }

        private static GreenAreaZone CreateAllowedZone()
        {
            return new GreenAreaZone
            {
                Name = "Allowed",
                Type = ZoneType.Allowed,
                Vertices = new List<DomainPoint3D>
                {
                    new(-10, -10, 0),
                    new(10, -10, 0),
                    new(10, 10, 0),
                    new(-10, 10, 0)
                }
            };
        }
    }
}
