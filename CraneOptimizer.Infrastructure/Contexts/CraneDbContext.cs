using CraneOptimizer.Domain.Entities;
using Microsoft.EntityFrameworkCore;


namespace CraneOptimizer.Infrastructure.Contexts
{
    public class CraneDbContext : DbContext
    {
        public CraneDbContext(DbContextOptions<CraneDbContext> options) : base(options)
        {
        }

        public DbSet<Crane> Cranes { get; set; }
        public DbSet<CraneSpecification> CraneSpecifications { get; set; }
        public DbSet<TargetPoint> TargetPoints { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Crane>()
                .HasMany(c => c.Specifications)
                .WithOne(s => s.Crane)
                .HasForeignKey(s => s.CraneId);

            modelBuilder.Entity<CraneSpecification>()
                .HasMany(s => s.LoadChart)
                .WithOne(t => t.CraneSpecification)
                .HasForeignKey(t => t.CraneSpecificationId);
        }
    }
}
