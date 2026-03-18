// FILE: ZoneModule/Drawing/ZoneDrawingHandler.cs  (FIXED)
// FIXES vs uploaded version:
//   1. OptionsBar?.UpdateLive(preview)  →  OptionsBar?.UpdatePointCount(_currentPoints.Count)
//      DrawingOptionsBar has no UpdateLive method.
//   2. $"Draw {zone.ZoneType} zone "{ zone.Name ... }"   (broken interpolation)
//      →  $"Draw {zone.ZoneType} zone \"{zone.Name ?? zone.ZoneType.ToString()}\""
//   3. $"Delete "{ zone.Name ... }"   (broken interpolation)
//      →  $"Delete \"{zone.Name ?? zone.ZoneType.ToString()}\""

using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using WPF_IFC_test1.ZoneModule.Models;
using WPF_IFC_test1.ZoneModule.Rendering;
using MediaColor = System.Windows.Media.Color;
using WPF_IFC_test1.ZoneModule.UI;

namespace WPF_IFC_test1.ZoneModule.Drawing
{
    public enum DrawMode { Select, Drawing, Dimension }
    public enum ShapeTool { Polyline, Rectangle, Circle, Arc }

    public class ZoneDrawingHandler
    {
        // ── Dependencies ──────────────────────────────────────────────────────
        private readonly HelixViewport3D _viewport;
        private readonly ZoneManager _zoneManager;
        private readonly ZoneRenderer _renderer;
        private readonly OsnapSystem _osnap;

        public UndoRedoManager UndoManager { get; set; }
        public DrawingOptionsBar OptionsBar { get; set; }
        public OsnapSystem Osnap => _osnap;

        /// <summary>
        /// BUG 1 FIX: Guard delegate — when this returns true, ZoneDrawingHandler
        /// yields all mouse events to the external tool (ShapeInputController / MeasureTool).
        /// Wired in MainWindow.InitializeDrawingTools after both tools are created.
        /// </summary>
        public Func<bool> IsExternalToolActive { get; set; }

        // ── State ─────────────────────────────────────────────────────────────
        public DrawMode ActiveMode { get; private set; } = DrawMode.Select;
        public ShapeTool ActiveTool { get; private set; } = ShapeTool.Polyline;

        private ZoneType _pendingZoneType = ZoneType.Allowed;
        private bool _isDrawing = false;
        private bool _dialogOpen = false;   // BUG4 FIX: suppress mouse events while modal is open
        private List<Point3D> _currentPoints = new();
        private Point3D? _rectFirstCorner = null;
        private Point3D? _circleCenter = null;
        private Point3D? _dimStart = null;

        // ── Preview visuals ───────────────────────────────────────────────────
        private LinesVisual3D _previewLines;
        private LinesVisual3D _cursorLine;
        private LinesVisual3D _closingRing;  // green ring near start point signals auto-close
        private List<SphereVisual3D> _vertexMarkers = new();
        private Point3D _lastMouseWorld;

        // ── Events ────────────────────────────────────────────────────────────
        public event Action<string> StatusChanged;
        public event Action<double, double> CursorWorldPositionChanged;
        public event Action<DrawMode> ModeChanged;
        public event Action<OsnapType?, Point3D?> OsnapSnapped;
        public event Action<ZoneType, ShapeTool> DrawingStarted;
        public event Action DrawingEnded;
        /// <summary>
        /// Fired when StartDraw() rejects the request (e.g. no site boundary set).
        /// Use to un-toggle ribbon buttons and hide the draw tools panel.
        /// </summary>
        public event Action DrawingRejected;
        public event Action<List<Point3D>> LivePointsChanged;

        // ── Constructor ───────────────────────────────────────────────────────
        public ZoneDrawingHandler(HelixViewport3D viewport,
                                   ZoneManager manager,
                                   ZoneRenderer renderer,
                                   UndoRedoManager undoManager = null)
        {
            _viewport = viewport;
            _zoneManager = manager;
            _renderer = renderer;
            _osnap = new OsnapSystem(manager);
            UndoManager = undoManager ?? new UndoRedoManager();
            AttachEvents();
        }

