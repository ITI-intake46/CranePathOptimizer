using CraneOptimizer.Application.Reports.Services;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Schemas;
using Xunit;

namespace CraneOptimizer.Application.Tests.Reports;

public sealed class ReportGridReferenceTests
{
    [Fact]
    public void ResolveReportableElements_ExcludesReferenceGridsBeforeRowsAreBuilt()
    {
        var beam = new PrecastElement
        {
            ElementID = "1zqWVRVLz5h8EdjS4I_HxE",
            ElementName = "My BEAM",
            ElementType = "Beam"
        };
        var grid = new PrecastElement
        {
            ElementID = "GRID-UPPER",
            ElementName = "Main Grid",
            ElementType = "Grid",
            RawIfcType = "IfcGrid",
            Role = IfcElementRole.InternalAuxiliary,
            IncludeInGa = false,
            IncludeInRender = true
        };

        var rows = new ReportableElementResolver()
            .ResolveReportableElements([beam, grid]);

        var row = Assert.Single(rows);
        Assert.Same(beam, row);
    }
}
