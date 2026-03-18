using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WPF_IFC_test1.ZoneModule.Models;
using WPF_IFC_test1.ZoneModule.Drawing;

namespace WPF_IFC_test1.ZoneModule.UI
{
    public partial class RevitDrawPanel : UserControl
    {
        // ── Original events (kept) ────────────────────────────────────────
        public event Action<List<Point>, ZoneType> ZoneCompleted;
        public event Action DrawingCancelled;
        public event Action<string> StatusChanged;

        // ── New event expected by ZoneModuleController ────────────────────
        public event Action<OsnapType, bool> OsnapToggled;

        // ── Injected dependency ───────────────────────────────────────────
        private DrawingOptionsBar _optionsBar;
        private ZoneDrawingHandler _handler;

        // ── Active drawing state ──────────────────────────────────────────
        private readonly List<Point> _pts = new List<Point>();
        private bool _isDrawing = false;
        private Point _lastMouse;

        // ── Pan / zoom ────────────────────────────────────────────────────
        private double _scale = 1.0;
        private Point _panOffset = new Point(0, 0);

        private bool _isPanning = false;
        private Point _panStart;

        // ── Grid constants ────────────────────────────────────────────────
        private const double BaseGrid = 50.0;
        private const double SnapDist = 10.0;

        // ── Preview colour ────────────────────────────────────────────────
        private static readonly Color ColPreview = Color.FromArgb(55, 255, 255, 255);

        public RevitDrawPanel()
        {
            InitializeComponent();
            Loaded += (s, e) => { CentreOrigin(); DrawGrid(); };
            SizeChanged += (s, e) => DrawGrid();
        }

        // ══════════════════════════════════════════════════════════════════
        // PUBLIC API — called by ZoneModuleController
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Called by ZoneModuleController.AttachDrawPanel().
        /// Wires draw tool buttons and OSNAP checkboxes to the handler.
        /// </summary>
        public void Initialize(ZoneDrawingHandler handler)
        {
            _handler = handler;

            // Wire OSNAP checkboxes if they exist in XAML
            // Each checkbox should have a Tag set to the OsnapType enum value
            // e.g. <CheckBox Tag="Endpoint" Checked="OsnapCheck_Changed" .../>
            // The handler wiring is done via OsnapToggled in the controller,
            // so we just need to fire the event from the checkboxes.
        }

        public void SetOptionsBar(DrawingOptionsBar bar)
        {
            _optionsBar = bar;
            if (bar == null) return;
            bar.FinishZoneRequested += FinishZone;
            bar.CancelZoneRequested += CancelDrawing;
        }

        public Canvas GetZonesCanvas() => ZonesCanvas;
        public Canvas GetCraneRadiusCanvas() => CraneRadiusCanvas;
        public Canvas GetPreviewCanvas() => PreviewCanvas;

        public Point CanvasToWorld(Point cp) =>
            new Point((cp.X - _panOffset.X) / _scale,
                      (cp.Y - _panOffset.Y) / _scale);

        public Point WorldToCanvas(Point wp) =>
            new Point(wp.X * _scale + _panOffset.X,
                      wp.Y * _scale + _panOffset.Y);

        public double CurrentScale => _scale;

        // ══════════════════════════════════════════════════════════════════
        // OSNAP CHECKBOX HANDLER (wire from XAML checkboxes)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Call this from OSNAP checkbox Checked/Unchecked events in XAML.
        /// Each checkbox Tag must be set to the OsnapType string, e.g. Tag="Endpoint"
        /// </summary>
        private void OsnapCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.Tag is string tagStr
                && Enum.TryParse<OsnapType>(tagStr, out var osnapType))
            {
                OsnapToggled?.Invoke(osnapType, cb.IsChecked == true);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // MOUSE EVENTS
        // ══════════════════════════════════════════════════════════════════

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Focus();

            if (e.ClickCount == 2)
            {
                if (_pts.Count > 0) _pts.RemoveAt(_pts.Count - 1);
                FinishZone();
                e.Handled = true;
                return;
            }

            var snap = SnapToGrid(e.GetPosition(DrawCanvas));
            _pts.Add(snap);
            _isDrawing = true;

            DrawPreview(e.GetPosition(DrawCanvas));
            _optionsBar?.UpdatePointCount(_pts.Count);
            _optionsBar?.SetDrawingActive(true, _pts.Count);
            e.Handled = true;
        }

