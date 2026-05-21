using CraneOptimizer.Domain.CreatedProject;
using CraneOptimizer.Domain.CreatedProject.CraneFiles;
using CraneOptimizer.Domain.CreatedProject.IfcFiles;
using CraneOptimizer.Domain.CreatedProject.OptimizationFiles;
using Microsoft.EntityFrameworkCore;

namespace CraneOptimizer.Infrastructure.Contexts.CreatedProject
{
    public sealed class CreatedProjectDbContext : DbContext
    {
        public CreatedProjectDbContext(DbContextOptions<CreatedProjectDbContext> options)
            : base(options)
        {
        }

        public DbSet<ProjectInfoEntity> ProjectInfo => Set<ProjectInfoEntity>();
        public DbSet<ProjectIfcFileEntity> IfcFiles => Set<ProjectIfcFileEntity>();
        public DbSet<ProjectIfcElementEntity> IfcElements => Set<ProjectIfcElementEntity>();
        public DbSet<ProjectCraneSnapshotEntity> CraneSnapshots => Set<ProjectCraneSnapshotEntity>();
        public DbSet<ProjectCraneLoadChartSnapshotEntity> CraneLoadChartSnapshots => Set<ProjectCraneLoadChartSnapshotEntity>();
        public DbSet<ProjectOptimizationRunEntity> OptimizationRuns => Set<ProjectOptimizationRunEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Deferred entities for later phases.
            modelBuilder.Ignore<ProjectSettingEntity>();
            modelBuilder.Ignore<ProjectGeometryCacheEntity>();
            modelBuilder.Ignore<ProjectOptimizationElementResultEntity>();

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(CreatedProjectDbContext).Assembly);
        }
    }
}
