using HelixToolkit.Wpf;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface IRenderingService
    {
        void Initialize(HelixViewport3D viewport);

        void Render<T>(IEnumerable<T> items, IVisualBuilder<T> builder, string layerName);

        void Clear();

        void ClearLayer(string layerKey);

        void SetGridSize(double widthMillimeters, double lengthMillimeters);

        void SetGridSize(double widthMillimeters, double lengthMillimeters, double centerXMillimeters, double centerYMillimeters);


        // NEW — needed for mouse coordinate conversion during drag
        HelixViewport3D? GetViewport();

        void ZoomExtents();
    }
}
