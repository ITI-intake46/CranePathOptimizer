using CraneOptimizer.Domain.Entities;
using CraneOptimizer.Domain.Entities.Enums;
using CraneOptimizer.Domain.Units;
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
                VehicleLength = Mm(14635),
                VehicleWidth = Mm(2750),
                VehicleHeight = Mm(3429),
                OutriggerLength = Mm(6100),
                OutriggerWidth = Mm(7600),
                Specifications = new List<CraneSpecification>()
            };

            var spec = new CraneSpecification
            {
                MaxCapacity = Kg(80000),
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

        private static double Mm(double value) => UnitSystem.ToMeters(value, LengthUnit.Millimeter);

        private static double Kg(double value) => UnitSystem.ToTons(value, MassUnit.Kilogram);

        private static List<TargetPoint> GetTargetPoints()
        {
            return new List<TargetPoint>
            {
                new() { Radius = 3, BoomLength = 11.8, Capacity = Kg(80000) },
                new() { Radius = 3.5, BoomLength = 11.8, Capacity = Kg(71000) },
                new() { Radius = 4, BoomLength = 11.8, Capacity = Kg(63000) },
                new() { Radius = 4.5, BoomLength = 11.8, Capacity = Kg(56000) },
                new() { Radius = 5, BoomLength = 11.8, Capacity = Kg(51000) },
                new() { Radius = 5.5, BoomLength = 11.8, Capacity = Kg(46000) },
                new() { Radius = 6.5, BoomLength = 11.8, Capacity = Kg(36000) },
                new() { Radius = 6, BoomLength = 11.8, Capacity = Kg(41000) },
                new() { Radius = 7, BoomLength = 11.8, Capacity = Kg(32000) },
                new() { Radius = 8, BoomLength = 11.8, Capacity = Kg(25000) },
                new() { Radius = 9, BoomLength = 11.8, Capacity = Kg(19000) },

                new() { Radius = 3, BoomLength = 15.95, Capacity = Kg(51000) },
                new() { Radius = 3.5, BoomLength = 15.95, Capacity = Kg(51000) },
                new() { Radius = 4, BoomLength = 15.95, Capacity = Kg(51000) },
                new() { Radius = 4.5, BoomLength = 15.95, Capacity = Kg(48000) },
                new() { Radius = 5, BoomLength = 15.95, Capacity = Kg(45000) },
                new() { Radius = 5.5, BoomLength = 15.95, Capacity = Kg(42000) },
                new() { Radius = 6, BoomLength = 15.95, Capacity = Kg(38000) },
                new() { Radius = 6.5, BoomLength = 15.95, Capacity = Kg(34000) },
                new() { Radius = 7, BoomLength = 15.95, Capacity = Kg(30500) },
                new() { Radius = 8, BoomLength = 15.95, Capacity = Kg(24500) },
                new() { Radius = 9, BoomLength = 15.95, Capacity = Kg(20000) },
                new() { Radius = 10, BoomLength = 15.95, Capacity = Kg(16000) },
                new() { Radius = 11, BoomLength = 15.95, Capacity = Kg(13000) },
                new() { Radius = 12, BoomLength = 15.95, Capacity = Kg(10500) },

                new() { Radius = 3.5, BoomLength = 20.1, Capacity = Kg(43000) },
                new() { Radius = 4, BoomLength = 20.1, Capacity = Kg(43000) },
                new() { Radius = 4.5, BoomLength = 20.1, Capacity = Kg(40500) },
                new() { Radius = 5, BoomLength = 20.1, Capacity = Kg(38000) },
                new() { Radius = 5.5, BoomLength = 20.1, Capacity = Kg(35400) },
                new() { Radius = 6, BoomLength = 20.1, Capacity = Kg(33100) },
                new() { Radius = 6.5, BoomLength = 20.1, Capacity = Kg(31000) },
                new() { Radius = 7, BoomLength = 20.1, Capacity = Kg(28500) },
                new() { Radius = 8, BoomLength = 20.1, Capacity = Kg(23800) },
                new() { Radius = 9, BoomLength = 20.1, Capacity = Kg(19500) },
                new() { Radius = 10, BoomLength = 20.1, Capacity = Kg(15900) },
                new() { Radius = 11, BoomLength = 20.1, Capacity = Kg(13100) },
                new() { Radius = 12, BoomLength = 20.1, Capacity = Kg(10800) },
                new() { Radius = 14, BoomLength = 20.1, Capacity = Kg(7700) },
                new() { Radius = 16, BoomLength = 20.1, Capacity = Kg(5500) },
                new() { Radius = 18, BoomLength = 20.1, Capacity = Kg(3800) },

                new() { Radius = 4.5, BoomLength = 28.4, Capacity = Kg(30000) },
                new() { Radius = 5, BoomLength = 28.4, Capacity = Kg(30000) },
                new() { Radius = 5.5, BoomLength = 28.4, Capacity = Kg(28500) },
                new() { Radius = 6, BoomLength = 28.4, Capacity = Kg(27300) },
                new() { Radius = 6.5, BoomLength = 28.4, Capacity = Kg(26000) },
                new() { Radius = 7, BoomLength = 28.4, Capacity = Kg(24600) },
                new() { Radius = 8, BoomLength = 28.4, Capacity = Kg(22000) },
                new() { Radius = 9, BoomLength = 28.4, Capacity = Kg(19200) },
                new() { Radius = 10, BoomLength = 28.4, Capacity = Kg(16300) },
                new() { Radius = 11, BoomLength = 28.4, Capacity = Kg(13900) },
                new() { Radius = 12, BoomLength = 28.4, Capacity = Kg(12000) },
                new() { Radius = 14, BoomLength = 28.4, Capacity = Kg(9000) },
                new() { Radius = 16, BoomLength = 28.4, Capacity = Kg(6800) },
                new() { Radius = 18, BoomLength = 28.4, Capacity = Kg(5100) },
                new() { Radius = 20, BoomLength = 28.4, Capacity = Kg(3900) },
                new() { Radius = 22, BoomLength = 28.4, Capacity = Kg(2900) },
                new() { Radius = 24, BoomLength = 28.4, Capacity = Kg(2100) },
                new() { Radius = 26, BoomLength = 28.4, Capacity = Kg(1500) },

                new() { Radius = 6.5, BoomLength = 36.7, Capacity = Kg(16000) },
                new() { Radius = 7, BoomLength = 36.7, Capacity = Kg(16000) },
                new() { Radius = 8, BoomLength = 36.7, Capacity = Kg(15000) },
                new() { Radius = 9, BoomLength = 36.7, Capacity = Kg(14500) },
                new() { Radius = 10, BoomLength = 36.7, Capacity = Kg(13900) },
                new() { Radius = 11, BoomLength = 36.7, Capacity = Kg(13000) },
                new() { Radius = 12, BoomLength = 36.7, Capacity = Kg(12000) },
                new() { Radius = 14, BoomLength = 36.7, Capacity = Kg(9500) },
                new() { Radius = 16, BoomLength = 36.7, Capacity = Kg(7500) },
                new() { Radius = 18, BoomLength = 36.7, Capacity = Kg(6000) },
                new() { Radius = 20, BoomLength = 36.7, Capacity = Kg(4700) },
                new() { Radius = 22, BoomLength = 36.7, Capacity = Kg(3700) },
                new() { Radius = 24, BoomLength = 36.7, Capacity = Kg(2800) },
                new() { Radius = 26, BoomLength = 36.7, Capacity = Kg(2100) },
                new() { Radius = 28, BoomLength = 36.7, Capacity = Kg(1600) },
                new() { Radius = 30, BoomLength = 36.7, Capacity = Kg(1200) },
                new() { Radius = 32, BoomLength = 36.7, Capacity = Kg(800) },
                new() { Radius = 34, BoomLength = 36.7, Capacity = Kg(500) },

                new() { Radius = 8, BoomLength = 45, Capacity = Kg(9500) },
                new() { Radius = 9, BoomLength = 45, Capacity = Kg(9500) },
                new() { Radius = 10, BoomLength = 45, Capacity = Kg(9200) },
                new() { Radius = 11, BoomLength = 45, Capacity = Kg(9000) },
                new() { Radius = 12, BoomLength = 45, Capacity = Kg(8800) },
                new() { Radius = 14, BoomLength = 45, Capacity = Kg(8500) },
                new() { Radius = 16, BoomLength = 45, Capacity = Kg(7700) },
                new() { Radius = 18, BoomLength = 45, Capacity = Kg(6500) },
                new() { Radius = 20, BoomLength = 45, Capacity = Kg(5200) },
                new() { Radius = 22, BoomLength = 45, Capacity = Kg(4100) },
                new() { Radius = 24, BoomLength = 45, Capacity = Kg(3300) },
                new() { Radius = 26, BoomLength = 45, Capacity = Kg(2600) },
                new() { Radius = 28, BoomLength = 45, Capacity = Kg(2000) },
                new() { Radius = 30, BoomLength = 45, Capacity = Kg(1600) },
                new() { Radius = 32, BoomLength = 45, Capacity = Kg(1200) },
                new() { Radius = 34, BoomLength = 45, Capacity = Kg(900) },
                new() { Radius = 36, BoomLength = 45, Capacity = Kg(600) }
            };
        }
    }
}
