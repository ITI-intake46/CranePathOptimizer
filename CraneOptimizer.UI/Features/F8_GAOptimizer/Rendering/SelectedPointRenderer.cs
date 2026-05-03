using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Interfaces;
using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;
using CraneOptimizer.UI.Core.Models;
using CraneOptimizer.UI.Features.F2_IfcViewer;
using CraneOptimizer.UI.Features.F3_CraneSetup;
using CraneOptimizer.UI.Features.F8_GAOptimizer.DTOs;

namespace CraneOptimizer.UI.Features.F8_GAOptimizer.Rendering
{
    internal static class SelectedPointRenderer
    {
        public const string PointLayer = "GA_SelectedPoint";
        public const string CranePreviewLayer = "GA_CranePreview";

        private const double MarkerRadius = 200.0;
        private const double MarkerElevationOffset = 500.0;

        private static readonly BestPointVisualBuilder PointBuilder = new(MarkerRadius);

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

        // ── Crane Preview ────────────────────────────────────────────────────

        /// <summary>
        /// Renders a crane preview at the specified GA candidate point.
        /// Uses a dedicated layer so it can be cleared without affecting
        /// the element colors or sphere marker.
        /// </summary>
        public static void RenderCranePreview(
            IRenderingService renderingService,
            Crane crane,
            CranePositionResult point)
        {
            ClearCranePreview(renderingService);

            var activeSpec = crane.Specifications
                ?.OrderByDescending(s => s.MaxRadius)
                .FirstOrDefault();

            var sceneCrane = new SceneCrane
            {
                CraneDefinition = crane,
                ActiveSpecification = activeSpec,
                Position = new DomainPoint3D(point.X, point.Y, 0),
                Rotation = 0,
                PivotOffset = new DomainPoint3D(0, 0, Math.Max(1.2, crane.VehicleHeight)),
                BoomLength = activeSpec != null
                    ? Math.Clamp(activeSpec.MaxBoomLength * 0.6, activeSpec.MinBoomLength, activeSpec.MaxBoomLength)
                    : 20,
                BoomAngle = 45,
                IsValidPlacement = true
            };

            renderingService.Render(
                new[] { sceneCrane },
                new CraneVisualBuilder(),
                CranePreviewLayer);
        }

        /// <summary>Removes the GA crane-preview from the viewport.</summary>
        public static void ClearCranePreview(IRenderingService renderingService)
        {
            renderingService.ClearLayer(CranePreviewLayer);
        }
    }
}
