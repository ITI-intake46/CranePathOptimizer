using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Models;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Schemas;
using HelixToolkit.Wpf;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface IRenderingService
    {
        void Initialize(HelixViewport3D viewport);

        // =========================
        // Elements
        // =========================

        // Basic rendering (no GA)
        void RenderElements(IEnumerable<PrecastElement> elements);

        // Advanced rendering (with GA result)
        void RenderElementsWithStatus(
            IEnumerable<(PrecastElement element, LiftStatus status)> elements);

        // =========================
        // Zones
        // =========================
        void RenderZone(GreenAreaZone zone);

        // =========================
        // Crane
        // =========================
        void RenderCrane(Crane crane);

        // =========================
        // GA Result
        // =========================
        void RenderBestPosition(double x, double y);

        // =========================
        // Drawing
        // =========================
        void UpdatePreview(List<DomainPoint3D> points);
        void ClearPreview();

        // =========================
        // Scene Control
        // =========================
        void ClearScene();
    }
}