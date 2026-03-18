// FILE: ZoneModule/Rendering/GridRenderer.cs
// FIXED: OOM crash, label glitch, plan-view grid size bug
//
// ROOT CAUSES FIXED:
//   1. OOM — BillboardTextVisual3D labels allocate WPF render resources per label.
//      At 5000m zoom distance, 6000+ labels → OutOfMemoryException.
//      FIX: Labels are ONLY rendered via overlay canvas (2D TextBlocks). Never as 3D visuals.
//   2. OOM — Minor grid extent = camDist × 3. At 5000m = 15000m × 15000m with 1m spacing
//      = 60,000 line points per axis. FIX: Adaptive minor/major spacing based on camera height.
//      Minor lines suppressed when they'd be < 4px apart on screen.
//   3. Label glitch — labels stacked diagonally because BillboardText has no screen-space
//      deduplication and renders at wrong world positions during PlanView. FIX: removed.
//   4. Plan View Reset → grid in small box — OrthographicCamera.Width=100 fixed at 100m
//      regardless of scene size. FIX: Width is set from AABB on EnterPlanView.

using HelixToolkit.Wpf;
using WPF_IFC_test1.ZoneModule.UI;
using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using WPF_IFC_test1.Utilities;

namespace WPF_IFC_test1.ZoneModule.Rendering
{
    public class GridRenderer
    {
        // ── Dependencies ──────────────────────────────────────────────────────
        private readonly HelixViewport3D _viewport;
        private ViewportOverlayCanvas _overlayCanvas;

        // ── Tracked 3D visuals (ONLY lines — no BillboardText) ───────────────
        private LinesVisual3D _minorVisual;
        private LinesVisual3D _majorVisual;
        private readonly List<LinesVisual3D> _axisVisuals = new();

        // ── State ─────────────────────────────────────────────────────────────
        public bool IsVisible { get; private set; } = true;

        // Last camera values — used to skip redundant refreshes
        private Point3D _lastCamPos;
        private double _lastCamDist = -1;

        // Computed on last refresh
        private double _majorSpacing;
        private double _minorSpacing;
        private double _extent;
        private Point3D _center;

        // ── Rebuild throttle — skip if camera barely moved ────────────────────
        // Position threshold: 5% of extent; distance threshold: 15%
        private double _lastExtent;

        // ── Constructor ───────────────────────────────────────────────────────
        public GridRenderer(HelixViewport3D viewport)
        {
            _viewport = viewport;
            _unitChangedHandler = () => { if (IsVisible) Refresh(); };
            UnitSystem.Changed += _unitChangedHandler;
        }

        private readonly Action _unitChangedHandler;

