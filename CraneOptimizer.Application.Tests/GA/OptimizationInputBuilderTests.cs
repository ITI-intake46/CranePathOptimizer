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
        public void Build_GroupsExplicitAssemblyMarkMembersIntoSingleLiftUnit()
        {
            var builder = new OptimizationInputBuilder();
            var input = builder.Build(
                new GASettings(),
                CreateCrane(),
                new List<PrecastElement>
                {
                    CreateElement("W1-A", includeInGa: true, assemblyMark: "W1", weightTons: 2, cogX: 0),
                    CreateElement("W1-B", includeInGa: true, assemblyMark: "W1", weightTons: 4, cogX: 9),
                    CreateElement("W2-A", includeInGa: true, assemblyMark: "W2", weightTons: 3, cogX: 20)
                },
                new List<GreenAreaZone> { CreateAllowedZone() });

            Assert.Equal(2, input.Elements.Count);
            var group = Assert.Single(input.Elements.Where(element => element.ElementID == "ASM:W1"));
            Assert.Equal("W1", group.AssemblyMark);
            Assert.Null(group.ElementName);
            Assert.Equal(6, group.WeightTons);
            Assert.Equal(6, group.ElementWT);
            Assert.Equal(6, group.ElementCOG.X, 3);
            Assert.Equal(new[] { "W1-A", "W1-B" }, group.ChildGlobalIds);
            Assert.Equal(IfcElementRole.LiftAssembly, group.Role);
            Assert.DoesNotContain(input.Elements, element => element.ElementID == "W1-A");
            Assert.Contains(input.Elements, element => element.ElementID == "W2-A");
        }

        [Fact]
        public void Build_DoesNotGroupWeakFallbackAssemblyMarks()
        {
            var builder = new OptimizationInputBuilder();
            var input = builder.Build(
                new GASettings(),
                CreateCrane(),
                new List<PrecastElement>
                {
                    CreateElement("WALL-1", includeInGa: true, assemblyMark: "WALL", isExplicitAssemblyMark: false, confidence: AssemblyMarkConfidence.Low),
                    CreateElement("WALL-2", includeInGa: true, assemblyMark: "WALL", isExplicitAssemblyMark: false, confidence: AssemblyMarkConfidence.Low)
                },
                new List<GreenAreaZone> { CreateAllowedZone() });

            Assert.Equal(2, input.Elements.Count);
            Assert.Contains(input.Elements, element => element.ElementID == "WALL-1");
            Assert.Contains(input.Elements, element => element.ElementID == "WALL-2");
            Assert.DoesNotContain(input.Elements, element => element.ElementID.StartsWith("ASM:", StringComparison.Ordinal));
        }
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
            string? assemblyMark = null,
            bool isExplicitAssemblyMark = true,
            AssemblyMarkConfidence confidence = AssemblyMarkConfidence.High,
            double weightTons = 5,
            double cogX = 0)
        {
            return new PrecastElement
            {
                ElementID = id,
                ElementType = "Beam",
                IncludeInGa = includeInGa,
                ElementCOG = new DomainPoint3D(cogX, 0, 1),
                COG_M = new DomainPoint3D(cogX, 0, 1),
                ElementWT = weightTons,
                WeightTons = weightTons,
                WeightTrustLevel = IfcWeightTrustLevel.ExplicitProperty,
                AssemblyMark = assemblyMark,
                AssemblyMarkIsExplicit = isExplicitAssemblyMark,
                AssemblyMarkConfidence = confidence,
                SourceSystem = SourceSystem.Tekla,
                ExporterProfile = IfcExporterProfile.Tekla
            };
        }
    }
}
