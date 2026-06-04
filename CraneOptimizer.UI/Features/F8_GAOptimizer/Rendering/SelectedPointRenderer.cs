using CraneOptimizer.Domain.Common;
using CraneOptimizer.UI.Core.Interfaces;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Rendering
{
    internal static class SelectedPointRenderer
    {
        public const string PointLayer = "GA_SelectedPoint";
        private const string LegacyStatusOverlayLayer = "GA_StatusOverlay";

        private const double MarkerRadius = 0.2;
        private const double MarkerElevationOffset = 0.5;

        private static readonly BestPointVisualBuilder PointBuilder = new(MarkerRadius);

        /// <summary>Clears the blue sphere marker from the viewport.</summary>
        public static void Clear(IRenderingService renderingService)
        {
            renderingService.ClearLayer(PointLayer);
            renderingService.ClearLayer(LegacyStatusOverlayLayer);
        }

        public static void RenderPlacementPoint(IRenderingService renderingService, DomainPoint3D point)
        {
            RenderPoint(renderingService, point.X, point.Y, point.Z + MarkerElevationOffset);
        }

        public static void RenderPoint(
            IRenderingService renderingService,
            double x,
            double y,
            double z)
        {
            renderingService.ClearLayer(PointLayer);
            renderingService.Render(
                new[] { (x, y, z) },
                PointBuilder,
                PointLayer);
        }

        /// <summary>
        /// Renders the blue sphere marker. Element coloring is handled directly on the base layer.
        /// </summary>
        public static void Render(
            IRenderingService renderingService,
            DomainPoint3D? point)
        {
            Clear(renderingService);

            if (point == null)
                return;

            RenderPoint(renderingService, point.X, point.Y, point.Z + MarkerElevationOffset);
        }
    }
}
