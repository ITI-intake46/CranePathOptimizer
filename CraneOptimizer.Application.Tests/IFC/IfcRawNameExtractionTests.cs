using CraneOptimizer.Infrastructure.Ifc;
using CraneOptimizer.Infrastructure.Repositories;
using Xbim.Ifc;
using Xunit;

namespace CraneOptimizer.Application.Tests.IFC;

public sealed class IfcRawNameExtractionTests
{
    [Fact]
    public void ReadElements_PreservesKnownRawProductNames()
    {
        var path = Path.Combine(Path.GetTempPath(), $"IfcRawNames_{Guid.NewGuid():N}.ifc");
        using (var model = IfcStore.Create(
                   Xbim.Common.Step21.XbimSchemaVersion.Ifc4,
                   Xbim.IO.XbimStoreType.InMemoryModel))
        {
            using (var transaction = model.BeginTransaction("Create named beams"))
            {
                var project = model.Instances.New<Xbim.Ifc4.Kernel.IfcProject>();
                project.Name = "Raw name SSOT test";

                var first = model.Instances.New<Xbim.Ifc4.SharedBldgElements.IfcBeam>();
                first.GlobalId = "1zqWVRVLz5h8EdjS4I_HxE";
                first.Name = "My BEAM";
                first.Tag = "B-101";

                var second = model.Instances.New<Xbim.Ifc4.SharedBldgElements.IfcBeam>();
                second.GlobalId = "1hiQd8Xon3rRRv$q2e7P3T";
                second.Name = "Sec BEAM";
                second.Tag = "B-102";
                transaction.Commit();
            }

            model.SaveAs(path);
        }

        try
        {
            using var session = new IfcModelSessionService();
            var audit = new IfcDiagnosticAuditService(session);
            var normalization = new IfcNormalizationDecisionService(new IfcExporterProfileResolver());
            using var reader = new IfcReaderRepository(audit, normalization, session);

            var elements = reader.ReadElements(path);
            var first = Assert.Single(elements, element => element.ElementID == "1zqWVRVLz5h8EdjS4I_HxE");
            var second = Assert.Single(elements, element => element.ElementID == "1hiQd8Xon3rRRv$q2e7P3T");
            Assert.Equal("My BEAM", first.ElementName);
            Assert.Equal("Sec BEAM", second.ElementName);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
