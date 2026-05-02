using CraneOptimizer.Domain.Schemas;

namespace CraneOptimizer.UI.Features.F5_ElementManagement.Services
{
    public sealed class ElementGridSizingService : IElementGridSizingService
    {
        private const double DefaultGridSizeMeters = 50d;
        private const double GridPaddingFactor = 1.2d;
        private const double GridRoundStepMeters = 10d;

        public (double widthMeters, double lengthMeters, double centerXMillimeters, double centerYMillimeters)
            CalculateGridSizeMeters(IReadOnlyList<PrecastElement> elements)
        {
            if (elements.Count == 0)
                return (DefaultGridSizeMeters, DefaultGridSizeMeters, 0d, 0d);

            var minX = elements.Min(e => e.ElementCOG.X - ((e.BBoxLength ?? 0d) / 2d));
            var maxX = elements.Max(e => e.ElementCOG.X + ((e.BBoxLength ?? 0d) / 2d));
            var minY = elements.Min(e => e.ElementCOG.Y - ((e.BBoxWidth ?? 0d) / 2d));
            var maxY = elements.Max(e => e.ElementCOG.Y + ((e.BBoxWidth ?? 0d) / 2d));

            var buildingWidthMeters = Math.Max(0d, (maxX - minX) / 1000.0);
            var buildingLengthMeters = Math.Max(0d, (maxY - minY) / 1000.0);
            var centerXMillimeters = (minX + maxX) / 2.0;
            var centerYMillimeters = (minY + maxY) / 2.0;

            return (
                CalculateGridAxisMeters(buildingWidthMeters),
                CalculateGridAxisMeters(buildingLengthMeters),
                centerXMillimeters,
                centerYMillimeters);
        }

        private static double CalculateGridAxisMeters(double buildingAxisMeters)
        {
            if (buildingAxisMeters <= DefaultGridSizeMeters)
                return DefaultGridSizeMeters;

            var padded = buildingAxisMeters * GridPaddingFactor;
            return Math.Ceiling(padded / GridRoundStepMeters) * GridRoundStepMeters;
        }
    }
}
