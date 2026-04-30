using CraneOptimizer.UI.Core.Interfaces;
using HelixToolkit.Wpf;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Core.Services
{
    public class HelixRenderingService : IRenderingService
    {
        private HelixViewport3D? _viewport;

        private readonly Dictionary<string, List<Visual3D>> _layers = new();
        private readonly Dictionary<string, Dictionary<string, Visual3D>> _keyedLayers = new();

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
            if (_layers.TryGetValue(layerKey, out var visuals))
            {
                foreach (var visual in visuals)
                    _viewport!.Children.Remove(visual);

                _layers.Remove(layerKey);
            }

            if (_keyedLayers.TryGetValue(layerKey, out var keyedVisuals))
            {
                foreach (var visual in keyedVisuals.Values)
                    _viewport!.Children.Remove(visual);

                _keyedLayers.Remove(layerKey);
            }
        }

        public void AddOrUpdateVisual(string layerKey, string visualKey, Visual3D visual)
        {
            EnsureInitialized();

            if (!_keyedLayers.TryGetValue(layerKey, out var keyedVisuals))
            {
                keyedVisuals = new Dictionary<string, Visual3D>();
                _keyedLayers[layerKey] = keyedVisuals;
            }

            if (keyedVisuals.TryGetValue(visualKey, out var existing))
                _viewport!.Children.Remove(existing);

            keyedVisuals[visualKey] = visual;
            _viewport!.Children.Add(visual);
        }

        public void RemoveVisual(string layerKey, string visualKey)
        {
            EnsureInitialized();

            if (!_keyedLayers.TryGetValue(layerKey, out var keyedVisuals))
                return;

            if (!keyedVisuals.TryGetValue(visualKey, out var visual))
                return;

            _viewport!.Children.Remove(visual);
            keyedVisuals.Remove(visualKey);

            if (keyedVisuals.Count == 0)
                _keyedLayers.Remove(layerKey);
        }

        public void UpdateVisualTransform(string layerKey, string visualKey, Transform3D transform)
        {
            EnsureInitialized();

            if (!_keyedLayers.TryGetValue(layerKey, out var keyedVisuals))
                return;

            if (!keyedVisuals.TryGetValue(visualKey, out var visual))
                return;

            visual.Transform = transform;
        }

        // =========================
        // Clear all
        // =========================
        public void Clear()
        {
            EnsureInitialized();

            _viewport!.Children.Clear();
            _layers.Clear();
            _keyedLayers.Clear();

            _viewport.Children.Add(new SunLight());
            _viewport.Children.Add(new GridLinesVisual3D { Width = 200, Length = 200 });
            _viewport.Children.Add(new CoordinateSystemVisual3D());
        }

        private void EnsureInitialized()
        {
            if (_viewport == null)
                throw new System.InvalidOperationException("RenderingService not initialized");
        }
    }
}
