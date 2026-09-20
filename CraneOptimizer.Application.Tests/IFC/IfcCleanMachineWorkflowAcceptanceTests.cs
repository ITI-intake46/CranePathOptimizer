using CraneOptimizer.Infrastructure.Ifc;
using CraneOptimizer.Infrastructure.Repositories;
using Xbim.Ifc4.Interfaces;

namespace CraneOptimizer.Application.Tests.IFC;

public sealed class IfcCleanMachineWorkflowAcceptanceTests
{
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public void GeometryEngine_ImportsCreatesSelectableGeometryAndReopensCleanly()
    {
        var path = Fixture("rev05_acceptance.ifc");
        Assert.True(File.Exists(path), $"Acceptance IFC fixture missing: {path}");

        using var sessionService = new IfcModelSessionService();
        var first = sessionService.OpenSession(path);
        var firstSessionId = first.SessionId;

        Assert.False(first.IsDisposed);
        Assert.Equal(1, first.CreateContextCount);
        Assert.True(first.ShapeInstanceCount >= 5,
            $"Expected at least five xBIM shape instances but found {first.ShapeInstanceCount}. " +
            "This normally indicates that the native Xbim.Geometry.Engine64.dll is missing or unusable.");

        var productLabels = first.GeometryContext.ShapeInstances()
            .Select(instance => instance.IfcProductLabel)
            .Where(label => label > 0)
            .Distinct()
            .ToList();

        Assert.True(productLabels.Count >= 5);
        var firstProduct = first.Model.Instances
            .OfType<IIfcProduct>()
            .FirstOrDefault(product => productLabels.Contains(product.EntityLabel));
        Assert.NotNull(firstProduct); // selection/hit-test identity can resolve geometry back to an IFC product.
        Assert.False(string.IsNullOrWhiteSpace(firstProduct!.GlobalId.ToString()));

        sessionService.Clear();
        Assert.True(first.IsDisposed);
        Assert.Null(sessionService.Current);

        var reopened = sessionService.OpenSession(path);
        Assert.NotEqual(firstSessionId, reopened.SessionId);
        Assert.Equal(1, reopened.CreateContextCount);
        Assert.True(reopened.ShapeInstanceCount >= 5);
        Assert.NotNull(sessionService.GetActiveSession(path));
    }

    [Fact]
    public void GeometryAndReader_ProduceFiveGaEligibleMassResolvedElements()
    {
        var path = Fixture("rev05_acceptance.ifc");
        using var sessionService = new IfcModelSessionService();
        var auditService = new IfcDiagnosticAuditService(sessionService);
        var normalization = new IfcNormalizationDecisionService(new IfcExporterProfileResolver());
        using var reader = new IfcReaderRepository(auditService, normalization, sessionService);

        var session = sessionService.OpenSession(path);
        Assert.True(session.ShapeInstanceCount >= 5);

        var elements = reader.ReadElements(path)
            .Where(element => element.WeightTons is > 0)
            .ToList();

        var expected = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["3t3TDZl_D9NOIWB0BSjzJI"] = 8.5,
            ["1xS3BCk291UvhgP2a6eflT"] = 5.0,
            ["0YvctVUKr2A9XkQZxR2W3q"] = 4.0,
            ["3J0f$_Dhv7eA5qURx2A4lH"] = 3.0,
            ["1DA9$Kf2H1WhX8C0x7YV4F"] = 2.0
        };

        foreach (var (globalId, tonnes) in expected)
        {
            var element = Assert.Single(elements, candidate => candidate.ElementID == globalId);
            Assert.Equal(tonnes, element.WeightTons!.Value, 4);
        }
    }
}
