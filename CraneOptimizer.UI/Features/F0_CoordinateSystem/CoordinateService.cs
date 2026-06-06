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
            return new PrecastElement
            {
                ElementID = element.ElementID,
                ElementRevitId = element.ElementRevitId,
                ElementMark = element.ElementMark,
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
                BBoxHeight = NormalizeNullableLength(element.BBoxHeight),

                // Copy and preserve all Phase 2A Normalization properties
                RawIfcType = element.RawIfcType,
                ResolvedCategory = element.ResolvedCategory,
                Role = element.Role,
                ParentGlobalId = element.ParentGlobalId,
                ChildGlobalIds = new List<string>(element.ChildGlobalIds ?? new()),
                VisibleGeometryOwnerGlobalIds = new List<string>(element.VisibleGeometryOwnerGlobalIds ?? new()),
                HiddenInternalGeometryOwnerGlobalIds = new List<string>(element.HiddenInternalGeometryOwnerGlobalIds ?? new()),
                IncludeInRender = element.IncludeInRender,
                IncludeInElementTree = element.IncludeInElementTree,
                IncludeInGa = element.IncludeInGa,
                WeightTons = element.WeightTons,
                WeightSource = element.WeightSource,
                WeightTrustLevel = element.WeightTrustLevel,
                VolumeM3 = element.VolumeM3,
                COG_M = NormalizePoint(element.COG_M),
                BoundingBox_M = NormalizePoint(element.BoundingBox_M),
                ExtractionWarnings = new List<string>(element.ExtractionWarnings ?? new()),
                NormalizationDecisionReason = element.NormalizationDecisionReason
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
