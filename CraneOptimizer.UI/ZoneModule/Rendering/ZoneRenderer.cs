// FILE: ZoneModule/Rendering/ZoneRenderer.cs
// CHANGES vs original:
//   + RenderDimension(DimensionAnnotation) — renders placed dimension annotations (spec §10.2)
//   + RemoveDimension(DimensionAnnotation)
//   + ClearDimensions()
//   + Dimension line rendered as yellow line + small endpoint spheres + BillboardTextVisual3D label

using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using WPF_IFC_test1.ZoneModule.Models;

namespace WPF_IFC_test1.ZoneModule.Rendering
{
    public class ZoneRenderer
    {
        private readonly HelixViewport3D _viewport;
        private readonly Dictionary<string, List<ModelVisual3D>> _zoneVisuals = new();
        private readonly Dictionary<string, List<ModelVisual3D>> _dimensionVisuals = new();

        public ZoneRenderer(HelixViewport3D viewport)
        {
            _viewport = viewport;
        }

        /// <summary>
        /// When true, all zones render as flat ground-plane polygons (Z=0 fill + outline only).
        /// No vertical walls, no height. Toggle from ribbon or plan view button.
        /// Defaults to true so that zone meshes never occlude IFC building elements.
        /// </summary>
        public bool FlatMode { get; set; } = true;

        // ── Zone rendering ────────────────────────────────────────────────────

        public void RenderZone(CraneZone zone)
        {
            RemoveZone(zone);
            var visuals = new List<ModelVisual3D>();

            // BUG5 FIX: Allowed zones render slightly above Forbidden zones (Z offset)
            // so the green fill is not buried under the red when they overlap.
            double zLift = zone.ZoneType == ZoneType.Allowed ? 0.05 :
                           zone.ZoneType == ZoneType.SiteBoundary ? 0.02 : 0.0;

            // FlatMode: collapse to ground plane — no vertical walls shown
            double zMin = FlatMode ? 0.01 + zLift : zone.MinHeight + zLift;
            double zMax = FlatMode ? 0.01 + zLift : zone.MaxHeight;

            var mesh = BuildPrismMesh(zone.Vertices, zMin, zMax);
            if (mesh != null)
            {
                var fillColor = GetFillColor(zone.ZoneType);
                var fillMat = new DiffuseMaterial(new SolidColorBrush(fillColor));
                var geoModel = new GeometryModel3D { Geometry = mesh, Material = fillMat, BackMaterial = fillMat };
                var fillVisual = new ModelVisual3D { Content = geoModel };
                _viewport.Children.Add(fillVisual);
                visuals.Add(fillVisual);
            }

            var outline = BuildWireframe(zone.Vertices, zMin, zMax, GetOutlineColor(zone.ZoneType));
            if (outline != null)
            {
                _viewport.Children.Add(outline);
                visuals.Add(outline);
            }

            var hatching = BuildHatching(zone.Vertices, zMax + zLift, zone.ZoneType);
            if (hatching != null)
            {
                _viewport.Children.Add(hatching);
                visuals.Add(hatching);
            }

            _zoneVisuals[zone.Id] = visuals;
        }

        public void RemoveZone(CraneZone zone)
        {
            if (!_zoneVisuals.TryGetValue(zone.Id, out var visuals)) return;
            foreach (var v in visuals) _viewport.Children.Remove(v);
            _zoneVisuals.Remove(zone.Id);
        }

        public void UpdateZoneVisibility(CraneZone zone)
        {
            if (!_zoneVisuals.TryGetValue(zone.Id, out var visuals)) return;
            foreach (var v in visuals)
                v.Content = zone.IsVisible ? v.Content : null;
        }

        public void RefreshZone(CraneZone zone) => RenderZone(zone);

        public void Clear()
        {
            foreach (var visuals in _zoneVisuals.Values)
                foreach (var v in visuals)
                    _viewport.Children.Remove(v);
            _zoneVisuals.Clear();
        }

        // ── Dimension annotation rendering (spec §10.2) ───────────────────────

