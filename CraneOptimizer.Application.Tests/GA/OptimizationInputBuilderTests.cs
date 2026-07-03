using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.OptimizationScope;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;
using Xunit;

namespace CraneOptimizer.Application.Tests
{
    public class OptimizationInputBuilderTests
    {
        // ====================================================================
        // Existing tests (preserved with no behavioral change)
        // ====================================================================

        [Fact]
        public void Build_ExcludesElementsBlockedFromGa()
        {
            var builder = new OptimizationInputBuilder();
            var input = builder.Build(
                new GASettings(),
                CreateCrane(),
                new List<PrecastElement>
                {
                    CreateElement("GA-1", includeInGa: true),
                    CreateElement("BLOCKED-1", includeInGa: false)
                },
                new List<GreenAreaZone> { CreateAllowedZone() });

            Assert.Single(input.Elements);
            Assert.Equal("GA-1", input.Elements[0].ElementID);
        }

        [Fact]
        public void Build_ExcludesGridReferencesEvenWhenIncludeInGaIsIncorrectlyTrue()
        {
            var builder = new OptimizationInputBuilder();
            var grid = CreateElement("GRID-1", includeInGa: true);
            grid.RawIfcType = "IfcGrid";

            var input = builder.Build(
                new GASettings(),
                CreateCrane(),
                new List<PrecastElement>
                {
                    CreateElement("GA-1", includeInGa: true),
                    grid
                },
                new List<GreenAreaZone> { CreateAllowedZone() });

            var element = Assert.Single(input.Elements);
            Assert.Equal("GA-1", element.ElementID);
        }

        [Fact]
        public void Build_ThrowsWhenAllElementsAreBlockedFromGa()
        {
            var builder = new OptimizationInputBuilder();

            var exception = Assert.Throws<InvalidOperationException>(() => builder.Build(
                new GASettings(),
                CreateCrane(),
                new List<PrecastElement> { CreateElement("BLOCKED-1", includeInGa: false) },
                new List<GreenAreaZone> { CreateAllowedZone() }));

            Assert.Contains("No GA-enabled elements", exception.Message);
        }

        [Fact]
        public void Build_DoesNotGroupByDefaultEvenWithAssemblyMark()
        {
            var builder = new OptimizationInputBuilder();
            var input = builder.Build(
                new GASettings { EnableGroupedLifts = false },
                CreateCrane(),
                new List<PrecastElement>
                {
                    CreateElement("W1-A", includeInGa: true, assemblyMark: "W1", weightTons: 2, cogX: 0),
                    CreateElement("W1-B", includeInGa: true, assemblyMark: "W1", weightTons: 4, cogX: 9)
                },
                new List<GreenAreaZone> { CreateAllowedZone() });

            Assert.Equal(2, input.Elements.Count);
            Assert.Contains(input.Elements, element => element.ElementID == "W1-A");
            Assert.Contains(input.Elements, element => element.ElementID == "W1-B");
            Assert.DoesNotContain(input.Elements, element => element.ElementID.StartsWith("ASM:", StringComparison.Ordinal));
        }

