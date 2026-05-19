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
        double VehicleLengthMm,
        double VehicleHeightMm,
        double VehicleWidthMm,
        double? OutriggerWidthMm,
        double? OutriggerLengthMm,
        double MinRadius,
        double MaxRadius,
        double MinBoomLength,
        double MaxBoomLength,
        double MaxCapacity,
        IReadOnlyList<LoadChartPointDto> Points);

    /// <summary>
    /// Simple load chart point used by the load chart dialogs.
    /// </summary>
    public sealed record LoadChartPointDto(
        double Radius,
        double BoomLength,
        double Capacity);
}