        // ── Mode & Tool switching ─────────────────────────────────────────────

        public void SetMode(DrawMode mode)
        {
            if (_isDrawing) CancelDraw();
            ActiveMode = mode;
            UpdateCursor();
            ModeChanged?.Invoke(mode);
            StatusChanged?.Invoke($"Mode: {mode}");
        }

        public void SetTool(ShapeTool tool)
        {
            if (_isDrawing) CancelDraw();
            ActiveTool = tool;
            StatusChanged?.Invoke(ToolHint(tool));
        }

        public void SetPendingZoneType(ZoneType type) => _pendingZoneType = type;

        // ── Drawing entry points ──────────────────────────────────────────────

        public void StartDraw(ZoneType zoneType)
        {
            // Site boundary is no longer required before drawing Allowed/Forbidden zones.
            _pendingZoneType = zoneType;
            SetMode(DrawMode.Drawing);
            _isDrawing = true;
            _rectFirstCorner = null;
            _circleCenter = null;
            _currentPoints.Clear();
            ClearVertexMarkers();
            RemovePreviewLines();
            StatusChanged?.Invoke(ToolHint(ActiveTool));
            DrawingStarted?.Invoke(zoneType, ActiveTool);
        }

        // ── Mouse events ──────────────────────────────────────────────────────

        private void AttachEvents()
        {
            _viewport.MouseLeftButtonDown += OnMouseLeftDown;
            _viewport.MouseMove += OnMouseMove;
            _viewport.MouseRightButtonDown += OnMouseRightDown;
        }

        public void Detach()
        {
            _viewport.MouseLeftButtonDown -= OnMouseLeftDown;
            _viewport.MouseMove -= OnMouseMove;
            _viewport.MouseRightButtonDown -= OnMouseRightDown;
        }

