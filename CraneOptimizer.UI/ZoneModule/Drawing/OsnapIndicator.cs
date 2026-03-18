// FILE: ZoneModule/Drawing/OsnapIndicator.cs
// Fixed: Color → System.Windows.Media.Color (HelixToolkit ambiguity resolved)
// Fixed: MathUtils.Point3DtoPoint2D → manual projection via Viewport3DHelper
// Fixed: LineSegment → System.Windows.Media.LineSegment

using HelixToolkit.Wpf;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using MediaColor = System.Windows.Media.Color;        // alias to avoid HelixToolkit clash
using MediaLineSegment = System.Windows.Media.LineSegment; // alias to avoid HelixToolkit clash

namespace WPF_IFC_test1.ZoneModule.Drawing
{
    /// <summary>
    /// WPF Canvas overlay that renders snap indicators.
    /// Must be placed as a transparent child over the HelixViewport3D.
    /// Shows: □ Endpoint, △ Midpoint, ✕ Intersection, ⊥ Perpendicular, ◇ Nearest, + Grid
    /// </summary>
    public class OsnapIndicator
    {
        private readonly Canvas _overlay;
        private readonly HelixViewport3D _viewport;

        private readonly Path _markerPath = new Path();
        private readonly TextBlock _markerLabel = new TextBlock();

        // ── Colors (explicit System.Windows.Media.Color) ──────────────────────
        private static readonly SolidColorBrush BrushEndpoint = new SolidColorBrush(MediaColor.FromRgb(255, 215, 0)); // Gold
        private static readonly SolidColorBrush BrushMidpoint = new SolidColorBrush(MediaColor.FromRgb(255, 152, 0)); // Orange
        private static readonly SolidColorBrush BrushIntersection = new SolidColorBrush(MediaColor.FromRgb(231, 76, 60)); // Red
        private static readonly SolidColorBrush BrushPerpendicular = new SolidColorBrush(MediaColor.FromRgb(52, 152, 219)); // Blue
        private static readonly SolidColorBrush BrushNearest = new SolidColorBrush(MediaColor.FromRgb(26, 188, 156)); // Teal
        private static readonly SolidColorBrush BrushGrid = new SolidColorBrush(MediaColor.FromRgb(149, 165, 166)); // Grey

