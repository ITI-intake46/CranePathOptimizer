using HelixToolkit.Wpf;

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
    }
}