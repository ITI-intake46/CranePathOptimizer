using CraneOptimizer.Domain.CreatedProject;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CraneOptimizer.Infrastructure.Contexts.CreatedProject.CreatedProjectConfigurations
{
    public sealed class ProjectInfoConfiguration : IEntityTypeConfiguration<ProjectInfoEntity>
    {
        public void Configure(EntityTypeBuilder<ProjectInfoEntity> entity)
        {
            entity.ToTable("ProjectInfo");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.ProjectGuid)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.Description)
                .HasMaxLength(4000);

            entity.Property(x => x.UnitSystem)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.LengthUnit)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(x => x.AreaUnit)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(x => x.AppVersion)
                .HasMaxLength(50);

            entity.Property(x => x.SchemaVersion)
                .IsRequired();

            entity.Property(x => x.CacheVersion)
                .IsRequired();

            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();

            entity.Property(x => x.LastOpenedAtUtc)
                .IsRequired();

            entity.HasIndex(x => x.ProjectGuid)
                .IsUnique();

            // Deferred entities for later phases.
            entity.Ignore(x => x.Settings);
            entity.Ignore(x => x.GeometryCache);
        }
    }
}
