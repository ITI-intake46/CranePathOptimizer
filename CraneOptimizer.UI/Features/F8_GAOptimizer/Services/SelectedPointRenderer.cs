using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Services
{
    internal static class SelectedPointRenderer
    {
        public const string PointLayer = "GA_SelectedPoint";
        public const string ElementsLayer = "Elements";
        private const double MarkerRadius = 200.0;
        private const double MarkerZ = 500.0;

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
            renderingService.ClearLayer(PointLayer);
            renderingService.ClearLayer(ElementsLayer);

            if (point == null)
                return;

            renderingService.Render(
                new[] { (point.X, point.Y, MarkerZ) },
                new BestPointVisualBuilder(MarkerRadius),
                PointLayer
            );

            if (elements == null)
                return;

            var statusMap = point.ElementResults
                .ToDictionary(e => e.ElementId, e => e.Status);

            var elementsWithStatus = elements.Select(element =>
            {
                if (statusMap.TryGetValue(element.ElementID, out var status))
                    return (element, (LiftStatus?)status);

                return (element, (LiftStatus?)null);
            });

            renderingService.Render(
                elementsWithStatus,
                new ElementVisualBuilder(),
                ElementsLayer
            );
        }
    }
}
