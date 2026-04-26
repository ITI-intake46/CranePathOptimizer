using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.UI.Features.F0_CoordinateSystem
{
    public class CoordinateService
    {
        public const double MillimetersToMetersScale = 0.001;
        public const double MetersToMillimetersScale = 1000.0;

        public double NormalizeLength(double lengthInMillimeters)
        {
            return lengthInMillimeters * MillimetersToMetersScale;
        }

        public double DenormalizeLength(double lengthInMeters)
        {
            return lengthInMeters * MetersToMillimetersScale;
        }

        public DomainPoint3D NormalizePoint(DomainPoint3D point)
        {
            return new DomainPoint3D(
                NormalizeLength(point.X),
                NormalizeLength(point.Y),
                NormalizeLength(point.Z));
        }

        public DomainPoint3D DenormalizePoint(DomainPoint3D point)
        {
            return new DomainPoint3D(
                DenormalizeLength(point.X),
                DenormalizeLength(point.Y),
                DenormalizeLength(point.Z));
        }

        public bool IsValidPoint(DomainPoint3D? point)
        {
            return point != null
                && IsFinite(point.X)
                && IsFinite(point.Y)
                && IsFinite(point.Z);
        }

        public PrecastElement NormalizeElementToMeters(PrecastElement element)
        {
            return new PrecastElement
            {
                ElementID = element.ElementID,
                ElementType = element.ElementType,
                ElementLength = NormalizeNullableLength(element.ElementLength),
                ElementWidth = NormalizeNullableLength(element.ElementWidth),
                ElementHeight = NormalizeNullableLength(element.ElementHeight),
                ElementMaterial = element.ElementMaterial,
                ElementWT = element.ElementWT,
                ElementVolume = element.ElementVolume,
                ElementCOG = NormalizePoint(element.ElementCOG),
                BBoxLength = NormalizeNullableLength(element.BBoxLength),
                BBoxWidth = NormalizeNullableLength(element.BBoxWidth),
                BBoxHeight = NormalizeNullableLength(element.BBoxHeight)
            };
        }

        public GreenAreaZone NormalizeZoneToMeters(GreenAreaZone zone)
        {
            var normalizedVertices = new List<DomainPoint3D>(zone.Vertices.Count);

            for (int i = 0; i < zone.Vertices.Count; i++)
            {
                normalizedVertices.Add(NormalizePoint(zone.Vertices[i]));
            }

            return new GreenAreaZone
            {
                ZoneId = zone.ZoneId,
                Name = zone.Name,
                Type = zone.Type,
                Area = zone.Area * MillimetersToMetersScale * MillimetersToMetersScale,
                Vertices = normalizedVertices
            };
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private double? NormalizeNullableLength(double? value)
        {
            return value.HasValue ? NormalizeLength(value.Value) : null;
        }
    }
}
