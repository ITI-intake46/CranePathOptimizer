using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WPF_IFC_test1.Utilities;

namespace WPF_IFC_test1.ZoneModule.UI
{
    // ══════════════════════════════════════════════════════════════════════════
    //  Zone colour helper — maps zone type to its theme colour
    // ══════════════════════════════════════════════════════════════════════════

    public enum ZonePreviewType
    {
        SiteBoundary,   // Blue  #007ACC
        Allowed,        // Green #27AE60
        Forbidden       // Red   #E74C3C
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  ViewportOverlayCanvas
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Transparent WPF Canvas that floats on top of the 3D viewport.
    /// ALL temporary drawing lives here — nothing is ever added to the 3D scene.
    ///
    /// Three independent layers are managed separately so clearing one does not
    /// disturb the others:
    ///   • Measure layer  — dashed line + distance label
    ///   • Preview layer  — ghost shape outlines + dimension labels
    ///   • Grid layer     — coordinate labels along viewport edges
    ///
    /// Usage (from MainWindow or ViewportController):
    ///
    ///   // Measure
    ///   Overlay.DrawMeasureLine(screenPtA, screenPtB);
    ///   Overlay.ShowMeasureLabel(midPoint, "12.34 m");
    ///   Overlay.ClearMeasure();
    ///
    ///   // Shape preview
    ///   Overlay.DrawPreviewRect(rect, ZonePreviewType.Allowed);
    ///   Overlay.ClearPreview();
    ///
    ///   // Grid labels
    ///   Overlay.SetGridLabels(labelList);
    ///   Overlay.ClearGridLabels();
    ///
    ///   // Nuke everything
    ///   Overlay.ClearAll();
    /// </summary>
    public partial class ViewportOverlayCanvas : UserControl
    {
        // ── Layer tag constants ───────────────────────────────────────────────
        // Every element added to RootCanvas gets a Tag so we can remove
        // only elements belonging to a specific layer without clearing all.

        private const string TAG_MEASURE = "measure";
        private const string TAG_PREVIEW = "preview";
        private const string TAG_GRID = "grid";

        // ── Theme colours ─────────────────────────────────────────────────────

        private static readonly Color ColMeasure = Color.FromRgb(0x1A, 0xBC, 0x9C); // teal
        private static readonly Color ColBoundary = Color.FromRgb(0x00, 0x7A, 0xCC); // blue
        private static readonly Color ColAllowed = Color.FromRgb(0x27, 0xAE, 0x60); // green
        private static readonly Color ColForbidden = Color.FromRgb(0xE7, 0x4C, 0x3C); // red
        private static readonly Color ColLabel = Colors.White;
        private static readonly Color ColLabelBg = Color.FromArgb(0xCC, 0x1E, 0x1E, 0x1E);
        private static readonly Color ColGrid = Color.FromRgb(0x88, 0x88, 0x88);
        private static readonly Color ColDimLabel = Color.FromRgb(0xFF, 0xFF, 0xFF);

        // ── Constructor ───────────────────────────────────────────────────────

        public ViewportOverlayCanvas()
        {
            InitializeComponent();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  MEASURE LAYER
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Draws a temporary dashed line from <paramref name="screenA"/> to
        /// <paramref name="screenB"/> in the measure colour (teal).
        /// Call <see cref="ClearMeasure"/> to remove it.
        /// </summary>
        public void DrawMeasureLine(Point screenA, Point screenB)
        {
            // Remove any existing measure line first (only one at a time)
            RemoveByTag(TAG_MEASURE, "line");

            var line = new Line
            {
                X1 = screenA.X,
                Y1 = screenA.Y,
                X2 = screenB.X,
                Y2 = screenB.Y,
                Stroke = new SolidColorBrush(ColMeasure),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 5, 4 },
                Opacity = 0.92,
                Tag = TAG_MEASURE + ":line",
                SnapsToDevicePixels = true
            };

            RootCanvas.Children.Add(line);
        }

        /// <summary>
        /// Draws a live rubber-band measure line that follows the cursor.
        /// Replaces any existing rubber-band line. Used while the user moves
        /// the mouse after placing point A.
        /// </summary>
        public void UpdateMeasureRubberBand(Point screenA, Point cursorScreen)
            => DrawMeasureLine(screenA, cursorScreen);

