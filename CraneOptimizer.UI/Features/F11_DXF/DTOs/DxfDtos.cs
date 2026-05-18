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
}
