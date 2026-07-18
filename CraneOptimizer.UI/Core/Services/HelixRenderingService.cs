using CraneOptimizer.UI.Core.Interfaces;
using HelixToolkit.Wpf;
using System.Windows.Media;
using System.Windows;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Core.Services
{
    public class HelixRenderingService : IRenderingService
    {
        private const double DefaultGridSizeMeters = 50d;
        private const double LegacyGridUnitsPerMeter = 1000d;
        private static readonly ScaleTransform3D LegacyGridToMetersTransform = new(1d / LegacyGridUnitsPerMeter, 1d / LegacyGridUnitsPerMeter, 1d / LegacyGridUnitsPerMeter);

        private HelixViewport3D? _viewport;
        private GridLinesVisual3D? _grid;
        private LinesVisual3D? _majorGridLines;
        private LinesVisual3D? _xAxis;
        private LinesVisual3D? _yAxis;
        private double _gridCenterX;
        private double _gridCenterY;
        private bool _isGridVisible = true;

        private readonly Dictionary<string, List<Visual3D>> _layers = new();

        public void Initialize(HelixViewport3D viewport)
        {
            if (ReferenceEquals(_viewport, viewport)) return;

            _viewport = viewport;
            _layers.Clear();
            _viewport.Children.Clear();

            SetupPhongLighting();
            AddGrid(DefaultGridSizeMeters, DefaultGridSizeMeters);
            _viewport.Children.Add(new CoordinateSystemVisual3D());
        }

        public void SetGridSize(double widthMeters, double lengthMeters)
            => SetGridSize(widthMeters, lengthMeters, _gridCenterX, _gridCenterY);

        public bool IsGridVisible => _isGridVisible;

        public void SetGridVisible(bool isVisible)
        {
            if (_isGridVisible == isVisible)
                return;

            _isGridVisible = isVisible;
            SetGridVisualAttached(_grid, isVisible);
            SetGridVisualAttached(_majorGridLines, isVisible);
            SetGridVisualAttached(_xAxis, isVisible);
            SetGridVisualAttached(_yAxis, isVisible);
        }

        public void SetGridSize(
            double widthMeters,
            double lengthMeters,
            double centerXMeters,
            double centerYMeters)
        {
            EnsureInitialized();

            widthMeters = Math.Max(1d, widthMeters);
            lengthMeters = Math.Max(1d, lengthMeters);
            _gridCenterX = centerXMeters;
            _gridCenterY = centerYMeters;

            if (_grid == null)
            {
                AddGrid(widthMeters, lengthMeters);
                return;
            }

            _grid.Width = ToLegacyGridUnits(widthMeters);
            _grid.Length = ToLegacyGridUnits(lengthMeters);
            _grid.Center = ToLegacyGridPoint(_gridCenterX, _gridCenterY, 0);
            UpdateMajorGridLines(widthMeters, lengthMeters);
            UpdateGridAxes(widthMeters, lengthMeters);
        }

        private void AddGrid(double widthMeters, double lengthMeters)
        {
            if (_viewport == null) return;

            _grid = new GridLinesVisual3D
            {
                Width = ToLegacyGridUnits(widthMeters),
                Length = ToLegacyGridUnits(lengthMeters),
                MajorDistance = 10_000,
                MinorDistance = 5_000,
                Thickness = 0.75,
                Fill = new SolidColorBrush(Color.FromArgb(95, 150, 158, 168)),
                Center = ToLegacyGridPoint(_gridCenterX, _gridCenterY, 0),
                Transform = LegacyGridToMetersTransform
            };
            SetGridVisualAttached(_grid, _isGridVisible);
            AddMajorGridLines(widthMeters, lengthMeters);
            AddGridAxes(widthMeters, lengthMeters);
        }

        private void AddMajorGridLines(double widthMeters, double lengthMeters)
        {
            if (_viewport == null) return;

            _majorGridLines = new LinesVisual3D
            {
                Color = Color.FromArgb(155, 176, 185, 195),
                Thickness = 1.2,
                Transform = LegacyGridToMetersTransform
            };
            SetGridVisualAttached(_majorGridLines, _isGridVisible);
            UpdateMajorGridLines(widthMeters, lengthMeters);
        }

        private void AddGridAxes(double widthMeters, double lengthMeters)
        {
            if (_viewport == null) return;

            _xAxis = new LinesVisual3D { Color = Color.FromRgb(255, 120, 120), Thickness = 4, Transform = LegacyGridToMetersTransform };
            _yAxis = new LinesVisual3D { Color = Color.FromRgb(120, 230, 150), Thickness = 4, Transform = LegacyGridToMetersTransform };
            SetGridVisualAttached(_xAxis, _isGridVisible);
            SetGridVisualAttached(_yAxis, _isGridVisible);
            UpdateGridAxes(widthMeters, lengthMeters);
        }

        private void SetGridVisualAttached(Visual3D? visual, bool isAttached)
        {
            if (_viewport == null || visual == null)
                return;

            bool isCurrentlyAttached = _viewport.Children.Contains(visual);
            if (isAttached && !isCurrentlyAttached)
                _viewport.Children.Add(visual);
            else if (!isAttached && isCurrentlyAttached)
                _viewport.Children.Remove(visual);
        }

        private void UpdateMajorGridLines(double widthMeters, double lengthMeters)
        {
            if (_majorGridLines == null) return;

            var widthLegacy = ToLegacyGridUnits(widthMeters);
            var lengthLegacy = ToLegacyGridUnits(lengthMeters);
            var centerXLegacy = ToLegacyGridUnits(_gridCenterX);
            var centerYLegacy = ToLegacyGridUnits(_gridCenterY);
            var halfWidth = widthLegacy / 2.0;
            var halfLength = lengthLegacy / 2.0;
            var step = CalculateMajorGridStep(widthLegacy, lengthLegacy);
            const double z = 0.8;
            var points = new Point3DCollection();

            for (var x = RoundUpToStep(-halfWidth, step); x <= halfWidth + 0.001; x += step)
            {
                points.Add(new Point3D(centerXLegacy + x, centerYLegacy - halfLength, z));
                points.Add(new Point3D(centerXLegacy + x, centerYLegacy + halfLength, z));
            }

            for (var y = RoundUpToStep(-halfLength, step); y <= halfLength + 0.001; y += step)
            {
                points.Add(new Point3D(centerXLegacy - halfWidth, centerYLegacy + y, z));
                points.Add(new Point3D(centerXLegacy + halfWidth, centerYLegacy + y, z));
            }

            _majorGridLines.Points = points;
        }

        private static double CalculateMajorGridStep(double widthMeters, double lengthMeters)
        {
            const double baseStep = 10_000d;
            const double maxLinesPerAxis = 32d;
            var largestAxis = Math.Max(widthMeters, lengthMeters);
            var multiplier = Math.Max(1d, Math.Ceiling(largestAxis / (baseStep * maxLinesPerAxis)));
            return baseStep * multiplier;
        }

        private static double RoundUpToStep(double value, double step)
            => Math.Ceiling(value / step) * step;

        private void UpdateGridAxes(double widthMeters, double lengthMeters)
        {
            if (_xAxis == null || _yAxis == null) return;

            var widthLegacy = ToLegacyGridUnits(widthMeters);
            var lengthLegacy = ToLegacyGridUnits(lengthMeters);
            var centerXLegacy = ToLegacyGridUnits(_gridCenterX);
            var centerYLegacy = ToLegacyGridUnits(_gridCenterY);
            var halfWidth = widthLegacy / 2.0;
            var halfLength = lengthLegacy / 2.0;
            const double axisZ = 1.0;

            _xAxis.Points = new Point3DCollection
            {
                new(centerXLegacy - halfWidth, centerYLegacy, axisZ),
                new(centerXLegacy + halfWidth, centerYLegacy, axisZ)
            };

            _yAxis.Points = new Point3DCollection
            {
                new(centerXLegacy, centerYLegacy - halfLength, axisZ),
                new(centerXLegacy, centerYLegacy + halfLength, axisZ)
            };
        }

        private static double ToLegacyGridUnits(double meters) => meters * LegacyGridUnitsPerMeter;

        private static Point3D ToLegacyGridPoint(double xMeters, double yMeters, double zMeters) =>
            new(ToLegacyGridUnits(xMeters), ToLegacyGridUnits(yMeters), ToLegacyGridUnits(zMeters));

        private void SetupPhongLighting()
        {
            if (_viewport == null) return;

            var lg = new Model3DGroup();

            // Key light: warm, high and directional enough to produce crisp specular highlights.
            lg.Children.Add(CreateDirectionalLight(
                Color.FromRgb(255, 238, 205),
                new Vector3D(-0.45, -0.65, -0.62)));

            // Fill light: cool sky contribution that preserves form without flattening shadows.
            lg.Children.Add(CreateDirectionalLight(
                Color.FromRgb(100, 132, 178),
                new Vector3D(0.75, -0.25, -0.32)));

            // Rim light: separates the boom and counterweight edges from dense model views.
            lg.Children.Add(CreateDirectionalLight(
                Color.FromRgb(150, 160, 170),
                new Vector3D(0.25, 0.72, -0.18)));

            // Ambient floor: contributes the low, non-directional term in the Phong model.
            lg.Children.Add(new AmbientLight
            {
                Color = Color.FromRgb(42, 48, 58)
            });

            // Ground bounce: weak warm return light from the site plane.
            lg.Children.Add(CreateDirectionalLight(
                Color.FromRgb(70, 56, 38),
                new Vector3D(0.08, 0.15, 0.98)));

            _viewport.Children.Insert(0, new ModelVisual3D { Content = lg });
        }

        private static DirectionalLight CreateDirectionalLight(Color color, Vector3D direction)
        {
            if (direction.LengthSquared > 0)
                direction.Normalize();

            return new DirectionalLight
            {
                Color = color,
                Direction = direction
            };
        }

        // =========================
        // Render with Layer
        // =========================
        //public void Render<T>(IEnumerable<T> items, IVisualBuilder<T> builder, string layerKey)
        //{
        //    EnsureInitialized();

        //    ClearLayer(layerKey);

        //    var visuals = new List<Visual3D>();

        //    // Use the high-performance batch builder
        //    var batchVisual = builder.BuildBatch(items);
        //    _viewport!.Children.Add(batchVisual);
        //    visuals.Add(batchVisual);

        //    _layers[layerKey] = visuals;

        //    if (layerKey == "Elements")
        //    {
        //        _viewport!.UpdateLayout();
        //        _viewport.ZoomExtents();
        //    }
        //}

        //public void Render<T>(IEnumerable<T> items, IVisualBuilder<T> builder, string layerKey)
        //{
        //    EnsureInitialized();

        //    ClearLayer(layerKey);

        //    var visuals = new List<Visual3D>();

        //    foreach (var item in items)
        //    {
        //        var visual = builder.Build(item);
        //        // Tag visuals by layer so OSnap can selectively harvest meshes (e.g. IFC elements only).
        //        if (visual is ModelVisual3D mv)
        //            mv.SetValue(FrameworkElement.TagProperty, layerKey);
        //        _viewport!.Children.Add(visual);
        //        visuals.Add(visual);
        //    }

        //    _layers[layerKey] = visuals;

        //    if (layerKey == "Elements")
        //    {
        //        _viewport!.UpdateLayout();
        //        _viewport.ZoomExtents();
        //    }
        //}
        public void Render<T>(IEnumerable<T> items, IVisualBuilder<T> builder, string layerKey)
        {
            EnsureInitialized();

            ClearLayer(layerKey);

            var visuals = new List<Visual3D>();

            // Try batch first (for performance)
            var batchVisual = builder.BuildBatch(items);

            if (batchVisual != null)
            {
                // Tag the whole batch (fallback tagging)
                if (batchVisual is ModelVisual3D mv)
                    mv.SetValue(FrameworkElement.TagProperty, layerKey);

                _viewport!.Children.Add(batchVisual);
                visuals.Add(batchVisual);
            }
            else
            {
                // Fallback to per-item rendering (for features like OSnap)
                foreach (var item in items)
                {
                    var visual = builder.Build(item);

                    if (visual is ModelVisual3D mv)
                        mv.SetValue(FrameworkElement.TagProperty, layerKey);

                    _viewport!.Children.Add(visual);
                    visuals.Add(visual);
                }
            }

            _layers[layerKey] = visuals;

            if (layerKey == "Elements")
            {
                _viewport!.UpdateLayout();
            }
        }

        // =========================
        // Clear specific layer
        // =========================
        public void ClearLayer(string layerKey)
        {
            if (!_layers.ContainsKey(layerKey)) return;

            foreach (var visual in _layers[layerKey])
            {
                _viewport!.Children.Remove(visual);
            }

            _layers.Remove(layerKey);
        }

        // =========================
        // Clear all
        // =========================
        public void Clear()
        {
            EnsureInitialized();

            _viewport!.Children.Clear();
            _layers.Clear();

            SetupPhongLighting();
            AddGrid(DefaultGridSizeMeters, DefaultGridSizeMeters);
            _viewport.Children.Add(new CoordinateSystemVisual3D());
        }

        private void EnsureInitialized()
        {
            if (_viewport == null)
                throw new System.InvalidOperationException("RenderingService not initialized");
        }

        public HelixViewport3D? GetViewport() => _viewport;

        public void ZoomExtents()
        {
            if (_viewport != null)
            {
                _viewport.UpdateLayout();
                _viewport.ZoomExtents();
            }
        }
    }
}
