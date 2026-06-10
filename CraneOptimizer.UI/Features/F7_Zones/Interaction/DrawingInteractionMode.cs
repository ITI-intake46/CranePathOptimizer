using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F7_Zones.ViewModels;
using HelixToolkit.Wpf;

namespace CraneOptimizer.UI.Features.F7_Zones.Interaction
{
    public class DrawingInteractionMode : IInteractionMode
    {
        private const double VertexSnapRadiusPixels = 19.0;
        private const double EdgeSnapRadiusPixels = 12.0;

        private static readonly HashSet<string> IgnoredMeasurementLayers =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "Preview",
                "MeasurePreview",
                "MeasureFinal",
                "SnapIndicator"
            };

        private readonly DrawingViewModel _vm;
        private readonly IViewportHitTestService _hitTest;
        private readonly IViewportInteractionService _interactionService;

        public DrawingInteractionMode(
            DrawingViewModel vm,
            IViewportHitTestService hitTest,
            IViewportInteractionService interactionService)
        {
            _vm = vm;
            _hitTest = hitTest;
            _interactionService = interactionService;
        }

        public void OnMouseDown(HelixViewport3D viewport, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                if (_vm.IsMeasuring)
                    return;

                _vm.HandleRightClick();
                e.Handled = true;
                return;
            }

            if (e.ChangedButton != MouseButton.Left)
                return;

            Point screen = e.GetPosition(viewport);
            if (!TryGetInteractionPoint(viewport, screen, out Point3D world))
                return;

            if (_vm.IsDrawing && e.ClickCount > 1)
            {
                _vm.HandleDoubleClick();
                e.Handled = true;
                return;
            }

