using HelixToolkit.Wpf;
using netDxf;
using netDxf.Header;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using System.Windows.Media;

namespace CraneOptimizer.UI.Features.F11_DXF.Views
{
    public class DxfUploader
    {
        private readonly HelixViewport3D _viewport;
        private readonly List<ModelVisual3D> _dxfVisuals = new();

        // Large DXF world coordinates (e.g. survey coordinates) can make WPF 3D projection
        // numerically unstable. We optionally rebase DXF geometry near origin for rendering
        // and snapping consistency (does not affect IFC/zones).
        private Matrix3D _dxfRebase = Matrix3D.Identity;

        public List<Point3D> SnapPoints { get; private set; } = new();
        /// <summary>Clean A→B segment pairs for edge-based snaps (midpoint / nearest / perpendicular).</summary>
        public List<(Point3D A, Point3D B)> SnapSegments { get; private set; } = new();
        /// <summary>Arc / circle center points for Center snap.</summary>
        public List<Point3D> SnapCenters { get; private set; } = new();
        /// <summary>Quadrant points (0°, 90°, 180°, 270°) on arcs / circles.</summary>
        public List<Point3D> SnapQuadrants { get; private set; } = new();
        public bool IsLoaded { get; private set; }
        public string? LoadedFilePath { get; private set; }
        public bool IsVisible { get; private set; } = true;

        public string LastLoadDiagnostic { get; private set; } = "";

        /// <summary>Fired whenever snap data is repopulated (after Load, ReApplyTransform, or Clear).</summary>
        public event Action? SnapDataChanged;

        private double _opacity = 0.7;
        public double Opacity
        {
            get => _opacity;
            set { _opacity = Math.Clamp(value, 0.1, 1.0); RefreshOpacity(); }
        }

        public Matrix3D AlignmentTransform { get; set; } = Matrix3D.Identity;

        public DxfUploader(HelixViewport3D viewport)
        {
            _viewport = viewport;
        }

        // ── LoadDxf — returns true on success, never throws ───────────────────

        public bool LoadDxf(string filePath)
        {
            LastLoadDiagnostic = "";
            try
            {
                DxfVersion version;
                try
                {
                    version = DxfDocument.CheckDxfFileVersion(filePath);
                }
                catch (Exception ex)
                {
                    LastLoadDiagnostic = $"Cannot read DXF version: {ex.Message}";
                    return false;
                }

                Log($"DXF version detected: {version}");

                if (version < DxfVersion.AutoCad2000)
                {
                    LastLoadDiagnostic =
                        $"DXF version too old: {version}\n" +
                        "netDxf requires AutoCad2000 or higher.\n" +
                        "Save your file as AutoCAD 2010 DXF and try again.";
                    Log(LastLoadDiagnostic);
                    return false;
                }

                DxfDocument? dxf = null;
                try
                {
                    dxf = DxfDocument.Load(filePath);
                }
                catch (Exception ex)
                {
                    LastLoadDiagnostic = $"DxfDocument.Load threw: {ex.GetType().Name}: {ex.Message}";
                    Log(LastLoadDiagnostic);
                    return false;
                }

                if (dxf == null)
                {
                    LastLoadDiagnostic = "DxfDocument.Load returned null — file may be corrupt.";
                    Log(LastLoadDiagnostic);
                    return false;
                }

                Clear();
                LoadedFilePath = filePath;
                IsLoaded = true;

                int pointCount = RenderDxf(dxf);
                Log($"Render done. Points: {pointCount}");
                RaiseSnapDataChanged();
                return true;
            }
            catch (Exception ex)
            {
                LastLoadDiagnostic = $"Unexpected: {ex.GetType().Name}: {ex.Message}";
                Log(LastLoadDiagnostic);
                IsLoaded = false;
                return false;
            }
        }

        // ── RenderDxf ─────────────────────────────────────────────────────────

