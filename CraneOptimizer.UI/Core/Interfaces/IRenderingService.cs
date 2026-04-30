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

        void AddOrUpdateVisual(string layerKey, string visualKey, Visual3D visual);

        void UpdateVisualTransform(string layerKey, string visualKey, Transform3D transform);

        void RemoveVisual(string layerKey, string visualKey);
    }
}