        public OsnapIndicator(Canvas overlay, HelixViewport3D viewport)
        {
            _overlay = overlay;
            _viewport = viewport;

            _markerPath.StrokeThickness = 1.5;
            _markerPath.IsHitTestVisible = false;

            _markerLabel.FontSize = 9;
            _markerLabel.FontWeight = FontWeights.Bold;
            _markerLabel.IsHitTestVisible = false;
            _markerLabel.Background = new SolidColorBrush(MediaColor.FromArgb(160, 30, 30, 30));
            _markerLabel.Padding = new Thickness(2, 1, 2, 1);

            _overlay.Children.Add(_markerPath);
            _overlay.Children.Add(_markerLabel);

            Hide();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Call from OnMouseMove after OsnapSystem.Snap() to show the snap marker.</summary>
        public void Update(OsnapType? snapType, Point3D? worldPoint)
        {
            if (snapType == null || worldPoint == null) { Hide(); return; }

            var screenPt = ProjectToScreen(worldPoint.Value);
            if (!screenPt.HasValue) { Hide(); return; }

            double sx = screenPt.Value.X;
            double sy = screenPt.Value.Y;

            var brush = GetBrush(snapType.Value);
            _markerPath.Stroke = brush;
            _markerPath.Fill = new SolidColorBrush(
                MediaColor.FromArgb(60, brush.Color.R, brush.Color.G, brush.Color.B));
            _markerPath.Data = BuildGeometry(snapType.Value, sx, sy);

            _markerLabel.Text = GetLabel(snapType.Value);
            _markerLabel.Foreground = brush;
            Canvas.SetLeft(_markerLabel, sx + 12);
            Canvas.SetTop(_markerLabel, sy - 8);

            _markerPath.Visibility = Visibility.Visible;
            _markerLabel.Visibility = Visibility.Visible;
        }

        public void Hide()
        {
            _markerPath.Visibility = Visibility.Collapsed;
            _markerLabel.Visibility = Visibility.Collapsed;
        }

        // ── World → Screen projection (no MathUtils dependency) ──────────────

        private Point? ProjectToScreen(Point3D worldPt)
        {
            try
            {
                // Use HelixToolkit's Viewport3DHelper which works with Viewport3D
                var vp = _viewport.Viewport;
                // Transform world point through camera projection
                var matrixCamera = vp.Camera as MatrixCamera;
                if (matrixCamera == null && vp.Camera != null)
                {
                    // Manual projection via the visual's TransformToAncestor path
                    var matrix = MathHelpers.GetTotalTransform(vp);
                    if (matrix.HasValue)
                    {
                        var pt4 = matrix.Value.Transform(new Point4D(worldPt.X, worldPt.Y, worldPt.Z, 1));
                        if (Math.Abs(pt4.W) < 1e-10) return null;
                        double ndcX = pt4.X / pt4.W;
                        double ndcY = pt4.Y / pt4.W;
                        double screenX = (ndcX + 1) / 2.0 * vp.ActualWidth;
                        double screenY = (1 - ndcY) / 2.0 * vp.ActualHeight;
                        // Point is relative to viewport; canvas is also over viewport so same coords
                        return new Point(screenX, screenY);
                    }
                }
                return null;
            }
            catch { return null; }
        }

        // ── Geometry builders ─────────────────────────────────────────────────

        private static Geometry BuildGeometry(OsnapType type, double cx, double cy)
        {
            const double s = 6;
            switch (type)
            {
                case OsnapType.Endpoint:
                    return new RectangleGeometry(new Rect(cx - s, cy - s, s * 2, s * 2));

                case OsnapType.Midpoint:
                    return new PathGeometry(new[]
                    {
                        new PathFigure(new Point(cx, cy - s), new PathSegment[]
                        {
                            new MediaLineSegment(new Point(cx + s, cy + s), true),
                            new MediaLineSegment(new Point(cx - s, cy + s), true)
                        }, true)
                    });

                case OsnapType.Intersection:
                    return BuildCross(cx, cy, s);

                case OsnapType.Perpendicular:
                    return BuildPerp(cx, cy, s);

                case OsnapType.Nearest:
                    return BuildDiamond(cx, cy, s);

                case OsnapType.Grid:
                    return BuildPlus(cx, cy, s);

                default:
                    return new EllipseGeometry(new Point(cx, cy), s, s);
            }
        }

        private static Geometry BuildCross(double cx, double cy, double s)
        {
            var g = new GeometryGroup();
            g.Children.Add(new LineGeometry(new Point(cx - s, cy - s), new Point(cx + s, cy + s)));
            g.Children.Add(new LineGeometry(new Point(cx + s, cy - s), new Point(cx - s, cy + s)));
            return g;
        }

        private static Geometry BuildPerp(double cx, double cy, double s)
        {
            var g = new GeometryGroup();
            g.Children.Add(new LineGeometry(new Point(cx, cy + s), new Point(cx, cy - s)));
            g.Children.Add(new LineGeometry(new Point(cx - s, cy + s), new Point(cx + s, cy + s)));
            g.Children.Add(new RectangleGeometry(new Rect(cx, cy + s - 3, 3, 3)));
            return g;
        }

        private static Geometry BuildDiamond(double cx, double cy, double s)
        {
            return new PathGeometry(new[]
            {
                new PathFigure(new Point(cx, cy - s), new PathSegment[]
                {
                    new MediaLineSegment(new Point(cx + s, cy),     true),
                    new MediaLineSegment(new Point(cx,     cy + s), true),
                    new MediaLineSegment(new Point(cx - s, cy),     true)
                }, true)
            });
        }

        private static Geometry BuildPlus(double cx, double cy, double s)
        {
            var g = new GeometryGroup();
            g.Children.Add(new LineGeometry(new Point(cx - s, cy), new Point(cx + s, cy)));
            g.Children.Add(new LineGeometry(new Point(cx, cy - s), new Point(cx, cy + s)));
            return g;
        }

        private static SolidColorBrush GetBrush(OsnapType type)
        {
            switch (type)
            {
                case OsnapType.Endpoint: return BrushEndpoint;
                case OsnapType.Midpoint: return BrushMidpoint;
                case OsnapType.Intersection: return BrushIntersection;
                case OsnapType.Perpendicular: return BrushPerpendicular;
                case OsnapType.Nearest: return BrushNearest;
                case OsnapType.Grid: return BrushGrid;
                default: return BrushNearest;
            }
        }

        private static string GetLabel(OsnapType type)
        {
            switch (type)
            {
                case OsnapType.Endpoint: return "END";
                case OsnapType.Midpoint: return "MID";
                case OsnapType.Intersection: return "INT";
                case OsnapType.Perpendicular: return "PER";
                case OsnapType.Nearest: return "NEA";
                case OsnapType.Grid: return "GRID";
                default: return "";
            }
        }
    }

