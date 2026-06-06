using CraneOptimizer.UI.Features.F11_DXF.DTOs;
using netDxf;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F11_DXF.Services
{
    public interface IDxfZoneImportService
    {
        IReadOnlyList<DxfImportLayerSummary> GetLayerSummaries(DxfDocument document, Matrix3D transform);

        IReadOnlyList<DxfZoneCandidate> GetCandidates(DxfDocument document, string layerName, Matrix3D transform);
    }
}
