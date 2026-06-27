using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Infrastructure.Ifc;
using CraneOptimizer.Infrastructure.Repositories;
using Xbim.Common.XbimExtensions;
using Xbim.Ifc;
using Xunit;

namespace CraneOptimizer.Application.Tests.IFC;

public sealed class IfcGridDeduplicationTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    [Fact]
    public void ReadElements_IdenticalPlanGridsAtDifferentZ_KeepsBottomGridOnly()
    {
        var bottomId = Guid.NewGuid().ToString();
        var upperId = Guid.NewGuid().ToString();
        var path = CreateIfc(model =>
        {
            AddGrid(model, bottomId, "Main Grid", "A", 0, 0);
            AddGrid(model, upperId, "Main Grid", "A", 0, 12);
        });

        var elements = ReadElements(path);

        var grid = Assert.Single(elements, element => element.RawIfcType == "IfcGrid");
        Assert.Equal(bottomId, grid.ElementID);
        Assert.False(grid.IncludeInGa);
    }

    [Fact]
    public void ReadElements_SameAxisTagsAtDifferentPlanCoordinates_KeepsBothGrids()
    {
        var firstId = Guid.NewGuid().ToString();
        var secondId = Guid.NewGuid().ToString();
        var path = CreateIfc(model =>
        {
            AddGrid(model, firstId, "Main Grid", "A", 0, 0);
            AddGrid(model, secondId, "Main Grid", "A", 100, 0);
        });

        var elements = ReadElements(path);

        var gridIds = elements
            .Where(element => element.RawIfcType == "IfcGrid")
            .Select(element => element.ElementID)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(2, gridIds.Count);
        Assert.Contains(firstId, gridIds);
        Assert.Contains(secondId, gridIds);
    }

    private static List<CraneOptimizer.Domain.Schemas.PrecastElement> ReadElements(string path)
    {
        using var session = new IfcModelSessionService();
        var audit = new IfcDiagnosticAuditService(session);
        var normalization = new IfcNormalizationDecisionService(new IfcExporterProfileResolver());
        using var reader = new IfcReaderRepository(audit, normalization, session);
        return reader.ReadElements(path, new IfcReadingOptions(HideGrids: false));
    }

    private string CreateIfc(Action<IfcStore> build)
    {
        var path = Path.Combine(Path.GetTempPath(), $"GridDedup_{Guid.NewGuid():N}.ifc");
        _tempFiles.Add(path);
        using var model = IfcStore.Create(
            Xbim.Common.Step21.XbimSchemaVersion.Ifc4,
            Xbim.IO.XbimStoreType.InMemoryModel);
        using (var transaction = model.BeginTransaction("Create duplicate grids"))
        {
            var project = model.Instances.New<Xbim.Ifc4.Kernel.IfcProject>();
            project.Name = "Grid deduplication test";
            build(model);
            transaction.Commit();
        }

        model.SaveAs(path);
        return path;
    }

    private static void AddGrid(
        IfcStore model,
        string globalId,
        string name,
        string axisTag,
        double startX,
        double elevation)
    {
        var grid = model.Instances.New<Xbim.Ifc4.ProductExtension.IfcGrid>();
        grid.GlobalId = globalId;
        grid.Name = name;

        var start = model.Instances.New<Xbim.Ifc4.GeometryResource.IfcCartesianPoint>();
        start.SetXYZ(startX, 0, elevation);
        var end = model.Instances.New<Xbim.Ifc4.GeometryResource.IfcCartesianPoint>();
        end.SetXYZ(startX + 10, 0, elevation);
        var curve = model.Instances.New<Xbim.Ifc4.GeometryResource.IfcPolyline>();
        curve.Points.Add(start);
        curve.Points.Add(end);

        var axis = model.Instances.New<Xbim.Ifc4.GeometricConstraintResource.IfcGridAxis>();
        axis.AxisTag = axisTag;
        axis.AxisCurve = curve;
        axis.SameSense = true;
        grid.UAxes.Add(axis);
    }

    public void Dispose()
    {
        foreach (var path in _tempFiles)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Best-effort cleanup for xBIM file handles on Windows.
            }
        }
    }
}