        private int RenderDxf(DxfDocument dxf)
        {
            var pts = new Point3DCollection();
            SnapPoints.Clear();
            SnapSegments.Clear();
            SnapCenters.Clear();
            SnapQuadrants.Clear();

            // Reset rebase each render (will be recomputed after we know bounds).
            _dxfRebase = Matrix3D.Identity;

            int lineCount = 0, polyCount = 0, circleCount = 0, arcCount = 0, splineCount = 0;

            // Lines
            try
            {
                foreach (var line in dxf.Entities.Lines)
                {
                    try
                    {
                        var a = TransformPoint(line.StartPoint.X, line.StartPoint.Y);
                        var b = TransformPoint(line.EndPoint.X, line.EndPoint.Y);
                        pts.Add(a); pts.Add(b);
                        SnapPoints.Add(a); SnapPoints.Add(b);
                        SnapPoints.Add(new Point3D((a.X + b.X) / 2, (a.Y + b.Y) / 2, 0));
                        SnapSegments.Add((a, b));   // clean A,B pair for edge snaps
                        lineCount++;
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Log($"Lines collection error: {ex.Message}"); }

            // Polylines2D
            try
            {
                foreach (var poly in dxf.Entities.Polylines2D)
                {
                    try
                    {
                        var verts = new List<Point3D>();
                        foreach (var v in poly.Vertexes)
                            verts.Add(TransformPoint(v.Position.X, v.Position.Y));

                        if (verts.Count < 2) continue;

                        int count = poly.IsClosed ? verts.Count : verts.Count - 1;
                        for (int i = 0; i < count; i++)
                        {
                            pts.Add(verts[i]);
                            pts.Add(verts[(i + 1) % verts.Count]);
                            SnapSegments.Add((verts[i], verts[(i + 1) % verts.Count]));
                        }
                        SnapPoints.AddRange(verts);
                        polyCount++;
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Log($"Polylines2D collection error: {ex.Message}"); }

            // Circles
            try
            {
                foreach (var circle in dxf.Entities.Circles)
                {
                    try
                    {
                        var center = TransformPoint(circle.Center.X, circle.Center.Y);
                        double r = circle.Radius;
                        const int seg = 36;
                        for (int i = 0; i < seg; i++)
                        {
                            double a1 = i * Math.PI * 2 / seg;
                            double a2 = (i + 1) * Math.PI * 2 / seg;
                            pts.Add(new Point3D(center.X + r * Math.Cos(a1), center.Y + r * Math.Sin(a1), 0.002));
                            pts.Add(new Point3D(center.X + r * Math.Cos(a2), center.Y + r * Math.Sin(a2), 0.002));
                        }
                        SnapPoints.Add(center);
                        SnapCenters.Add(center);

                        // Quadrant points (0°, 90°, 180°, 270°)
                        SnapQuadrants.Add(new Point3D(center.X + r, center.Y,     0.002));
                        SnapQuadrants.Add(new Point3D(center.X,     center.Y + r, 0.002));
                        SnapQuadrants.Add(new Point3D(center.X - r, center.Y,     0.002));
                        SnapQuadrants.Add(new Point3D(center.X,     center.Y - r, 0.002));

                        // Add circle segments for edge-based snaps
                        const int cSeg = 36;
                        for (int i = 0; i < cSeg; i++)
                        {
                            double a1 = i * Math.PI * 2 / cSeg;
                            double a2 = (i + 1) * Math.PI * 2 / cSeg;
                            var p1 = new Point3D(center.X + r * Math.Cos(a1), center.Y + r * Math.Sin(a1), 0.002);
                            var p2 = new Point3D(center.X + r * Math.Cos(a2), center.Y + r * Math.Sin(a2), 0.002);
                            SnapSegments.Add((p1, p2));
                        }
                        circleCount++;
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Log($"Circles collection error: {ex.Message}"); }

            // Arcs
            try
            {
                foreach (var arc in dxf.Entities.Arcs)
                {
                    try
                    {
                        var center = TransformPoint(arc.Center.X, arc.Center.Y);
                        double r = arc.Radius;
                        double startDeg = arc.StartAngle;
                        double endDeg = arc.EndAngle;
                        if (endDeg <= startDeg) endDeg += 360;
                        double span = endDeg - startDeg;
                        int seg = Math.Max(3, (int)(span / 5));
                        for (int i = 0; i < seg; i++)
                        {
                            double a1 = (startDeg + i * span / seg) * Math.PI / 180;
                            double a2 = (startDeg + (i + 1) * span / seg) * Math.PI / 180;
                            pts.Add(new Point3D(center.X + r * Math.Cos(a1), center.Y + r * Math.Sin(a1), 0.002));
                            pts.Add(new Point3D(center.X + r * Math.Cos(a2), center.Y + r * Math.Sin(a2), 0.002));
                        }
                        SnapPoints.Add(center);
                        SnapCenters.Add(center);

                        // Endpoints on the arc
                        double startRad = startDeg * Math.PI / 180;
                        double endRad   = (startDeg + span) * Math.PI / 180;
                        SnapPoints.Add(new Point3D(center.X + r * Math.Cos(startRad), center.Y + r * Math.Sin(startRad), 0.002));
                        SnapPoints.Add(new Point3D(center.X + r * Math.Cos(endRad),   center.Y + r * Math.Sin(endRad),   0.002));

                        // Quadrant points that fall within the arc span
                        for (int qi = 0; qi < 4; qi++)
                        {
                            double qDeg = qi * 90.0;
                            double qNorm = qDeg >= startDeg ? qDeg - startDeg : qDeg + 360 - startDeg;
                            if (qNorm >= 0 && qNorm <= span)
                            {
                                double qRad = qDeg * Math.PI / 180;
                                SnapQuadrants.Add(new Point3D(center.X + r * Math.Cos(qRad), center.Y + r * Math.Sin(qRad), 0.002));
                            }
                        }

                        // Add arc segments for edge-based snaps
                        for (int i = 0; i < seg; i++)
                        {
                            double a1 = (startDeg + i * span / seg) * Math.PI / 180;
                            double a2 = (startDeg + (i + 1) * span / seg) * Math.PI / 180;
                            var p1 = new Point3D(center.X + r * Math.Cos(a1), center.Y + r * Math.Sin(a1), 0.002);
                            var p2 = new Point3D(center.X + r * Math.Cos(a2), center.Y + r * Math.Sin(a2), 0.002);
                            SnapSegments.Add((p1, p2));
                        }
                        arcCount++;
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Log($"Arcs collection error: {ex.Message}"); }

            // Splines
            try
            {
                foreach (var spline in dxf.Entities.Splines)
                {
                    try
                    {
                        var ctrlPts = new List<Point3D>();
                        foreach (var cp in spline.ControlPoints)
                        {
                            double cx = 0, cy = 0;
                            bool ok = false;

                            if (!ok)
                            {
                                try { var pos = ((dynamic)cp).Position; cx = (double)pos.X; cy = (double)pos.Y; ok = true; }
                                catch { }
                            }
                            if (!ok)
                            {
                                try { cx = (double)((dynamic)cp).X; cy = (double)((dynamic)cp).Y; ok = true; }
                                catch { }
                            }

                            if (ok) ctrlPts.Add(TransformPoint(cx, cy));
                        }

                        if (ctrlPts.Count < 2) continue;
                        for (int i = 0; i < ctrlPts.Count - 1; i++)
                        {
                            pts.Add(ctrlPts[i]);
                            pts.Add(ctrlPts[i + 1]);
                        }
                        SnapPoints.AddRange(ctrlPts);
                        splineCount++;
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Log($"Splines collection error: {ex.Message}"); }

            Log($"Entities — Lines:{lineCount} Polys:{polyCount} Circles:{circleCount} Arcs:{arcCount} Splines:{splineCount}");

            // If the DXF is very far from origin, rebase all snap points and visuals closer
            // to improve projection precision and OSnap behavior at far zoom.
            // Thresholds are conservative; only engage for large coordinates.
            if (SnapPoints.Count > 0)
            {
                try
                {
                    double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
                    double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
                    foreach (var p in SnapPoints)
                    {
                        if (p.X < minX) minX = p.X;
                        if (p.Y < minY) minY = p.Y;
                        if (p.X > maxX) maxX = p.X;
                        if (p.Y > maxY) maxY = p.Y;
                    }

                    double cx = (minX + maxX) / 2.0;
                    double cy = (minY + maxY) / 2.0;
                    double maxAbs = Math.Max(Math.Abs(cx), Math.Abs(cy));

                    if (maxAbs > 10_000) // engage only for "big-world" drawings
                    {
                        _dxfRebase = Matrix3D.Identity;
                        _dxfRebase.Translate(new Vector3D(-cx, -cy, 0));

                        // Apply to all snap data
                        for (int i = 0; i < SnapPoints.Count; i++)
                            SnapPoints[i] = _dxfRebase.Transform(SnapPoints[i]);
                        for (int i = 0; i < SnapCenters.Count; i++)
                            SnapCenters[i] = _dxfRebase.Transform(SnapCenters[i]);
                        for (int i = 0; i < SnapQuadrants.Count; i++)
                            SnapQuadrants[i] = _dxfRebase.Transform(SnapQuadrants[i]);
                        for (int i = 0; i < SnapSegments.Count; i++)
                            SnapSegments[i] = (_dxfRebase.Transform(SnapSegments[i].A), _dxfRebase.Transform(SnapSegments[i].B));

                        // Apply to rendered line collection too
                        for (int i = 0; i < pts.Count; i++)
                            pts[i] = _dxfRebase.Transform(pts[i]);

                        Log($"Rebased DXF near origin. Center=({cx:F2},{cy:F2})");
                    }
                }
                catch { }
            }

            if (pts.Count > 0)
            {
                byte alpha = (byte)(_opacity * 255);
                var dxfLines = new LinesVisual3D
                {
                    Color = Color.FromArgb(alpha, 160, 160, 160),
                    Thickness = 1.0,
                    Points = pts
                };
                _viewport.Children.Add(dxfLines);
                _dxfVisuals.Add(dxfLines);
            }

            return pts.Count;
        }

        private void RaiseSnapDataChanged() => SnapDataChanged?.Invoke();

        // ── Helpers ───────────────────────────────────────────────────────────

        private Point3D TransformPoint(double x, double y)
        {
            var pt = new Point3D(x, y, 0.002);
            return AlignmentTransform.Transform(pt);
        }

        public void ToggleVisibility()
        {
            IsVisible = !IsVisible;
            foreach (var v in _dxfVisuals)
            {
                if (IsVisible) { if (!_viewport.Children.Contains(v)) _viewport.Children.Add(v); }
                else { _viewport.Children.Remove(v); }
            }
        }

        private void RefreshOpacity()
        {
            if (!IsLoaded || string.IsNullOrEmpty(LoadedFilePath)) return;
            LoadDxf(LoadedFilePath);
        }

        public void ReApplyTransform()
        {
            if (!IsLoaded || string.IsNullOrEmpty(LoadedFilePath)) return;
            LoadDxf(LoadedFilePath);
        }

        public void Clear()
        {
            foreach (var v in _dxfVisuals) _viewport.Children.Remove(v);
            _dxfVisuals.Clear();
            SnapPoints.Clear();
            SnapSegments.Clear();
            SnapCenters.Clear();
            SnapQuadrants.Clear();
            IsLoaded = false;
            RaiseSnapDataChanged();
        }

        private static void Log(string msg)
            => System.Diagnostics.Debug.WriteLine($"[DxfUploader] {msg}");
    }
}
