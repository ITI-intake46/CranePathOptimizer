using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F8_GAOptimizer;
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
        // Incremental Updates
        // =========================
        public void AddOrUpdate<T>(T item, IVisualBuilder<T> builder, string layerKey)
        {
            EnsureInitialized();

            if (!_layers.ContainsKey(layerKey))
            {
                _layers[layerKey] = new List<Visual3D>();
            }

            var layer = _layers[layerKey];
            var existing = layer.FirstOrDefault(v => v is ModelVisual3D mv && mv.GetValue(System.Windows.FrameworkElement.TagProperty)?.Equals(item) == true);

            if (existing != null)
            {
                _viewport!.Children.Remove(existing);
                layer.Remove(existing);
            }

            var visual = builder.Build(item);
            _viewport!.Children.Add(visual);
            layer.Add(visual);
        }

        public void Remove<T>(T item, string layerKey)
        {
            EnsureInitialized();

            if (!_layers.ContainsKey(layerKey)) return;

            var layer = _layers[layerKey];
            var existing = layer.FirstOrDefault(v => v is ModelVisual3D mv && mv.GetValue(System.Windows.FrameworkElement.TagProperty)?.Equals(item) == true);

            if (existing != null)
            {
                _viewport!.Children.Remove(existing);
                layer.Remove(existing);
            }
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

        // =========================
        // Selection & Highlighting
        // =========================
        private readonly Dictionary<PrecastElement, Material?> _originalMaterials = new();

        public void Highlight(PrecastElement element)
        {
            EnsureInitialized();
            ResetHighlight();

            foreach (var layer in _layers.Values)
            {
                foreach (var visual in layer)
                {
                    if (visual is ModelVisual3D mv && mv.Content is GeometryModel3D geo)
                    {
                        var tag = mv.GetValue(System.Windows.FrameworkElement.TagProperty) as PrecastElement;
                        if (tag == element)
                        {
                            _originalMaterials[element] = geo.Material;
                            geo.Material = MaterialHelper.CreateMaterial(System.Windows.Media.Colors.Yellow);
                            return;
                        }
                    }
                }
            }
        }

        public void ResetHighlight()
        {
            EnsureInitialized();

            if (_originalMaterials.Count == 0)
            {
                // Fallback: Ensure all elements are at least gray if somehow the dictionary is empty
                foreach (var layer in _layers.Values)
                {
                    foreach (var visual in layer)
                    {
                        if (visual is ModelVisual3D mv && mv.Content is GeometryModel3D geo)
                        {
                            var tag = mv.GetValue(System.Windows.FrameworkElement.TagProperty) as PrecastElement;
                            if (tag != null)
                            {
                                geo.Material = MaterialHelper.CreateMaterial(System.Windows.Media.Colors.Gray);
                            }
                        }
                    }
                }
                return;
            }

            foreach (var kvp in _originalMaterials)
            {
                var element = kvp.Key;
                var originalMat = kvp.Value;

                foreach (var layer in _layers.Values)
                {
                    foreach (var visual in layer)
                    {
                        if (visual is ModelVisual3D mv && mv.Content is GeometryModel3D geo)
                        {
                            var tag = mv.GetValue(System.Windows.FrameworkElement.TagProperty) as PrecastElement;
                            if (tag == element)
                            {
                                geo.Material = originalMat;
                            }
                        }
                    }
                }
            }
            _originalMaterials.Clear();
        }

        public void Focus(PrecastElement element)
        {
            if (_viewport == null) return;

            foreach (var layer in _layers.Values)
            {
                foreach (var visual in layer)
                {
                    if (visual is ModelVisual3D mv && mv.Content != null)
                    {
                        var tag = mv.GetValue(System.Windows.FrameworkElement.TagProperty) as PrecastElement;
                        if (tag == element)
                        {
                            _viewport.ZoomExtents(mv.Content.Bounds, 200);
                            return;
                        }
                    }
                }
            }
        }

        public PrecastElement? FindElementAt(Point3D point)
        {
            EnsureInitialized();

            foreach (var layer in _layers.Values)
            {
                foreach (var visual in layer)
                {
                    if (visual is ModelVisual3D mv && mv.Content != null)
                    {
                        var tag = mv.GetValue(System.Windows.FrameworkElement.TagProperty) as PrecastElement;
                        if (tag == null) continue;

                        var bounds = mv.Content.Bounds;
                        
                        // Check if point is within bounds (with small tolerance)
                        var inflated = new Rect3D(bounds.X - 0.5, bounds.Y - 0.5, bounds.Z - 0.5, 
                                                bounds.SizeX + 1.0, bounds.SizeY + 1.0, bounds.SizeZ + 1.0);

                        if (inflated.Contains(point))
                        {
                            return tag;
                        }
                    }
                }
            }

            return null;
        }

        public void RenderCranePreview(DomainPoint3D point, CraneConstraintResult? validation = null)
        {
            var color = System.Windows.Media.Colors.DeepSkyBlue; // Default
            if (validation != null)
            {
                color = validation.IsValid ? System.Windows.Media.Colors.LimeGreen : System.Windows.Media.Colors.Crimson;
            }

            Render(
                new[] { (point.X, point.Y, point.Z + 5.0) }, // Offset slightly to avoid Z-fighting
                new BestPointVisualBuilder(200.0, color), // Ghost crane with validation color
                "CranePreview"
            );
        }

        public void RenderCraneFinal(DomainPoint3D point)
        {
            Render(
                new[] { (point.X, point.Y, point.Z + 5.0) },
                new BestPointVisualBuilder(200.0, System.Windows.Media.Colors.Crimson), // Final crane
                "CraneFinal"
            );
        }

        public void ClearCranePreview()
        {
            ClearLayer("CranePreview");
        }
    }
}