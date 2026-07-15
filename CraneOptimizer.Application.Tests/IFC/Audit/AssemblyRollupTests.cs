using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.Domain.Services;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.OptimizationScope;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.DTOs;
using CraneOptimizer.Infrastructure.Repositories;
using CraneOptimizer.Infrastructure.Ifc;
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

            // When disabled, individual elements are not grouped, but parent assembly container must still NOT enter GA.
            Assert.Single(liftUnits);
            Assert.DoesNotContain(liftUnits, u => u.ElementID == "PARENT_1");
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
                WeightTons = 5.0,
                PhysicalCOGAvailable = true,
                ElementCOG = new DomainPoint3D(10, 20, 30)
            };

            var secondary = new PrecastElement
            {
                ElementID = "SEC_1",
                ElementName = "Accessory Bracket",
                ResolvedCategory = "Bracket",
                ParentGlobalId = "PARENT_1",
                WeightTons = 5.0,
                PhysicalCOGAvailable = true,
                ElementCOG = new DomainPoint3D(20, 30, 40)
            };

            var rebar = new PrecastElement
            {
                ElementID = "REBAR_1",
                RawIfcType = "IfcReinforcingBar",
                ParentGlobalId = "PARENT_1",
                WeightTons = 2.0,
                PhysicalCOGAvailable = true,
                ElementCOG = new DomainPoint3D(30, 40, 50)
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

            // Rollup Assertions
            Assert.NotNull(parent.AssemblyLiftWeight);
            Assert.Equal(12.0, parent.AssemblyLiftWeight.Value, precision: 4);
            Assert.NotNull(parent.AssemblyLiftCOG);
            Assert.Equal(17.5, parent.AssemblyLiftCOG.X, precision: 4);
            Assert.Equal(27.5, parent.AssemblyLiftCOG.Y, precision: 4);
            Assert.Equal(37.5, parent.AssemblyLiftCOG.Z, precision: 4);
            Assert.NotNull(parent.AssemblyMemberIds);
            Assert.Equal(3, parent.AssemblyMemberIds.Count);
        }

        private static Crane CreateTestCrane()
        {
            return new Crane
            {
                Name = "TestCrane",
                Specifications = new List<CraneSpecification>
                {
                    new()
                    {
                        MaxRadius = 150,
                        MaxBoomLength = 150,
                        LoadChart = new List<TargetPoint>
                        {
                            new() { Radius = 10, BoomLength = 20, Capacity = 50 },
                            new() { Radius = 150, BoomLength = 150, Capacity = 5 }
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
                    new(-1000, -1000, 0),
                    new(1000, -1000, 0),
                    new(1000, 1000, 0),
                    new(-1000, 1000, 0)
                }
            };
        }

        [Fact]
        public void TestFullGatingAndRollupPipeline_Project2()
        {
            const string ifcPath = @"F:\06-ITI\00-Project\tryProjects\Project2\Models\IFC\ifc4_withAssemblyMark_2.ifc";
            if (!File.Exists(ifcPath)) return;

            var sessionService = new IfcModelSessionService();
            var aliasResolver = new CentralIfcPropertyAliasResolver(new IfcExporterProfileResolver());
            var auditService = new IfcDiagnosticAuditService(sessionService, aliasResolver);
            var normalizationService = new IfcNormalizationDecisionService(new IfcExporterProfileResolver());
            var georefExtractor = new CraneOptimizer.Infrastructure.Ifc.IfcGeoreferencingExtractor();
            var reader = new IfcReaderRepository(
                auditService,
                normalizationService,
                sessionService,
                georefExtractor,
                aliasResolver);

            // 1. Read elements (normalizes them and reconstructs classifications)
            var elements = reader.ReadElements(ifcPath);

            // Verify gating flags (Phase 4.5)
            var parent = elements.FirstOrDefault(e => e.ElementID == "2KFTYALGT0ghARgUhfEqjI");
            Assert.NotNull(parent);
            Assert.Equal(IfcElementRole.LiftAssembly, parent.Role);
            Assert.True(parent.IncludeInGa);

            var childWall = elements.FirstOrDefault(e => e.ElementID == "2K1eRGMijEj9PRqHskPwYb");
            Assert.NotNull(childWall);
            Assert.Equal(IfcElementRole.Excluded, childWall.Role);
            Assert.False(childWall.IncludeInGa);
            Assert.Equal("Swallowed as child component of valid Lift Assembly.", childWall.NormalizationDecisionReason);

            // 2. Build Optimization Inputs: Case A - Grouped ON
            var settingsGroupedOn = new GASettings
            {
                EnableGroupedLifts = true,
                TargetElementTypes = new List<string>()
            };

            var crane = CreateTestCrane();
            var zones = new List<GreenAreaZone> { CreateAllowedZone() };

            var builder = new OptimizationInputBuilder();
            var scopeOn = builder.Build(settingsGroupedOn, crane, elements, zones);
            Assert.NotNull(scopeOn);
            Assert.NotEmpty(scopeOn.Elements);

            // Assert rolled up Lift Unit exists
            var rollupUnit = scopeOn.Elements.FirstOrDefault(e => e.ElementID == "2K1eRGMijEj9PRqHskPwYb");
            Assert.NotNull(rollupUnit);
            Assert.Equal(IfcElementRole.LiftAssembly, rollupUnit.Role);
            Assert.True(rollupUnit.IncludeInGa);
            Assert.Equal(9.2802, rollupUnit.WeightTons ?? 0.0, 3);

            // Centroid COG verification
            Assert.NotNull(rollupUnit.COG_M);
            Assert.Equal(89.5304, rollupUnit.COG_M.X, 3);
            Assert.Equal(109.1101, rollupUnit.COG_M.Y, 3);
            Assert.Equal(1.9401, rollupUnit.COG_M.Z, 3);

            // Verify members
            Assert.NotNull(rollupUnit.ChildGlobalIds);
            Assert.Contains("2K1eRGMijEj9PRqHskPwYb", rollupUnit.ChildGlobalIds);
            Assert.Contains("0LQDWLQ954Q9QwwTtOHEV0", rollupUnit.ChildGlobalIds);
            Assert.Contains("1hiQd8Xon3rRRv$q2e7P3T", rollupUnit.ChildGlobalIds);

            // Verify parent container does not enter GA independently
            Assert.DoesNotContain(scopeOn.Elements, e => e.ElementID == "2KFTYALGT0ghARgUhfEqjI");

            // Verify children do not enter GA independently
            Assert.DoesNotContain(scopeOn.Elements, e => e.ElementID == "0LQDWLQ954Q9QwwTtOHEV0");
            Assert.DoesNotContain(scopeOn.Elements, e => e.ElementID == "1hiQd8Xon3rRRv$q2e7P3T");

            // 3. Build Optimization Inputs: Case B - Grouped OFF
            var settingsGroupedOff = new GASettings
            {
                EnableGroupedLifts = false,
                TargetElementTypes = new List<string>()
            };

            var scopeOff = builder.Build(settingsGroupedOff, crane, elements, zones);
            Assert.NotNull(scopeOff);

            // Parent container must NOT enter GA
            Assert.DoesNotContain(scopeOff.Elements, e => e.ElementID == "2KFTYALGT0ghARgUhfEqjI");

            // Child components must enter individually
            var unrolledWall = scopeOff.Elements.FirstOrDefault(e => e.ElementID == "2K1eRGMijEj9PRqHskPwYb");
            Assert.NotNull(unrolledWall);
            Assert.Equal(5.87, unrolledWall.WeightTons ?? 0.0, 2);

            var unrolledBeam1 = scopeOff.Elements.FirstOrDefault(e => e.ElementID == "0LQDWLQ954Q9QwwTtOHEV0");
            Assert.NotNull(unrolledBeam1);
            Assert.Equal(1.70, unrolledBeam1.WeightTons ?? 0.0, 2);
        }
    }
}