            _vm.HandleViewportLeftClick(new DomainPoint3D(world.X, world.Y, world.Z));
            e.Handled = true;
        }

        public void OnMouseMove(HelixViewport3D viewport, MouseEventArgs e)
        {
            Point screen = e.GetPosition(viewport);
            if (!TryGetInteractionPoint(viewport, screen, out Point3D world))
            {
                if (_vm.IsMeasuring)
                    _vm.ClearMeasureHoverPoint();
                return;
            }

            _vm.SetHoverPoint(new DomainPoint3D(world.X, world.Y, world.Z));
        }

        public void OnMouseUp(HelixViewport3D viewport, MouseButtonEventArgs e)
        {
        }

        public void OnKeyDown(HelixViewport3D viewport, KeyEventArgs e)
        {
            _vm.HandleKeyDown(e);

            if (!_vm.IsDrawing && !_vm.IsMeasuring)
                _interactionService.PopMode();
        }

        private bool TryGetInteractionPoint(
            HelixViewport3D viewport,
            Point screen,
            out Point3D world)
        {
            return _vm.IsMeasuring
                ? TryResolveMeasurementPoint(viewport, screen, out world)
                : _hitTest.TryGetWorldPoint(viewport, screen, out world);
        }

        private static bool TryResolveMeasurementPoint(
            HelixViewport3D viewport,
            Point screen,
            out Point3D world)
        {
            world = default;

            try
            {
                IList<PointHitResult> hits = viewport.Viewport.FindHits(screen);
                foreach (PointHitResult hit in hits)
                {
                    if (hit.Visual == null || IsIgnoredMeasurementVisual(hit.Visual))
                        continue;

                    GreenAreaZone? zone = FindZone(hit.Visual);
                    if (zone != null && IsFinite(hit.Position))
                    {
                        world = ResolveZonePoint(viewport, screen, zone, hit.Position);
                        return true;
                    }

                    if (IsNonTargetHelperVisual(hit.Visual) ||
                        hit.Mesh == null ||
                        hit.RayHit is not RayMeshGeometry3DHitTestResult meshHit)
                    {
                        continue;
                    }

                    if (TryGetNearbyTriangleVertex(
                            viewport,
                            screen,
                            hit.Visual,
                            hit.Mesh,
                            meshHit,
                            out Point3D vertex))
                    {
                        world = vertex;
                        return true;
                    }

                    if (IsFinite(hit.Position))
                    {
                        world = hit.Position;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Measurement] Point resolving failed: {ex.Message}");
            }

            return false;
        }

        private static Point3D ResolveZonePoint(
            HelixViewport3D viewport,
            Point cursor,
            GreenAreaZone zone,
            Point3D faceHit)
        {
            IReadOnlyList<DomainPoint3D> vertices = zone.Vertices;
            if (TryGetNearbyZoneVertex(viewport, cursor, vertices, out Point3D vertex))
                return vertex;

            if (TryGetNearbyZoneEdgePoint(viewport, cursor, vertices, out Point3D edgePoint))
                return edgePoint;

            return ProjectToZonePlane(faceHit, vertices);
        }

        private static bool TryGetNearbyZoneVertex(
            HelixViewport3D viewport,
            Point cursor,
            IReadOnlyList<DomainPoint3D> vertices,
            out Point3D vertex)
        {
            vertex = default;
            double bestDistanceSquared = VertexSnapRadiusPixels * VertexSnapRadiusPixels;
            bool found = false;

            foreach (DomainPoint3D point in vertices)
            {
                Point3D candidate = ToPoint3D(point);
                if (!TryProjectToViewport(viewport, candidate, out Point projected))
                    continue;

                double distanceSquared = DistanceSquared(cursor, projected);
                if (distanceSquared > bestDistanceSquared)
                    continue;

                bestDistanceSquared = distanceSquared;
                vertex = candidate;
                found = true;
            }

            return found;
        }

        private static bool TryGetNearbyZoneEdgePoint(
            HelixViewport3D viewport,
            Point cursor,
            IReadOnlyList<DomainPoint3D> vertices,
            out Point3D edgePoint)
        {
            edgePoint = default;
            if (vertices.Count < 2)
                return false;

            double bestDistanceSquared = EdgeSnapRadiusPixels * EdgeSnapRadiusPixels;
            bool found = false;

            for (int index = 0; index < vertices.Count; index++)
            {
                Point3D start = ToPoint3D(vertices[index]);
                Point3D end = ToPoint3D(vertices[(index + 1) % vertices.Count]);
                if (!TryProjectToViewport(viewport, start, out Point startScreen) ||
                    !TryProjectToViewport(viewport, end, out Point endScreen))
                {
                    continue;
                }

                double t = ClosestSegmentParameter(cursor, startScreen, endScreen);
                Point closestScreen = new(
                    startScreen.X + (endScreen.X - startScreen.X) * t,
                    startScreen.Y + (endScreen.Y - startScreen.Y) * t);
                double distanceSquared = DistanceSquared(cursor, closestScreen);
                if (distanceSquared > bestDistanceSquared)
                    continue;

                bestDistanceSquared = distanceSquared;
                edgePoint = new Point3D(
                    start.X + (end.X - start.X) * t,
                    start.Y + (end.Y - start.Y) * t,
                    start.Z + (end.Z - start.Z) * t);
                found = true;
            }

            return found;
        }

        private static Point3D ProjectToZonePlane(
            Point3D point,
            IReadOnlyList<DomainPoint3D> vertices)
        {
            if (vertices.Count < 3)
                return point;

            Point3D origin = ToPoint3D(vertices[0]);
            for (int index = 1; index < vertices.Count - 1; index++)
            {
                Vector3D first = ToPoint3D(vertices[index]) - origin;
                Vector3D second = ToPoint3D(vertices[index + 1]) - origin;
                Vector3D normal = Vector3D.CrossProduct(first, second);
                if (normal.LengthSquared < 1e-12)
                    continue;

                Vector3D offset = point - origin;
                double scale = Vector3D.DotProduct(offset, normal) / normal.LengthSquared;
                return point - normal * scale;
            }

            return point;
        }

        private static bool TryGetNearbyTriangleVertex(
            HelixViewport3D viewport,
            Point cursor,
            Visual3D visual,
            MeshGeometry3D mesh,
            RayMeshGeometry3DHitTestResult meshHit,
            out Point3D vertex)
        {
            vertex = default;
            if (mesh.Positions == null || mesh.Positions.Count == 0)
                return false;

            int[] indices =
            {
                meshHit.VertexIndex1,
                meshHit.VertexIndex2,
                meshHit.VertexIndex3
            };

            Matrix3D transform = Visual3DHelper.GetTransform(visual);
            double bestDistanceSquared = VertexSnapRadiusPixels * VertexSnapRadiusPixels;
            bool found = false;

            foreach (int index in indices)
            {
                if (index < 0 || index >= mesh.Positions.Count)
                    continue;

                Point3D candidate = transform.Transform(mesh.Positions[index]);
                if (!IsFinite(candidate) ||
                    !TryProjectToViewport(viewport, candidate, out Point projected))
                {
                    continue;
                }

                double distanceSquared = DistanceSquared(cursor, projected);
                if (distanceSquared > bestDistanceSquared)
                    continue;

                bestDistanceSquared = distanceSquared;
                vertex = candidate;
                found = true;
            }

            return found;
        }

        private static GreenAreaZone? FindZone(Visual3D visual)
        {
            DependencyObject? current = visual;
            while (current != null)
            {
                if (current.GetValue(FrameworkElement.TagProperty) is GreenAreaZone zone)
                    return zone;

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private static bool IsIgnoredMeasurementVisual(Visual3D visual)
        {
            DependencyObject? current = visual;
            while (current != null)
            {
                if (current.GetValue(FrameworkElement.TagProperty) is string layer &&
                    IgnoredMeasurementLayers.Contains(layer))
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private static bool IsNonTargetHelperVisual(Visual3D visual)
        {
            DependencyObject? current = visual;
            while (current != null)
            {
                if (current is GridLinesVisual3D ||
                    current is BillboardTextVisual3D ||
                    current is PointsVisual3D ||
                    current is LinesVisual3D)
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private static bool TryProjectToViewport(
            HelixViewport3D viewport,
            Point3D world,
            out Point screen)
        {
            screen = default;
            try
            {
                Point inner = Viewport3DHelper.Point3DtoPoint2D(viewport.Viewport, world);
                screen = viewport.Viewport.TranslatePoint(inner, viewport);
                return IsFinite(screen);
            }
            catch
            {
                return false;
            }
        }

        private static double ClosestSegmentParameter(Point point, Point start, Point end)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double lengthSquared = dx * dx + dy * dy;
            if (lengthSquared < 1e-12)
                return 0.0;

            double t = ((point.X - start.X) * dx + (point.Y - start.Y) * dy) /
                       lengthSquared;
            return Math.Clamp(t, 0.0, 1.0);
        }

        private static double DistanceSquared(Point first, Point second)
        {
            double dx = first.X - second.X;
            double dy = first.Y - second.Y;
            return dx * dx + dy * dy;
        }

        private static Point3D ToPoint3D(DomainPoint3D point) =>
            new(point.X, point.Y, point.Z);

        private static bool IsFinite(Point3D point) =>
            double.IsFinite(point.X) &&
            double.IsFinite(point.Y) &&
            double.IsFinite(point.Z);

        private static bool IsFinite(Point point) =>
            double.IsFinite(point.X) &&
            double.IsFinite(point.Y);
    }
}
