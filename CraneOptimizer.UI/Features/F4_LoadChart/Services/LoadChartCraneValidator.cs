using CraneOptimizer.UI.Features.F4_LoadChart.DTOs;

namespace CraneOptimizer.UI.Features.F4_LoadChart.Services
{
    internal static class LoadChartCraneValidator
    {
        public const string OnRubberConfiguration = "On Rubber";

        public static bool IsOnRubber(string? outriggerConfiguration)
        {
            return string.Equals(
                outriggerConfiguration?.Trim(),
                OnRubberConfiguration,
                StringComparison.OrdinalIgnoreCase);
        }

        public static void ValidateForSave(LoadChartCraneDto crane)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(crane.Name))
                errors.Add("Model name is required.");

            if (string.IsNullOrWhiteSpace(crane.Manufacturer))
                errors.Add("Manufacturer is required.");

            if (!IsFiniteNonNegative(crane.Counterweight))
                errors.Add("Counterweight must be a valid non-negative value.");

            ValidatePositiveDimension(crane.VehicleLengthMeters, "Total length", errors);
            ValidatePositiveDimension(crane.VehicleWidthMeters, "Travel width", errors);
            ValidatePositiveDimension(crane.VehicleHeightMeters, "Transport height", errors);

            if (!IsOnRubber(crane.OutriggerConfig))
            {
                ValidatePositiveDimension(crane.OutriggerWidthMeters, "Outrigger span", errors);
                ValidatePositiveDimension(crane.OutriggerLengthMeters, "Front-to-rear outrigger span", errors);
            }

            if (crane.Points.Count == 0)
                errors.Add("Load chart data is required.");

            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        private static void ValidatePositiveDimension(double? value, string fieldName, ICollection<string> errors)
        {
            if (!value.HasValue || !double.IsFinite(value.Value) || value.Value <= 0d)
                errors.Add($"{fieldName} is required and must be greater than zero.");
        }

        private static bool IsFiniteNonNegative(double value)
        {
            return double.IsFinite(value) && value >= 0d;
        }
    }
}
