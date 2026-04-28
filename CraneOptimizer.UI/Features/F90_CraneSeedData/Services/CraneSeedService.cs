using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Entities.Enums;
using CraneOptimizer.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace CraneOptimizer.UI.F90_CraneSeedData.Services
{
    public class CraneSeedService
    {
        private readonly CraneDbContext _context;

        public CraneSeedService(CraneDbContext context)
        {
            _context = context;
        }

        public async Task SeedAsync()
        {
            if (await _context.Cranes.AnyAsync())
                return;

            var crane = new Crane
            {
                Name = "STC800",
                Manufacturer = "Sany",
                CraneType = CraneType.Mobile,
                VehicleLength = 14635,
                VehicleWidth = 2750,
                VehicleHeight = 3429,
                OutriggerLength = 6100,
                OutriggerWidth = 7600,
                Specifications = new List<CraneSpecification>()
            };

            var spec = new CraneSpecification
            {
                MaxCapacity = 80000,
                MaxRadius = 36,
                MinRadius = 3,
                MaxBoomLength = 45,
                MinBoomLength = 11.8,
                CounterWeight = 4,
                MachineRotation = 360,
                LoadChart = GetTargetPoints()
            };

            crane.Specifications.Add(spec);
            _context.Cranes.Add(crane);

            await _context.SaveChangesAsync();
        }

        private static List<TargetPoint> GetTargetPoints()
        {
            return new List<TargetPoint>
            {
                new() { Radius = 3, BoomLength = 11.8, Capacity = 80000 },
                new() { Radius = 6.5, BoomLength = 11.8, Capacity = 36000 },
                new() { Radius = 5, BoomLength = 11.8, Capacity = 51000 },
                new() { Radius = 7, BoomLength = 11.8, Capacity = 32000 },
                new() { Radius = 8, BoomLength = 11.8, Capacity = 25000 },
                new() { Radius = 5.5, BoomLength = 11.8, Capacity = 46000 },
                new() { Radius = 4.5, BoomLength = 11.8, Capacity = 56000 },
                new() { Radius = 4, BoomLength = 11.8, Capacity = 63000 },
                new() { Radius = 3.5, BoomLength = 11.8, Capacity = 71000 },
                new() { Radius = 9, BoomLength = 11.8, Capacity = 19000 },
                new() { Radius = 6, BoomLength = 11.8, Capacity = 41000 },
                new() { Radius = 8, BoomLength = 15.95, Capacity = 24500 },
                new() { Radius = 9, BoomLength = 15.95, Capacity = 20000 },
                new() { Radius = 7, BoomLength = 15.95, Capacity = 30500 },
                new() { Radius = 11, BoomLength = 15.95, Capacity = 13000 },
                new() { Radius = 6.5, BoomLength = 15.95, Capacity = 34000 },
                new() { Radius = 12, BoomLength = 15.95, Capacity = 10500 },
                new() { Radius = 6, BoomLength = 15.95, Capacity = 38000 },
                new() { Radius = 10, BoomLength = 15.95, Capacity = 16000 },
                new() { Radius = 3, BoomLength = 15.95, Capacity = 51000 },
                new() { Radius = 4, BoomLength = 15.95, Capacity = 51000 },
                new() { Radius = 6, BoomLength = 15.95, Capacity = 45000 },
                new() { Radius = 4.5, BoomLength = 15.95, Capacity = 48000 },
                new() { Radius = 3.5, BoomLength = 15.95, Capacity = 51000 },
                new() { Radius = 5.5, BoomLength = 15.95, Capacity = 42000 },
                new() { Radius = 10, BoomLength = 20.1, Capacity = 15900 },
                new() { Radius = 5.5, BoomLength = 20.1, Capacity = 35400 },
                new() { Radius = 12, BoomLength = 20.1, Capacity = 10800 },
                new() { Radius = 9, BoomLength = 20.1, Capacity = 19500 },
                new() { Radius = 3.5, BoomLength = 20.1, Capacity = 43000 },
                new() { Radius = 14, BoomLength = 20.1, Capacity = 7700 },
                new() { Radius = 16, BoomLength = 20.1, Capacity = 0 },
                new() { Radius = 6, BoomLength = 20.1, Capacity = 33100 },
                new() { Radius = 4, BoomLength = 20.1, Capacity = 43000 },
                new() { Radius = 8, BoomLength = 20.1, Capacity = 23800 },
                new() { Radius = 18, BoomLength = 20.1, Capacity = 3800 },
                new() { Radius = 7, BoomLength = 20.1, Capacity = 28500 },
                new() { Radius = 4.5, BoomLength = 20.1, Capacity = 40500 },
                new() { Radius = 6.5, BoomLength = 20.1, Capacity = 31000 },
                new() { Radius = 5, BoomLength = 20.1, Capacity = 38000 },
                new() { Radius = 11, BoomLength = 20.1, Capacity = 13100 },
                new() { Radius = 16, BoomLength = 28.4, Capacity = 6800 },
                new() { Radius = 14, BoomLength = 28.4, Capacity = 9000 },
                new() { Radius = 18, BoomLength = 28.4, Capacity = 6100 },
                new() { Radius = 20, BoomLength = 28.4, Capacity = 3900 },
                new() { Radius = 22, BoomLength = 28.4, Capacity = 2900 },
                new() { Radius = 12, BoomLength = 28.4, Capacity = 12000 },
                new() { Radius = 26, BoomLength = 28.4, Capacity = 1500 },
                new() { Radius = 11, BoomLength = 28.4, Capacity = 13900 },
                new() { Radius = 24, BoomLength = 28.4, Capacity = 2100 },
                new() { Radius = 5.5, BoomLength = 28.4, Capacity = 28500 },
                new() { Radius = 8, BoomLength = 28.4, Capacity = 22000 },
                new() { Radius = 10, BoomLength = 28.4, Capacity = 16300 },
                new() { Radius = 6, BoomLength = 28.4, Capacity = 30000 },
                new() { Radius = 6.5, BoomLength = 28.4, Capacity = 26000 },
                new() { Radius = 9, BoomLength = 28.4, Capacity = 19200 },
                new() { Radius = 4.5, BoomLength = 28.4, Capacity = 30000 },
                new() { Radius = 7, BoomLength = 28.4, Capacity = 24600 },
                new() { Radius = 6, BoomLength = 28.4, Capacity = 27300 },
                new() { Radius = 22, BoomLength = 36.7, Capacity = 3700 },
                new() { Radius = 26, BoomLength = 36.7, Capacity = 2100 },
                new() { Radius = 20, BoomLength = 36.7, Capacity = 4700 },
                new() { Radius = 6.5, BoomLength = 36.7, Capacity = 16000 },
                new() { Radius = 18, BoomLength = 36.7, Capacity = 6000 },
                new() { Radius = 28, BoomLength = 36.7, Capacity = 1600 },
                new() { Radius = 16, BoomLength = 36.7, Capacity = 7500 },
                new() { Radius = 10, BoomLength = 36.7, Capacity = 13900 },
                new() { Radius = 14, BoomLength = 36.7, Capacity = 9500 },
                new() { Radius = 8, BoomLength = 36.7, Capacity = 15000 },
                new() { Radius = 30, BoomLength = 36.7, Capacity = 1200 },
                new() { Radius = 12, BoomLength = 36.7, Capacity = 12000 },
                new() { Radius = 9, BoomLength = 36.7, Capacity = 14500 },
                new() { Radius = 32, BoomLength = 36.7, Capacity = 800 },
                new() { Radius = 11, BoomLength = 36.7, Capacity = 13000 },
                new() { Radius = 34, BoomLength = 36.7, Capacity = 500 },
                new() { Radius = 7, BoomLength = 36.7, Capacity = 16000 },
                new() { Radius = 24, BoomLength = 36.7, Capacity = 2800 },
                new() { Radius = 28, BoomLength = 45, Capacity = 2000 },
                new() { Radius = 30, BoomLength = 45, Capacity = 1600 },
                new() { Radius = 26, BoomLength = 45, Capacity = 2600 },
                new() { Radius = 32, BoomLength = 45, Capacity = 1200 },
                new() { Radius = 18, BoomLength = 45, Capacity = 6500 },
                new() { Radius = 22, BoomLength = 45, Capacity = 4100 },
                new() { Radius = 20, BoomLength = 45, Capacity = 5200 },
                new() { Radius = 10, BoomLength = 45, Capacity = 9200 },
                new() { Radius = 16, BoomLength = 45, Capacity = 7700 },
                new() { Radius = 14, BoomLength = 45, Capacity = 8500 },
                new() { Radius = 8, BoomLength = 45, Capacity = 9500 },
                new() { Radius = 12, BoomLength = 45, Capacity = 8800 },
                new() { Radius = 9, BoomLength = 45, Capacity = 9500 },
                new() { Radius = 11, BoomLength = 45, Capacity = 9000 },
                new() { Radius = 34, BoomLength = 45, Capacity = 900 },
                new() { Radius = 24, BoomLength = 45, Capacity = 3300 },
                new() { Radius = 7, BoomLength = 45, Capacity = 10000 }
            };
        }
    }
}
