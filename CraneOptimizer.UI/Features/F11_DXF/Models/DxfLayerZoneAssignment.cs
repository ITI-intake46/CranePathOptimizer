using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.UI.Features.F11_DXF.Models;

public enum DxfLayerZoneAssignment
{
    None,
    Allowed,
    Forbidden,
    Inconsistent
}

public readonly record struct DxfLayerZoneKey(Guid SourceDxfId, string LayerName);

public sealed record DxfLayerZoneSource(
    Guid SourceDxfId,
    string FilePath,
    string DisplayName,
    bool AllowLegacyFileNameMatch = false);

public sealed record DxfLayerZoneState(
    DxfLayerZoneAssignment Assignment,
    int ZoneCount,
    IReadOnlyList<Guid> ZoneIds);

public sealed record DxfLayerZoneTransaction(
    DxfLayerZoneKey Key,
    DxfLayerZoneAssignment PreviousAssignment,
    DxfLayerZoneAssignment NewAssignment,
    IReadOnlyList<GreenAreaZone> PreviousZones,
    IReadOnlyList<GreenAreaZone> NextZones,
    string Description)
{
    public bool HasChanges =>
        PreviousZones.Count > 0 ||
        NextZones.Count > 0 ||
        PreviousAssignment != NewAssignment;
}

public sealed record DxfLayerZoneOperationResult(
    DxfLayerZoneAssignment Assignment,
    int ZoneCount,
    int AddedCount,
    int RemovedCount,
    string Message);
