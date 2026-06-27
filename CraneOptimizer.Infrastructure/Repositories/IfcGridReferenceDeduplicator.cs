using System.Collections;
using System.Globalization;
using Xbim.Common.Geometry;
using Xbim.Ifc4.Interfaces;
using Xbim.ModelGeometry.Scene;

namespace CraneOptimizer.Infrastructure.Repositories;

internal sealed class IfcGridReferenceDeduplicator
{
    private readonly Xbim3DModelContext _geometryContext;
    private readonly double _lengthToMetersFactor;

    public IfcGridReferenceDeduplicator(
        Xbim3DModelContext geometryContext,
        double lengthToMetersFactor)
    {
        _geometryContext = geometryContext;
        _lengthToMetersFactor = lengthToMetersFactor;
    }

    public HashSet<string> SelectBottomGridGlobalIds(IEnumerable<IIfcGrid> grids)
    {
        var candidates = grids
            .Where(grid => !string.IsNullOrWhiteSpace(grid.GlobalId))
            .Select(BuildCandidate)
            .ToList();

        return candidates
            .GroupBy(candidate => candidate.PlanSignature, StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(candidate => candidate.MinZ)
                .ThenBy(candidate => candidate.PlacementZ)
                .ThenBy(candidate => candidate.GlobalId, StringComparer.Ordinal)
                .First()
                .GlobalId)
            .ToHashSet(StringComparer.Ordinal);
    }

    private GridCandidate BuildCandidate(IIfcGrid grid)
    {
        var placement = ReadPlacementOffset(grid.ObjectPlacement);
        var axes = EnumerateAxes(grid).ToList();
        var bounds = TryGetWorldBounds(grid);
        var axisPoints = axes
            .SelectMany(axis => ReadAxisPoints(axis.Axis.AxisCurve, placement))
            .ToList();

        var minZ = bounds?.MinZ
            ?? (axisPoints.Count > 0 ? axisPoints.Min(point => point.Z) : placement.Z);
        var planSignature = string.Join("|",
            NormalizeToken(grid.Name?.ToString()),
            BuildAxisTagSignature(axes),
            BuildPlanGeometrySignature(bounds, axisPoints, placement));

        return new GridCandidate(
            grid.GlobalId.ToString(),
            planSignature,
            minZ,
            placement.Z);
    }

    private string BuildPlanGeometrySignature(
        GridBounds? bounds,
        IReadOnlyList<GridPoint> axisPoints,
        GridPoint placement)
    {
        var boundsSignature = bounds == null
            ? $"P:{Quantize(placement.X)},{Quantize(placement.Y)}"
            : $"B:{Quantize(bounds.MinX)},{Quantize(bounds.MinY)},{Quantize(bounds.MaxX)},{Quantize(bounds.MaxY)}";
        var pointsSignature = string.Join(";", axisPoints.Select(point =>
            $"{Quantize(point.X)},{Quantize(point.Y)}"));
        return $"{boundsSignature}|A:{pointsSignature}";
    }

    private static string BuildAxisTagSignature(
        IReadOnlyList<(string Group, int Index, IIfcGridAxis Axis)> axes) =>
        string.Join(";", axes.Select(axis =>
            $"{axis.Group}{axis.Index}:{NormalizeToken(axis.Axis.AxisTag?.ToString())}"));

    private static IEnumerable<(string Group, int Index, IIfcGridAxis Axis)> EnumerateAxes(IIfcGrid grid)
    {
        var index = 0;
        foreach (var axis in grid.UAxes)
            yield return ("U", index++, axis);

        index = 0;
        foreach (var axis in grid.VAxes)
            yield return ("V", index++, axis);

        index = 0;
        foreach (var axis in grid.WAxes)
            yield return ("W", index++, axis);
    }

    private IEnumerable<GridPoint> ReadAxisPoints(IIfcCurve? curve, GridPoint placement)
    {
        if (curve is not IIfcPolyline polyline)
            yield break;

        foreach (var point in polyline.Points)
        {
            var coordinates = ReadCoordinates(point.Coordinates).ToList();
            if (coordinates.Count < 2)
                continue;

            yield return new GridPoint(
                ToMeters(coordinates[0]) + placement.X,
                ToMeters(coordinates[1]) + placement.Y,
                ToMeters(coordinates.Count > 2 ? coordinates[2] : 0) + placement.Z);
        }
    }

