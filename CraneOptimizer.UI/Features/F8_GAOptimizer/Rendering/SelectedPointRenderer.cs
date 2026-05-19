using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Rendering
{
    internal static class SelectedPointRenderer
    {
        public const string PointLayer = "GA_SelectedPoint";
        public const string StatusOverlayLayer = "GA_StatusOverlay";

        private const double MarkerRadius = 0.2;
        private const double MarkerElevationOffset = 0.5;

        private static readonly BestPointVisualBuilder PointBuilder = new(MarkerRadius);

        /// <summary>Clears the blue sphere marker from the viewport.</summary>
        public static void Clear(IRenderingService renderingService)
        {
            renderingService.ClearLayer(PointLayer);
            renderingService.ClearLayer(StatusOverlayLayer);
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

        public static void Render(
            IRenderingService renderingService,
            ISceneService sceneService,
            CranePositionResult? point)
        {
            Render(renderingService, sceneService.GetElements(), point);
        }

        public static void Render(
            IRenderingService renderingService,
            IEnumerable<PrecastElement>? elements,
            CranePositionResult? point)
        {
            Render(renderingService, elements, point, null, null, null);
        }

        /// <summary>
        /// Renders the blue sphere marker and a lightweight status overlay.
        /// The base IFC mesh is intentionally left intact to avoid rebuilding
        /// heavy geometry every time the selected GA point changes.
        /// </summary>
        public static void Render(
            IRenderingService renderingService,
            IEnumerable<PrecastElement>? elements,
            CranePositionResult? point,
            IIfcReaderRepository? ifcReader,
            DomainPoint3D? origin,
            DomainPoint3D? placementOffset)
        {
            Clear(renderingService);

            if (point == null)
                return;

            RenderPoint(renderingService, point.X, point.Y, MarkerElevationOffset);

            if (elements == null || ifcReader == null)
                return;

            var statusMap = point.ElementResults
                .Where(result => !string.IsNullOrWhiteSpace(result.ElementId))
                .GroupBy(result => result.ElementId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last().Status, StringComparer.OrdinalIgnoreCase);

            var elementsWithStatus = elements.Select(element =>
            {
                LiftStatus? status = null;
                if (statusMap.TryGetValue(element.ElementID, out var s))
                    status = s;
  
                return (element, status, false);
            });

            var previousQuality = ElementVisualBuilder.CurrentQuality;
            try
            {
                ElementVisualBuilder.CurrentQuality = ElementVisualBuilder.MeshQuality.Low;
                // Issue 3 fix: pass null for placementOffset because elements
                // from the scene have already had the placementOffset applied
                // to their ElementCOG.
                renderingService.Render(
                    elementsWithStatus,
                    new ElementVisualBuilder(null, origin, null, renderOnlyStatusElements: true, statusAlpha: 125),
                    StatusOverlayLayer);
            }
            finally
            {
                ElementVisualBuilder.CurrentQuality = previousQuality;
            }
        }
    }
}
