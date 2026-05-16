using CraneOptimizer.Domain.Schemas;
using CraneOptimizer.UI.Core.Interfaces;

namespace CraneOptimizer.UI.Core.Services
{
    public sealed class SceneGridSizingService : ISceneGridSizingService
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

            // The grid is a site/world reference, so auto sizing must not re-center it
            // around the inserted IFC. Grow around world origin instead.
            var maxAbsX = Math.Max(Math.Abs(minX), Math.Abs(maxX));
            var maxAbsY = Math.Max(Math.Abs(minY), Math.Abs(maxY));
            var buildingWidthMeters = Math.Max(0d, (maxAbsX * 2d) / 1000.0);
            var buildingLengthMeters = Math.Max(0d, (maxAbsY * 2d) / 1000.0);

            return (
                CalculateGridAxisMeters(buildingWidthMeters),
                CalculateGridAxisMeters(buildingLengthMeters),
                0d,
                0d);
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