        /// <summary>
        /// Renders a placed dimension annotation as:
        ///   - Yellow line from start to end
        ///   - Small spheres at endpoints
        ///   - BillboardTextVisual3D label at midpoint showing measured distance
        /// </summary>
        public void RenderDimension(DimensionAnnotation annotation)
        {
            RemoveDimension(annotation);
            var visuals = new List<ModelVisual3D>();

            var start = annotation.StartPoint;
            var end = annotation.EndPoint;
            var mid = new Point3D((start.X + end.X) / 2, (start.Y + end.Y) / 2, Math.Max(start.Z, end.Z) + 0.3);
            double offset = annotation.OffsetDistance;

            // Line
            var line = new LinesVisual3D
            {
                Color = Colors.Yellow,
                Thickness = 1.5,
                Points = new Point3DCollection
                {
                    new Point3D(start.X, start.Y, start.Z + 0.01),
                    new Point3D(end.X,   end.Y,   end.Z   + 0.01)
                }
            };
            _viewport.Children.Add(line);
            visuals.Add(line);

            // Endpoint spheres
            foreach (var pt in new[] { start, end })
            {
                var sphere = new SphereVisual3D
                {
                    Center = new Point3D(pt.X, pt.Y, pt.Z + 0.01),
                    Radius = 0.15,
                    Material = MaterialHelper.CreateMaterial(Colors.Yellow)
                };
                _viewport.Children.Add(sphere);
                visuals.Add(sphere);
            }

            // Label
            string labelText = string.IsNullOrEmpty(annotation.Label)
                ? $"{annotation.MeasuredDistance:F2}m"
                : annotation.Label;

            var label = new BillboardTextVisual3D
            {
                Text = labelText,
                Position = mid,
                FontSize = 1.0,
                Foreground = new SolidColorBrush(Colors.Yellow),
                Background = new SolidColorBrush(Color.FromArgb(160, 30, 30, 30))
            };
            _viewport.Children.Add(label);
            visuals.Add(label);

            _dimensionVisuals[annotation.Id] = visuals;
        }

        public void RemoveDimension(DimensionAnnotation annotation)
        {
            if (!_dimensionVisuals.TryGetValue(annotation.Id, out var visuals)) return;
            foreach (var v in visuals) _viewport.Children.Remove(v);
            _dimensionVisuals.Remove(annotation.Id);
        }

        public void ClearDimensions()
        {
            foreach (var visuals in _dimensionVisuals.Values)
                foreach (var v in visuals)
                    _viewport.Children.Remove(v);
            _dimensionVisuals.Clear();
        }

        // ── Mesh builders ─────────────────────────────────────────────────────

        private MeshGeometry3D BuildPrismMesh(List<Point3D> footprint, double zMin, double zMax)
        {
            if (footprint == null || footprint.Count < 3) return null;
            int n = footprint.Count;

            var positions = new Point3DCollection();
            var normals = new Vector3DCollection();
            var indices = new Int32Collection();

            for (int i = 0; i < n; i++) positions.Add(new Point3D(footprint[i].X, footprint[i].Y, zMin));
            for (int i = 0; i < n; i++) positions.Add(new Point3D(footprint[i].X, footprint[i].Y, zMax));
            var botCenter = new Point3D(footprint.Average(p => p.X), footprint.Average(p => p.Y), zMin);
            positions.Add(botCenter);
            int botCenterIdx = positions.Count - 1;
            var topCenter = new Point3D(botCenter.X, botCenter.Y, zMax);
            positions.Add(topCenter);
            int topCenterIdx = positions.Count - 1;

            for (int i = 0; i < n; i++)
            {
                indices.Add(botCenterIdx); indices.Add((i + 1) % n); indices.Add(i);
                for (int k = 0; k < 3; k++) normals.Add(new Vector3D(0, 0, -1));
            }
            for (int i = 0; i < n; i++)
            {
                indices.Add(topCenterIdx); indices.Add(n + i); indices.Add(n + (i + 1) % n);
                for (int k = 0; k < 3; k++) normals.Add(new Vector3D(0, 0, 1));
            }
            for (int i = 0; i < n; i++)
            {
                int b0 = i, b1 = (i + 1) % n, t0 = n + i, t1 = n + (i + 1) % n;
                var edge = footprint[b1] - footprint[b0];
                var norm = Vector3D.CrossProduct(edge, new Vector3D(0, 0, 1));
                norm.Normalize();
                indices.Add(b0); indices.Add(b1); indices.Add(t0);
                indices.Add(b1); indices.Add(t1); indices.Add(t0);
                for (int k = 0; k < 6; k++) normals.Add(norm);
            }

            return new MeshGeometry3D { Positions = positions, Normals = normals, TriangleIndices = indices };
        }