        /// <summary>
        /// Displays a floating distance label at <paramref name="screenMid"/>.
        /// The label has a semi-transparent dark background pill.
        /// </summary>
        /// <param name="screenMid">Screen position — typically the midpoint of the measure line.</param>
        /// <param name="text">Formatted distance string, e.g. "12.34 m".</param>
        public void ShowMeasureLabel(Point screenMid, string text)
        {
            // Remove old label
            RemoveByTag(TAG_MEASURE, "label");

            var label = BuildLabel(text, ColMeasure, TAG_MEASURE + ":label");

            // Position: update after measure so the element is in the tree
            RootCanvas.Children.Add(label);

            // Force layout so ActualWidth/Height are available
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double w = label.DesiredSize.Width;
            double h = label.DesiredSize.Height;

            Canvas.SetLeft(label, screenMid.X - w / 2.0);
            Canvas.SetTop(label, screenMid.Y - h - 8.0); // 8px above midpoint
        }

        /// <summary>
        /// Removes the measure line AND the distance label from the overlay.
        /// Call this on: Escape, tool switch, second Measure click, new measure start.
        /// </summary>
        public void ClearMeasure()
            => RemoveByTag(TAG_MEASURE);

        // ══════════════════════════════════════════════════════════════════════
        //  PREVIEW LAYER  (ghost shapes before placing)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Draws a ghost rectangle preview at the given screen rectangle.
        /// Shows W and H dimension labels on the bottom and right edges.
        /// </summary>
        public void DrawPreviewRect(Rect screenRect, ZonePreviewType zoneType,
                                    double widthMetres = 0, double heightMetres = 0)
        {
            ClearPreview();

            Color col = ZoneColor(zoneType);
            Color fill = Color.FromArgb(60, col.R, col.G, col.B);  // 24% opacity fill

            var rect = new Rectangle
            {
                Width = screenRect.Width,
                Height = screenRect.Height,
                Fill = new SolidColorBrush(fill),
                Stroke = new SolidColorBrush(col),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 5, 3 },
                Tag = TAG_PREVIEW + ":shape",
                Opacity = 0.9,
            };
            Canvas.SetLeft(rect, screenRect.Left);
            Canvas.SetTop(rect, screenRect.Top);
            RootCanvas.Children.Add(rect);

            // Dimension labels
            if (widthMetres > 0)
            {
                string wText = UnitSystem.Format(widthMetres);
                var wLabel = BuildLabel(wText, col, TAG_PREVIEW + ":label");
                RootCanvas.Children.Add(wLabel);
                wLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(wLabel, screenRect.Left + screenRect.Width / 2 - wLabel.DesiredSize.Width / 2);
                Canvas.SetTop(wLabel, screenRect.Bottom + 4);
            }

            if (heightMetres > 0)
            {
                string hText = UnitSystem.Format(heightMetres);
                var hLabel = BuildLabel(hText, col, TAG_PREVIEW + ":label");
                RootCanvas.Children.Add(hLabel);
                hLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(hLabel, screenRect.Right + 4);
                Canvas.SetTop(hLabel, screenRect.Top + screenRect.Height / 2 - hLabel.DesiredSize.Height / 2);
            }
        }

        /// <summary>
        /// Draws a ghost circle preview. Centre and radius are in screen coordinates.
        /// </summary>
        public void DrawPreviewCircle(Point screenCenter, double screenRadius,
                                      ZonePreviewType zoneType, double radiusMetres = 0)
        {
            ClearPreview();

            Color col = ZoneColor(zoneType);
            Color fill = Color.FromArgb(60, col.R, col.G, col.B);

            var ellipse = new Ellipse
            {
                Width = screenRadius * 2,
                Height = screenRadius * 2,
                Fill = new SolidColorBrush(fill),
                Stroke = new SolidColorBrush(col),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 5, 3 },
                Tag = TAG_PREVIEW + ":shape",
                Opacity = 0.9,
            };
            Canvas.SetLeft(ellipse, screenCenter.X - screenRadius);
            Canvas.SetTop(ellipse, screenCenter.Y - screenRadius);
            RootCanvas.Children.Add(ellipse);

