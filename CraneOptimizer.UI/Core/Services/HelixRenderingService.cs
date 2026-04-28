using CraneOptimizer.UI.Core.Interfaces;
using HelixToolkit.Wpf;
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

            _viewport.Children.Add(new SunLight());
            _viewport.Children.Add(new GridLinesVisual3D { Width = 5000, Length = 5000 });
            _viewport.Children.Add(new CoordinateSystemVisual3D());
        }

        // =========================
        // Render with Layer
        // =========================
        public void Render<T>(IEnumerable<T> items, IVisualBuilder<T> builder, string layerKey)
        {
            EnsureInitialized();

            ClearLayer(layerKey);

            var visuals = new List<Visual3D>();

            foreach (var item in items)
            {
                var visual = builder.Build(item);
                _viewport!.Children.Add(visual);
                visuals.Add(visual);
            }

            _layers[layerKey] = visuals;
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

            _viewport.Children.Add(new SunLight());
            _viewport.Children.Add(new GridLinesVisual3D { Width = 200, Length = 200 });
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