namespace CraneOptimizer.UI.Features.F4_LoadChart.Services
{
    /// <summary>
    /// Tracks which crane metadata values were populated automatically from a PDF so
    /// future PDF imports can replace stale auto-filled values without overwriting
    /// values the user has edited manually.
    /// </summary>
    internal sealed class LoadChartMetadataAutoFillState
    {
        private const double DimensionToleranceMeters = 0.0005d;

        private string? _sourceIdentity;
        private double? _vehicleLengthMeters;
        private double? _vehicleHeightMeters;
        private double? _vehicleWidthMeters;
        private double? _outriggerWidthMeters;
        private double? _outriggerLengthMeters;

        public string? ModelText { get; private set; }
        public string? ManufacturerText { get; private set; }
        public string? CounterweightText { get; private set; }

        public void BeginSource(string sourceIdentity)
        {
            _sourceIdentity = NormalizeSourceIdentity(sourceIdentity);
        }

        public bool IsSameSource(string sourceIdentity)
        {
            string normalized = NormalizeSourceIdentity(sourceIdentity);
            return !string.IsNullOrWhiteSpace(_sourceIdentity)
                && string.Equals(_sourceIdentity, normalized, StringComparison.Ordinal);
        }

        public void Clear()
        {
            _sourceIdentity = null;
            ModelText = null;
            ManufacturerText = null;
            CounterweightText = null;
            ClearDimensionOwnership();
        }

        public void RememberModel(string? text)
        {
            ModelText = NormalizeAutoText(text);
        }

        public void RememberManufacturer(string? text)
        {
            ManufacturerText = NormalizeAutoText(text);
        }

        public void RememberCounterweight(string? text)
        {
            CounterweightText = NormalizeAutoText(text);
        }

        public void RememberVehicleLength(double value)
        {
            _vehicleLengthMeters = NormalizeDimension(value);
        }

        public void RememberVehicleHeight(double value)
        {
            _vehicleHeightMeters = NormalizeDimension(value);
        }

        public void RememberVehicleWidth(double value)
        {
            _vehicleWidthMeters = NormalizeDimension(value);
        }

        public void RememberOutriggerWidth(double? value)
        {
            _outriggerWidthMeters = NormalizeDimension(value);
        }

        public void RememberOutriggerLength(double? value)
        {
            _outriggerLengthMeters = NormalizeDimension(value);
        }

        public bool ShouldReplaceModel(string? currentText, string? fallbackModelName)
        {
            return IsBlank(currentText)
                || TextStillMatchesAutoFill(currentText, ModelText)
                || TextStillMatchesAutoFill(currentText, fallbackModelName);
        }

        public bool ShouldReplaceManufacturer(string? currentText)
        {
            return IsBlank(currentText)
                || TextStillMatchesAutoFill(currentText, ManufacturerText);
        }

        public bool ShouldReplaceCounterweight(string? currentText)
        {
            return IsBlank(currentText)
                || TextStillMatchesAutoFill(currentText, CounterweightText);
        }

        public bool ShouldReplaceVehicleLength(double currentValue)
        {
            return ShouldReplaceDimension(currentValue, _vehicleLengthMeters);
        }

        public bool ShouldReplaceVehicleHeight(double currentValue)
        {
            return ShouldReplaceDimension(currentValue, _vehicleHeightMeters);
        }

        public bool ShouldReplaceVehicleWidth(double currentValue)
        {
            return ShouldReplaceDimension(currentValue, _vehicleWidthMeters);
        }

        public bool ShouldReplaceOutriggerWidth(double? currentValue)
        {
            return ShouldReplaceDimension(currentValue, _outriggerWidthMeters);
        }

        public bool ShouldReplaceOutriggerLength(double? currentValue)
        {
            return ShouldReplaceDimension(currentValue, _outriggerLengthMeters);
        }

        public void ClearModelOwnership()
        {
            ModelText = null;
        }

        public void ClearManufacturerOwnership()
        {
            ManufacturerText = null;
        }

        public void ClearCounterweightOwnership()
        {
            CounterweightText = null;
        }