        [Fact]
        public void Build_GroupsByParentWhenEnabledAndTrusted()
        {
            var builder = new OptimizationInputBuilder();
            var input = builder.Build(
                new GASettings { EnableGroupedLifts = true },
                CreateCrane(),
                new List<PrecastElement>
                {
                    new PrecastElement
                    {
                        ElementID = "PARENT-1",
                        Role = IfcElementRole.LiftAssembly,
                        IncludeInGa = true,
                        MainPartGlobalId = "W1-B",
                        ChildGlobalIds = new List<string> { "W1-A", "W1-B" }
                    },
                    CreateElement("W1-A", includeInGa: true, assemblyMark: "W1", weightTons: 2, cogX: 0, parentGlobalId: "PARENT-1", parentGlobalIdSource: "IfcDecomposition"),
                    CreateElement("W1-B", includeInGa: true, assemblyMark: "W1", weightTons: 4, cogX: 9, parentGlobalId: "PARENT-1", parentGlobalIdSource: "IfcDecomposition"),
                    CreateElement("W2-A", includeInGa: true, assemblyMark: "W2", weightTons: 3, cogX: 20)
                },
                new List<GreenAreaZone> { CreateAllowedZone() });

            Assert.Equal(2, input.Elements.Count);
            // Representative Lift Unit has the ID of the Main Part (W1-B) per Option E
            var group = Assert.Single(input.Elements.Where(element => element.ElementID == "W1-B"));
            Assert.Equal("W1", group.AssemblyMark);
            Assert.Equal(6, group.WeightTons);
            Assert.Equal(6, group.ElementWT);
            // W1-A: weight 2, x=0. W1-B: weight 4, x=9. Weighted average = (0*2 + 9*4) / 6 = 36/6 = 6.0
            Assert.Equal(6.0, group.ElementCOG.X, 3);
            Assert.Contains("W1-A", group.ChildGlobalIds);
            Assert.Contains("W1-B", group.ChildGlobalIds);
            Assert.Equal(IfcElementRole.LiftAssembly, group.Role);
            Assert.DoesNotContain(input.Elements, element => element.ElementID == "W1-A");
            Assert.DoesNotContain(input.Elements, element => element.ElementID == "PARENT-1");
            Assert.Contains(input.Elements, element => element.ElementID == "W2-A");
        }

        [Fact]
        public void Build_DoesNotGroupWhenParentSourceIsUntrusted()
        {
            var builder = new OptimizationInputBuilder();
            var input = builder.Build(
                new GASettings { EnableGroupedLifts = true },
                CreateCrane(),
                new List<PrecastElement>
                {
                    CreateElement("W1-A", includeInGa: true, assemblyMark: "W1", weightTons: 2, cogX: 0, parentGlobalId: "PARENT-1", parentGlobalIdSource: "InferredFromMark"),
                    CreateElement("W1-B", includeInGa: true, assemblyMark: "W1", weightTons: 4, cogX: 9, parentGlobalId: "PARENT-1", parentGlobalIdSource: "InferredFromMark")
                },
                new List<GreenAreaZone> { CreateAllowedZone() });

            Assert.Equal(2, input.Elements.Count);
            Assert.Contains(input.Elements, element => element.ElementID == "W1-A");
            Assert.Contains(input.Elements, element => element.ElementID == "W1-B");
        }

        [Fact]
        public void Build_FiltersByElementType_WhenTargetElementTypesSpecified()
        {
            var builder = new OptimizationInputBuilder();
            var settings = new GASettings { TargetElementTypes = new List<string> { "Column" } };

            var input = builder.Build(
                settings,
                CreateCrane(),
                new List<PrecastElement>
                {
                    CreateElement("ELEM-1", includeInGa: true, elementType: "Column"),
                    CreateElement("ELEM-2", includeInGa: true, elementType: "Beam"),
                },
                new List<GreenAreaZone> { CreateAllowedZone() });

            Assert.Single(input.Elements);
            Assert.Equal("ELEM-1", input.Elements[0].ElementID);
        }

        [Fact]
        public void Build_IncludesAllGaEnabled_WhenNoElementTypesSpecified()
        {
            var builder = new OptimizationInputBuilder();
            var input = builder.Build(
                new GASettings(),
                CreateCrane(),
                new List<PrecastElement>
                {
                    CreateElement("ELEM-1", includeInGa: true),
                    CreateElement("ELEM-2", includeInGa: true),
                },
                new List<GreenAreaZone> { CreateAllowedZone() });

            Assert.Equal(2, input.Elements.Count);
        }