    private GridBounds? TryGetWorldBounds(IIfcGrid grid)
    {
        var instances = _geometryContext.ShapeInstancesOf(grid).ToList();
        if (instances.Count == 0)
            return null;

        double minX = double.MaxValue;
        double minY = double.MaxValue;
        double minZ = double.MaxValue;
        double maxX = double.MinValue;
        double maxY = double.MinValue;
        double maxZ = double.MinValue;

        foreach (var instance in instances)
        {
            var box = instance.BoundingBox;
            var transform = instance.Transformation;
            var corners = new[]
            {
                new XbimPoint3D(box.Min.X, box.Min.Y, box.Min.Z),
                new XbimPoint3D(box.Max.X, box.Min.Y, box.Min.Z),
                new XbimPoint3D(box.Min.X, box.Max.Y, box.Min.Z),
                new XbimPoint3D(box.Max.X, box.Max.Y, box.Min.Z),
                new XbimPoint3D(box.Min.X, box.Min.Y, box.Max.Z),
                new XbimPoint3D(box.Max.X, box.Min.Y, box.Max.Z),
                new XbimPoint3D(box.Min.X, box.Max.Y, box.Max.Z),
                new XbimPoint3D(box.Max.X, box.Max.Y, box.Max.Z)
            };

            foreach (var corner in corners)
            {
                var world = transform.Transform(corner);
                minX = Math.Min(minX, world.X);
                minY = Math.Min(minY, world.Y);
                minZ = Math.Min(minZ, world.Z);
                maxX = Math.Max(maxX, world.X);
                maxY = Math.Max(maxY, world.Y);
                maxZ = Math.Max(maxZ, world.Z);
            }
        }

        return minX == double.MaxValue
            ? null
            : new GridBounds(
                ToMeters(minX), ToMeters(minY), ToMeters(minZ),
                ToMeters(maxX), ToMeters(maxY), ToMeters(maxZ));
    }

    private GridPoint ReadPlacementOffset(IIfcObjectPlacement? objectPlacement)
    {
        double x = 0;
        double y = 0;
        double z = 0;
        object? placement = objectPlacement;
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);

        while (placement != null && visited.Add(placement))
        {
            var relativePlacement = placement.GetType().GetProperty("RelativePlacement")?.GetValue(placement);
            var location = relativePlacement?.GetType().GetProperty("Location")?.GetValue(relativePlacement);
            var coordinates = location?.GetType().GetProperty("Coordinates")?.GetValue(location);
            var values = ReadCoordinates(coordinates).ToList();
            if (values.Count > 0) x += values[0];
            if (values.Count > 1) y += values[1];
            if (values.Count > 2) z += values[2];
            placement = placement.GetType().GetProperty("PlacementRelTo")?.GetValue(placement);
        }

        return new GridPoint(ToMeters(x), ToMeters(y), ToMeters(z));
    }

    private static IEnumerable<double> ReadCoordinates(object? coordinates)
    {
        if (coordinates is not IEnumerable values)
            yield break;

        foreach (var value in values)
        {
            if (double.TryParse(
                    value?.ToString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsed))
            {
                yield return parsed;
            }
        }
    }

    private double ToMeters(double value) => value * _lengthToMetersFactor;

    private static string NormalizeToken(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    private static string Quantize(double value) =>
        Math.Round(value, 3, MidpointRounding.AwayFromZero)
            .ToString("0.000", CultureInfo.InvariantCulture);

    private sealed record GridCandidate(
        string GlobalId,
        string PlanSignature,
        double MinZ,
        double PlacementZ);

    private sealed record GridPoint(double X, double Y, double Z);

    private sealed record GridBounds(
        double MinX,
        double MinY,
        double MinZ,
        double MaxX,
        double MaxY,
        double MaxZ);
}
