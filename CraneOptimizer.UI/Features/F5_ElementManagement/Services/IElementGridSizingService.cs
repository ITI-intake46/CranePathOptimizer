using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.UI.Features.F5_ElementManagement.Services
{
    public interface IElementGridSizingService
    {
        (double widthMeters, double lengthMeters, double centerXMillimeters, double centerYMillimeters)
            CalculateGridSizeMeters(IReadOnlyList<PrecastElement> elements);
    }
}
