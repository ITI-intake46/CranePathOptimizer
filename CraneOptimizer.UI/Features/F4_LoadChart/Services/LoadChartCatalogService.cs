using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Entities.Enums;
using CraneOptimizer.Infrastructure.Contexts;
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

        public async Task<List<Crane>> GetAllAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Cranes
                .Include(crane => crane.Specifications)
                .ThenInclude(spec => spec.LoadChart)
                .AsNoTracking()
                .OrderBy(crane => crane.Name)
                .ToListAsync();
        }

        public async Task CreateAsync(Crane crane)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await context.Cranes.AddAsync(crane);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Crane updatedCrane)
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
            existingCrane.CraneType = updatedCrane.CraneType;
            existingCrane.VehicleLength = updatedCrane.VehicleLength;
            existingCrane.VehicleWidth = updatedCrane.VehicleWidth;
            existingCrane.VehicleHeight = updatedCrane.VehicleHeight;
            existingCrane.OutriggerLength = updatedCrane.OutriggerLength;
            existingCrane.OutriggerWidth = updatedCrane.OutriggerWidth;

            var updatedSpec = updatedCrane.Specifications?.FirstOrDefault()
                ?? throw new InvalidOperationException("Updated crane data does not contain a specification.");

            existingCrane.Specifications ??= new List<CraneSpecification>();
            var existingSpec = existingCrane.Specifications.FirstOrDefault();
            if (existingSpec == null)
            {
                existingCrane.Specifications.Add(CloneSpecification(updatedSpec));
            }
            else
            {
                existingSpec.MaxCapacity = updatedSpec.MaxCapacity;
                existingSpec.MaxRadius = updatedSpec.MaxRadius;
                existingSpec.MinRadius = updatedSpec.MinRadius;
                existingSpec.MaxBoomLength = updatedSpec.MaxBoomLength;
                existingSpec.MinBoomLength = updatedSpec.MinBoomLength;
                existingSpec.CounterWeight = updatedSpec.CounterWeight;
                existingSpec.MachineRotation = updatedSpec.MachineRotation;
                existingSpec.DeratingRatio = updatedSpec.DeratingRatio;
                existingSpec.OutriggerConfig = updatedSpec.OutriggerConfig;

                context.TargetPoints.RemoveRange(existingSpec.LoadChart);
                existingSpec.LoadChart.Clear();

                foreach (var point in updatedSpec.LoadChart)
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

        public static Crane BuildCrane(
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
            return new Crane
            {
                Id = craneId,
                Name = model,
                Manufacturer = manufacturer,
                CraneType = CraneType.Mobile,
                VehicleLength = vehicleLength,
                VehicleWidth = vehicleWidth,
                VehicleHeight = vehicleHeight,
                OutriggerWidth = outriggerWidth,
                OutriggerLength = outriggerLength,
                Specifications =
                {
                    new CraneSpecification
                    {
                        MaxCapacity = parsedChart.MaxCapacity,
                        MaxRadius = parsedChart.MaxRadius,
                        MinRadius = parsedChart.MinRadius,
                        MaxBoomLength = parsedChart.MaxBoomLength,
                        MinBoomLength = parsedChart.MinBoomLength,
                        CounterWeight = counterWeight,
                        DeratingRatio = null,
                        OutriggerConfig = outriggerConfig,
                        LoadChart = parsedChart.Points
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

        private static CraneSpecification CloneSpecification(CraneSpecification source)
        {
            return new CraneSpecification
            {
                MaxCapacity = source.MaxCapacity,
                MaxRadius = source.MaxRadius,
                MinRadius = source.MinRadius,
                MaxBoomLength = source.MaxBoomLength,
                MinBoomLength = source.MinBoomLength,
                CounterWeight = source.CounterWeight,
                MachineRotation = source.MachineRotation,
                DeratingRatio = source.DeratingRatio,
                OutriggerConfig = source.OutriggerConfig,
                LoadChart = source.LoadChart
                    .Select(point => new TargetPoint
                    {
                        Radius = point.Radius,
                        BoomLength = point.BoomLength,
                        Capacity = point.Capacity
                    })
                    .ToList()
            };
        }
    }
}
