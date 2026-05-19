namespace CraneOptimizer.UI.Features.F7_Zones.DTOs
{
    /// <summary>
    /// Simple zone data used by the zones list and export flow.
    /// </summary>
    public sealed record ZoneDto(
        Guid ZoneId,
        string Name,
        string ZoneType,
        double MinHeight,
        double MaxHeight,
        IReadOnlyList<ZoneVertexDto> Vertices);

    /// <summary>
    /// Simple vertex data used by zone DTOs.
    /// </summary>
    public sealed record ZoneVertexDto(
        double X,
        double Y,
        double Z);

    /// <summary>
    /// Simple export model for zone JSON files.
    /// </summary>
    public sealed record ZoneExportDto(
        string Name,
        string Type,
        double MinHeight,
        double MaxHeight,
        IReadOnlyList<ZoneVertexDto> Vertices);
}
