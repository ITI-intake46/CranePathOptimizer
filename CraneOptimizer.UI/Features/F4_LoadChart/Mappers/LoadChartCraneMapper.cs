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
                .Select(point => new LoadChartPointDto(point.Radius, point.BoomLength, point.Capacity))
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
                points);
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
                        LoadChart = dto.Points
                            .Select(point => new TargetPoint
                            {
                                Radius = point.Radius,
                                BoomLength = point.BoomLength,
                                Capacity = point.Capacity
                            })
                            .ToList()
                    }
                }
            };
        }
    }
}