    // ── Camera projection helper (no MathUtils dependency) ───────────────────

    internal static class MathHelpers
    {
        /// <summary>
        /// Returns the combined View*Projection matrix from a Viewport3D camera
        /// so we can manually project world coordinates to NDC.
        /// </summary>
        public static Matrix3D? GetTotalTransform(System.Windows.Controls.Viewport3D vp)
        {
            try
            {
                if (vp.Camera == null || vp.ActualWidth <= 0 || vp.ActualHeight <= 0)
                    return null;

                var camera = vp.Camera;
                double aspectRatio = vp.ActualWidth / vp.ActualHeight;

                Matrix3D view, proj;

                if (camera is PerspectiveCamera persp)
                {
                    view = GetViewMatrix(persp.Position, persp.LookDirection, persp.UpDirection);
                    proj = GetPerspectiveMatrix(persp.FieldOfView, aspectRatio,
                                                persp.NearPlaneDistance, persp.FarPlaneDistance);
                }
                else if (camera is OrthographicCamera orth)
                {
                    view = GetViewMatrix(orth.Position, orth.LookDirection, orth.UpDirection);
                    proj = GetOrthographicMatrix(orth.Width, aspectRatio,
                                                 orth.NearPlaneDistance, orth.FarPlaneDistance);
                }
                else return null;

                view.Append(proj);
                return view;
            }
            catch { return null; }
        }

        private static Matrix3D GetViewMatrix(Point3D pos, Vector3D look, Vector3D up)
        {
            var zAxis = -look; zAxis.Normalize();
            var xAxis = Vector3D.CrossProduct(up, zAxis); xAxis.Normalize();
            var yAxis = Vector3D.CrossProduct(zAxis, xAxis);

            return new Matrix3D(
                xAxis.X, yAxis.X, zAxis.X, 0,
                xAxis.Y, yAxis.Y, zAxis.Y, 0,
                xAxis.Z, yAxis.Z, zAxis.Z, 0,
                -Vector3D.DotProduct(xAxis, (Vector3D)pos),
                -Vector3D.DotProduct(yAxis, (Vector3D)pos),
                -Vector3D.DotProduct(zAxis, (Vector3D)pos), 1);
        }

        private static Matrix3D GetPerspectiveMatrix(double fovDeg, double aspect, double near, double far)
        {
            double fovRad = fovDeg * Math.PI / 180.0;
            double yScale = 1.0 / Math.Tan(fovRad / 2.0);
            double xScale = yScale / aspect;
            double zRange = far - near;
            return new Matrix3D(
                xScale, 0, 0, 0,
                0, yScale, 0, 0,
                0, 0, -(far + near) / zRange, -1,
                0, 0, -2 * far * near / zRange, 0);
        }

        private static Matrix3D GetOrthographicMatrix(double width, double aspect, double near, double far)
        {
            double height = width / aspect;
            double zRange = far - near;
            return new Matrix3D(
                2.0 / width, 0, 0, 0,
                0, 2.0 / height, 0, 0,
                0, 0, -2.0 / zRange, 0,
                0, 0, -(far + near) / zRange, 1);
        }
    }
}