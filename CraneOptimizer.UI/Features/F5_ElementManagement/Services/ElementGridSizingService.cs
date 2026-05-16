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

            var minX = double.MaxValue;
            var minY = double.MaxValue;
            var maxX = double.MinValue;
            var maxY = double.MinValue;

            foreach (var element in elements)
            {
                var cog = element.ElementCOG;
                if (!IsFinite(cog.X) || !IsFinite(cog.Y))
                    continue;

                var halfLength = GetHalfExtent(element.BBoxLength);
                var halfWidth = GetHalfExtent(element.BBoxWidth);
                minX = Math.Min(minX, cog.X - halfLength);
                maxX = Math.Max(maxX, cog.X + halfLength);
                minY = Math.Min(minY, cog.Y - halfWidth);
                maxY = Math.Max(maxY, cog.Y + halfWidth);
            }

            if (minX == double.MaxValue)
                return (DefaultGridSizeMeters, DefaultGridSizeMeters, 0d, 0d);

            var buildingWidthMeters = Math.Max(0d, (maxX - minX) / 1000.0);
            var buildingLengthMeters = Math.Max(0d, (maxY - minY) / 1000.0);
            var centerXMillimeters = (minX + maxX) * 0.5;
            var centerYMillimeters = (minY + maxY) * 0.5;

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

        private static double GetHalfExtent(double? value)
        {
            if (!value.HasValue || !IsFinite(value.Value) || value.Value <= 0)
                return 0d;

            return value.Value * 0.5;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
