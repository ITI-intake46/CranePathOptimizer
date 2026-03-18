using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using WPF_IFC_test1.Database.Entities;
using WPF_IFC_test1.Database.Services;
using WPF_IFC_test1.Models;

namespace WPF_IFC_test1.Utilities
{
    /// <summary>
    /// Draws colour-coded capacity rings around a mobile crane in the 3D viewport.
    /// Each ring corresponds to one radius entry in the load chart and is coloured
    /// by the heatmap gradient (red=low, green=high capacity).
    /// </summary>
    public class CraneCapacityRingRenderer
    {
        private readonly System.Windows.Controls.Viewport3D _viewport;
        private readonly MobileCraneParameters _craneParams;
        private readonly List<ModelVisual3D> _rings = new();
        private bool _visible = false;

        private List<CraneLoadChartPointEntity>? _chartPoints;

        public CraneCapacityRingRenderer(
            System.Windows.Controls.Viewport3D viewport,
            MobileCraneParameters craneParams)
        {
            _viewport    = viewport;
            _craneParams = craneParams;
        }

        /// <summary>
        /// Updates the chart data and refreshes ring geometry.
        /// Call this when a new load chart is assigned.
        /// </summary>
        public void SetChartPoints(List<CraneLoadChartPointEntity>? points)
        {
            _chartPoints = points;
            if (_visible) Refresh(_craneParams.BoomAngle);
        }

        /// <summary>
        /// Call this from MobileCraneVisual3D when BoomAngle changes.
        /// </summary>
        public void OnBoomAngleChanged(double boomAngleDeg)
        {
            if (_visible) Refresh(boomAngleDeg);
        }

        public void Show()
        {
            _visible = true;
            Refresh(_craneParams.BoomAngle);
        }

        public void Hide()
        {
            _visible = false;
            ClearRings();
        }

        public bool IsVisible => _visible;

        private void Refresh(double boomAngleDeg)
        {
            ClearRings();
            if (_chartPoints == null || _chartPoints.Count == 0) return;

            // Current boom length from crane parameters
            double boomLen = _craneParams.BoomLength;

            // Get all rated radii at the closest boom length
            var pts = _chartPoints.Where(p => p.CapacityTonne.HasValue).ToList();
            if (pts.Count == 0) return;

            // Find the closest boom length in the chart
            var distinctBooms = pts.Select(p => p.BoomLengthM).Distinct().OrderBy(b => b).ToList();
            double closestBoom = distinctBooms.MinBy(b => Math.Abs(b - boomLen));

            // Get radii and capacities at this boom length
            var radiiAtBoom = pts
                .Where(p => Math.Abs(p.BoomLengthM - closestBoom) < 0.5)
                .OrderBy(p => p.RadiusM)
                .ToList();

            if (radiiAtBoom.Count < 2) return;

            double minCap = radiiAtBoom.Min(p => p.CapacityTonne!.Value);
            double maxCap = radiiAtBoom.Max(p => p.CapacityTonne!.Value);

            double craneX = _craneParams.PositionX;
            double craneY = _craneParams.PositionY;

            foreach (var pt in radiiAtBoom)
            {
                double r = pt.RadiusM;
                double cap = pt.CapacityTonne!.Value;

                Color ringColor = CraneLoadChartLookup.GetHeatmapColor(cap, minCap, maxCap);
                ringColor.A = 200;

                var mat = MaterialHelper.CreateMaterial(ringColor);

                // Ring geometry: thin torus at height 0.3m above ground
                var ringMesh = CreateRingMesh(r, 0.25, 32);
                var ringModel = new GeometryModel3D(ringMesh, mat);

                var visual = new ModelVisual3D
                {
                    Content = ringModel,
                    Transform = new TranslateTransform3D(craneX, craneY, 0.3)
                };

                _rings.Add(visual);
                _viewport.Children.Add(visual);

                // Add capacity label visual at 3 o'clock (East side, X+)
                var labelVisual = CreateCapacityLabel(
                    new Point3D(craneX + r + 0.5, craneY, 0.6),
                    $"{cap:F1}t",
                    ringColor);
                if (labelVisual != null)
                {
                    _rings.Add(labelVisual);
                    _viewport.Children.Add(labelVisual);
                }
            }
        }

        private static MeshGeometry3D CreateRingMesh(double outerRadius, double tubeRadius, int segments)
        {
            var mesh = new MeshGeometry3D();
            int tubeSegments = 8;

            for (int i = 0; i < segments; i++)
            {
                double angle1 = 2 * Math.PI * i / segments;
                double angle2 = 2 * Math.PI * (i + 1) / segments;

                var center1 = new Point3D(outerRadius * Math.Cos(angle1), outerRadius * Math.Sin(angle1), 0);
                var center2 = new Point3D(outerRadius * Math.Cos(angle2), outerRadius * Math.Sin(angle2), 0);

                for (int j = 0; j < tubeSegments; j++)
                {
                    double ta = 2 * Math.PI * j / tubeSegments;
                    double tb = 2 * Math.PI * (j + 1) / tubeSegments;

                    // Local tube cross-section points around center1 and center2
                    var n1 = new Vector3D(Math.Cos(angle1), Math.Sin(angle1), 0);
                    var n2 = new Vector3D(Math.Cos(angle2), Math.Sin(angle2), 0);
                    var up = new Vector3D(0, 0, 1);

                    var p00 = center1 + tubeRadius * (Math.Cos(ta) * n1 + Math.Sin(ta) * up);
                    var p01 = center1 + tubeRadius * (Math.Cos(tb) * n1 + Math.Sin(tb) * up);
                    var p10 = center2 + tubeRadius * (Math.Cos(ta) * n2 + Math.Sin(ta) * up);
                    var p11 = center2 + tubeRadius * (Math.Cos(tb) * n2 + Math.Sin(tb) * up);

                    int idx = mesh.Positions.Count;
                    mesh.Positions.Add(new Point3D(p00.X, p00.Y, p00.Z));
                    mesh.Positions.Add(new Point3D(p01.X, p01.Y, p01.Z));
                    mesh.Positions.Add(new Point3D(p10.X, p10.Y, p10.Z));
                    mesh.Positions.Add(new Point3D(p11.X, p11.Y, p11.Z));

                    mesh.TriangleIndices.Add(idx);
                    mesh.TriangleIndices.Add(idx + 1);
                    mesh.TriangleIndices.Add(idx + 2);
                    mesh.TriangleIndices.Add(idx + 1);
                    mesh.TriangleIndices.Add(idx + 3);
                    mesh.TriangleIndices.Add(idx + 2);
                }
            }
            return mesh;
        }

        private static ModelVisual3D? CreateCapacityLabel(Point3D position, string text, Color color)
        {
            try
            {
                // Use BillboardTextVisual3D from HelixToolkit if available, otherwise a simple sphere marker
                var marker = new BillboardTextVisual3D
                {
                    Text = text,
                    Position = position,
                    Foreground = new SolidColorBrush(color),
                    FontSize = 10
                };
                return marker;
            }
            catch
            {
                return null;
            }
        }

        private void ClearRings()
        {
            foreach (var ring in _rings)
                _viewport.Children.Remove(ring);
            _rings.Clear();
        }
    }
}
