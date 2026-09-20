using CraneOptimizer.Infrastructure.Ifc;
using CraneOptimizer.Infrastructure.Repositories;

namespace CraneOptimizer.Application.Tests.IFC;

public sealed class IfcMassUnitReaderAcceptanceTests
{
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public async Task ActualReaderPath_ResolvesProjectKgExplicitTonneAndConvertedPoundWithoutMagnitudeGuessing()
    {
        var path = Fixture("rev05_acceptance.ifc");
        Assert.True(File.Exists(path), $"Acceptance IFC fixture missing: {path}");

        using var session = new IfcModelSessionService();
        var auditService = new IfcDiagnosticAuditService(session);
        var normalization = new IfcNormalizationDecisionService(new IfcExporterProfileResolver());
        using var reader = new IfcReaderRepository(auditService, normalization, session);

        var audit = await auditService.RunAuditAsync(path);
        Assert.True(audit.Metadata.MassUnitWasDeclared);
        Assert.NotNull(audit.Metadata.MassConversionFactor);
        Assert.Equal(1d, audit.Metadata.MassConversionFactor!.Value, 8);

        var kgProject = Assert.Single(audit.Weights, w => w.GlobalId == "3t3TDZl_D9NOIWB0BSjzJI");
        Assert.Equal(8500d, kgProject.WeightRawValue!.Value, 6);
        Assert.Equal(8.5d, kgProject.WeightStandardValueTons!.Value, 6);
        Assert.Equal("ProjectMassUnit", kgProject.WeightUnitTrustStatus);
        Assert.True(kgProject.IsGaEligibleByUnit);

        var explicitTonne = Assert.Single(audit.Weights, w => w.GlobalId == "1xS3BCk291UvhgP2a6eflT");
        Assert.Equal(5d, explicitTonne.WeightRawValue!.Value, 6);
        Assert.Equal(5d, explicitTonne.WeightStandardValueTons!.Value, 6);
        Assert.Contains("ExplicitQuantityUnit", explicitTonne.WeightUnitTrustStatus, StringComparison.Ordinal);
        Assert.True(explicitTonne.HasUnitConflict); // Explicit tonne correctly overrides project kg.
        Assert.True(explicitTonne.IsGaEligibleByUnit);

        var pounds = Assert.Single(audit.Weights, w => w.GlobalId == "0YvctVUKr2A9XkQZxR2W3q");
        Assert.Equal(4d, pounds.WeightStandardValueTons!.Value, 4);
        Assert.True(pounds.IsGaEligibleByUnit);

        var elements = reader.ReadElements(path);
        Assert.Equal(8.5d, Assert.Single(elements, e => e.ElementID == "3t3TDZl_D9NOIWB0BSjzJI").WeightTons!.Value, 6);
        Assert.Equal(5d, Assert.Single(elements, e => e.ElementID == "1xS3BCk291UvhgP2a6eflT").WeightTons!.Value, 6);
        Assert.Equal(4d, Assert.Single(elements, e => e.ElementID == "0YvctVUKr2A9XkQZxR2W3q").WeightTons!.Value, 4);
        Assert.Equal(3d, Assert.Single(elements, e => e.ElementID == "3J0f$_Dhv7eA5qURx2A4lH").WeightTons!.Value, 6);
        Assert.Equal(2d, Assert.Single(elements, e => e.ElementID == "1DA9$Kf2H1WhX8C0x7YV4F").WeightTons!.Value, 6);
    }

    [Fact]
    public async Task ActualReaderPath_MissingProjectAndQuantityUnit_RemainsUnresolvedAndGaIneligible()
    {
        var path = Fixture("rev05_missing_mass_unit.ifc");
        Assert.True(File.Exists(path), $"Acceptance IFC fixture missing: {path}");

        using var session = new IfcModelSessionService();
        var auditService = new IfcDiagnosticAuditService(session);
        var normalization = new IfcNormalizationDecisionService(new IfcExporterProfileResolver());
        using var reader = new IfcReaderRepository(auditService, normalization, session);

        var audit = await auditService.RunAuditAsync(path);
        Assert.False(audit.Metadata.MassUnitWasDeclared);
        Assert.Null(audit.Metadata.MassConversionFactor);

        var unresolved = Assert.Single(audit.Weights, w => w.GlobalId == "3t3TDZl_D9NOIWB0BSjzJI");
        Assert.Equal(8500d, unresolved.WeightRawValue!.Value, 6);
        Assert.Null(unresolved.WeightStandardValueTons);
        Assert.Equal("MissingUnit", unresolved.WeightUnitTrustStatus);
        Assert.False(unresolved.IsGaEligibleByUnit);
        Assert.Contains("no", unresolved.Warning, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unit", unresolved.Warning, StringComparison.OrdinalIgnoreCase);

        var elements = reader.ReadElements(path);
        var element = Assert.Single(elements, e => e.ElementID == "3t3TDZl_D9NOIWB0BSjzJI");
        Assert.Null(element.WeightTons);
    }
    [Theory]
    [InlineData("rev05_malformed_explicit_mass_unit.ifc")]
    [InlineData("rev05_malformed_project_mass_unit.ifc")]
    public async Task MalformedConversionBasedMassUnit_FailsClosed(string fixtureName)
    {
        var path=Fixture(fixtureName);
        using var session=new IfcModelSessionService();
        var auditService=new IfcDiagnosticAuditService(session);
        var normalization=new IfcNormalizationDecisionService(new IfcExporterProfileResolver());
        using var reader=new IfcReaderRepository(auditService,normalization,session);
        var audit=await auditService.RunAuditAsync(path);
        Assert.Contains(audit.Weights,w=>w.WeightRawValue.HasValue && !w.IsGaEligibleByUnit);
    }

}
