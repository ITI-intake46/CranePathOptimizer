using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.IO;

namespace CraneOptimizer.Infrastructure.Contexts
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CraneDbContext>
    {
        public CraneDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<CraneDbContext>();

            // Design-time DB path aligned with runtime convention: ./Data/crane.db
            var dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
            Directory.CreateDirectory(dataDir);
            var dbPath = Path.Combine(dataDir, "crane.db");

            optionsBuilder.UseSqlite($"Data Source={dbPath}");

            return new CraneDbContext(optionsBuilder.Options);
        }
    }
}
