using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Services;
using CraneOptimizer.UI.Features.F0_CoordinateSystem;
using Xunit;

namespace CraneOptimizer.Application.Tests.IFC;

public sealed class IfcIdentitySsotTests
{
    [Fact]
    public void TranslateElements_PreservesIdentityAndClassification()
    {
        var source = CreateElement();
        var service = new IfcSceneOperationService(new SceneService(), null!);

        var translated = Assert.Single(service.TranslateElements(
            [source],
            new DomainPoint3D(10, 20, 30)));

        AssertIdentityPreserved(source, translated);
        Assert.Equal(11, translated.ElementCOG.X);
        Assert.Equal(22, translated.ElementCOG.Y);
        Assert.Equal(33, translated.ElementCOG.Z);
        Assert.NotSame(source.ValidationWarnings, translated.ValidationWarnings);
        Assert.NotSame(source.ChildGlobalIds, translated.ChildGlobalIds);
    }

    [Fact]
    public void NormalizeElementToMeters_PreservesIdentityAndClassification()
    {
        var source = CreateElement();
        var service = new CoordinateService();

        var normalized = service.NormalizeElementToMeters(source);

        AssertIdentityPreserved(source, normalized);
        Assert.Equal(source.ElementCOG.X, normalized.ElementCOG.X);
        Assert.Equal(source.ElementCOG.Y, normalized.ElementCOG.Y);
        Assert.Equal(source.ElementCOG.Z, normalized.ElementCOG.Z);
        Assert.NotSame(source.ValidationWarnings, normalized.ValidationWarnings);
        Assert.NotSame(source.ChildGlobalIds, normalized.ChildGlobalIds);
    }

    private static PrecastElement CreateElement() => new()
    {
        ElementID = "1zqWVRVLz5h8EdjS4I_HxE",
        ElementName = "My BEAM",
        ElementMark = "B-101",
        AssemblyMark = "ASM-101",
        AssemblyMarkSource = "Pset_Tekla_Common.ASSEMBLY_POS",
        AssemblyMarkConfidence = AssemblyMarkConfidence.High,
        AssemblyMarkIsExplicit = true,
        ExplicitAssemblyMarkFound = true,
        ResolvedDisplayMark = "ASM-101",
        ElementType = "Beam",
        RawIfcType = "IfcBeam",
        ResolvedCategory = "Beam",
        Role = IfcElementRole.InternalAuxiliary,
        IncludeInGa = false,
        IncludeInRender = true,
        IncludeInElementTree = true,
        SourceSystem = SourceSystem.Tekla,
        ExporterProfile = IfcExporterProfile.Tekla,
        ValidationStatus = ValidationStatus.Warning,
        ValidationWarnings = ["Reference-only test"],
        ChildGlobalIds = ["CHILD-1"],
        VisibleGeometryOwnerGlobalIds = ["GEOMETRY-1"],
        HiddenInternalGeometryOwnerGlobalIds = ["HIDDEN-1"],
        ExtractionWarnings = ["Extraction warning"],
        ElementMaterial = "Concrete",
        ElementLength = 4,
        ElementWidth = 0.5,
        ElementHeight = 0.7,
        ElementWT = 2.5,
        WeightTons = 2.5,
        ElementVolume = 1.2,
        VolumeM3 = 1.2,
        ElementCOG = new DomainPoint3D(1, 2, 3),
        COG_M = new DomainPoint3D(1, 2, 3),
        BoundingBox_M = new DomainPoint3D(4, 0.5, 0.7)
    };

    private static void AssertIdentityPreserved(PrecastElement expected, PrecastElement actual)
    {
        Assert.Equal(expected.ElementID, actual.ElementID);
        Assert.Equal(expected.ElementName, actual.ElementName);
        Assert.Equal(expected.ElementMark, actual.ElementMark);
        Assert.Equal(expected.AssemblyMark, actual.AssemblyMark);
        Assert.Equal(expected.ResolvedDisplayMark, actual.ResolvedDisplayMark);
        Assert.Equal(expected.IncludeInGa, actual.IncludeInGa);
        Assert.Equal(expected.IncludeInRender, actual.IncludeInRender);
        Assert.Equal(expected.IncludeInElementTree, actual.IncludeInElementTree);
        Assert.Equal(expected.Role, actual.Role);
        Assert.Equal(expected.RawIfcType, actual.RawIfcType);
        Assert.Equal(expected.ResolvedCategory, actual.ResolvedCategory);
        Assert.Equal(expected.SourceSystem, actual.SourceSystem);
        Assert.Equal(expected.ExporterProfile, actual.ExporterProfile);
        Assert.Equal(expected.ValidationStatus, actual.ValidationStatus);
        Assert.Equal(expected.ValidationWarnings, actual.ValidationWarnings);
    }
}
