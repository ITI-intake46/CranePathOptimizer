using CraneOptimizer.UI.Core.Interfaces;
using HelixToolkit.Wpf;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Core.Services
{
    public class HelixRenderingService : IRenderingService
    {
        private HelixViewport3D? _viewport;

        private readonly Dictionary<string, List<Visual3D>> _layers = new();

        public void Initialize(HelixViewport3D viewport)
        {
            if (_viewport != null) return;

            _viewport = viewport;
            _viewport.Children.Clear();

            SetupUltraLighting();
            _viewport.Children.Add(new GridLinesVisual3D { Width = 5000, Length = 5000 });
            _viewport.Children.Add(new CoordinateSystemVisual3D());
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
        public void Render<T>(IEnumerable<T> items, IVisualBuilder<T> builder, string layerKey)
        {
            EnsureInitialized();

            ClearLayer(layerKey);

            var visuals = new List<Visual3D>();

            // Use the high-performance batch builder
            var batchVisual = builder.BuildBatch(items);
            _viewport!.Children.Add(batchVisual);
            visuals.Add(batchVisual);

            _layers[layerKey] = visuals;

            if (layerKey == "Elements")
            {
                _viewport!.UpdateLayout();
                _viewport.ZoomExtents();
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
            _viewport.Children.Add(new GridLinesVisual3D { Width = 5000, Length = 5000 });
            _viewport.Children.Add(new CoordinateSystemVisual3D());
        }

        private void EnsureInitialized()
        {
            if (_viewport == null)
                throw new System.InvalidOperationException("RenderingService not initialized");
        }

        public HelixViewport3D? GetViewport() => _viewport;

    }
}
