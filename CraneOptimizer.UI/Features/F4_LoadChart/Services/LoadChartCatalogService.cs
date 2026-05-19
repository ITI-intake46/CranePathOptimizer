using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Entities.Enums;
using CraneOptimizer.Infrastructure.Contexts;
using CraneOptimizer.UI.Features.F4_LoadChart.DTOs;
using CraneOptimizer.UI.Features.F4_LoadChart.Mappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CraneOptimizer.UI.Features.F4_LoadChart.Services
{
    internal sealed class LoadChartCatalogService
    {
        private readonly IDbContextFactory<CraneDbContext> _contextFactory;

        public LoadChartCatalogService(IDbContextFactory<CraneDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<LoadChartCraneDto>> GetAllAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var cranes = await context.Cranes
                .Include(crane => crane.Specifications)
                .ThenInclude(spec => spec.LoadChart)
                .AsNoTracking()
                .OrderBy(crane => crane.Name)
                .ToListAsync();

            return cranes
                .Select(crane => crane.ToLoadChartCraneDto())
                .ToList();
        }

        public async Task CreateAsync(LoadChartCraneDto crane)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await context.Cranes.AddAsync(crane.ToCraneEntity());
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(LoadChartCraneDto updatedCrane)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var existingCrane = await context.Cranes
                .Include(crane => crane.Specifications)
                .ThenInclude(spec => spec.LoadChart)
                .FirstOrDefaultAsync(crane => crane.Id == updatedCrane.Id);

            if (existingCrane == null)
                throw new InvalidOperationException("The crane to update could not be found.");

            existingCrane.Name = updatedCrane.Name;
            existingCrane.Manufacturer = updatedCrane.Manufacturer;
            existingCrane.CraneType = Enum.TryParse<CraneType>(updatedCrane.CraneType, true, out var craneType)
                ? craneType
                : existingCrane.CraneType;
            existingCrane.VehicleLength = updatedCrane.VehicleLengthMeters;
            existingCrane.VehicleWidth = updatedCrane.VehicleWidthMeters;
            existingCrane.VehicleHeight = updatedCrane.VehicleHeightMeters;
            existingCrane.OutriggerLength = updatedCrane.OutriggerLengthMeters;
            existingCrane.OutriggerWidth = updatedCrane.OutriggerWidthMeters;

            existingCrane.Specifications ??= new List<CraneSpecification>();
            var existingSpec = existingCrane.Specifications.FirstOrDefault();
            if (existingSpec == null)
            {
                existingCrane.Specifications.Add(new CraneSpecification
                {
                    MaxCapacity = updatedCrane.MaxCapacity,
                    MaxRadius = updatedCrane.MaxRadius,
                    MinRadius = updatedCrane.MinRadius,
                    MaxBoomLength = updatedCrane.MaxBoomLength,
                    MinBoomLength = updatedCrane.MinBoomLength,
                    CounterWeight = updatedCrane.Counterweight,
                    OutriggerConfig = updatedCrane.OutriggerConfig,
                    LoadChart = updatedCrane.Points
                        .Select(point => new TargetPoint
                        {
                            Radius = point.Radius,
                            BoomLength = point.BoomLength,
                            Capacity = point.Capacity
                        })
                        .ToList()
                });
            }
            else
            {
                existingSpec.MaxCapacity = updatedCrane.MaxCapacity;
                existingSpec.MaxRadius = updatedCrane.MaxRadius;
                existingSpec.MinRadius = updatedCrane.MinRadius;
                existingSpec.MaxBoomLength = updatedCrane.MaxBoomLength;
                existingSpec.MinBoomLength = updatedCrane.MinBoomLength;
                existingSpec.CounterWeight = updatedCrane.Counterweight;
                existingSpec.OutriggerConfig = updatedCrane.OutriggerConfig;

                context.TargetPoints.RemoveRange(existingSpec.LoadChart);
                existingSpec.LoadChart.Clear();

                foreach (var point in updatedCrane.Points)
                {
                    existingSpec.LoadChart.Add(new TargetPoint
                    {
                        Radius = point.Radius,
                        BoomLength = point.BoomLength,
                        Capacity = point.Capacity
                    });
                }
            }

            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int craneId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var crane = await context.Cranes.FirstOrDefaultAsync(item => item.Id == craneId);
            if (crane == null)
                return;

            context.Cranes.Remove(crane);
            await context.SaveChangesAsync();
        }

        public static LoadChartCraneDto BuildCraneDto(
            string model,
            string manufacturer,
            string outriggerConfig,
            double counterWeight,
            double vehicleLength,
            double vehicleHeight,
            double vehicleWidth,
            double? outriggerWidth,
            double? outriggerLength,
            ParsedLoadChart parsedChart,
            int craneId = 0)
        {
            return new LoadChartCraneDto(
                craneId,
                model,
                manufacturer,
                CraneType.Mobile.ToString(),
                outriggerConfig,
                counterWeight,
                vehicleLength,
                vehicleHeight,
                vehicleWidth,
                outriggerWidth,
                outriggerLength,
                parsedChart.MinRadius,
                parsedChart.MaxRadius,
                parsedChart.MinBoomLength,
                parsedChart.MaxBoomLength,
                parsedChart.MaxCapacity,
                parsedChart.Points
                    .Select(point => new LoadChartPointDto(point.Radius, point.BoomLength, point.Capacity))
                    .ToList());
        }
    }
}
