using HelixToolkit.Wpf;
using netDxf;
using netDxf.Header;
using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Features.F11_DXF.Views
{
    public class DxfUploader
    {
        private readonly HelixViewport3D _viewport;
        private readonly List<ModelVisual3D> _dxfVisuals = new();

        // Large survey coordinates can destabilize WPF 3D projection. Rebase only
        // the rendered overlay/snap data when the drawing is far from the origin.
        private const double FarCoordinateRebaseThreshold = 1_000_000.0;
        private const double PlacementTranslationTolerance = 1e-6;
        private Matrix3D _dxfRebase = Matrix3D.Identity;

        public List<Point3D> SnapPoints { get; private set; } = new();
        public List<(Point3D A, Point3D B)> SnapSegments { get; private set; } = new();
        public List<Point3D> SnapCenters { get; private set; } = new();
        public List<Point3D> SnapQuadrants { get; private set; } = new();

        public bool IsLoaded { get; private set; }
        public string? LoadedFilePath { get; private set; }
        public bool IsVisible { get; private set; } = true;
        public string LastLoadDiagnostic { get; private set; } = "";

        public event Action? SnapDataChanged;

        private double _opacity = 0.7;
        public double Opacity
        {
            get => _opacity;
            set
            {
                double clamped = Math.Clamp(value, 0.1, 1.0);
                if (Math.Abs(_opacity - clamped) < 1e-9) return;
                _opacity = clamped;
                RefreshOpacity();
            }
        }

        public Matrix3D AlignmentTransform { get; set; } = Matrix3D.Identity;

        public DxfUploader(HelixViewport3D viewport)
        {
            _viewport = viewport;
        }

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

                DxfDocument? dxf;
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
                    LastLoadDiagnostic = "DxfDocument.Load returned null - file may be corrupt.";
                    Log(LastLoadDiagnostic);
                    return false;
                }

                ClearVisualsAndSnapData();
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

        private int RenderDxf(DxfDocument dxf)
        {
            var pts = new Point3DCollection();
            _dxfRebase = Matrix3D.Identity;
            int lineCount = 0, polyCount = 0, circleCount = 0, arcCount = 0, splineCount = 0;

            try
            {
                foreach (var line in dxf.Entities.Lines)
                {
                    try
                    {
                        var a = TransformPoint(line.StartPoint.X, line.StartPoint.Y);
                        var b = TransformPoint(line.EndPoint.X, line.EndPoint.Y);
                        AddSegment(pts, a, b);
                        SnapPoints.Add(a);
                        SnapPoints.Add(b);
                        SnapPoints.Add(MidPoint(a, b));
                        lineCount++;
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Log($"Lines collection error: {ex.Message}"); }

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
                            AddSegment(pts, verts[i], verts[(i + 1) % verts.Count]);

                        SnapPoints.AddRange(verts);
                        polyCount++;
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Log($"Polylines2D collection error: {ex.Message}"); }

            try
            {
                foreach (var circle in dxf.Entities.Circles)
                {
                    try
                    {
                        var center = TransformPoint(circle.Center.X, circle.Center.Y);
                        SnapPoints.Add(center);
                        SnapCenters.Add(center);

                        const int seg = 36;
                        for (int i = 0; i < seg; i++)
                        {
                            double a1 = i * Math.PI * 2.0 / seg;
                            double a2 = (i + 1) * Math.PI * 2.0 / seg;
                            AddSegment(
                                pts,
                                TransformPoint(circle.Center.X + circle.Radius * Math.Cos(a1), circle.Center.Y + circle.Radius * Math.Sin(a1)),
                                TransformPoint(circle.Center.X + circle.Radius * Math.Cos(a2), circle.Center.Y + circle.Radius * Math.Sin(a2)));
                        }

                        SnapQuadrants.Add(TransformPoint(circle.Center.X + circle.Radius, circle.Center.Y));
                        SnapQuadrants.Add(TransformPoint(circle.Center.X, circle.Center.Y + circle.Radius));
                        SnapQuadrants.Add(TransformPoint(circle.Center.X - circle.Radius, circle.Center.Y));
                        SnapQuadrants.Add(TransformPoint(circle.Center.X, circle.Center.Y - circle.Radius));
                        circleCount++;
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Log($"Circles collection error: {ex.Message}"); }

            try
            {
                foreach (var arc in dxf.Entities.Arcs)
                {
                    try
                    {
                        double startDeg = arc.StartAngle;
                        double endDeg = arc.EndAngle;
                        if (endDeg <= startDeg) endDeg += 360;

                        double span = endDeg - startDeg;
                        int seg = Math.Max(3, (int)(span / 5));

                        var center = TransformPoint(arc.Center.X, arc.Center.Y);
                        SnapPoints.Add(center);
                        SnapCenters.Add(center);

                        for (int i = 0; i < seg; i++)
                        {
                            double a1 = (startDeg + i * span / seg) * Math.PI / 180.0;
                            double a2 = (startDeg + (i + 1) * span / seg) * Math.PI / 180.0;
                            AddSegment(
                                pts,
                                TransformPoint(arc.Center.X + arc.Radius * Math.Cos(a1), arc.Center.Y + arc.Radius * Math.Sin(a1)),
                                TransformPoint(arc.Center.X + arc.Radius * Math.Cos(a2), arc.Center.Y + arc.Radius * Math.Sin(a2)));
                        }

                        double startRad = startDeg * Math.PI / 180.0;
                        double endRad = (startDeg + span) * Math.PI / 180.0;
                        SnapPoints.Add(TransformPoint(arc.Center.X + arc.Radius * Math.Cos(startRad), arc.Center.Y + arc.Radius * Math.Sin(startRad)));
                        SnapPoints.Add(TransformPoint(arc.Center.X + arc.Radius * Math.Cos(endRad), arc.Center.Y + arc.Radius * Math.Sin(endRad)));

                        for (int qi = 0; qi < 4; qi++)
                        {
                            double qDeg = qi * 90.0;
                            double qNorm = qDeg >= startDeg ? qDeg - startDeg : qDeg + 360 - startDeg;
                            if (qNorm < 0 || qNorm > span) continue;

                            double qRad = qDeg * Math.PI / 180.0;
                            SnapQuadrants.Add(TransformPoint(arc.Center.X + arc.Radius * Math.Cos(qRad), arc.Center.Y + arc.Radius * Math.Sin(qRad)));
                        }

                        arcCount++;
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Log($"Arcs collection error: {ex.Message}"); }

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

                            try { var pos = ((dynamic)cp).Position; cx = (double)pos.X; cy = (double)pos.Y; ok = true; }
                            catch { }

                            if (!ok)
                            {
                                try { cx = (double)((dynamic)cp).X; cy = (double)((dynamic)cp).Y; ok = true; }
                                catch { }
                            }

                            if (ok) ctrlPts.Add(TransformPoint(cx, cy));
                        }

                        if (ctrlPts.Count < 2) continue;
                        for (int i = 0; i < ctrlPts.Count - 1; i++)
                            AddSegment(pts, ctrlPts[i], ctrlPts[i + 1]);

                        SnapPoints.AddRange(ctrlPts);
                        splineCount++;
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Log($"Splines collection error: {ex.Message}"); }

            Log($"Entities - Lines:{lineCount} Polys:{polyCount} Circles:{circleCount} Arcs:{arcCount} Splines:{splineCount}");
            RebaseFarFromOrigin(pts);

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

        private Point3D TransformPoint(double x, double y)
            => AlignmentTransform.Transform(new Point3D(x, y, 0.002));

        private void AddSegment(Point3DCollection points, Point3D a, Point3D b)
        {
            if (!IsFinite(a) || !IsFinite(b)) return;

            points.Add(a);
            points.Add(b);
            SnapSegments.Add((a, b));
        }

        private void RebaseFarFromOrigin(Point3DCollection points)
        {
            if (points.Count == 0) return;

            try
            {
                if (HasUserPlacementTranslation())
                {
                    Log("Rebase skipped because alignment transform already has placement translation.");
                    return;
                }

                double minX = double.PositiveInfinity;
                double minY = double.PositiveInfinity;
                double maxX = double.NegativeInfinity;
                double maxY = double.NegativeInfinity;

                foreach (var p in points)
                {
                    if (!IsFinite(p)) continue;
                    if (p.X < minX) minX = p.X;
                    if (p.Y < minY) minY = p.Y;
                    if (p.X > maxX) maxX = p.X;
                    if (p.Y > maxY) maxY = p.Y;
                }

                if (!double.IsFinite(minX) || !double.IsFinite(minY) ||
                    !double.IsFinite(maxX) || !double.IsFinite(maxY))
                {
                    return;
                }

                double centerX = (minX + maxX) / 2.0;
                double centerY = (minY + maxY) / 2.0;
                if (Math.Max(Math.Abs(centerX), Math.Abs(centerY)) <= FarCoordinateRebaseThreshold)
                    return;

                _dxfRebase = Matrix3D.Identity;
                _dxfRebase.Translate(new Vector3D(-centerX, -centerY, 0));

                for (int i = 0; i < points.Count; i++)
                    points[i] = _dxfRebase.Transform(points[i]);
                for (int i = 0; i < SnapPoints.Count; i++)
                    SnapPoints[i] = _dxfRebase.Transform(SnapPoints[i]);
                for (int i = 0; i < SnapCenters.Count; i++)
                    SnapCenters[i] = _dxfRebase.Transform(SnapCenters[i]);
                for (int i = 0; i < SnapQuadrants.Count; i++)
                    SnapQuadrants[i] = _dxfRebase.Transform(SnapQuadrants[i]);
                for (int i = 0; i < SnapSegments.Count; i++)
                    SnapSegments[i] = (_dxfRebase.Transform(SnapSegments[i].A), _dxfRebase.Transform(SnapSegments[i].B));

                Log($"Rebased DXF near origin. Center=({centerX:F2}, {centerY:F2})");
            }
            catch (Exception ex)
            {
                Log($"Rebase skipped: {ex.Message}");
            }
        }

        private bool HasUserPlacementTranslation()
            => Math.Abs(AlignmentTransform.OffsetX) > PlacementTranslationTolerance ||
               Math.Abs(AlignmentTransform.OffsetY) > PlacementTranslationTolerance ||
               Math.Abs(AlignmentTransform.OffsetZ) > PlacementTranslationTolerance;

        private static bool IsFinite(Point3D point)
            => double.IsFinite(point.X) &&
               double.IsFinite(point.Y) &&
               double.IsFinite(point.Z);

        private static Point3D MidPoint(Point3D a, Point3D b)
            => new((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0, (a.Z + b.Z) / 2.0);

        private void RaiseSnapDataChanged() => SnapDataChanged?.Invoke();

        public void ToggleVisibility()
        {
            IsVisible = !IsVisible;
            foreach (var v in _dxfVisuals)
            {
                if (IsVisible)
                {
                    if (!_viewport.Children.Contains(v)) _viewport.Children.Add(v);
                }
                else
                {
                    _viewport.Children.Remove(v);
                }
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
            ClearVisualsAndSnapData();
            LoadedFilePath = null;
            IsLoaded = false;
            RaiseSnapDataChanged();
        }

        private void ClearVisualsAndSnapData()
        {
            foreach (var v in _dxfVisuals) _viewport.Children.Remove(v);
            _dxfVisuals.Clear();
            SnapPoints.Clear();
            SnapSegments.Clear();
            SnapCenters.Clear();
            SnapQuadrants.Clear();
        }

        private static void Log(string msg)
            => System.Diagnostics.Debug.WriteLine($"[DxfUploader] {msg}");
    }
}
