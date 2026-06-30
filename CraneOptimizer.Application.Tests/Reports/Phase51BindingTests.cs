using System.Globalization;
using CraneOptimizer.Application.Reports.Definitions;
using CraneOptimizer.Application.Reports.DTOs;
using CraneOptimizer.Application.Reports.Services;
using CraneOptimizer.Application.Services;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Units;
using Xunit;

namespace CraneOptimizer.Application.Tests.Reports;

public sealed class Phase51BindingTests
{
    [Fact]
    public void BasicElementList_UsesMainPartBoundingBoxForGroupedAssembly()
    {
        List<PrecastElement> elements = CreateAssemblyFixture();
        var provider = new ElementReportDataProvider(
            new ReportableElementResolver(),
            new ImportReadinessService());

        ElementReportRowDto row = Assert.Single(
            provider.BuildRows(ReportTemplateCatalog.BasicElementList, elements));

        Assert.Equal("parent", row.ElementId);
        Assert.Equal(4.0, row.Length);
        Assert.Equal(0.2, row.Width);
        Assert.Equal(3.0, row.Height);
        Assert.Equal("3.00 m", row.TopLevelElevation);
        Assert.Equal("0.00 m", row.BottomLevelElevation);
        Assert.Equal(3.8, row.VolumeM3);
        Assert.Equal(9.28, row.WeightTons);
    }

    [Fact]
    public void GeometryResolver_DistinguishesGroupedAndIndividualModes()
    {
        List<PrecastElement> elements = CreateAssemblyFixture();
        PrecastElement parent = Assert.Single(elements, element => element.ElementID == "parent");
        PrecastElement secondary = Assert.Single(elements, element => element.ElementID == "secondary");

        ElementGeometryBinding grouped = ElementBindingResolver.ResolveGeometry(
            parent,
            elements,
            preferAssemblyMainPart: true);
        ElementGeometryBinding individual = ElementBindingResolver.ResolveGeometry(secondary, elements);

        Assert.Equal("main", grouped.SourceElement.ElementID);
        Assert.Equal(4.0, grouped.LengthM);
        Assert.Equal(0.2, grouped.WidthM);
        Assert.Equal("secondary", individual.SourceElement.ElementID);
        Assert.Equal(0.2, individual.LengthM);
        Assert.Equal(5.8, individual.WidthM);
        Assert.Equal(0.6, individual.HeightM);

        var millimeters = new UnitSettings
        {
            DisplayLengthUnit = LengthUnit.Millimeter,
            DecimalPlaces = 0
        };
        Assert.Equal("200 mm", UnitSystem.FormatLength(grouped.WidthM!.Value, millimeters, CultureInfo.InvariantCulture));
        Assert.Equal("4,000 mm", UnitSystem.FormatLength(grouped.LengthM!.Value, millimeters, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("main")]
    [InlineData("secondary")]
    public void LiftingReport_MapsAssemblyFieldsForGroupedAndIndividualRows(string elementId)
    {
        var source = new LiftingResultSourceRowDto
        {
            ElementId = elementId,
            ElementMark = "W1",
            MainPartGlobalId = "main",
            ParentAssemblyGlobalId = "parent",
            AssemblyLiftWeight = 9.28,
            AssemblyLiftCOG = new DomainPoint3D(10.0, 20.0, 1.5),
            AssemblyMemberIds = new[] { "main", "secondary", "hidden" },
            AssignedSetupNo = 1,
            Radius = 10,
            BoomLength = 15,
            RequiredCapacity = 5,
            AvailableCapacity = 10,
            InternalLiftStatus = "Liftable"
        };

        LiftingResultReportRowDto row = Assert.Single(
            new LiftingResultReportDataProvider().BuildRows(
                ReportTemplateCatalog.BasicLiftingResult,
                "Optimization Result",
                new[] { source }));

        Assert.Equal("main", row.Values["MainPartGlobalId"]);
        Assert.Equal("parent", row.Values["ParentAssemblyGlobalId"]);
        Assert.Equal(9.28, row.Values["AssemblyLiftWeight"]);
        Assert.Equal("(10.000; 20.000; 1.500)", row.Values["AssemblyLiftCOG"]);
        Assert.Equal("main, secondary, hidden", row.Values["AssemblyMemberIds"]);
    }

    [Fact]
    public void LiftingReportCatalog_ExposesEveryRequiredAssemblyField()
    {
        string[] expectedKeys =
        {
            "MainPartGlobalId",
            "ParentAssemblyGlobalId",
            "AssemblyLiftWeight",
            "AssemblyLiftCOG",
            "AssemblyMemberIds"
        };

        var templateKeys = ReportTemplateCatalog
            .GetBuiltInTemplate(ReportFamily.LiftingResult, ReportTemplateCatalog.BasicLiftingResult)
            .Columns
            .Select(column => column.Key)
            .ToHashSet(StringComparer.Ordinal);
        var availableKeys = ReportTemplateCatalog
            .GetAvailableColumns(ReportFamily.LiftingResult)
            .Select(column => column.Key)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(expectedKeys, key => Assert.Contains(key, templateKeys));
        Assert.All(expectedKeys, key => Assert.Contains(key, availableKeys));
    }

    private static List<PrecastElement> CreateAssemblyFixture()
    {
        var parent = new PrecastElement
        {
            ElementID = "parent",
            ElementMark = "W1",
            ElementName = "WALL",
            ElementType = "Assembly",
            ElementMaterial = "CONCRETE",
            Role = IfcElementRole.NeedsReview,
            MainPartGlobalId = "main",
            ChildGlobalIds = new List<string> { "main", "secondary", "hidden" },
            AssemblyMemberIds = new List<string> { "main", "secondary", "hidden" },
            AssemblyLiftWeight = 9.28,
            WeightTons = 9.28,
            ElementWT = 9.28,
            VolumeM3 = 3.8,
            ElementVolume = 3.8,
            PhysicalCOGAvailable = false
        };

        var main = new PrecastElement
        {
            ElementID = "main",
            ParentGlobalId = "parent",
            ElementType = "Wall",
            ChildClassification = AssemblyChildClassification.MainPart,
            BBoxLength = 4.0,
            BBoxWidth = 0.2,
            BBoxHeight = 3.0,
            BoundingBox_M = new DomainPoint3D(4.0, 0.2, 3.0),
            ElementCOG = new DomainPoint3D(10.0, 20.0, 1.5),
            COG_M = new DomainPoint3D(10.0, 20.0, 1.5),
            PhysicalCOGAvailable = true
        };

        var secondary = new PrecastElement
        {
            ElementID = "secondary",
            ParentGlobalId = "parent",
            ElementType = "Beam",
            ChildClassification = AssemblyChildClassification.VisibleSecondaryPart,
            BBoxLength = 0.2,
            BBoxWidth = 5.8,
            BBoxHeight = 0.6,
            BoundingBox_M = new DomainPoint3D(0.2, 5.8, 0.6),
            ElementCOG = new DomainPoint3D(9.5, 20.0, 2.7),
            COG_M = new DomainPoint3D(9.5, 20.0, 2.7),
            PhysicalCOGAvailable = true
        };

        var hidden = new PrecastElement
        {
            ElementID = "hidden",
            ParentGlobalId = "parent",
            ElementType = "Rebar",
            ChildClassification = AssemblyChildClassification.HiddenWeightContributor,
            WeightTons = 0.01
        };

        return new List<PrecastElement> { parent, main, secondary, hidden };
    }
}
