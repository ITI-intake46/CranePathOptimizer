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

        private const double MarkerRadius = 200.0;
        private const double MarkerElevationOffset = 500.0;

        private static readonly BestPointVisualBuilder PointBuilder = new(MarkerRadius);

        /// <summary>Clears the blue sphere marker from the viewport.</summary>
        public static void Clear(IRenderingService renderingService)
        {
            renderingService.ClearLayer(PointLayer);
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
        /// Renders the blue sphere marker at the point location and
        /// re-renders all IFC elements with their actual GA LiftStatus colors.
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
                .ToDictionary(result => result.ElementId, result => result.Status);

            var elementsWithStatus = elements.Select(element =>
            {
                LiftStatus? status = null;
                if (statusMap.TryGetValue(element.ElementID, out var s))
                    status = s;
 
                return (element, status, false);
            });
 
            renderingService.Render(
                elementsWithStatus,
                new ElementVisualBuilder(ifcReader, origin, placementOffset),
                "Elements");
        }
    }
}
