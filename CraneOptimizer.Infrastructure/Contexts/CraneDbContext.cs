using CraneOptimizer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xbim.IO.Xml.BsConf;

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
        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectIfcFile> ProjectIfcFiles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Crane>()
                .HasMany(c => c.Specifications)
                .WithOne(s => s.Crane)
                .HasForeignKey(s => s.CraneId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CraneSpecification>(entity =>
            {
                entity.HasMany(s => s.LoadChart)
                    .WithOne(t => t.CraneSpecification)
                    .HasForeignKey(t => t.CraneSpecificationId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(s => s.LoadChartStorageMode)
                    .HasMaxLength(80);

                entity.Property(s => s.LoadChartCapacityPolicy)
                    .HasMaxLength(160);

                entity.Property(s => s.LoadChartSourceDescription)
                    .HasMaxLength(500);
            });

            modelBuilder.Entity<TargetPoint>(entity =>
            {
                entity.Property(t => t.VisualColumnHeader)
                    .HasMaxLength(120);
            });

            modelBuilder.Entity<Project>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
                entity.Property(x => x.Notes).HasMaxLength(4000);
                entity.Property(x => x.ConcreteDensityKgPerM3)
                    .HasDefaultValue(2500.0);
                entity.HasIndex(x => x.LastOpenedDate);
                entity.HasIndex(x => x.IsFavorite);
                entity.HasQueryFilter(x => !x.IsDeleted);

                entity.HasMany(x => x.IfcFiles)
                    .WithOne(x => x.Project)
                    .HasForeignKey(x => x.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.Property(x => x.ProjectFolderPath)
                    .IsRequired()
                    .HasMaxLength(1000);
            });

            modelBuilder.Entity<ProjectIfcFile>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.FilePath).IsRequired().HasMaxLength(1000);
                entity.Property(x => x.FileName).IsRequired().HasMaxLength(260);
                entity.HasIndex(x => x.ProjectId);
            });
        }
    }
}