            // Radius label
            if (radiusMetres > 0)
            {
                string rText = "r = " + UnitSystem.Format(radiusMetres);
                var rLabel = BuildLabel(rText, col, TAG_PREVIEW + ":label");
                RootCanvas.Children.Add(rLabel);
                rLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                // Place at 3-o-clock position
                Canvas.SetLeft(rLabel, screenCenter.X + screenRadius + 6);
                Canvas.SetTop(rLabel, screenCenter.Y - rLabel.DesiredSize.Height / 2);
            }
        }

        /// <summary>
        /// Draws a ghost polygon preview from a list of screen-space points.
        /// Used for polyline / arc drawing live preview.
        /// </summary>
        public void DrawPreviewPolygon(IList<Point> screenPoints, ZonePreviewType zoneType, bool closed = false)
        {
            ClearPreview();

            if (screenPoints == null || screenPoints.Count < 2) return;

            Color col = ZoneColor(zoneType);
            Color fill = Color.FromArgb(40, col.R, col.G, col.B);

            var pc = new PointCollection(screenPoints);

            if (closed && screenPoints.Count >= 3)
            {
                var polygon = new Polygon
                {
                    Points = pc,
                    Fill = new SolidColorBrush(fill),
                    Stroke = new SolidColorBrush(col),
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection { 5, 3 },
                    Tag = TAG_PREVIEW + ":shape",
                    Opacity = 0.9,
                };
                RootCanvas.Children.Add(polygon);
            }
            else
            {
                var polyline = new Polyline
                {
                    Points = pc,
                    Stroke = new SolidColorBrush(col),
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection { 5, 3 },
                    Tag = TAG_PREVIEW + ":shape",
                    Opacity = 0.9,
                };
                RootCanvas.Children.Add(polyline);
            }
        }

