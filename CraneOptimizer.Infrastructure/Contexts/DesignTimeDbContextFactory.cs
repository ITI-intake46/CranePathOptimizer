using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CraneOptimizer.Infrastructure.Contexts
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CraneDbContext>
    {
        public CraneDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<CraneDbContext>();

            optionsBuilder.UseSqlite("Data Source=crane.db");

            return new CraneDbContext(optionsBuilder.Options);
        }
    }
}