        private void OnMouseLeftDown(object sender, MouseButtonEventArgs e)
        {
            // BUG 1 FIX: yield to ShapeInputController / MeasureTool when active
            if (IsExternalToolActive?.Invoke() == true) return;

            var raw = HitGround(e.GetPosition(_viewport));
            if (raw == null) return;
            var snapped = _osnap.Snap(raw.Value, _currentPoints, _viewport);
            var pt = snapped ?? raw.Value;

            if (ActiveMode == DrawMode.Dimension) { HandleDimensionClick(pt); return; }
            if (ActiveMode != DrawMode.Drawing) return;

            switch (ActiveTool)
            {
                case ShapeTool.Polyline: HandlePolylineClick(pt); break;
                case ShapeTool.Rectangle: HandleRectangleClick(pt); break;
                case ShapeTool.Circle: HandleCircleClick(pt); break;
                case ShapeTool.Arc: HandlePolylineClick(pt); break;
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            // BUG 1 FIX: yield to ShapeInputController / MeasureTool when active
            if (IsExternalToolActive?.Invoke() == true) return;
            if (_dialogOpen) return;   // BUG4 FIX: don't update status bar while Finalize Zone dialog is open
            var raw = HitGround(e.GetPosition(_viewport));
            if (raw == null) return;
            var snapped = _osnap.Snap(raw.Value, _currentPoints, _viewport);
            var world = snapped ?? raw.Value;
            _lastMouseWorld = world;

            OsnapSnapped?.Invoke(_osnap.LastSnapType, _osnap.LastSnapPoint);
            CursorWorldPositionChanged?.Invoke(world.X, world.Y);

            if (ActiveMode == DrawMode.Drawing)
            {
                UpdateDrawingCursorLine(world);

                var preview = new List<Point3D>(_currentPoints) { world };
                LivePointsChanged?.Invoke(preview);

                // ── FIX 1: UpdateLive does not exist on DrawingOptionsBar.
                //    The correct public API is UpdatePointCount(int).
                OptionsBar?.UpdatePointCount(_currentPoints.Count);
            }
            else if (ActiveMode == DrawMode.Dimension && _dimStart.HasValue)
                UpdateDimensionCursorLine(world);
        }

        private void OnMouseRightDown(object sender, MouseButtonEventArgs e)
        {
            // BUG 1 FIX: yield to ShapeInputController / MeasureTool when active
            if (IsExternalToolActive?.Invoke() == true) return;

            if (ActiveMode == DrawMode.Drawing && _currentPoints.Count >= 3)
                TryFinalizePolygon();
        }

        // ── POLYLINE ──────────────────────────────────────────────────────────

        private void HandlePolylineClick(Point3D pt)
        {
            if (_currentPoints.Count >= 3)
            {
                if (Distance2D(pt, _currentPoints[0]) < SnapCloseThreshold())
                { TryFinalizePolygon(); return; }
            }
            _currentPoints.Add(pt);
            AddVertexMarker(pt);
            UpdatePreviewLines();
            StatusChanged?.Invoke(
                $"Points: {_currentPoints.Count}" +
                (_currentPoints.Count >= 3 ? " — C to close / right-click" : ""));
        }

        // ── RECTANGLE ─────────────────────────────────────────────────────────

        private void HandleRectangleClick(Point3D pt)
        {
            if (_rectFirstCorner == null)
            {
                _rectFirstCorner = pt;
                AddVertexMarker(pt);
                StatusChanged?.Invoke("Rectangle: click opposite corner");
            }
            else
            {
                var a = _rectFirstCorner.Value;
                var b = pt;
                _currentPoints = new List<Point3D>
                {
                    new(a.X, a.Y, 0), new(b.X, a.Y, 0),
                    new(b.X, b.Y, 0), new(a.X, b.Y, 0)
                };
                _rectFirstCorner = null;
                TryFinalizePolygon();
            }
        }

        // ── CIRCLE ────────────────────────────────────────────────────────────

        private void HandleCircleClick(Point3D pt)
        {
            if (_circleCenter == null)
            {
                _circleCenter = pt;
                AddVertexMarker(pt);
                StatusChanged?.Invoke("Circle: click a point on the radius");
            }
            else
            {
                var center = _circleCenter.Value;
                double radius = Distance2D(center, pt);
                if (radius < 0.1)
                { StatusChanged?.Invoke("⚠ Radius too small."); return; }

                const int segments = 36;
                _currentPoints = new List<Point3D>();
                for (int i = 0; i < segments; i++)
                {
                    double angle = i * Math.PI * 2.0 / segments;
                    _currentPoints.Add(new(center.X + radius * Math.Cos(angle),
                                          center.Y + radius * Math.Sin(angle), 0));
                }
                _circleCenter = null;
                TryFinalizePolygon();
            }
        }

        // ── DIMENSION ─────────────────────────────────────────────────────────

        private void HandleDimensionClick(Point3D pt)
        {
            if (_dimStart == null)
            {
                _dimStart = pt;
                AddVertexMarker(pt);
                StatusChanged?.Invoke("Dimension: click end point");
            }
            else
            {
                var start = _dimStart.Value;
                var end = pt;
                double dist = (end - start).Length;

                var annotation = new DimensionAnnotation
                {
                    Type = DimensionType.Linear,
                    StartPoint = start,
                    EndPoint = end,
                    OffsetDistance = 0.5,
                    Label = $"{dist:F2}m",
                    IsVisible = true
                };
                _zoneManager.Annotations.Add(annotation);
                _renderer.RenderDimension(annotation);

                UndoManager?.Push($"Dimension {dist:F2}m",
                    new UndoAction { Type = UndoType.AddDimension, Annotation = annotation });

                ClearVertexMarkers();
                RemovePreviewLines();
                _dimStart = null;
                StatusChanged?.Invoke($"✅ Dimension placed: {dist:F2}m — click to place another");
            }
        }

        // ── Finalize polygon ──────────────────────────────────────────────────

        public void TryFinalizePolygon()
        {
            if (_currentPoints.Count < 3)
            {
                StatusChanged?.Invoke("⚠ Need at least 3 points to close a zone.");
                return;
            }
            double area = CalculateArea(_currentPoints);
            if (area < 1.0)
            {
                StatusChanged?.Invoke($"⚠ Zone area {area:F2}m² is too small (min 1m²).");
                return;
            }

            var points = new List<Point3D>(_currentPoints);
            EndDrawingMode();
            DrawingEnded?.Invoke();

            // BUG4 FIX: flag that a modal is open so OnMouseMove is suppressed
            _dialogOpen = true;
            var dialog = new ZoneFinalizationDialog(_pendingZoneType, area);
            bool confirmed = dialog.ShowDialog() == true;
            _dialogOpen = false;

            // BUG1 FIX: force the viewport to repaint after the modal closes
            // (HelixViewport3D can go black/stale after a WPF modal dialog)
            _viewport.Dispatcher.BeginInvoke(new Action(() =>
            {
                try { _viewport.InvalidateVisual(); }
                catch { }
            }));

            if (!confirmed) return;

            var zone = new CraneZone
            {
                Vertices = points,
                ZoneType = dialog.SelectedZoneType,
                MinHeight = dialog.MinHeight,
                MaxHeight = dialog.MaxHeight,
                Name = dialog.ZoneName
            };

            try
            {
                if (zone.ZoneType == ZoneType.SiteBoundary)
                    _zoneManager.SetSiteBoundary(points);
                else
                    _zoneManager.AddZone(zone);

                _renderer.RenderZone(zone);

                // ── FIX 2: Original had broken interpolation:
                //    $"Draw {zone.ZoneType} zone "{ zone.Name ... }"
                //    Embedded quotes must be escaped with backslash inside $"..."
                string undoName = zone.ZoneType == ZoneType.SiteBoundary
                    ? "Draw Site Boundary"
                    : $"Draw {zone.ZoneType} zone \"{zone.Name ?? zone.ZoneType.ToString()}\"";

                UndoManager?.Push(undoName,
                    new UndoAction { Type = UndoType.AddZone, Zone = zone });

                StatusChanged?.Invoke($"✅ {zone.DisplayLabel} created.");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"⚠ {ex.Message}");
            }
        }