        private LinesVisual3D BuildWireframe(List<Point3D> footprint, double zMin, double zMax, Color color)
        {
            if (footprint == null || footprint.Count < 2) return null;
            int n = footprint.Count;
            var pts = new Point3DCollection();
            for (int i = 0; i < n; i++)
            {
                pts.Add(new Point3D(footprint[i].X, footprint[i].Y, zMin));
                pts.Add(new Point3D(footprint[(i + 1) % n].X, footprint[(i + 1) % n].Y, zMin));
            }
            for (int i = 0; i < n; i++)
            {
                pts.Add(new Point3D(footprint[i].X, footprint[i].Y, zMax));
                pts.Add(new Point3D(footprint[(i + 1) % n].X, footprint[(i + 1) % n].Y, zMax));
            }
            for (int i = 0; i < n; i++)
            {
                pts.Add(new Point3D(footprint[i].X, footprint[i].Y, zMin));
                pts.Add(new Point3D(footprint[i].X, footprint[i].Y, zMax));
            }
            return new LinesVisual3D { Color = color, Thickness = 1.5, Points = pts };
        }

        private LinesVisual3D BuildHatching(List<Point3D> footprint, double z, ZoneType type)
        {
            if (footprint == null || footprint.Count < 3) return null;
            double minX = footprint.Min(p => p.X), maxX = footprint.Max(p => p.X);
            double minY = footprint.Min(p => p.Y), maxY = footprint.Max(p => p.Y);
            var pts = new Point3DCollection();
            double step = type == ZoneType.Allowed ? 2.5 : 2.0;
            double zh = z + 0.05;

            for (double y = minY; y <= maxY; y += step)
            {
                var intersections = new List<double>();
                int n = footprint.Count;
                for (int i = 0; i < n; i++)
                {
                    var a = footprint[i]; var b = footprint[(i + 1) % n];
                    if ((a.Y <= y && b.Y > y) || (b.Y <= y && a.Y > y))
                        intersections.Add(a.X + (y - a.Y) / (b.Y - a.Y) * (b.X - a.X));
                }
                intersections.Sort();
                for (int k = 0; k + 1 < intersections.Count; k += 2)
                {
                    pts.Add(new Point3D(intersections[k], y, zh));
                    pts.Add(new Point3D(intersections[k + 1], y, zh));
                }
            }

            if (pts.Count == 0) return null;
            return new LinesVisual3D
            {
                Color = type == ZoneType.Allowed ? Colors.DarkGreen : Colors.DarkRed,
                Thickness = 0.8,
                Points = pts
            };
        }

        private Color GetFillColor(ZoneType type) => type switch
        {
            // BUG5 FIX: Forbidden uses lower alpha (60) so it doesn't overpower Allowed zones
            // when they overlap. Allowed stays at 110 so it remains visually green even under red.
            ZoneType.Allowed => Color.FromArgb(110, 39, 174, 96),
            ZoneType.Forbidden => Color.FromArgb(60, 231, 76, 60),
            ZoneType.SiteBoundary => Color.FromArgb(50, 0, 122, 204),
            _ => Color.FromArgb(80, 150, 150, 150)
        };

        private Color GetOutlineColor(ZoneType type) => type switch
        {
            ZoneType.Allowed => Colors.LimeGreen,
            ZoneType.Forbidden => Colors.Red,
            ZoneType.SiteBoundary => Colors.DodgerBlue,
            _ => Colors.Gray
        };
    }
}