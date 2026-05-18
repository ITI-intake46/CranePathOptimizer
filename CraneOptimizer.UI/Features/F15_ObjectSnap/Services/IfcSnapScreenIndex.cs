using CraneOptimizer.UI.Features.F15_ObjectSnap.Models;
using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Services
{
    internal sealed class IfcSnapScreenIndex
    {
        private const double CellSize = 64.0;

        private readonly Dictionary<(int X, int Y), List<SnapPoint>> _endpoints = new();
        private readonly Dictionary<(int X, int Y), List<SnapPoint>> _midpoints = new();
        private readonly Dictionary<(int X, int Y), List<SnapSegment>> _segments = new();

        private IfcSnapScreenIndex(string key)
            => Key = key;

        public string Key { get; }

        public static IfcSnapScreenIndex Build(
            HelixViewport3D viewport,
            IReadOnlyDictionary<string, IfcSnapGeometry> geometryByElementId,
            int geometryVersion)
        {
            var index = new IfcSnapScreenIndex(BuildKey(viewport, geometryVersion));

            foreach (var geometry in geometryByElementId.Values)
            {
                foreach (var vertex in geometry.Vertices)
                {
                    if (OsnapProjection.TryProject(viewport, vertex, out var screen))
                        AddPoint(index._endpoints, new SnapPoint(geometry.ElementId, vertex, screen));
                }

                foreach (var (a, b) in geometry.Segments)
                {
                    if (!OsnapProjection.TryProject(viewport, a, out var sa) ||
                        !OsnapProjection.TryProject(viewport, b, out var sb))
                    {
                        continue;
                    }

                    var segment = new SnapSegment(geometry.ElementId, a, b, sa, sb);
                    AddSegment(index._segments, segment);

                    var midpoint = new Point3D((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0, (a.Z + b.Z) / 2.0);
                    var midScreen = new Point((sa.X + sb.X) / 2.0, (sa.Y + sb.Y) / 2.0);
                    AddPoint(index._midpoints, new SnapPoint(geometry.ElementId, midpoint, midScreen));
                }
            }

            return index;
        }

        public static string BuildKey(HelixViewport3D viewport, int geometryVersion)
        {
            static string F(double value) => value.ToString("F2", CultureInfo.InvariantCulture);

            double width = viewport.Viewport.ActualWidth > 0 ? viewport.Viewport.ActualWidth : viewport.ActualWidth;
            double height = viewport.Viewport.ActualHeight > 0 ? viewport.Viewport.ActualHeight : viewport.ActualHeight;

            return viewport.Camera switch
            {
                PerspectiveCamera c =>
                    string.Join("|",
                        geometryVersion,
                        "P",
                        F(width), F(height),
                        F(c.Position.X), F(c.Position.Y), F(c.Position.Z),
                        F(c.LookDirection.X), F(c.LookDirection.Y), F(c.LookDirection.Z),
                        F(c.UpDirection.X), F(c.UpDirection.Y), F(c.UpDirection.Z),
                        F(c.FieldOfView)),

                OrthographicCamera c =>
                    string.Join("|",
                        geometryVersion,
                        "O",
                        F(width), F(height),
                        F(c.Position.X), F(c.Position.Y), F(c.Position.Z),
                        F(c.LookDirection.X), F(c.LookDirection.Y), F(c.LookDirection.Z),
                        F(c.UpDirection.X), F(c.UpDirection.Y), F(c.UpDirection.Z),
                        F(c.Width)),

                _ => $"{geometryVersion}|unknown|{F(width)}|{F(height)}"
            };
        }

        public IEnumerable<SnapPoint> QueryEndpoints(Point cursor, double radius)
            => QueryPoints(_endpoints, cursor, radius);

        public IEnumerable<SnapPoint> QueryMidpoints(Point cursor, double radius)
            => QueryPoints(_midpoints, cursor, radius);

        public IEnumerable<SnapSegment> QuerySegments(Point cursor, double radius)
            => QueryCells(_segments, cursor, radius)
                .DistinctBy(segment => segment.Id)
                .Where(segment => DistanceToSegment(cursor, segment.ScreenA, segment.ScreenB) <= radius);

        private static IEnumerable<SnapPoint> QueryPoints(
            Dictionary<(int X, int Y), List<SnapPoint>> grid,
            Point cursor,
            double radius)
            => QueryCells(grid, cursor, radius)
                .Where(point => SnapAccumulator.PixelDistance(cursor, point.Screen) <= radius);

        private static IEnumerable<T> QueryCells<T>(
            Dictionary<(int X, int Y), List<T>> grid,
            Point cursor,
            double radius)
        {
            int minX = Cell(cursor.X - radius);
            int maxX = Cell(cursor.X + radius);
            int minY = Cell(cursor.Y - radius);
            int maxY = Cell(cursor.Y + radius);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (!grid.TryGetValue((x, y), out var items))
                        continue;

                    foreach (var item in items)
                        yield return item;
                }
            }
        }

        private static void AddPoint(Dictionary<(int X, int Y), List<SnapPoint>> grid, SnapPoint point)
            => AddToCell(grid, Cell(point.Screen.X), Cell(point.Screen.Y), point);

        private static void AddSegment(Dictionary<(int X, int Y), List<SnapSegment>> grid, SnapSegment segment)
        {
            int minX = Cell(Math.Min(segment.ScreenA.X, segment.ScreenB.X));
            int maxX = Cell(Math.Max(segment.ScreenA.X, segment.ScreenB.X));
            int minY = Cell(Math.Min(segment.ScreenA.Y, segment.ScreenB.Y));
            int maxY = Cell(Math.Max(segment.ScreenA.Y, segment.ScreenB.Y));

            const int maxCellsPerAxis = 40;
            if (maxX - minX > maxCellsPerAxis || maxY - minY > maxCellsPerAxis)
            {
                AddToCell(grid, Cell((segment.ScreenA.X + segment.ScreenB.X) / 2.0), Cell((segment.ScreenA.Y + segment.ScreenB.Y) / 2.0), segment);
                return;
            }

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                    AddToCell(grid, x, y, segment);
            }
        }

        private static void AddToCell<T>(Dictionary<(int X, int Y), List<T>> grid, int x, int y, T item)
        {
            var key = (x, y);
            if (!grid.TryGetValue(key, out var items))
            {
                items = new List<T>();
                grid[key] = items;
            }

            items.Add(item);
        }

        private static int Cell(double value)
            => (int)Math.Floor(value / CellSize);

        public static double DistanceToSegment(Point p, Point a, Point b)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            double lenSq = dx * dx + dy * dy;
            if (lenSq < 1e-6)
                return SnapAccumulator.PixelDistance(p, a);

            double t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq, 0, 1);
            var closest = new Point(a.X + t * dx, a.Y + t * dy);
            return SnapAccumulator.PixelDistance(p, closest);
        }

        public readonly record struct SnapPoint(string ElementId, Point3D World, Point Screen);

        public readonly record struct SnapSegment(
            string ElementId,
            Point3D WorldA,
            Point3D WorldB,
            Point ScreenA,
            Point ScreenB)
        {
            public string Id => $"{ElementId}:{WorldA.X:F2},{WorldA.Y:F2},{WorldA.Z:F2}>{WorldB.X:F2},{WorldB.Y:F2},{WorldB.Z:F2}";
        }
    }
}
