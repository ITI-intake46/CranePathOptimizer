using CraneOptimizer.UI.Features.F11_DXF.Models;
using netDxf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CraneOptimizer.UI.Features.F11_DXF.Views
{
    /// <summary>
    /// Renders a <see cref="DxfDocument"/> onto a WPF <see cref="Canvas"/> as 2-D line
    /// geometry for the alignment-dialog preview.
    ///
    /// Responsibilities:
    ///  • Parse DXF entities → WPF shapes on the canvas.
    ///  • Maintain pan / zoom via a <see cref="TransformGroup"/>.
    ///  • Expose <see cref="RawBounds"/> for auto-fit calculations.
    ///  • Expose <see cref="Layers"/> for the layers ListBox.
    /// </summary>
    public sealed class DxfCanvasRenderer
    {
        // ── State ──────────────────────────────────────────────────────────────

        private Canvas? _canvas;
        private DxfTransformModel? _transformModel;
        private DxfDocument? _document;

        private readonly TranslateTransform _panTransform = new();
        private readonly ScaleTransform _zoomTransform = new(1, 1);
        private readonly TransformGroup _viewTransform = new();

        private Point _lastPan;
        private bool _isPanning;

        // ── Public surface ─────────────────────────────────────────────────────

        /// <summary>Axis-aligned bounding box of all raw (untransformed) DXF geometry.</summary>
        public DxfBounds RawBounds { get; private set; } = DxfBounds.Empty;

        /// <summary>Observable list of layers for UI binding.</summary>
        public ObservableCollection<DxfLayerInfo> Layers { get; } = new();

        // ════════════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Wire up the renderer to a XAML <see cref="Canvas"/>.
        /// Call this from the Window.Loaded handler so ActualWidth / Height are valid.
        /// </summary>
        public void Attach(Canvas canvas, DxfTransformModel transformModel)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            _transformModel = transformModel ?? throw new ArgumentNullException(nameof(transformModel));

            _viewTransform.Children.Clear();
            _viewTransform.Children.Add(_zoomTransform);
            _viewTransform.Children.Add(_panTransform);

            _canvas.RenderTransform = _viewTransform;

            _canvas.MouseWheel += Canvas_MouseWheel;
            _canvas.MouseLeftButtonDown += Canvas_MouseLeftButtonDown;
            _canvas.MouseLeftButtonUp += Canvas_MouseLeftButtonUp;
            _canvas.MouseMove += Canvas_MouseMove;

            if (_document != null)
                Redraw();
        }

        /// <summary>Unhook events so the canvas can be GC'd.</summary>
        public void Detach()
        {
            if (_canvas != null)
            {
                _canvas.MouseWheel -= Canvas_MouseWheel;
                _canvas.MouseLeftButtonDown -= Canvas_MouseLeftButtonDown;
                _canvas.MouseLeftButtonUp -= Canvas_MouseLeftButtonUp;
                _canvas.MouseMove -= Canvas_MouseMove;
                _canvas.RenderTransform = Transform.Identity;
                _canvas = null;
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Load / draw
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>Loads and immediately renders a DXF document.</summary>
        public void LoadDocument(DxfDocument doc)
        {
            _document = doc ?? throw new ArgumentNullException(nameof(doc));
            BuildLayerList(doc);
            Redraw();
            FitToView();
        }

        /// <summary>Clears and redraws the canvas from the current document.</summary>
        public void Redraw()
        {
            if (_canvas == null || _document == null) return;

            _canvas.Children.Clear();
            RawBounds = DxfBounds.Empty;

            var bounds = new DxfBounds();
            var visibleLayers = new HashSet<string>(
                Layers.Where(l => l.IsVisible).Select(l => l.Name),
                StringComparer.OrdinalIgnoreCase);

            DrawLines(_document, bounds, visibleLayers);
            DrawPolylines2D(_document, bounds, visibleLayers);
            DrawCircles(_document, bounds, visibleLayers);
            DrawArcs(_document, bounds, visibleLayers);

            RawBounds = bounds;
            Log($"Redraw done. Bounds: {bounds}");
        }

        /// <summary>
        /// Resets pan/zoom so the entire drawing fits inside the current canvas size.
        /// </summary>
        public void FitToView()
        {
            if (_canvas == null || RawBounds.IsEmpty) return;

            double cw = _canvas.ActualWidth;
            double ch = _canvas.ActualHeight;
            if (cw <= 0 || ch <= 0) return;

            double padding = 20;
            double usableW = cw - padding * 2;
            double usableH = ch - padding * 2;
            if (usableW <= 0 || usableH <= 0) return;

            double scaleX = usableW / RawBounds.Width;
            double scaleY = usableH / RawBounds.Height;
            double scale = Math.Min(scaleX, scaleY);

            // DXF Y-axis points up; WPF Y-axis points down → flip Y
            _zoomTransform.ScaleX = scale;
            _zoomTransform.ScaleY = -scale;

            _panTransform.X = padding - RawBounds.MinX * scale;
            _panTransform.Y = ch - padding + RawBounds.MinY * scale;
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Entity rendering
        // ════════════════════════════════════════════════════════════════════════

        private void DrawLines(DxfDocument doc, DxfBounds bounds, HashSet<string> visibleLayers)
        {
            try
            {
                foreach (var line in doc.Entities.Lines)
                {
                    if (!visibleLayers.Contains(line.Layer.Name)) continue;

                    double x1 = line.StartPoint.X, y1 = line.StartPoint.Y;
                    double x2 = line.EndPoint.X, y2 = line.EndPoint.Y;

                    bounds.Expand(x1, y1);
                    bounds.Expand(x2, y2);

                    AddLine(x1, y1, x2, y2, ResolveColor(line.Color, line.Layer));
                }
            }
            catch (Exception ex) { Log($"Lines error: {ex.Message}"); }
        }

        private void DrawPolylines2D(DxfDocument doc, DxfBounds bounds, HashSet<string> visibleLayers)
        {
            try
            {
                foreach (var poly in doc.Entities.Polylines2D)
                {
                    if (!visibleLayers.Contains(poly.Layer.Name)) continue;

                    var pts = new List<Point>();
                    foreach (var v in poly.Vertexes)
                    {
                        double x = v.Position.X, y = v.Position.Y;
                        bounds.Expand(x, y);
                        pts.Add(new Point(x, y));
                    }

                    if (pts.Count < 2) continue;
                    var brush = ResolveColor(poly.Color, poly.Layer);

                    int count = poly.IsClosed ? pts.Count : pts.Count - 1;
                    for (int i = 0; i < count; i++)
                        AddLine(pts[i].X, pts[i].Y,
                                pts[(i + 1) % pts.Count].X,
                                pts[(i + 1) % pts.Count].Y,
                                brush);
                }
            }
            catch (Exception ex) { Log($"Polylines2D error: {ex.Message}"); }
        }

        private void DrawCircles(DxfDocument doc, DxfBounds bounds, HashSet<string> visibleLayers)
        {
            try
            {
                foreach (var circle in doc.Entities.Circles)
                {
                    if (!visibleLayers.Contains(circle.Layer.Name)) continue;

                    double cx = circle.Center.X, cy = circle.Center.Y, r = circle.Radius;
                    bounds.Expand(cx - r, cy - r);
                    bounds.Expand(cx + r, cy + r);

                    var brush = ResolveColor(circle.Color, circle.Layer);
                    const int seg = 36;
                    for (int i = 0; i < seg; i++)
                    {
                        double a1 = i * Math.PI * 2 / seg;
                        double a2 = (i + 1) * Math.PI * 2 / seg;
                        AddLine(cx + r * Math.Cos(a1), cy + r * Math.Sin(a1),
                                cx + r * Math.Cos(a2), cy + r * Math.Sin(a2),
                                brush);
                    }
                }
            }
            catch (Exception ex) { Log($"Circles error: {ex.Message}"); }
        }

        private void DrawArcs(DxfDocument doc, DxfBounds bounds, HashSet<string> visibleLayers)
        {
            try
            {
                foreach (var arc in doc.Entities.Arcs)
                {
                    if (!visibleLayers.Contains(arc.Layer.Name)) continue;

                    double cx = arc.Center.X, cy = arc.Center.Y, r = arc.Radius;
                    bounds.Expand(cx - r, cy - r);
                    bounds.Expand(cx + r, cy + r);

                    var brush = ResolveColor(arc.Color, arc.Layer);

                    double startDeg = arc.StartAngle;
                    double endDeg = arc.EndAngle;
                    if (endDeg <= startDeg) endDeg += 360;
                    double span = endDeg - startDeg;
                    int seg = Math.Max(3, (int)(span / 5));

                    for (int i = 0; i < seg; i++)
                    {
                        double a1 = (startDeg + i * span / seg) * Math.PI / 180;
                        double a2 = (startDeg + (i + 1) * span / seg) * Math.PI / 180;
                        AddLine(cx + r * Math.Cos(a1), cy + r * Math.Sin(a1),
                                cx + r * Math.Cos(a2), cy + r * Math.Sin(a2),
                                brush);
                    }
                }
            }
            catch (Exception ex) { Log($"Arcs error: {ex.Message}"); }
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Canvas helpers
        // ════════════════════════════════════════════════════════════════════════

        private void AddLine(double x1, double y1, double x2, double y2, Brush stroke)
        {
            if (_canvas == null) return;
            _canvas.Children.Add(new Line
            {
                X1 = x1, Y1 = y1,
                X2 = x2, Y2 = y2,
                Stroke = stroke,
                StrokeThickness = 0.5
            });
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Layer extraction
        // ════════════════════════════════════════════════════════════════════════

        private void BuildLayerList(DxfDocument doc)
        {
            Layers.Clear();

            // Count entities per layer
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in doc.Entities.Lines) Inc(counts, e.Layer.Name);
            foreach (var e in doc.Entities.Polylines2D) Inc(counts, e.Layer.Name);
            foreach (var e in doc.Entities.Circles) Inc(counts, e.Layer.Name);
            foreach (var e in doc.Entities.Arcs) Inc(counts, e.Layer.Name);

            foreach (var layer in doc.Layers)
            {
                counts.TryGetValue(layer.Name, out int cnt);
                Layers.Add(new DxfLayerInfo
                {
                    Name = layer.Name,
                    ColorHex = AciToHex(layer.Color),
                    EntityCount = cnt,
                    IsVisible = true
                });
            }

            // Wire visibility toggling
            foreach (var li in Layers)
                li.PropertyChanged += (_, _) => Redraw();
        }

        private static void Inc(Dictionary<string, int> d, string key)
        {
            d.TryGetValue(key, out int c);
            d[key] = c + 1;
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Colour helpers
        // ════════════════════════════════════════════════════════════════════════

        private static Brush ResolveColor(netDxf.AciColor color, netDxf.Tables.Layer layer)
        {
            try
            {
                var c = color.IsByLayer ? layer.Color : color;
                byte r = (byte)((c.Index > 0 && c.Index < 256) ? AciPalette(c.Index, 0) : 160);
                byte g = (byte)((c.Index > 0 && c.Index < 256) ? AciPalette(c.Index, 1) : 160);
                byte b = (byte)((c.Index > 0 && c.Index < 256) ? AciPalette(c.Index, 2) : 160);
                return new SolidColorBrush(Color.FromRgb(r, g, b));
            }
            catch
            {
                return Brushes.Gray;
            }
        }

        private static string AciToHex(netDxf.AciColor color)
        {
            try
            {
                if (color.Index > 0 && color.Index < 256)
                {
                    byte r = (byte)AciPalette(color.Index, 0);
                    byte g = (byte)AciPalette(color.Index, 1);
                    byte b = (byte)AciPalette(color.Index, 2);
                    return $"#{r:X2}{g:X2}{b:X2}";
                }
            }
            catch { }
            return "#A0A0A0";
        }

        /// <summary>
        /// Minimal ACI palette lookup (first 10 standard colours, rest grey).
        /// Component: 0=R, 1=G, 2=B.
        /// </summary>
        private static int AciPalette(int index, int component)
        {
            // Standard ACI colours 1-9
            int[][] table =
            {
                new[]{ 255, 0, 0 },     // 1 = Red
                new[]{ 255, 255, 0 },   // 2 = Yellow
                new[]{ 0, 255, 0 },     // 3 = Green
                new[]{ 0, 255, 255 },   // 4 = Cyan
                new[]{ 0, 0, 255 },     // 5 = Blue
                new[]{ 255, 0, 255 },   // 6 = Magenta
                new[]{ 255, 255, 255 }, // 7 = White
                new[]{ 128, 128, 128 }, // 8 = Dark grey
                new[]{ 192, 192, 192 }, // 9 = Light grey
            };

            if (index >= 1 && index <= 9)
                return table[index - 1][component];

            return 160; // fallback grey
        }

        // ════════════════════════════════════════════════════════════════════════
        //  Pan / zoom handlers
        // ════════════════════════════════════════════════════════════════════════

        private void Canvas_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            double factor = e.Delta > 0 ? 1.15 : 1.0 / 1.15;
            _zoomTransform.ScaleX *= factor;
            _zoomTransform.ScaleY *= factor; // preserves Y-flip sign
        }

        private void Canvas_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isPanning = true;
            _lastPan = e.GetPosition((UIElement)_canvas!.Parent);
            _canvas.CaptureMouse();
        }

        private void Canvas_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isPanning = false;
            _canvas?.ReleaseMouseCapture();
        }

        private void Canvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isPanning || _canvas == null) return;

            var pos = e.GetPosition((UIElement)_canvas.Parent);
            _panTransform.X += pos.X - _lastPan.X;
            _panTransform.Y += pos.Y - _lastPan.Y;
            _lastPan = pos;
        }

        // ════════════════════════════════════════════════════════════════════════

        private static void Log(string msg)
            => Debug.WriteLine($"[DxfCanvasRenderer] {msg}");
    }
}
