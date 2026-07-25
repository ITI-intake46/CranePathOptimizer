using CraneOptimizer.Domain.Schemas.Enums;
using CraneOptimizer.UI.Features.F11_DXF.DTOs;
using CraneOptimizer.UI.Features.F11_DXF.Models;

namespace CraneOptimizer.UI.Features.F11_DXF.Services;

public interface IDxfLayerZoneAssignmentService
{
    Task<IReadOnlyDictionary<string, DxfLayerZoneState>> GetAssignmentStatesAsync(
        DxfLayerZoneSource source,
        IEnumerable<string> layerNames,
        CancellationToken cancellationToken);

    Task<DxfLayerZoneTransaction> AssignAsync(
        DxfLayerZoneSource source,
        string layerName,
        ZoneType zoneType,
        Func<CancellationToken, IReadOnlyList<DxfZoneCandidate>> candidateProvider,
        CancellationToken cancellationToken);

    Task<DxfLayerZoneTransaction> ReplaceAsync(
        DxfLayerZoneSource source,
        string layerName,
        ZoneType zoneType,
        Func<CancellationToken, IReadOnlyList<DxfZoneCandidate>> candidateProvider,
        CancellationToken cancellationToken);

    Task<DxfLayerZoneTransaction> ClearAsync(
        DxfLayerZoneSource source,
        string layerName,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DxfLayerZoneTransaction>> ClearAllAsync(
        DxfLayerZoneSource source,
        IEnumerable<string> layerNames,
        CancellationToken cancellationToken);
}
