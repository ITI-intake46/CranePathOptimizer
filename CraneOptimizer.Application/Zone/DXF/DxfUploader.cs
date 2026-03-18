using HelixToolkit.Wpf;
using netDxf;
using netDxf.Entities;
using netDxf.Header;
using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace WPF_IFC_test1.ZoneModule.DXF
{
    public class DxfUploader
    {
        private readonly HelixViewport3D _viewport;
        private readonly List<ModelVisual3D> _dxfVisuals = new();

        public List<Point3D> SnapPoints { get; private set; } = new();
        public bool IsLoaded { get; private set; }
        public string LoadedFilePath { get; private set; }
        public bool IsVisible { get; private set; } = true;

        // Diagnostic info from last load — shown to user on failure
        public string LastLoadDiagnostic { get; private set; } = "";

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

        // ─────────────────────────────────────────────────────────────────────
        // LoadDxf — returns true on success, false on failure (never throws)
        // Failure reason is in LastLoadDiagnostic
        // ─────────────────────────────────────────────────────────────────────
        public bool LoadDxf(string filePath)
        {
            LastLoadDiagnostic = "";
            try
            {
                // ── STEP 1: Check version BEFORE loading (official netDxf pattern) ──
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
                        $"netDxf requires AutoCad2000 or higher.\n" +
                        $"Save your file as AutoCAD 2010 DXF and try again.";
                    Log(LastLoadDiagnostic);
                    return false;
                }

                // ── STEP 2: Load ──────────────────────────────────────────────────
                DxfDocument dxf = null;
                try
                {
                    dxf = DxfDocument.Load(filePath);
                }
                catch (Exception ex)
                {
                    // In DEBUG mode netDxf throws instead of returning null
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

        // ─────────────────────────────────────────────────────────────────────
        // RenderDxf — processes every entity type safely, returns point count
        // ─────────────────────────────────────────────────────────────────────
        private int RenderDxf(DxfDocument dxf)
        {
            var pts = new Point3DCollection();
            SnapPoints.Clear();

            int lineCount = 0, polyCount = 0, circleCount = 0,
                arcCount = 0, splineCount = 0;

            // ── Lines ─────────────────────────────────────────────────────────
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
                        lineCount++;
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Log($"Lines collection error: {ex.Message}"); }

            // ── Polylines2D ───────────────────────────────────────────────────
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
                        }
                        SnapPoints.AddRange(verts);
                        polyCount++;
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Log($"Polylines2D collection error: {ex.Message}"); }

            // ── Circles ───────────────────────────────────────────────────────
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
                        circleCount++;
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Log($"Circles collection error: {ex.Message}"); }

            // ── Arcs ──────────────────────────────────────────────────────────
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
                        arcCount++;
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Log($"Arcs collection error: {ex.Message}"); }

            // ── Splines ───────────────────────────────────────────────────────
            // netDxf 2023.11.10: ControlPoints = IReadOnlyList<SplineVertex>
            // SplineVertex.Position = Vector3 → .X / .Y
            // BUT some builds expose IReadOnlyList<Vector3> directly.
            // We try SplineVertex.Position first, then fall back to Vector3 fields.
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

                            // Attempt 1: SplineVertex — has .Position.X / .Y
                            if (!ok)
                            {
                                try
                                {
                                    var pos = ((dynamic)cp).Position;
                                    cx = (double)pos.X;
                                    cy = (double)pos.Y;
                                    ok = true;
                                }
                                catch { }
                            }

                            // Attempt 2: Vector3 — has .X / .Y directly
                            if (!ok)
                            {
                                try
                                {
                                    cx = (double)((dynamic)cp).X;
                                    cy = (double)((dynamic)cp).Y;
                                    ok = true;
                                }
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

            Log($"Entities found — Lines:{lineCount} Polys:{polyCount} Circles:{circleCount} Arcs:{arcCount} Splines:{splineCount}");

            // ── Build visual ──────────────────────────────────────────────────
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

        // ─────────────────────────────────────────────────────────────────────

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
                if (IsVisible)
                {
                    if (!_viewport.Children.Contains(v))
                        _viewport.Children.Add(v);
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
            foreach (var v in _dxfVisuals) _viewport.Children.Remove(v);
            _dxfVisuals.Clear();
            SnapPoints.Clear();
            IsLoaded = false;
        }

        private static void Log(string msg)
            => System.Diagnostics.Debug.WriteLine($"[DxfUploader] {msg}");
    }
}