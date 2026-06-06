namespace CraneOptimizer.UI.Features.F4_LoadChart.DTOs
{
    /// <summary>
    /// Simple crane data used by the load chart dialogs.
    /// </summary>
    public sealed record LoadChartCraneDto(
        int Id,
        string Name,
        string Manufacturer,
        string CraneType,
        string OutriggerConfig,
        double Counterweight,
        double VehicleLengthMeters,
        double VehicleHeightMeters,
        double VehicleWidthMeters,
        double? OutriggerWidthMeters,
        double? OutriggerLengthMeters,
        double MinRadius,
        double MaxRadius,
        double MinBoomLength,
        double MaxBoomLength,
        double MaxCapacity,
        IReadOnlyList<LoadChartPointDto> Points,
        string LoadChartStorageMode = "Standard2D",
        string LoadChartCapacityPolicy = "Standard2D",
        string ExactLoadChartRawGridJson = "",
        string LoadChartSourceDescription = "")
    {
        public bool HasExactVisualLoadChart =>
            string.Equals(LoadChartStorageMode, "ExactPdfVisual", StringComparison.OrdinalIgnoreCase)
            || Points.Any(point => point.VisualColumnIndex.HasValue);
    }

    /// <summary>
    /// Simple load chart point used by the load chart dialogs.
    /// </summary>
    public sealed record LoadChartPointDto(
        double Radius,
        double BoomLength,
        double Capacity,
        int? VisualColumnIndex = null,
        string VisualColumnHeader = "");
}
