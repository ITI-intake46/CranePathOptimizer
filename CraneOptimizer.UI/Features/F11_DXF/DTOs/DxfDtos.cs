using CraneOptimizer.Domain.Common;

namespace CraneOptimizer.UI.Features.F11_DXF.DTOs
{
    /// <summary>
    /// Simple DXF layer data shown in the alignment dialog.
    /// </summary>
    public sealed record DxfLayerDto(
        string Name,
        string ColorHex,
        int EntityCount,
        bool IsVisible);

    /// <summary>
    /// Simple DXF bounds data used when previewing and aligning a drawing.
    /// </summary>
    public sealed record DxfBoundsDto(
        double MinX,
        double MaxX,
        double MinY,
        double MaxY);

    /// <summary>
    /// Simple DXF transform data that can be saved or loaded.
    /// </summary>
    public sealed record DxfTransformDto(
        double OffsetX,
        double OffsetY,
        double Scale,
        double RotationDegrees,
        double DxfRefX,
        double DxfRefY,
        double IfcRefX,
        double IfcRefY,
        double GlobalOffsetX,
        double GlobalOffsetY,
        int DxfUnitIndex,
        int GlobalOffsetUnitIndex);

    /// <summary>
    /// Import readiness summary for a DXF layer.
    /// </summary>
    public sealed record DxfImportLayerSummary(
        string Name,
        string ColorHex,
        int TotalEntityCount,
        int ValidClosedCount,
        int InvalidCount);

    /// <summary>
    /// Candidate zone polygon extracted from a DXF layer.
    /// </summary>
    public sealed record DxfZoneCandidate(
        Guid Id,
        string LayerName,
        IReadOnlyList<DomainPoint3D> Vertices,
        double Area,
        string SourceEntityType,
        bool IsValid,
        IReadOnlyList<string> Issues);

    /// <summary>
    /// Aggregated result for DXF zone import discovery.
    /// </summary>
    public sealed record DxfZoneImportResult(
        IReadOnlyList<DxfImportLayerSummary> Layers,
        IReadOnlyList<DxfZoneCandidate> Candidates,
        IReadOnlyList<string> Issues);
}
