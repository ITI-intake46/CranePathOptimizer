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
        private LinesVisual3D? _xAxis;
        private LinesVisual3D? _yAxis;
        private PointsVisual3D? _gridIntersections;
        private double _gridCenterX;
        private double _gridCenterY;

        private readonly Dictionary<string, List<Visual3D>> _layers = new();

        public void Initialize(HelixViewport3D viewport)
        {
            if (_viewport != null) return;

            _viewport = viewport;
            _viewport.Children.Clear();

            SetupUltraLighting();
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
            UpdateGridAxes(widthMillimeters, lengthMillimeters);
            UpdateGridIntersections(widthMillimeters, lengthMillimeters);
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
                Thickness = 1.2,
                Fill = new SolidColorBrush(Color.FromArgb(180, 150, 150, 150)),
                Center = new Point3D(_gridCenterX, _gridCenterY, 0)
            };
            _viewport.Children.Add(_grid);
            AddGridIntersections(widthMillimeters, lengthMillimeters);
            AddGridAxes(widthMillimeters, lengthMillimeters);
        }

        private void AddGridIntersections(double widthMillimeters, double lengthMillimeters)
        {
            if (_viewport == null) return;

            _gridIntersections = new PointsVisual3D
            {
                Color = Color.FromRgb(105, 105, 105),
                Size = 6
            };
            _viewport.Children.Add(_gridIntersections);
            UpdateGridIntersections(widthMillimeters, lengthMillimeters);
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

        private void UpdateGridIntersections(double widthMillimeters, double lengthMillimeters)
        {
            if (_gridIntersections == null) return;

            var halfWidth = widthMillimeters / 2.0;
            var halfLength = lengthMillimeters / 2.0;
            var step = CalculateIntersectionStep(widthMillimeters, lengthMillimeters);
            const double dotZ = 2.0;
            var points = new Point3DCollection();

            for (var x = -halfWidth; x <= halfWidth + 0.001; x += step)
            {
                for (var y = -halfLength; y <= halfLength + 0.001; y += step)
                {
                    points.Add(new Point3D(_gridCenterX + x, _gridCenterY + y, dotZ));
                }
            }

            _gridIntersections.Points = points;
        }

        private static double CalculateIntersectionStep(double widthMillimeters, double lengthMillimeters)
        {
            const double baseStep = 5_000d;
            const double maxLinesPerAxis = 45d;
            var largestAxis = Math.Max(widthMillimeters, lengthMillimeters);
            var multiplier = Math.Max(1d, Math.Ceiling(largestAxis / (baseStep * maxLinesPerAxis)));
            return baseStep * multiplier;
        }

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

        private void SetupUltraLighting()
        {
            if (_viewport == null) return;

            var lg = new Model3DGroup();

            // 1 — KEY: warm afternoon sun, upper-right-front
            lg.Children.Add(new DirectionalLight
            {
                Color = Color.FromRgb(255, 242, 210),
                Direction = new Vector3D(-0.55, -0.75, -0.45)
            });

            // 2 — FILL: cool open sky, upper-left, ~38% of key
            lg.Children.Add(new DirectionalLight
            {
                Color = Color.FromRgb(130, 165, 215),
                Direction = new Vector3D(0.75, -0.55, 0.35)
            });

            // 3 — RIM: neutral back-separation, behind & slightly below
            lg.Children.Add(new DirectionalLight
            {
                Color = Color.FromRgb(170, 178, 185),
                Direction = new Vector3D(0.15, 0.35, 0.92)
            });

            // 4 — TOP-DOWN: simulates overcast sky dome contribution
            lg.Children.Add(new DirectionalLight
            {
                Color = Color.FromRgb(95, 115, 145),
                Direction = new Vector3D(0.0, -1.0, 0.0)
            });

            // 5 — AMBIENT: absolute minimum so no face is pure black
            lg.Children.Add(new AmbientLight
            {
                Color = Color.FromRgb(38, 46, 58)
            });

            // 6 — GROUND BOUNCE: warm floor reflection
            lg.Children.Add(new DirectionalLight
            {
                Color = Color.FromRgb(90, 72, 48),
                Direction = new Vector3D(0.05, 1.0, 0.05)
            });

            // 7 — SECONDARY BOUNCE: subtle side warm-orange (interior walls)
            lg.Children.Add(new DirectionalLight
            {
                Color = Color.FromRgb(60, 48, 35),
                Direction = new Vector3D(-1.0, 0.2, 0.0)
            });

            _viewport.Children.Insert(0, new ModelVisual3D { Content = lg });
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

            SetupUltraLighting();
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
