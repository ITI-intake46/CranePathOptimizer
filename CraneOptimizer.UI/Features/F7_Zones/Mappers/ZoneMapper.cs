using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Features.F7_Zones.DTOs;

namespace CraneOptimizer.UI.Features.F7_Zones.Mappers
{
    /// <summary>
    /// Maps scene zones to small DTOs for the zone list and export.
    /// </summary>
    internal static class ZoneMapper
    {
        public static ZoneDto ToZoneDto(
            this GreenAreaZone zone,
            double minHeight = 0,
            double maxHeight = 999)
        {
            return new ZoneDto(
                zone.ZoneId,
                string.IsNullOrWhiteSpace(zone.Name) ? $"Zone {zone.ZoneId.ToString()[..8]}" : zone.Name,
                zone.Type.ToString(),
                minHeight,
                maxHeight,
                zone.Vertices
                    .Select(vertex => new ZoneVertexDto(vertex.X, vertex.Y, vertex.Z))
                    .ToList());
        }

        public static ZoneExportDto ToExportDto(this ZoneDto dto)
        {
            return new ZoneExportDto(
                dto.Name,
                dto.ZoneType,
                dto.MinHeight,
                dto.MaxHeight,
                dto.Vertices.ToList());
        }
    }
}
