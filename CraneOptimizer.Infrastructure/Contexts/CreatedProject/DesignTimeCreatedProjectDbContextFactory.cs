using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.IO;

namespace CraneOptimizer.Infrastructure.Contexts.CreatedProject
{
    public sealed class DesignTimeCreatedProjectDbContextFactory: IDesignTimeDbContextFactory<CreatedProjectDbContext>
    {
        public CreatedProjectDbContext CreateDbContext(string[] args)
        {
            var dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
            Directory.CreateDirectory(dataDir);

            var dbPath = Path.Combine(dataDir, "design-time-project.db");

            var options = new DbContextOptionsBuilder<CreatedProjectDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            return new CreatedProjectDbContext(options);
        }
    }
}
