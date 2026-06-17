using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F15_ObjectSnap.Services
{
    internal sealed class DxfSnapScreenIndex
    {
        private const double CellSize = 64.0;

        private readonly Dictionary<(int X, int Y), List<SnapPoint>> _endpoints = new();
        private readonly Dictionary<(int X, int Y), List<SnapPoint>> _midpoints = new();
        private readonly Dictionary<(int X, int Y), List<SnapPoint>> _centers = new();
        private readonly Dictionary<(int X, int Y), List<SnapPoint>> _quadrants = new();
        private readonly Dictionary<(int X, int Y), List<SnapSegment>> _segments = new();

        private DxfSnapScreenIndex(string key)
            => Key = key;

        public string Key { get; }
        public CameraSnapshot? Snapshot { get; private set; }

        public static DxfSnapScreenIndex Build(
            HelixViewport3D viewport,
            IReadOnlyList<Point3D> endpoints,
            IReadOnlyList<(Point3D A, Point3D B)> segments,
            IReadOnlyList<(Point3D A, Point3D B)> midpointSegments,
            IReadOnlyList<Point3D> centers,
            IReadOnlyList<Point3D> quadrants,
            int geometryVersion)
        {
            var index = new DxfSnapScreenIndex(BuildKey(viewport, geometryVersion));
            index.Snapshot = new CameraSnapshot(viewport, geometryVersion);

            foreach (var endpoint in endpoints)
            {
                if (OsnapProjection.TryProject(viewport, endpoint, out var screen))
                    AddPoint(index._endpoints, new SnapPoint(endpoint, screen));
            }

            foreach (var center in centers)
            {
                if (OsnapProjection.TryProject(viewport, center, out var screen))
                    AddPoint(index._centers, new SnapPoint(center, screen));
            }

            foreach (var quadrant in quadrants)
            {
                if (OsnapProjection.TryProject(viewport, quadrant, out var screen))
                    AddPoint(index._quadrants, new SnapPoint(quadrant, screen));
            }

            foreach (var (a, b) in segments)
            {
                if (!OsnapProjection.TryProject(viewport, a, out var sa) ||
                    !OsnapProjection.TryProject(viewport, b, out var sb))
                {
                    continue;
                }

                var segment = new SnapSegment(a, b, sa, sb);
                AddSegment(index._segments, segment);

            }

            foreach (var (a, b) in midpointSegments)
            {
                if (!OsnapProjection.TryProject(viewport, a, out var sa) ||
                    !OsnapProjection.TryProject(viewport, b, out var sb))
                {
                    continue;
                }

                var midpoint = new Point3D((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0, (a.Z + b.Z) / 2.0);
                var midScreen = new Point((sa.X + sb.X) / 2.0, (sa.Y + sb.Y) / 2.0);
                AddPoint(index._midpoints, new SnapPoint(midpoint, midScreen));
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

        public IEnumerable<SnapPoint> QueryCenters(Point cursor, double radius)
            => QueryPoints(_centers, cursor, radius);

        public IEnumerable<SnapPoint> QueryQuadrants(Point cursor, double radius)
            => QueryPoints(_quadrants, cursor, radius);

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
                AddLongSegment(grid, segment);
                return;
            }

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                    AddToCell(grid, x, y, segment);
            }
        }

        private static void AddLongSegment(Dictionary<(int X, int Y), List<SnapSegment>> grid, SnapSegment segment)
        {
            double dx = segment.ScreenB.X - segment.ScreenA.X;
            double dy = segment.ScreenB.Y - segment.ScreenA.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 1)
            {
                AddToCell(grid, Cell(segment.ScreenA.X), Cell(segment.ScreenA.Y), segment);
                return;
            }

            int steps = Math.Min(4096, Math.Max(1, (int)Math.Ceiling(length / CellSize)));
            (int X, int Y)? previousCell = null;
            for (int i = 0; i <= steps; i++)
            {
                double t = i / (double)steps;
                double x = segment.ScreenA.X + dx * t;
                double y = segment.ScreenA.Y + dy * t;
                var cell = (Cell(x), Cell(y));
                if (previousCell == cell)
                    continue;

                AddToCell(grid, cell.Item1, cell.Item2, segment);
                previousCell = cell;
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

        private static double DistanceToSegment(Point p, Point a, Point b)
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

        public readonly record struct SnapPoint(Point3D World, Point Screen);

        public readonly record struct SnapSegment(Point3D WorldA, Point3D WorldB, Point ScreenA, Point ScreenB)
        {
            public string Id => $"{WorldA.X:F2},{WorldA.Y:F2},{WorldA.Z:F2}>{WorldB.X:F2},{WorldB.Y:F2},{WorldB.Z:F2}";
        }
    }
}
