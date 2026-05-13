using CraneOptimizer.UI.Core.Interfaces;
using HelixToolkit.Wpf;
using System.Windows.Media;
using System.Windows;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Core.Services
{
    public class HelixRenderingService : IRenderingService
    {
        private const double DefaultGridSizeMillimeters = 50_000d;

        private HelixViewport3D? _viewport;
        private GridLinesVisual3D? _grid;
        private LinesVisual3D? _majorGridLines;
        private LinesVisual3D? _xAxis;
        private LinesVisual3D? _yAxis;
        private double _gridCenterX;
        private double _gridCenterY;

        private readonly Dictionary<string, List<Visual3D>> _layers = new();

        public void Initialize(HelixViewport3D viewport)
        {
            if (ReferenceEquals(_viewport, viewport)) return;

            _viewport = viewport;
            _layers.Clear();
            _viewport.Children.Clear();

            SetupPhongLighting();
            AddGrid(DefaultGridSizeMillimeters, DefaultGridSizeMillimeters);
            _viewport.Children.Add(new CoordinateSystemVisual3D());
        }

        public void SetGridSize(double widthMillimeters, double lengthMillimeters)
            => SetGridSize(widthMillimeters, lengthMillimeters, _gridCenterX, _gridCenterY);

        public void SetGridSize(
            double widthMillimeters,
            double lengthMillimeters,
            double centerXMillimeters,
            double centerYMillimeters)
        {
            EnsureInitialized();

            widthMillimeters = Math.Max(1_000d, widthMillimeters);
            lengthMillimeters = Math.Max(1_000d, lengthMillimeters);
            _gridCenterX = centerXMillimeters;
            _gridCenterY = centerYMillimeters;

            if (_grid == null)
            {
                AddGrid(widthMillimeters, lengthMillimeters);
                return;
            }

            _grid.Width = widthMillimeters;
            _grid.Length = lengthMillimeters;
            _grid.Center = new Point3D(_gridCenterX, _gridCenterY, 0);
            UpdateMajorGridLines(widthMillimeters, lengthMillimeters);
            UpdateGridAxes(widthMillimeters, lengthMillimeters);
        }

        private void AddGrid(double widthMillimeters, double lengthMillimeters)
        {
            if (_viewport == null) return;

            _grid = new GridLinesVisual3D
            {
                Width = widthMillimeters,
                Length = lengthMillimeters,
                MajorDistance = 10_000,
                MinorDistance = 5_000,
                Thickness = 0.75,
                Fill = new SolidColorBrush(Color.FromArgb(95, 150, 158, 168)),
                Center = new Point3D(_gridCenterX, _gridCenterY, 0)
            };
            _viewport.Children.Add(_grid);
            AddMajorGridLines(widthMillimeters, lengthMillimeters);
            AddGridAxes(widthMillimeters, lengthMillimeters);
        }

        private void AddMajorGridLines(double widthMillimeters, double lengthMillimeters)
        {
            if (_viewport == null) return;

            _majorGridLines = new LinesVisual3D
            {
                Color = Color.FromArgb(155, 176, 185, 195),
                Thickness = 1.2
            };
            _viewport.Children.Add(_majorGridLines);
            UpdateMajorGridLines(widthMillimeters, lengthMillimeters);
        }

        private void AddGridAxes(double widthMillimeters, double lengthMillimeters)
        {
            if (_viewport == null) return;

            _xAxis = new LinesVisual3D { Color = Color.FromRgb(255, 120, 120), Thickness = 4 };
            _yAxis = new LinesVisual3D { Color = Color.FromRgb(120, 230, 150), Thickness = 4 };
            _viewport.Children.Add(_xAxis);
            _viewport.Children.Add(_yAxis);
            UpdateGridAxes(widthMillimeters, lengthMillimeters);
        }

        private void UpdateMajorGridLines(double widthMillimeters, double lengthMillimeters)
        {
            if (_majorGridLines == null) return;

            var halfWidth = widthMillimeters / 2.0;
            var halfLength = lengthMillimeters / 2.0;
            var step = CalculateMajorGridStep(widthMillimeters, lengthMillimeters);
            const double z = 0.8;
            var points = new Point3DCollection();

            for (var x = RoundUpToStep(-halfWidth, step); x <= halfWidth + 0.001; x += step)
            {
                points.Add(new Point3D(_gridCenterX + x, _gridCenterY - halfLength, z));
                points.Add(new Point3D(_gridCenterX + x, _gridCenterY + halfLength, z));
            }

            for (var y = RoundUpToStep(-halfLength, step); y <= halfLength + 0.001; y += step)
            {
                points.Add(new Point3D(_gridCenterX - halfWidth, _gridCenterY + y, z));
                points.Add(new Point3D(_gridCenterX + halfWidth, _gridCenterY + y, z));
            }

            _majorGridLines.Points = points;
        }

        private static double CalculateMajorGridStep(double widthMillimeters, double lengthMillimeters)
        {
            const double baseStep = 10_000d;
            const double maxLinesPerAxis = 32d;
            var largestAxis = Math.Max(widthMillimeters, lengthMillimeters);
            var multiplier = Math.Max(1d, Math.Ceiling(largestAxis / (baseStep * maxLinesPerAxis)));
            return baseStep * multiplier;
        }

        private static double RoundUpToStep(double value, double step)
            => Math.Ceiling(value / step) * step;

        private void UpdateGridAxes(double widthMillimeters, double lengthMillimeters)
        {
            if (_xAxis == null || _yAxis == null) return;

            var halfWidth = widthMillimeters / 2.0;
            var halfLength = lengthMillimeters / 2.0;
            const double axisZ = 1.0;

            _xAxis.Points = new Point3DCollection
            {
                new(_gridCenterX - halfWidth, _gridCenterY, axisZ),
                new(_gridCenterX + halfWidth, _gridCenterY, axisZ)
            };

            _yAxis.Points = new Point3DCollection
            {
                new(_gridCenterX, _gridCenterY - halfLength, axisZ),
                new(_gridCenterX, _gridCenterY + halfLength, axisZ)
            };
        }

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
            AddGrid(DefaultGridSizeMillimeters, DefaultGridSizeMillimeters);
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