        // ── Keyboard ──────────────────────────────────────────────────────────

        public void HandleKeyDown(KeyEventArgs e)
        {
            // R8 FIX: use proper if/else if so Ctrl+Z can't fire two branches
            if (ActiveMode == DrawMode.Drawing)
            {
                if (e.Key == Key.C)
                { TryFinalizePolygon(); e.Handled = true; }
                else if (e.Key == Key.Escape)
                { CancelDraw(); e.Handled = true; }
                else if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
                { UndoLastVertex(); e.Handled = true; }
                else if (e.Key == Key.Y && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
                { Redo(); e.Handled = true; }
            }
            else if (ActiveMode == DrawMode.Dimension && e.Key == Key.Escape)
            {
                _dimStart = null;
                ClearVertexMarkers();
                RemovePreviewLines();
                StatusChanged?.Invoke("Dimension cancelled.");
                e.Handled = true;
            }
            else
            {
                // Not drawing — global undo/redo
                if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
                { Undo(); e.Handled = true; }
                else if (e.Key == Key.Y && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
                { Redo(); e.Handled = true; }
                else if (e.Key == Key.Delete && ActiveMode == DrawMode.Select)
                { DeleteSelectedZone(); e.Handled = true; }
            }
        }

        public void CancelDraw()
        {
            EndDrawingMode();
            _rectFirstCorner = null;
            _circleCenter = null;
            _dimStart = null;
            DrawingEnded?.Invoke();
            SetMode(DrawMode.Select);
            StatusChanged?.Invoke("Drawing cancelled.");
        }

        /// <summary>
        /// Clears drawing state WITHOUT firing DrawingEnded.
        /// Call this when switching to an external shape tool (ShapeInputController)
        /// so the Draw Tools panel stays visible.
        /// </summary>
        public void SilentCancelDraw()
        {
            if (!_isDrawing) return;
            EndDrawingMode();
            _rectFirstCorner = null;
            _circleCenter = null;
            _dimStart = null;
            // Intentionally do NOT fire DrawingEnded — panel must stay open
            ActiveMode = DrawMode.Select;
            UpdateCursor();
        }

        public void UndoLastVertex()
        {
            if (_currentPoints.Count == 0) return;
            _currentPoints.RemoveAt(_currentPoints.Count - 1);
            ClearVertexMarkers();
            foreach (var pt in _currentPoints) AddVertexMarker(pt);
            UpdatePreviewLines();
            StatusChanged?.Invoke($"Vertex removed — {_currentPoints.Count} points");
        }

        private void DeleteSelectedZone()
        {
            if (_zoneManager.SelectedZone == null) return;
            var zone = _zoneManager.SelectedZone;
            _renderer.RemoveZone(zone);
            _zoneManager.RemoveZone(zone);

            // ── FIX 3: Original had broken interpolation:
            //    $"Delete "{ zone.Name ... }"
            //    Same fix — escape the inner quotes.
            string undoName = $"Delete \"{zone.Name ?? zone.ZoneType.ToString()}\"";
            UndoManager?.Push(undoName,
                new UndoAction { Type = UndoType.RemoveZone, Zone = zone });

            StatusChanged?.Invoke($"Deleted: {zone.DisplayLabel}");
        }

        // ── Undo / Redo ───────────────────────────────────────────────────────

        public void Undo()
        {
            var actions = UndoManager?.Undo(1);
            if (actions == null) return;
            foreach (var a in actions) ApplyUndo(a);
        }

        public void Redo()
        {
            var actions = UndoManager?.Redo(1);
            if (actions == null) return;
            foreach (var a in actions) ApplyRedo(a);
        }

        internal void ApplyUndo(UndoAction action)
        {
            switch (action.Type)
            {
                case UndoType.AddZone:
                    _renderer.RemoveZone(action.Zone);
                    _zoneManager.RemoveZone(action.Zone);
                    break;
                case UndoType.RemoveZone:
                    _zoneManager.AddZone(action.Zone);
                    _renderer.RenderZone(action.Zone);
                    break;
                case UndoType.AddDimension:
                    _renderer.RemoveDimension(action.Annotation);
                    _zoneManager.Annotations.Remove(action.Annotation);
                    break;
                case UndoType.Custom:
                    action.CustomUndo?.Invoke();
                    break;
            }
            StatusChanged?.Invoke("Undo.");
        }

        internal void ApplyRedo(UndoAction action)
        {
            switch (action.Type)
            {
                case UndoType.AddZone:
                    _zoneManager.AddZone(action.Zone);
                    _renderer.RenderZone(action.Zone);
                    break;
                case UndoType.RemoveZone:
                    _renderer.RemoveZone(action.Zone);
                    _zoneManager.RemoveZone(action.Zone);
                    break;
                case UndoType.AddDimension:
                    _zoneManager.Annotations.Add(action.Annotation);
                    _renderer.RenderDimension(action.Annotation);
                    break;
                case UndoType.Custom:
                    action.CustomRedo?.Invoke();
                    break;
            }
            StatusChanged?.Invoke("Redo.");
        }

        // ── Cursor line helpers ───────────────────────────────────────────────

        private void UpdateDrawingCursorLine(Point3D world)
        {
            if (_cursorLine != null) _viewport.Children.Remove(_cursorLine);

            Point3D? from = null;
            if (ActiveTool == ShapeTool.Rectangle && _rectFirstCorner.HasValue)
                from = _rectFirstCorner.Value;
            else if (ActiveTool == ShapeTool.Circle && _circleCenter.HasValue)
                from = _circleCenter.Value;
            else if (_currentPoints.Count > 0)
                from = _currentPoints[_currentPoints.Count - 1];

            if (from == null) return;

            double segLen = Distance2D(from.Value, world);
            _cursorLine = new LinesVisual3D
            {
                Color = Colors.Cyan,
                Thickness = 1,
                Points = new Point3DCollection { from.Value, world }
            };
            _viewport.Children.Add(_cursorLine);

            if (ActiveTool == ShapeTool.Circle && _circleCenter.HasValue)
                StatusChanged?.Invoke($"Radius: {segLen:F2}m");
            else if (ActiveTool == ShapeTool.Rectangle && _rectFirstCorner.HasValue)
            {
                double w = Math.Abs(world.X - _rectFirstCorner.Value.X);
                double h = Math.Abs(world.Y - _rectFirstCorner.Value.Y);
                StatusChanged?.Invoke($"Width: {w:F2}m  Height: {h:F2}m  Area: {w * h:F1}m²");
            }
            else
            {
                bool nearStart = _currentPoints.Count >= 3 &&
                                 Distance2D(world, _currentPoints[0]) < SnapCloseThreshold();
                UpdateClosingRing(nearStart ? (Point3D?)_currentPoints[0] : null);
                StatusChanged?.Invoke(
                    $"Segment: {segLen:F2}m — Pts: {_currentPoints.Count}" +
                    (_currentPoints.Count >= 3 ? " — C to close" : ""));
            }
        }

        private void UpdateClosingRing(Point3D? center)
        {
            if (_closingRing != null) _viewport.Children.Remove(_closingRing);
            _closingRing = null;
            if (!center.HasValue) return;

            // Draw a small circle ring around the start point
            const int seg = 24;
            double r = SnapCloseThreshold() * 0.6;
            var pts = new Point3DCollection();
            for (int i = 0; i < seg; i++)
            {
                double a1 = i * Math.PI * 2 / seg;
                double a2 = (i + 1) * Math.PI * 2 / seg;
                pts.Add(new Point3D(center.Value.X + r * Math.Cos(a1),
                                    center.Value.Y + r * Math.Sin(a1), 0.01));
                pts.Add(new Point3D(center.Value.X + r * Math.Cos(a2),
                                    center.Value.Y + r * Math.Sin(a2), 0.01));
            }
            _closingRing = new LinesVisual3D { Color = Colors.LimeGreen, Thickness = 2, Points = pts };
            _viewport.Children.Add(_closingRing);
        }

        private void UpdateDimensionCursorLine(Point3D world)
        {
            if (_cursorLine != null) _viewport.Children.Remove(_cursorLine);
            if (!_dimStart.HasValue) return;
            double dist = (_dimStart.Value - world).Length;
            _cursorLine = new LinesVisual3D
            {
                Color = Colors.Yellow,
                Thickness = 1.5,
                Points = new Point3DCollection { _dimStart.Value, world }
            };
            _viewport.Children.Add(_cursorLine);
            StatusChanged?.Invoke($"Dimension: {dist:F2}m — click to place");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private Point3D? HitGround(Point screenPos)
        {
            try
            {
                var ray = _viewport.Viewport.Point2DtoRay3D(screenPos);
                if (ray == null) return null;

                if (Math.Abs(ray.Direction.Z) > 0.0001)
                {
                    double t = -ray.Origin.Z / ray.Direction.Z;
                    if (t > 0)
                        return new Point3D(
                            ray.Origin.X + t * ray.Direction.X,
                            ray.Origin.Y + t * ray.Direction.Y, 0);
                }
                if (Math.Abs(ray.Direction.Y) > 0.0001)
                {
                    double t = -ray.Origin.Y / ray.Direction.Y;
                    if (t > 0)
                        return new Point3D(
                            ray.Origin.X + t * ray.Direction.X, 0,
                            ray.Origin.Z + t * ray.Direction.Z);
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// R3 FIX: Compute a world-space snap radius that corresponds to
        /// approximately 12 screen pixels regardless of zoom level.
        /// Uses camera distance and half-FOV to convert pixels → metres.
        /// </summary>
        private double SnapCloseThreshold()
        {
            const double snapRadiusPx = 12.0;
            try
            {
                if (_viewport.Camera is PerspectiveCamera cam)
                {
                    double dist = cam.LookDirection.Length;
                    if (dist <= 0) return 0.5;
                    double viewH = _viewport.ActualHeight > 0 ? _viewport.ActualHeight : 600;
                    double halfFovRad = cam.FieldOfView * 0.5 * Math.PI / 180.0;
                    // world units visible vertically = 2 * dist * tan(halfFov)
                    double worldH = 2.0 * dist * Math.Tan(halfFovRad);
                    return Math.Max(0.05, snapRadiusPx * worldH / viewH);
                }
                if (_viewport.Camera is OrthographicCamera orth)
                {
                    double viewH = _viewport.ActualHeight > 0 ? _viewport.ActualHeight : 600;
                    return Math.Max(0.05, snapRadiusPx * orth.Width / viewH);
                }
            }
            catch { }
            return 0.5;
        }

        private static double Distance2D(Point3D a, Point3D b)
            => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

        private static double CalculateArea(List<Point3D> pts)
        {
            double area = 0;
            int n = pts.Count;
            for (int i = 0; i < n; i++)
                area += pts[i].X * pts[(i + 1) % n].Y - pts[(i + 1) % n].X * pts[i].Y;
            return Math.Abs(area) / 2.0;
        }

        private string ToolHint(ShapeTool tool) => tool switch
        {
            ShapeTool.Polyline => "Polyline: click vertices — C to close — Esc to cancel",
            ShapeTool.Rectangle => "Rectangle: click first corner",
            ShapeTool.Circle => "Circle: click center point",
            ShapeTool.Arc => "Arc: click vertices — C to close — Esc to cancel",
            _ => "Drawing mode active"
        };

        private void EndDrawingMode()
        {
            _isDrawing = false;
            _currentPoints.Clear();
            ClearVertexMarkers();
            RemovePreviewLines();
        }

        private void AddVertexMarker(Point3D pt)
        {
            var sphere = new SphereVisual3D
            {
                Center = pt,
                Radius = 0.25,
                Material = MaterialHelper.CreateMaterial(Colors.Cyan)
            };
            _viewport.Children.Add(sphere);
            _vertexMarkers.Add(sphere);
        }

        private void ClearVertexMarkers()
        {
            foreach (var m in _vertexMarkers) _viewport.Children.Remove(m);
            _vertexMarkers.Clear();
            if (_closingRing != null) { _viewport.Children.Remove(_closingRing); _closingRing = null; }
        }

        private void UpdatePreviewLines()
        {
            RemovePreviewLines();
            if (_currentPoints.Count < 2) return;
            var pts = new Point3DCollection();
            for (int i = 0; i < _currentPoints.Count - 1; i++)
            { pts.Add(_currentPoints[i]); pts.Add(_currentPoints[i + 1]); }
            _previewLines = new LinesVisual3D
            { Color = Colors.Cyan, Thickness = 2, Points = pts };
            _viewport.Children.Add(_previewLines);
        }

        private void RemovePreviewLines()
        {
            if (_previewLines != null)
            { _viewport.Children.Remove(_previewLines); _previewLines = null; }
            if (_cursorLine != null)
            { _viewport.Children.Remove(_cursorLine); _cursorLine = null; }
        }

        private void UpdateCursor()
        {
            _viewport.Cursor = ActiveMode switch
            {
                DrawMode.Drawing => Cursors.Cross,
                DrawMode.Dimension => Cursors.Pen,
                _ => Cursors.Arrow
            };
        }
    }

    // ── Undo action types ─────────────────────────────────────────────────────

    public enum UndoType { AddZone, RemoveZone, AddDimension, Custom }

    public class UndoAction
    {
        public UndoType Type { get; set; }
        public CraneZone Zone { get; set; }
        public DimensionAnnotation Annotation { get; set; }

        // For global (non-zone) undo actions like crane placement and IFC load
        public Action CustomUndo { get; set; }
        public Action CustomRedo { get; set; }
    }
}