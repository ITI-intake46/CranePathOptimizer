using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Entities.Enums;
using CraneOptimizer.UI.Features.F4_LoadChart.DTOs;

namespace CraneOptimizer.UI.Features.F4_LoadChart.Mappers
{
    /// <summary>
    /// Maps crane entities to simple load chart dialog data and back again.
    /// </summary>
    internal static class LoadChartCraneMapper
    {
        public static LoadChartCraneDto ToLoadChartCraneDto(this Crane crane)
        {
            var spec = crane.Specifications?.FirstOrDefault();
            var points = spec?.LoadChart
                .OrderBy(point => point.Radius)
                .ThenBy(point => point.VisualColumnIndex ?? int.MaxValue)
                .ThenBy(point => point.BoomLength)
                .Select(point => new LoadChartPointDto(
                    point.Radius,
                    point.BoomLength,
                    point.Capacity,
                    point.VisualColumnIndex,
                    point.VisualColumnHeader ?? string.Empty,
                    point.NumberOfLines,
                    point.OperatingConditionLabel ?? string.Empty))
                .ToList()
                ?? new List<LoadChartPointDto>();

            return new LoadChartCraneDto(
                crane.Id,
                crane.Name,
                crane.Manufacturer,
                crane.CraneType.ToString(),
                spec?.OutriggerConfig ?? "100%",
                spec?.CounterWeight ?? 0d,
                crane.VehicleLength,
                crane.VehicleHeight,
                crane.VehicleWidth,
                crane.OutriggerWidth,
                crane.OutriggerLength,
                spec?.MinRadius ?? 0d,
                spec?.MaxRadius ?? 0d,
                spec?.MinBoomLength ?? 0d,
                spec?.MaxBoomLength ?? 0d,
                spec?.MaxCapacity ?? 0d,
                points,
                spec?.LoadChartStorageMode ?? "Standard2D",
                spec?.LoadChartCapacityPolicy ?? "Standard2D",
                spec?.ExactLoadChartRawGridJson ?? string.Empty,
                spec?.LoadChartSourceDescription ?? string.Empty);
        }

        public static Crane ToCraneEntity(this LoadChartCraneDto dto)
        {
            return new Crane
            {
                Id = dto.Id,
                Name = dto.Name,
                Manufacturer = dto.Manufacturer,
                CraneType = Enum.TryParse<CraneType>(dto.CraneType, true, out var craneType)
                    ? craneType
                    : CraneType.Mobile,
                VehicleLength = dto.VehicleLengthMeters,
                VehicleWidth = dto.VehicleWidthMeters,
                VehicleHeight = dto.VehicleHeightMeters,
                OutriggerWidth = dto.OutriggerWidthMeters,
                OutriggerLength = dto.OutriggerLengthMeters,
                Specifications =
                {
                    new CraneSpecification
                    {
                        MaxCapacity = dto.MaxCapacity,
                        MaxRadius = dto.MaxRadius,
                        MinRadius = dto.MinRadius,
                        MaxBoomLength = dto.MaxBoomLength,
                        MinBoomLength = dto.MinBoomLength,
                        CounterWeight = dto.Counterweight,
                        DeratingRatio = null,
                        OutriggerConfig = dto.OutriggerConfig,
                        LoadChartStorageMode = string.IsNullOrWhiteSpace(dto.LoadChartStorageMode) ? "Standard2D" : dto.LoadChartStorageMode,
                        LoadChartCapacityPolicy = string.IsNullOrWhiteSpace(dto.LoadChartCapacityPolicy) ? "Standard2D" : dto.LoadChartCapacityPolicy,
                        ExactLoadChartRawGridJson = string.IsNullOrWhiteSpace(dto.ExactLoadChartRawGridJson) ? null : dto.ExactLoadChartRawGridJson,
                        LoadChartSourceDescription = string.IsNullOrWhiteSpace(dto.LoadChartSourceDescription) ? null : dto.LoadChartSourceDescription,
                        LoadChart = dto.Points
                            .Select(point => new TargetPoint
                            {
                                Radius = point.Radius,
                                BoomLength = point.BoomLength,
                                Capacity = point.Capacity,
                                VisualColumnIndex = point.VisualColumnIndex,
                                VisualColumnHeader = string.IsNullOrWhiteSpace(point.VisualColumnHeader) ? null : point.VisualColumnHeader,
                                NumberOfLines = point.NumberOfLines,
                                OperatingConditionLabel = string.IsNullOrWhiteSpace(point.OperatingConditionLabel) ? null : point.OperatingConditionLabel
                            })
                            .ToList()
                    }
                }
            };
        }
    }
}