        public void ClearVehicleLengthOwnership()
        {
            _vehicleLengthMeters = null;
        }

        public void ClearVehicleHeightOwnership()
        {
            _vehicleHeightMeters = null;
        }

        public void ClearVehicleWidthOwnership()
        {
            _vehicleWidthMeters = null;
        }

        public void ClearOutriggerWidthOwnership()
        {
            _outriggerWidthMeters = null;
        }

        public void ClearOutriggerLengthOwnership()
        {
            _outriggerLengthMeters = null;
        }

        public void ClearOutriggerOwnership()
        {
            ClearOutriggerWidthOwnership();
            ClearOutriggerLengthOwnership();
        }

        public void ClearDimensionOwnership()
        {
            ClearVehicleLengthOwnership();
            ClearVehicleHeightOwnership();
            ClearVehicleWidthOwnership();
            ClearOutriggerOwnership();
        }

        public void ClearChangedDimensionOwnership(
            double previousVehicleLengthMeters,
            double previousVehicleHeightMeters,
            double previousVehicleWidthMeters,
            double? previousOutriggerWidthMeters,
            double? previousOutriggerLengthMeters,
            double currentVehicleLengthMeters,
            double currentVehicleHeightMeters,
            double currentVehicleWidthMeters,
            double? currentOutriggerWidthMeters,
            double? currentOutriggerLengthMeters)
        {
            if (!DimensionsMatch(previousVehicleLengthMeters, currentVehicleLengthMeters))
                ClearVehicleLengthOwnership();

            if (!DimensionsMatch(previousVehicleHeightMeters, currentVehicleHeightMeters))
                ClearVehicleHeightOwnership();

            if (!DimensionsMatch(previousVehicleWidthMeters, currentVehicleWidthMeters))
                ClearVehicleWidthOwnership();

            if (!DimensionsMatch(previousOutriggerWidthMeters, currentOutriggerWidthMeters))
                ClearOutriggerWidthOwnership();

            if (!DimensionsMatch(previousOutriggerLengthMeters, currentOutriggerLengthMeters))
                ClearOutriggerLengthOwnership();
        }

        public static bool TextStillMatchesAutoFill(string? currentText, string? previousAutoFill)
        {
            string? current = NormalizeAutoText(currentText);
            string? previous = NormalizeAutoText(previousAutoFill);

            return current != null
                && previous != null
                && string.Equals(current, previous, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldReplaceDimension(double currentValue, double? autoFilledValue)
        {
            if (!HasUsableDimension(currentValue))
                return true;

            return autoFilledValue.HasValue
                && DimensionsMatch(currentValue, autoFilledValue.Value);
        }

        private static bool ShouldReplaceDimension(double? currentValue, double? autoFilledValue)
        {
            if (!currentValue.HasValue || !HasUsableDimension(currentValue.Value))
                return true;

            return autoFilledValue.HasValue
                && DimensionsMatch(currentValue.Value, autoFilledValue.Value);
        }

        private static bool DimensionsMatch(double? left, double? right)
        {
            if (!left.HasValue && !right.HasValue)
                return true;

            if (!left.HasValue || !right.HasValue)
                return false;

            return DimensionsMatch(left.Value, right.Value);
        }

        private static bool DimensionsMatch(double left, double right)
        {
            if (!double.IsFinite(left) || !double.IsFinite(right))
                return false;

            return Math.Abs(left - right) <= DimensionToleranceMeters;
        }

        private static double? NormalizeDimension(double? value)
        {
            if (!value.HasValue)
                return null;

            return NormalizeDimension(value.Value);
        }

        private static double? NormalizeDimension(double value)
        {
            return HasUsableDimension(value)
                ? value
                : null;
        }

        private static bool HasUsableDimension(double value)
        {
            return double.IsFinite(value) && value > 0d;
        }

        private static string NormalizeSourceIdentity(string? sourceIdentity)
        {
            return sourceIdentity?.Trim() ?? string.Empty;
        }

        private static string? NormalizeAutoText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            return text.Trim();
        }

        private static bool IsBlank(string? text)
        {
            return string.IsNullOrWhiteSpace(text);
        }
    }
}