using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Diagnostics;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.Application.Services;

/// <summary>
/// Resolves presentation bindings from the canonical element graph without
/// creating another source of engineering data.
/// </summary>
public static class ElementBindingResolver
{
    public static PrecastElement? ResolveAssemblyParent(
        PrecastElement? element,
        IReadOnlyList<PrecastElement>? allElements)
    {
        if (element == null)
            return null;

        if (HasAssemblyMetadata(element))
            return element;

        if (allElements == null || string.IsNullOrWhiteSpace(element.ParentGlobalId))
            return null;

        PrecastElement? parent = allElements.FirstOrDefault(candidate =>
            string.Equals(candidate.ElementID, element.ParentGlobalId, StringComparison.Ordinal));

        return parent != null && HasAssemblyMetadata(parent) ? parent : null;
    }

    public static PrecastElement ResolveMainPartOrSelf(
        PrecastElement element,
        IReadOnlyList<PrecastElement>? allElements)
    {
        ArgumentNullException.ThrowIfNull(element);

        PrecastElement? parent = ResolveAssemblyParent(element, allElements);
        if (parent == null || allElements == null)
            return element;

        if (!string.IsNullOrWhiteSpace(parent.MainPartGlobalId))
        {
            PrecastElement? mainPart = allElements.FirstOrDefault(candidate =>
                string.Equals(candidate.ElementID, parent.MainPartGlobalId, StringComparison.Ordinal));
            if (mainPart != null)
                return mainPart;
        }

        return allElements.FirstOrDefault(candidate =>
                   string.Equals(candidate.ParentGlobalId, parent.ElementID, StringComparison.Ordinal)
                   && candidate.ChildClassification == AssemblyChildClassification.MainPart)
               ?? element;
    }

    public static ElementGeometryBinding ResolveGeometry(
        PrecastElement element,
        IReadOnlyList<PrecastElement>? allElements = null,
        bool preferAssemblyMainPart = false)
    {
        ArgumentNullException.ThrowIfNull(element);

        PrecastElement source = preferAssemblyMainPart
            ? ResolveMainPartOrSelf(element, allElements)
            : element;
        double? length = FirstPositive(source.ElementLength, source.BBoxLength, source.BoundingBox_M?.X);
        double? width = FirstPositive(source.ElementWidth, source.BBoxWidth, source.BoundingBox_M?.Y);
        double? height = FirstPositive(source.ElementHeight, source.BBoxHeight, source.BoundingBox_M?.Z);
        DomainPoint3D? center = ResolvePhysicalCenter(source);

        return new ElementGeometryBinding(source, length, width, height, center);
    }

    private static bool HasAssemblyMetadata(PrecastElement element) =>
        element.Role is IfcElementRole.LiftAssembly or IfcElementRole.GroupingOnly
        || !string.IsNullOrWhiteSpace(element.MainPartGlobalId)
        || element.ChildGlobalIds.Count > 0
        || element.AssemblyLiftWeight.HasValue
        || element.AssemblyMemberIds.Count > 0;

    private static DomainPoint3D? ResolvePhysicalCenter(PrecastElement element)
    {
        if (!element.PhysicalCOGAvailable)
            return null;

        if (HasNonZeroCoordinate(element.COG_M))
            return element.COG_M;

        if (HasNonZeroCoordinate(element.ElementCOG))
            return element.ElementCOG;

        return element.COG_M ?? element.ElementCOG;
    }

    private static bool HasNonZeroCoordinate(DomainPoint3D? point) =>
        point != null && (Math.Abs(point.X) > double.Epsilon || Math.Abs(point.Y) > double.Epsilon || Math.Abs(point.Z) > double.Epsilon);

    private static double? FirstPositive(params double?[] values) =>
        values.FirstOrDefault(value => value is > 0);
}

public sealed record ElementGeometryBinding(
    PrecastElement SourceElement,
    double? LengthM,
    double? WidthM,
    double? HeightM,
    DomainPoint3D? CenterOfGravityM);