        private void Canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_pts.Count > 0)
            {
                _pts.RemoveAt(_pts.Count - 1);
                DrawPreview(_lastMouse);
                _optionsBar?.UpdatePointCount(_pts.Count);
                if (_pts.Count == 0) { CancelDrawing(); return; }
            }
            e.Handled = true;
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                _isPanning = true;
                _panStart = e.GetPosition(DrawCanvas);
                DrawCanvas.CaptureMouse();
                DrawCanvas.Cursor = Cursors.SizeAll;
                e.Handled = true;
            }
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle && _isPanning)
            {
                _isPanning = false;
                DrawCanvas.ReleaseMouseCapture();
                DrawCanvas.Cursor = Cursors.Cross;
                e.Handled = true;
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(DrawCanvas);
            _lastMouse = pos;

            var world = CanvasToWorld(pos);
            TxtCoords.Text = $"X: {world.X:F2}    Y: {world.Y:F2}";
            DrawCrosshair(pos);

            if (_isDrawing) DrawPreview(pos);

            if (_isPanning && e.MiddleButton == MouseButtonState.Pressed)
            {
                var delta = pos - _panStart;
                _panOffset.X += delta.X;
                _panOffset.Y += delta.Y;
                _panStart = pos;
                DrawGrid();
            }
        }

        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var pos = e.GetPosition(DrawCanvas);
            double factor = e.Delta > 0 ? 1.15 : (1.0 / 1.15);

            _panOffset.X = pos.X + (_panOffset.X - pos.X) * factor;
            _panOffset.Y = pos.Y + (_panOffset.Y - pos.Y) * factor;
            _scale = Math.Clamp(_scale * factor, 0.05, 20.0);

            DrawGrid();
            TxtZoom.Text = $"{(int)(_scale * 100)}%";
            e.Handled = true;
        }

        private void UserControl_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    FinishZone(); e.Handled = true; break;

                case Key.Escape:
                    CancelDrawing(); e.Handled = true; break;

                case Key.Z when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                    if (_pts.Count > 0)
                    {
                        _pts.RemoveAt(_pts.Count - 1);
                        DrawPreview(_lastMouse);
                        _optionsBar?.UpdatePointCount(_pts.Count);
                        if (_pts.Count == 0) CancelDrawing();
                    }
                    e.Handled = true; break;

                case Key.Delete:
                    if (_pts.Count > 0)
                    {
                        _pts.RemoveAt(_pts.Count - 1);
                        DrawPreview(_lastMouse);
                        _optionsBar?.UpdatePointCount(_pts.Count);
                    }
                    e.Handled = true; break;
            }
        }

        private void UserControl_MouseEnter(object sender, MouseEventArgs e) => Focus();

        // ══════════════════════════════════════════════════════════════════
        // ZOOM BUTTONS
        // ══════════════════════════════════════════════════════════════════

        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
            => ZoomAround(Centre(), 1.25);

        private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
            => ZoomAround(Centre(), 0.8);

        private void BtnZoomFit_Click(object sender, RoutedEventArgs e) => ResetView();
        private void BtnResetView_Click(object sender, RoutedEventArgs e) => ResetView();

        private void ChkGrid_Changed(object sender, RoutedEventArgs e)
        {
            GridCanvas.Visibility = ChkShowGrid.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;
            DrawGrid();
        }

        private void ChkCraneRadius_Changed(object sender, RoutedEventArgs e)
        {
            CraneRadiusCanvas.Visibility = ChkShowCraneRadius.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;
        }

        // ══════════════════════════════════════════════════════════════════
        // DRAWING
        // ══════════════════════════════════════════════════════════════════

        private void FinishZone()
        {
            if (_pts.Count < 3)
            {
                StatusChanged?.Invoke($"⚠ Need at least 3 points (have {_pts.Count}).");
                return;
            }

            var worldPts = _pts.Select(p => CanvasToWorld(p)).ToList();
            var type = _optionsBar?.SelectedZoneType ?? ZoneType.Allowed;

            ZoneCompleted?.Invoke(worldPts, type);
            ClearDrawState();
        }

        private void CancelDrawing()
        {
            ClearDrawState();
            DrawingCancelled?.Invoke();
            StatusChanged?.Invoke("Drawing cancelled.");
        }

        private void ClearDrawState()
        {
            _pts.Clear();
            _isDrawing = false;
            PreviewCanvas.Children.Clear();
            TxtPointCount.Text = "";
            _optionsBar?.SetDrawingActive(false);
        }

        // ══════════════════════════════════════════════════════════════════
        // GRID RENDERING
        // ══════════════════════════════════════════════════════════════════

        private void DrawGrid()
        {
            GridCanvas.Children.Clear();
            if (ChkShowGrid?.IsChecked == false) return;

            double w = DrawCanvas.ActualWidth;
            double h = DrawCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            double spacing = BaseGrid * _scale;
            if (spacing < 6) return;

            var minorBrush = new SolidColorBrush(Color.FromArgb(22, 255, 255, 255));
            var majorBrush = new SolidColorBrush(Color.FromArgb(45, 255, 255, 255));

            double offX = _panOffset.X % spacing;
            double offY = _panOffset.Y % spacing;

            for (double x = offX; x < w; x += spacing)
            {
                bool major = Math.Abs(((x - _panOffset.X) / _scale) % (BaseGrid * 5)) < 1.0;
                GridCanvas.Children.Add(new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = h,
                    Stroke = major ? majorBrush : minorBrush,
                    StrokeThickness = major ? 1 : 0.5
                });
            }

            for (double y = offY; y < h; y += spacing)
            {
                bool major = Math.Abs(((y - _panOffset.Y) / _scale) % (BaseGrid * 5)) < 1.0;
                GridCanvas.Children.Add(new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = w,
                    Y2 = y,
                    Stroke = major ? majorBrush : minorBrush,
                    StrokeThickness = major ? 1 : 0.5
                });
            }

            if (_panOffset.X >= 0 && _panOffset.X <= w)
                GridCanvas.Children.Add(new Line
                {
                    X1 = _panOffset.X,
                    Y1 = 0,
                    X2 = _panOffset.X,
                    Y2 = h,
                    Stroke = new SolidColorBrush(Color.FromArgb(80, 0, 122, 204)),
                    StrokeThickness = 1.5
                });

            if (_panOffset.Y >= 0 && _panOffset.Y <= h)
                GridCanvas.Children.Add(new Line
                {
                    X1 = 0,
                    Y1 = _panOffset.Y,
                    X2 = w,
                    Y2 = _panOffset.Y,
                    Stroke = new SolidColorBrush(Color.FromArgb(80, 0, 122, 204)),
                    StrokeThickness = 1.5
                });

            DrawScaleRuler(spacing);
        }

        private void DrawScaleRuler(double spacing)
        {
            double rulerX = 16;
            double rulerY = DrawCanvas.ActualHeight - 20;
            var brush = new SolidColorBrush(Color.FromArgb(160, 200, 200, 200));

            GridCanvas.Children.Add(new Line
            { X1 = rulerX, Y1 = rulerY, X2 = rulerX + spacing, Y2 = rulerY, Stroke = brush, StrokeThickness = 2 });
            GridCanvas.Children.Add(new Line
            { X1 = rulerX, Y1 = rulerY - 4, X2 = rulerX, Y2 = rulerY + 4, Stroke = brush, StrokeThickness = 1.5 });
            GridCanvas.Children.Add(new Line
            { X1 = rulerX + spacing, Y1 = rulerY - 4, X2 = rulerX + spacing, Y2 = rulerY + 4, Stroke = brush, StrokeThickness = 1.5 });

            var txt = new TextBlock { Text = $"{BaseGrid:0} m", FontSize = 9, Foreground = brush };
            Canvas.SetLeft(txt, rulerX + spacing / 2 - 10);
            Canvas.SetTop(txt, rulerY - 16);
            GridCanvas.Children.Add(txt);
        }

        // ══════════════════════════════════════════════════════════════════
        // PREVIEW POLYGON
        // ══════════════════════════════════════════════════════════════════

        private void DrawPreview(Point mousePos)
        {
            PreviewCanvas.Children.Clear();
            if (_pts.Count == 0) return;

            TxtPointCount.Text = $"Points: {_pts.Count}";

            var snap = SnapToGrid(mousePos);
            var allPts = new List<Point>(_pts) { snap };
            var ptColl = new PointCollection(allPts);

            if (allPts.Count >= 3)
                PreviewCanvas.Children.Add(new Polygon
                {
                    Points = ptColl,
                    Fill = new SolidColorBrush(ColPreview),
                    Stroke = Brushes.White,
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection { 5, 3 }
                });
            else
                PreviewCanvas.Children.Add(new Polyline
                {
                    Points = ptColl,
                    Stroke = Brushes.White,
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection { 5, 3 }
                });

            if (_pts.Count >= 2)
                PreviewCanvas.Children.Add(new Line
                {
                    X1 = snap.X,
                    Y1 = snap.Y,
                    X2 = _pts[0].X,
                    Y2 = _pts[0].Y,
                    Stroke = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 3, 3 }
                });

            for (int i = 0; i < _pts.Count; i++)
            {
                var p = _pts[i];
                bool first = i == 0;
                double r = first ? 5 : 4;

                var dot = new Ellipse
                {
                    Width = r * 2,
                    Height = r * 2,
                    Fill = first ? new SolidColorBrush(Color.FromRgb(0, 200, 100)) : Brushes.White,
                    Stroke = new SolidColorBrush(Color.FromRgb(0, 122, 204)),
                    StrokeThickness = 1.5
                };
                Canvas.SetLeft(dot, p.X - r);
                Canvas.SetTop(dot, p.Y - r);
                PreviewCanvas.Children.Add(dot);

                var lbl = new TextBlock { Text = (i + 1).ToString(), FontSize = 9, Foreground = Brushes.White };
                Canvas.SetLeft(lbl, p.X + 6);
                Canvas.SetTop(lbl, p.Y - 6);
                PreviewCanvas.Children.Add(lbl);
            }

            var cur = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = new SolidColorBrush(Color.FromArgb(180, 255, 220, 0)),
                Stroke = Brushes.White,
                StrokeThickness = 1
            };
            Canvas.SetLeft(cur, snap.X - 3);
            Canvas.SetTop(cur, snap.Y - 3);
            PreviewCanvas.Children.Add(cur);
        }

        private void DrawCrosshair(Point pos)
        {
            CursorCanvas.Children.Clear();
            const double size = 12;
            var brush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));

            CursorCanvas.Children.Add(new Line
            { X1 = pos.X - size, Y1 = pos.Y, X2 = pos.X + size, Y2 = pos.Y, Stroke = brush, StrokeThickness = 1 });
            CursorCanvas.Children.Add(new Line
            { X1 = pos.X, Y1 = pos.Y - size, X2 = pos.X, Y2 = pos.Y + size, Stroke = brush, StrokeThickness = 1 });
        }

        // ══════════════════════════════════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════════════════════════════════

        private Point SnapToGrid(Point cp)
        {
            if (_optionsBar?.SnapToGrid != true) return cp;
            double spacing = BaseGrid * _scale;
            if (spacing < 4) return cp;

            double offX = _panOffset.X % spacing;
            double offY = _panOffset.Y % spacing;

            double sx = Math.Round((cp.X - offX) / spacing) * spacing + offX;
            double sy = Math.Round((cp.Y - offY) / spacing) * spacing + offY;

            double d = Math.Sqrt((cp.X - sx) * (cp.X - sx) + (cp.Y - sy) * (cp.Y - sy));
            return d <= SnapDist ? new Point(sx, sy) : cp;
        }

        private void CentreOrigin()
        {
            _panOffset.X = DrawCanvas.ActualWidth / 2;
            _panOffset.Y = DrawCanvas.ActualHeight / 2;
        }

        private void ResetView()
        {
            _scale = 1.0;
            CentreOrigin();
            DrawGrid();
            TxtZoom.Text = "100%";
        }

        private void ZoomAround(Point c, double factor)
        {
            _panOffset.X = c.X + (_panOffset.X - c.X) * factor;
            _panOffset.Y = c.Y + (_panOffset.Y - c.Y) * factor;
            _scale = Math.Clamp(_scale * factor, 0.05, 20.0);
            DrawGrid();
            TxtZoom.Text = $"{(int)(_scale * 100)}%";
        }

        private Point Centre() =>
            new Point(DrawCanvas.ActualWidth / 2, DrawCanvas.ActualHeight / 2);
    }
}