        /// <summary>
        /// Draws a single vertex dot at the given screen point.
        /// Used to mark placed vertices during polyline drawing.
        /// </summary>
        public void AddPreviewVertex(Point screenPt, ZonePreviewType zoneType)
        {
            Color col = ZoneColor(zoneType);

            var dot = new Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = new SolidColorBrush(col),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 1,
                Tag = TAG_PREVIEW + ":vertex",
                Opacity = 0.95,
            };
            Canvas.SetLeft(dot, screenPt.X - 3.5);
            Canvas.SetTop(dot, screenPt.Y - 3.5);
            RootCanvas.Children.Add(dot);
        }

        /// <summary>
        /// Removes ALL preview geometry — shape outline, dimension labels, vertex dots.
        /// Call this when the user places the shape or presses Escape.
        /// </summary>
        public void ClearPreview()
            => RemoveByTag(TAG_PREVIEW);

        // ══════════════════════════════════════════════════════════════════════
        //  HOVER EDGE LAYER  (Pick Lines — highlights the edge under cursor)
        // ══════════════════════════════════════════════════════════════════════

        private const string TAG_HOVER = "hover";

        /// <summary>
        /// Draws a bright highlight on the mesh edge currently under the cursor.
        /// Shown during Pick Lines mouse-move so the user knows which edge will be picked.
        /// </summary>
        public void DrawHoverEdge(Point screenA, Point screenB)
        {
            ClearHoverEdge();

            // Bright cyan highlight line
            var line = new Line
            {
                X1 = screenA.X, Y1 = screenA.Y,
                X2 = screenB.X, Y2 = screenB.Y,
                Stroke = new SolidColorBrush(Color.FromArgb(230, 0x00, 0xFF, 0xFF)),
                StrokeThickness = 3.0,
                Tag = TAG_HOVER + ":edge",
                Opacity = 0.9,
            };
            RootCanvas.Children.Add(line);

            // Endpoint markers
            foreach (var pt in new[] { screenA, screenB })
            {
                var dot = new Ellipse
                {
                    Width = 10, Height = 10,
                    Fill = new SolidColorBrush(Color.FromArgb(220, 0x00, 0xFF, 0xFF)),
                    Stroke = new SolidColorBrush(Colors.White),
                    StrokeThickness = 1.5,
                    Tag = TAG_HOVER + ":vertex",
                    Opacity = 0.95,
                };
                Canvas.SetLeft(dot, pt.X - 5);
                Canvas.SetTop(dot, pt.Y - 5);
                RootCanvas.Children.Add(dot);
            }
        }

        /// <summary>Removes the hover edge highlight.</summary>
        public void ClearHoverEdge() => RemoveByTag(TAG_HOVER);

        // ══════════════════════════════════════════════════════════════════════
        //  GRID LABEL LAYER
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Replaces all grid coordinate labels with a fresh set.
        /// Each label is a (screenPosition, text) pair.
        /// Position is the screen coordinate where the label's top-left corner is placed.
        /// This is called by the viewport controller every time the camera moves.
        /// </summary>
        public void SetGridLabels(IEnumerable<(Point ScreenPos, string Text)> labels)
        {
            ClearGridLabels();

            foreach (var (pos, text) in labels)
            {
                var tb = new TextBlock
                {
                    Text = text,
                    Foreground = new SolidColorBrush(ColGrid),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 10,
                    Tag = TAG_GRID,
                    Opacity = 0.75,
                };
                Canvas.SetLeft(tb, pos.X);
                Canvas.SetTop(tb, pos.Y);
                RootCanvas.Children.Add(tb);
            }
        }

        /// <summary>Removes all grid coordinate labels.</summary>
        public void ClearGridLabels()
            => RemoveByTag(TAG_GRID);

        // ══════════════════════════════════════════════════════════════════════
        //  GLOBAL CLEAR
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Removes EVERYTHING from the overlay — measure, preview, and grid labels.
        /// Call on major mode resets (e.g. loading a new project).
        /// </summary>
        public void ClearAll()
            => RootCanvas.Children.Clear();

        // ══════════════════════════════════════════════════════════════════════
        //  HIT-TEST PASSTHROUGH CONTROL
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Call this when a drawing tool needs to capture mouse clicks
        /// (e.g. placing measure points or shape vertices).
        /// IMPORTANT: call <see cref="DisableHitTest"/> when the tool exits
        /// so clicks pass through to the viewport again.
        /// </summary>
        public void EnableHitTest()
        {
            IsHitTestVisible = true;
        }

        /// <summary>
        /// Restores transparent passthrough — clicks go to the 3D viewport again.
        /// Always call this when a tool that called <see cref="EnableHitTest"/> exits.
        /// </summary>
        public void DisableHitTest()
        {
            IsHitTestVisible = false;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  SIZE — expose canvas dimensions so callers can convert world→screen
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Current pixel width of the overlay canvas.</summary>
        public double CanvasWidth => RootCanvas.ActualWidth;

        /// <summary>Current pixel height of the overlay canvas.</summary>
        public double CanvasHeight => RootCanvas.ActualHeight;

        // ══════════════════════════════════════════════════════════════════════
        //  PRIVATE HELPERS
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds a floating label pill: dark semi-transparent background,
        /// coloured text, Consolas font.
        /// </summary>
        private static Border BuildLabel(string text, Color textColor, string tag)
        {
            return new Border
            {
                Background = new SolidColorBrush(ColLabelBg),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Tag = tag,
                Child = new TextBlock
                {
                    Text = text,
                    Foreground = new SolidColorBrush(textColor),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                }
            };
        }

        /// <summary>
        /// Removes all canvas children whose Tag starts with the given prefix.
        /// Optionally restrict to a specific sub-tag (e.g. "line" or "label").
        /// </summary>
        private void RemoveByTag(string tagPrefix, string subTag = null)
        {
            string fullPrefix = subTag == null ? tagPrefix : tagPrefix + ":" + subTag;

            var toRemove = new List<UIElement>();
            foreach (UIElement child in RootCanvas.Children)
            {
                if (child is FrameworkElement fe &&
                    fe.Tag is string t &&
                    t.StartsWith(fullPrefix, StringComparison.Ordinal))
                {
                    toRemove.Add(child);
                }
            }
            foreach (var el in toRemove)
                RootCanvas.Children.Remove(el);
        }

        /// <summary>Maps a zone type enum to its theme colour.</summary>
        private static Color ZoneColor(ZonePreviewType t) => t switch
        {
            ZonePreviewType.SiteBoundary => ColBoundary,
            ZonePreviewType.Allowed => ColAllowed,
            ZonePreviewType.Forbidden => ColForbidden,
            _ => ColBoundary
        };
    }
}