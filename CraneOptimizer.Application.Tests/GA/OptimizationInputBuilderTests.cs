using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Engines.OptimizationScope;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Settings;
using CraneOptimizer.Domain.Common;
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

        private static PrecastElement CreateElement(string id, bool includeInGa)
        {
            return new PrecastElement
            {
                ElementID = id,
                ElementType = "Beam",
                IncludeInGa = includeInGa,
                ElementCOG = new DomainPoint3D(0, 0, 1),
                ElementWT = 5
            };
        }
    }
}
