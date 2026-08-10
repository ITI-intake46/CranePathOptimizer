using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Units;
using System.Collections.Generic;

namespace CraneOptimizer.UI.Features.F0_CoordinateSystem
{
    public class CoordinateService
    {
        public double NormalizeLength(double length)
        {
            return length;
        }

        public double DenormalizeLength(double length)
        {
            return length;
        }

        public double NormalizeLength(double length, LengthUnit sourceUnit)
        {
            return UnitSystem.ToMeters(length, sourceUnit);
        }

        public double DenormalizeLength(double lengthInMeters, LengthUnit targetUnit)
        {
            return UnitSystem.FromMeters(lengthInMeters, targetUnit);
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
            var normalized = element.CreateCopy();
            normalized.ElementLength = NormalizeNullableLength(element.ElementLength);
            normalized.ElementWidth = NormalizeNullableLength(element.ElementWidth);
            normalized.ElementHeight = NormalizeNullableLength(element.ElementHeight);
            normalized.ElementCOG = NormalizePoint(element.ElementCOG);
            normalized.AssemblyLiftCOG = element.AssemblyLiftCOG == null
                ? null
                : NormalizePoint(element.AssemblyLiftCOG);
            normalized.BBoxLength = NormalizeNullableLength(element.BBoxLength);
            normalized.BBoxWidth = NormalizeNullableLength(element.BBoxWidth);
            normalized.BBoxHeight = NormalizeNullableLength(element.BBoxHeight);
            normalized.COG_M = NormalizePoint(element.COG_M);
            normalized.BoundingBox_M = NormalizePoint(element.BoundingBox_M);
            return normalized;
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
                Area = zone.Area,
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
