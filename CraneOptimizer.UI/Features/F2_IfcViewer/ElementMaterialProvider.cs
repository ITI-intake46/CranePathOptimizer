using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.UI.Features.F2_IfcViewer
{
    /// <summary>
    /// Centralized provider for element colors and materials.
    /// Manages the active coloring strategy and handles caching of frozen materials.
    /// </summary>
    public static class ElementMaterialProvider
    {
        private static readonly Dictionary<ElementVisualizationStatus, Material> MaterialCache = new();
        private static readonly Dictionary<ElementVisualizationStatus, Material> HighSpecularMaterialCache = new();
        private static readonly Dictionary<ElementVisualizationStatus, Material> LowQualityMaterialCache = new();
        private static readonly object LockObject = new();

        private static IElementColorStrategy _activeStrategy = new LiftabilityColorStrategy();

        /// <summary>
        /// Gets or sets the active visualization color strategy.
        /// </summary>
        public static IElementColorStrategy ActiveStrategy
        {
            get => _activeStrategy;
            set
            {
                lock (LockObject)
                {
                    _activeStrategy = value ?? new LiftabilityColorStrategy();
                    ClearCache();
                }
            }
        }

        /// <summary>
        /// Clears all cached materials.
        /// </summary>
        public static void ClearCache()
        {
            lock (LockObject)
            {
                MaterialCache.Clear();
                HighSpecularMaterialCache.Clear();
                LowQualityMaterialCache.Clear();
            }
        }

        /// <summary>
        /// Gets the BIM-compliant color for the specified visualization status.
        /// </summary>
        public static Color GetColor(ElementVisualizationStatus status)
        {
            return status switch
            {
                ElementVisualizationStatus.Default => Color.FromRgb(184, 184, 184),             // Concrete Gray #B8B8B8
                ElementVisualizationStatus.Liftable => Color.FromRgb(0, 158, 115),              // Calm Green #009E73
                ElementVisualizationStatus.Overweight => Color.FromRgb(213, 94, 0),             // Vermillion Red #D55E00
                ElementVisualizationStatus.OutOfRadius => Color.FromRgb(230, 159, 0),            // Calm Orange #E69F00
                ElementVisualizationStatus.HeightIssue => Color.FromRgb(142, 106, 184),           // Muted Purple #8E6AB8
                ElementVisualizationStatus.Selected => Color.FromRgb(78, 161, 243),             // Soft Blue #4EA1F3
                ElementVisualizationStatus.SpecialCase => Color.FromRgb(139, 139, 139),          // Neutral Gray #8B8B8B
                ElementVisualizationStatus.ChartDataIssue => Color.FromRgb(100, 116, 139),       // Muted Gray-Blue #64748B
                ElementVisualizationStatus.AssignedToOtherPoint => Color.FromRgb(156, 163, 175), // Dimmed Gray #9CA3AF
                _ => Color.FromRgb(184, 184, 184)
            };
        }

        /// <summary>
        /// Resolves the visualization status for an element using the active strategy.
        /// </summary>
        public static ElementVisualizationStatus GetStatus(PrecastElement element, LiftStatus? liftStatus, bool isSelected)
        {
            return _activeStrategy.GetStatus(element, liftStatus, isSelected);
        }

        /// <summary>
        /// Retrieves or creates a cached, frozen material for the specified status and mesh quality.
        /// </summary>
        public static Material GetMaterial(ElementVisualizationStatus status, ElementVisualBuilder.MeshQuality quality)
        {
            lock (LockObject)
            {
                var cache = quality switch
                {
                    ElementVisualBuilder.MeshQuality.High => HighSpecularMaterialCache,
                    ElementVisualBuilder.MeshQuality.Low => LowQualityMaterialCache,
                    _ => MaterialCache
                };

                if (cache.TryGetValue(status, out var material))
                {
                    return material;
                }

                var baseColor = GetColor(status);
                var mg = new MaterialGroup();
                mg.Children.Add(CreateFrozenDiffuseMaterial(baseColor));

                if (quality != ElementVisualBuilder.MeshQuality.Low)
                {
                    var specPower = quality == ElementVisualBuilder.MeshQuality.High ? 90.0 : 35.0;
                    var specularBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255));
                    specularBrush.Freeze();
                    mg.Children.Add(new SpecularMaterial(specularBrush, specPower));
                }

                mg.Freeze();
                cache[status] = mg;
                return mg;
            }
        }

        private static DiffuseMaterial CreateFrozenDiffuseMaterial(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            var material = new DiffuseMaterial(brush);
            material.Freeze();
            return material;
        }
    }
}
