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

            ValidatePositiveDimension(crane.MinRadius, "Minimum operating radius", errors);
            ValidatePositiveDimension(crane.MaxRadius, "Maximum operating radius", errors);
            ValidatePositiveDimension(crane.MinBoomLength, "Minimum boom length", errors);
            ValidatePositiveDimension(crane.MaxBoomLength, "Maximum boom length", errors);
            ValidatePositiveDimension(crane.MaxCapacity, "Maximum capacity", errors);

            if (double.IsFinite(crane.MinRadius) && double.IsFinite(crane.MaxRadius) && crane.MinRadius > crane.MaxRadius)
                errors.Add("Minimum operating radius cannot exceed maximum operating radius.");

            if (double.IsFinite(crane.MinBoomLength) && double.IsFinite(crane.MaxBoomLength) && crane.MinBoomLength > crane.MaxBoomLength)
                errors.Add("Minimum boom length cannot exceed maximum boom length.");

            if (crane.Points.Count == 0)
            {
                errors.Add("Load chart data is required.");
            }
            else
            {
                for (int i = 0; i < crane.Points.Count; i++)
                {
                    var point = crane.Points[i];
                    int row = i + 1;

                    if (!double.IsFinite(point.Radius) || point.Radius <= 0d)
                        errors.Add($"Load chart point {row}: radius must be finite and greater than zero.");

                    if (!double.IsFinite(point.BoomLength) || point.BoomLength <= 0d)
                        errors.Add($"Load chart point {row}: boom length must be finite and greater than zero.");

                    if (!double.IsFinite(point.Capacity) || point.Capacity <= 0d)
                        errors.Add($"Load chart point {row}: capacity must be finite and greater than zero.");

                    if (double.IsFinite(point.Radius) && double.IsFinite(crane.MinRadius) && point.Radius < crane.MinRadius)
                        errors.Add($"Load chart point {row}: radius is below the crane minimum operating radius.");

                    if (double.IsFinite(point.Radius) && double.IsFinite(crane.MaxRadius) && point.Radius > crane.MaxRadius)
                        errors.Add($"Load chart point {row}: radius exceeds the crane maximum operating radius.");

                    if (double.IsFinite(point.BoomLength) && double.IsFinite(crane.MinBoomLength) && point.BoomLength < crane.MinBoomLength)
                        errors.Add($"Load chart point {row}: boom length is below the crane minimum boom length.");

                    if (double.IsFinite(point.BoomLength) && double.IsFinite(crane.MaxBoomLength) && point.BoomLength > crane.MaxBoomLength)
                        errors.Add($"Load chart point {row}: boom length exceeds the crane maximum boom length.");
                }
            }

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
