using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface ISceneGridSizingService
    {
        (double widthMeters, double lengthMeters, double centerXMillimeters, double centerYMillimeters)
            CalculateGridSizeMeters(IReadOnlyList<PrecastElement> elements);
    }
}
