using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.UI.Core.Interfaces
{
    public interface ISceneGridSizingService
    {
        (double widthMeters, double lengthMeters, double centerXMeters, double centerYMeters)
            CalculateGridSizeMeters(IReadOnlyList<PrecastElement> elements);
    }
}
