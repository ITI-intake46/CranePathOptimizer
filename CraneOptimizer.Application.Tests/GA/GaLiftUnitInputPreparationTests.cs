using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Features.F8_GAOptimizer.Services;

namespace CraneOptimizer.Application.Tests.GA;

public sealed class GaLiftUnitInputPreparationTests
{
    [Fact]
    public void PrepareAssembly_UsesOnlyNonRebarChildWeightAndMemberIds()
    {
        var parent = new PrecastElement
        {
            ElementID = "assembly",
            ElementType = "Assembly",
            Role = IfcElementRole.LiftAssembly,
            IncludeInGa = true,
            MainPartGlobalId = "main",
            ChildGlobalIds = new List<string> { "main", "rebar" }
        };
        var main = new PrecastElement
        {
            ElementID = "main",
            ElementType = "Beam",
            ParentGlobalId = "assembly",
            IncludeInGa = false,
            IncludeInRender = true,
            WeightTons = 4,
            ElementWT = 4,
            WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
            ElementCOG = new DomainPoint3D(10, 20, 5)
        };
        var rebar = new PrecastElement
        {
            ElementID = "rebar",
            RawIfcType = "IfcReinforcingBar",
            ElementType = "Rebar",
            ParentGlobalId = "assembly",
            IncludeInGa = true,
            IncludeInRender = true,
            WeightTons = 100,
            ElementWT = 100,
            WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
            ElementCOG = new DomainPoint3D(100, 200, 50)
        };

        bool success = GaLiftUnitInputPreparation.TryPrepareAssemblyParent(
            parent,
            new[] { parent, main, rebar },
            out PrecastElement prepared,
            out string failureReason);

        Assert.True(success, failureReason);
        Assert.Equal(4, prepared.AssemblyLiftWeight);
        Assert.Equal("main", prepared.MainPartGlobalId);
        Assert.Equal(new[] { "main" }, prepared.ChildGlobalIds);
        Assert.Equal(new[] { "main" }, prepared.AssemblyMemberIds);
    }

    [Fact]
    public void CountLiftUnits_ExcludesStandaloneAndAssemblyReinforcement()
    {
        var rebarAssembly = new PrecastElement
        {
            ElementID = "rebar-assembly",
            ElementType = "Rebar Assembly",
            Role = IfcElementRole.LiftAssembly,
            IncludeInGa = true,
            WeightTons = 10,
            WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
            ChildGlobalIds = new List<string> { "rebar-child" }
        };
        var rebarChild = new PrecastElement
        {
            ElementID = "rebar-child",
            RawIfcType = "IfcReinforcingBar",
            ElementType = "Rebar",
            ParentGlobalId = "rebar-assembly",
            IncludeInGa = true,
            IncludeInRender = true,
            WeightTons = 10,
            WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
            ElementCOG = new DomainPoint3D(1, 1, 1)
        };

        GaLiftUnitCounts counts = GaLiftUnitInputPreparation.CountLiftUnits(
            new[] { rebarAssembly, rebarChild },
            new[] { rebarAssembly, rebarChild },
            null);

        Assert.Equal(0, counts.FinalCandidateCount);
        Assert.Equal(2, counts.RebarExcludedCount);
    }
    [Fact]
    public void CountLiftUnits_FiveTrustedAcceptanceAssemblies_ReturnsFiveGaReadyUnits()
    {
        var elements = new[]
        {
            AcceptanceAssembly("kg-project", 4, 8.5),
            AcceptanceAssembly("tonne-explicit", 6, 5.0),
            AcceptanceAssembly("lb-explicit", 8, 4.0),
            AcceptanceAssembly("kg-explicit", 10, 3.0),
            AcceptanceAssembly("kg-inherited", 12, 2.0)
        };

        GaLiftUnitCounts counts = GaLiftUnitInputPreparation.CountLiftUnits(
            elements,
            elements,
            new[] { "Beam" });

        Assert.Equal(5, counts.GroupedAssemblyCount);
        Assert.Equal(0, counts.StandaloneCount);
        Assert.Equal(5, counts.FinalCandidateCount);

        foreach (PrecastElement element in elements)
        {
            Assert.True(
                GaLiftUnitInputPreparation.TryPrepareAssemblyParent(
                    element,
                    elements,
                    out PrecastElement prepared,
                    out string reason),
                reason);
            Assert.True(prepared.AssemblyLiftWeight is > 0);
            Assert.NotNull(prepared.ElementCOG);
        }
    }

    private static PrecastElement AcceptanceAssembly(string id, double x, double weight) => new()
    {
        ElementID = id,
        ElementName = id,
        ElementType = "Beam",
        RawIfcType = "IfcBeam",
        ResolvedCategory = "Beam",
        Role = IfcElementRole.LiftAssembly,
        IncludeInGa = true,
        IncludeInRender = true,
        WeightTons = weight,
        ElementWT = weight,
        WeightTrustLevel = IfcWeightTrustLevel.ExplicitQuantity,
        ElementCOG = new DomainPoint3D(x, 0, 3),
        COG_M = new DomainPoint3D(x, 0, 3),
        PhysicalCOGAvailable = true,
        ElementLength = 6,
        ElementWidth = 0.4,
        ElementHeight = 0.6,
        BoundingBox_M = new DomainPoint3D(6, 0.4, 0.6)
    };

}