        /// <summary>Detach from static events. Call when the viewport is being torn down.</summary>
        public void Detach()
        {
            UnitSystem.Changed -= _unitChangedHandler;
            RemoveGrid();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════════════════

        public void AttachOverlay(ViewportOverlayCanvas overlay)
        {
            _overlayCanvas = overlay;
        }

        public void UpdateForCamera(Point3D camPos, double camDist)
        {
            if (!IsVisible) return;

            // Adaptive spacing based on camera distance
            // Use 10m major, 1m minor — but scale up if zoomed very far out
            double rawMajor = ComputeNiceSpacing(camDist * 0.3);
            _majorSpacing = Math.Max(rawMajor, 1.0);
            _minorSpacing = _majorSpacing / 10.0;

            // Coverage: For infinite grid appearance, use very large extent.
            // In ortho mode, camDist = orthoWidth*0.6, so 8x covers full viewport + pan margin.
            // In perspective mode, 5x is sufficient.
            bool isOrtho = _viewport.Camera is System.Windows.Media.Media3D.OrthographicCamera;
            double multiplier = isOrtho ? 8.0 : 5.0;
            double rawExtent = Math.Max(camDist * multiplier, 200.0);
            // Cap: max 300 major lines per axis (OOM protection)
            double maxExtent = _majorSpacing * 150;
            _extent = Math.Min(rawExtent, maxExtent);

            // Snap center to major grid to avoid shifting
            _center = new Point3D(
                Math.Round(camPos.X / _majorSpacing) * _majorSpacing,
                Math.Round(camPos.Y / _majorSpacing) * _majorSpacing,
                0);

            // Skip rebuild if camera barely moved relative to current grid cell
            double posDelta = (camPos - _lastCamPos).Length;
            double distDelta = Math.Abs(camDist - _lastCamDist);
            double threshold = _majorSpacing * 0.5;
            if (_lastCamDist > 0 && posDelta < threshold && distDelta < _lastCamDist * 0.15)
            {
                return;
            }

            _lastCamPos = camPos;
            _lastCamDist = camDist;
            _lastExtent = _extent;

            Refresh();
        }

        public void Show(double spacingHint = 1.0)
        {
            IsVisible = true;
            if (_lastCamDist > 0) Refresh();
        }

        public void Hide()
        {
            IsVisible = false;
            RemoveGrid();
        }

        public void Toggle()
        {
            if (IsVisible) Hide(); else Show();
        }

        public void SetSpacing(double meters) { /* adaptive — ignored */ }

        // ══════════════════════════════════════════════════════════════════════
        //  INTERNAL — rebuild geometry
        // ══════════════════════════════════════════════════════════════════════

        private bool _refreshing = false;

        private void Refresh()
        {
            if (_refreshing) return;
            _refreshing = true;
            try
            {
                RemoveGrid();
                if (_majorSpacing <= 0 || _extent <= 0) return;

                const double z = 0.001;
                double cx = _center.X, cy = _center.Y;
                double x0 = cx - _extent, x1 = cx + _extent;
                double y0 = cy - _extent, y1 = cy + _extent;

                // Only draw minor grid if lines would be at least 4px apart on screen
                double pxPerMinor = EstimateScreenPixelsPerMetre() * _minorSpacing;
                if (pxPerMinor >= 4.0)
                    BuildMinorGrid(x0, x1, y0, y1, z);

                BuildMajorGrid(x0, x1, y0, y1, z);
                BuildAxisLines(x0, x1, y0, y1, z);

                // Grid labels REMOVED per user request — no numbers on grid
            }
            finally
            {
                _refreshing = false;
            }
        }

        private void BuildMinorGrid(double x0, double x1, double y0, double y1, double z)
        {
            var pts = new Point3DCollection();
            double s = _minorSpacing;
            double startX = Math.Floor(x0 / s) * s;
            double startY = Math.Floor(y0 / s) * s;

            // Hard cap: never more than 300 lines per axis in minor grid
            double step = s;
            double span = x1 - x0;
            if (span / step > 300) step = span / 300.0;

            for (double x = startX; x <= x1 + step * 0.5; x += step)
            {
                if (Math.Abs(x % _majorSpacing) < step * 0.1) continue;
                pts.Add(new Point3D(x, y0, z));
                pts.Add(new Point3D(x, y1, z));
            }
            for (double y = startY; y <= y1 + step * 0.5; y += step)
            {
                if (Math.Abs(y % _majorSpacing) < step * 0.1) continue;
                pts.Add(new Point3D(x0, y, z));
                pts.Add(new Point3D(x1, y, z));
            }

            if (pts.Count == 0) return;
            _minorVisual = new LinesVisual3D
            {
                Color = Color.FromArgb(40, 160, 160, 190),
                Thickness = 0.4,
                Points = pts
            };
            _viewport.Children.Add(_minorVisual);
        }

        private void BuildMajorGrid(double x0, double x1, double y0, double y1, double z)
        {
            var pts = new Point3DCollection();
            double s = _majorSpacing;
            double startX = Math.Floor(x0 / s) * s;
            double startY = Math.Floor(y0 / s) * s;

            for (double x = startX; x <= x1 + s * 0.5; x += s)
            {
                if (Math.Abs(x) < s * 0.01) continue;
                pts.Add(new Point3D(x, y0, z + 0.001));
                pts.Add(new Point3D(x, y1, z + 0.001));
            }
            for (double y = startY; y <= y1 + s * 0.5; y += s)
            {
                if (Math.Abs(y) < s * 0.01) continue;
                pts.Add(new Point3D(x0, y, z + 0.001));
                pts.Add(new Point3D(x1, y, z + 0.001));
            }

            if (pts.Count == 0) return;
            _majorVisual = new LinesVisual3D
            {
                Color = Color.FromArgb(85, 195, 195, 215),
                Thickness = 0.7,
                Points = pts
            };
            _viewport.Children.Add(_majorVisual);
        }

        private void BuildAxisLines(double x0, double x1, double y0, double y1, double z)
        {
            var xAxis = new LinesVisual3D
            {
                Color = Color.FromArgb(170, 255, 70, 70),
                Thickness = 1.5,
                Points = new Point3DCollection
                    { new Point3D(x0, 0, z + 0.002), new Point3D(x1, 0, z + 0.002) }
            };
            var yAxis = new LinesVisual3D
            {
                Color = Color.FromArgb(170, 70, 255, 70),
                Thickness = 1.5,
                Points = new Point3DCollection
                    { new Point3D(0, y0, z + 0.002), new Point3D(0, y1, z + 0.002) }
            };
            _viewport.Children.Add(xAxis); _axisVisuals.Add(xAxis);
            _viewport.Children.Add(yAxis); _axisVisuals.Add(yAxis);
        }

        private void BuildOverlayLabels(double x0, double x1, double y0, double y1, double z)
        {
            if (_overlayCanvas == null) return;

            double s = _majorSpacing;

            // Compute pixel gap between labels — skip if < 60px
            double pxPerLabel = EstimateScreenPixelsPerMetre() * s;
            double labelStep = s;
            if (pxPerLabel < 60)
            {
                double mult = Math.Ceiling(60.0 / pxPerLabel);
                labelStep = ComputeNiceSpacing(s * mult);
            }

            // Hard cap: max 20 labels per axis
            double span = Math.Max(x1 - x0, y1 - y0);
            while (span / labelStep > 20)
                labelStep = ComputeNiceSpacing(labelStep * 2);

            double yRow = Math.Clamp(0.0, y0 + s * 0.15, y1 - s * 0.15);
            double xCol = Math.Clamp(0.0, x0 + s * 0.15, x1 - s * 0.15);

            var labelList = new List<(System.Windows.Point, string)>();

            for (double x = Math.Floor(x0 / labelStep) * labelStep; x <= x1 + labelStep * 0.5; x += labelStep)
            {
                if (Math.Abs(x) < labelStep * 0.01) continue;
                var sp = Project3DToScreen(new Point3D(x, yRow, z + 0.01));
                if (sp.HasValue) labelList.Add((sp.Value, UnitSystem.Format(x)));
            }
            for (double y = Math.Floor(y0 / labelStep) * labelStep; y <= y1 + labelStep * 0.5; y += labelStep)
            {
                if (Math.Abs(y) < labelStep * 0.01) continue;
                var sp = Project3DToScreen(new Point3D(xCol, y, z + 0.01));
                if (sp.HasValue) labelList.Add((sp.Value, UnitSystem.Format(y)));
            }
            var originPt = Project3DToScreen(new Point3D(xCol, yRow, z + 0.01));
            if (originPt.HasValue) labelList.Add((originPt.Value, "0"));

            _overlayCanvas.SetGridLabels(labelList);
        }

        private double EstimateScreenPixelsPerMetre()
        {
            // BUG FIX: Detect orthographic camera and use correct formula.
            // In ortho mode, _lastCamDist is actually the ortho width × 0.6 (from UpdateForOrthographicCamera),
            // but the camera itself has the true Width property.
            if (_viewport.Camera is System.Windows.Media.Media3D.OrthographicCamera ortho)
            {
                double viewportW = _viewport.ActualWidth > 0 ? _viewport.ActualWidth : 800;
                double orthoWidth = ortho.Width > 0 ? ortho.Width : 100;
                return viewportW / orthoWidth;
            }

            // Perspective camera formula (unchanged)
            if (_lastCamDist <= 0) return 10;
            double viewportH = _viewport.ActualHeight > 0 ? _viewport.ActualHeight : 600;
            double worldH = _lastCamDist * 0.828; // at 45° FOV
            return viewportH / worldH;
        }

        private System.Windows.Point? Project3DToScreen(Point3D worldPt)
        {
            try
            {
                var sp = Viewport3DHelper.Point3DtoPoint2D(_viewport.Viewport, worldPt);
                if (double.IsNaN(sp.X) || double.IsNaN(sp.Y)) return null;
                if (sp.X < -200 || sp.X > _viewport.ActualWidth + 200) return null;
                if (sp.Y < -200 || sp.Y > _viewport.ActualHeight + 200) return null;
                return sp;
            }
            catch { return null; }
        }

        private void RemoveGrid()
        {
            if (_minorVisual != null) { _viewport.Children.Remove(_minorVisual); _minorVisual = null; }
            if (_majorVisual != null) { _viewport.Children.Remove(_majorVisual); _majorVisual = null; }
            foreach (var v in _axisVisuals) _viewport.Children.Remove(v);
            _axisVisuals.Clear();
            _overlayCanvas?.ClearGridLabels();
        }

        private static double ComputeNiceSpacing(double target)
        {
            if (target <= 0) return 1.0;
            double mag = Math.Pow(10, Math.Floor(Math.Log10(target)));
            double frac = target / mag;
            double nice = frac <= 1 ? 1 : frac <= 2 ? 2 : frac <= 5 ? 5 : 10;
            return Math.Max(nice * mag, 0.001);
        }
    }
}