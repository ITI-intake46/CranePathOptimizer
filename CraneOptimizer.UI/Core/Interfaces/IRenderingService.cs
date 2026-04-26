using CraneOptimizer.Application.Optimization.GeneticAlgorithms.Services;
using CraneOptimizer.Domain.Common;
using CraneOptimizer.Domain.Schemas;
using HelixToolkit.Wpf;
using System.Windows.Media.Media3D;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface IRenderingService
    {
        void Initialize(HelixViewport3D viewport);

        void Render<T>(IEnumerable<T> items, IVisualBuilder<T> builder, string layerName);

        void Clear();

        void ClearLayer(string layerKey);

        void AddOrUpdate<T>(T item, IVisualBuilder<T> builder, string layerKey);

        void Remove<T>(T item, string layerKey);

        void Highlight(PrecastElement element);
        void ResetHighlight();
        void Focus(PrecastElement element);
        PrecastElement? FindElementAt(Point3D point);

        void RenderCranePreview(DomainPoint3D point, CraneConstraintResult? validation = null);
        void RenderCraneFinal(DomainPoint3D point);
        void ClearCranePreview();
    }
}