        [Fact]
        public void Build_ThrowsWhenNoElementsMatchSelectedElementTypes()
        {
            var builder = new OptimizationInputBuilder();
            var settings = new GASettings { TargetElementTypes = new List<string> { "Slab" } };

            var exception = Assert.Throws<InvalidOperationException>(() => builder.Build(
                settings,
                CreateCrane(),
                new List<PrecastElement>
                {
                    CreateElement("ELEM-1", includeInGa: true, elementType: "Column"),
                    CreateElement("ELEM-2", includeInGa: true, elementType: "Beam"),
                },
                new List<GreenAreaZone> { CreateAllowedZone() }));

            Assert.Contains("target element types", exception.Message);
        }


        [Fact]
        public void Build_IncludesChildren_OfMatchingElementTypeParent()
        {
            var builder = new OptimizationInputBuilder();
            var settings = new GASettings { TargetElementTypes = new List<string> { "Beam" } };

            var input = builder.Build(
                settings,
                CreateCrane(),
                new List<PrecastElement>
                {
                    new PrecastElement
                    {
                        ElementID = "PARENT-1",
                        ElementType = "Beam",
                        AssemblyMark = "PC2-1",
                        Role = IfcElementRole.LiftAssembly,
                        IncludeInGa = true,
                        ElementCOG = new DomainPoint3D(0, 0, 1),
                        COG_M = new DomainPoint3D(0, 0, 1),
                        WeightTons = 5,
                        WeightTrustLevel = IfcWeightTrustLevel.ExplicitProperty,
                    },
                    new PrecastElement
                    {
                        ElementID = "CHILD-1",
                        ElementType = "Rebar",
                        AssemblyMark = null,
                        ParentGlobalId = "PARENT-1",
                        Role = IfcElementRole.LiftableElement,
                        IncludeInGa = true,
                        ElementCOG = new DomainPoint3D(0, 0, 1),
                        COG_M = new DomainPoint3D(0, 0, 1),
                        WeightTons = 1.5,
                        WeightTrustLevel = IfcWeightTrustLevel.ExplicitProperty,
                    },
                },
                new List<GreenAreaZone> { CreateAllowedZone() });

            // In non-grouped mode, parent LiftAssemblies are excluded from the GA target set;
            // only their children are promoted as individual targets.
            Assert.Single(input.Elements);
            Assert.Contains(input.Elements, e => e.ElementID == "CHILD-1");
            Assert.DoesNotContain(input.Elements, e => e.ElementID == "PARENT-1");
        }


        // ====================================================================
        // Helpers
        // ====================================================================

        private static Crane CreateCrane()
        {
            return new Crane
            {
                Name = "Test Crane",
                VehicleLength = 10,
                VehicleWidth = 4,
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

        private static PrecastElement CreateElement(
            string id,
            bool includeInGa,
            string? elementType = null,
            string? assemblyMark = null,
            bool isExplicitAssemblyMark = true,
            AssemblyMarkConfidence confidence = AssemblyMarkConfidence.High,
            double weightTons = 5,
            double cogX = 0,
            string? parentGlobalId = null,
            string? parentGlobalIdSource = null)
        {
            return new PrecastElement
            {
                ElementID = id,
                ElementType = elementType ?? "Beam",
                IncludeInGa = includeInGa,
                ElementCOG = new DomainPoint3D(cogX, 0, 1),
                COG_M = new DomainPoint3D(cogX, 0, 1),
                ElementWT = weightTons,
                WeightTons = weightTons,
                WeightTrustLevel = IfcWeightTrustLevel.ExplicitProperty,
                AssemblyMark = assemblyMark,
                AssemblyMarkIsExplicit = isExplicitAssemblyMark,
                AssemblyMarkConfidence = confidence,
                ParentGlobalId = parentGlobalId,
                ParentGlobalIdSource = parentGlobalIdSource,
                SourceSystem = SourceSystem.Tekla,
                ExporterProfile = IfcExporterProfile.Tekla
            };
        }
    }
}
