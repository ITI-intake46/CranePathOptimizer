using HelixToolkit.Wpf;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface IRenderingService
    {
        void Initialize(HelixViewport3D viewport);

        void Render<T>(IEnumerable<T> items, IVisualBuilder<T> builder, string layerName);

        void Clear();

        void ClearLayer(string layerKey);

        void SetGridSize(double widthMeters, double lengthMeters);

        void SetGridSize(double widthMeters, double lengthMeters, double centerXMeters, double centerYMeters);

        bool IsGridVisible { get; }

        void SetGridVisible(bool isVisible);


        // NEW — needed for mouse coordinate conversion during drag
        HelixViewport3D? GetViewport();

        void ZoomExtents();
    }
}
