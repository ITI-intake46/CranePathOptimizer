using System;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Domain.Schemas.Enums;

namespace CraneOptimizer.UI.Core.Models
{
    /// <summary>
    /// Identifies a selectable object kind in the 3D viewport.
    /// GreenArea and RedArea are distinguished via ZoneType on GreenAreaZone.
    /// </summary>
    public enum SelectionKind
    {
        IfcElement,
        GreenArea,
        RedArea,
        Crane,
        DxfEntity
    }

    /// <summary>
    /// Generic selection target wrapping any selectable viewport object.
    /// Two targets are equal when their Kind and StableId match.
    /// </summary>
    public sealed class SelectionTarget
    {
        public SelectionKind Kind { get; }
        public string StableId { get; }
        public string DisplayName { get; }
        public object UnderlyingObject { get; }

        public SelectionTarget(SelectionKind kind, string stableId, string displayName, object underlyingObject)
        {
            Kind = kind;
            StableId = stableId ?? throw new ArgumentNullException(nameof(stableId));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            UnderlyingObject = underlyingObject ?? throw new ArgumentNullException(nameof(underlyingObject));
        }

        /// <summary>
        /// Factory: create a SelectionTarget from a raw viewport hit object.
        /// Returns null if the object is not a recognized selectable type.
        /// </summary>
        public static SelectionTarget? FromHitObject(object? hitObj)
        {
            if (hitObj == null) return null;

            if (hitObj is PrecastElement element)
            {
                return new SelectionTarget(
                    SelectionKind.IfcElement,
                    element.ElementID,
                    element.ElementMark ?? element.ElementID,
                    element);
            }

            if (hitObj is GreenAreaZone zone)
            {
                var kind = zone.Type == ZoneType.Forbidden
                    ? SelectionKind.RedArea
                    : SelectionKind.GreenArea;

                return new SelectionTarget(
                    kind,
                    zone.ZoneId.ToString(),
                    zone.Name ?? $"Zone {zone.ZoneId}",
                    zone);
            }

            if (hitObj is SceneCrane crane)
            {
                return new SelectionTarget(
                    SelectionKind.Crane,
                    crane.Id.ToString(),
                    crane.CraneDefinition?.Name ?? $"Crane {crane.Id}",
                    crane);
            }

            if (hitObj is SceneDxf dxf)
            {
                return new SelectionTarget(
                    SelectionKind.DxfEntity,
                    dxf.Id.ToString(),
                    dxf.Layer ?? $"DXF {dxf.Id}",
                    dxf);
            }

            return null;
        }

        public override bool Equals(object? obj)
        {
            return obj is SelectionTarget other
                && Kind == other.Kind
                && StableId == other.StableId;
        }

        public override int GetHashCode() => HashCode.Combine(Kind, StableId);

        public override string ToString() => $"[{Kind}] {DisplayName} ({StableId})";
    }
}
