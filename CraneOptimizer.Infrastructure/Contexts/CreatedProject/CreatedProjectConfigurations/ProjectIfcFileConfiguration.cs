using CraneOptimizer.Domain.CreatedProject.IfcFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CraneOptimizer.Infrastructure.Contexts.CreatedProject.CreatedProjectConfigurations
{
    public sealed class ProjectIfcFileConfiguration : IEntityTypeConfiguration<ProjectIfcFileEntity>
    {
        public void Configure(EntityTypeBuilder<ProjectIfcFileEntity> entity)
        {
            entity.ToTable("ProjectIfcFiles");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.FileName)
                .IsRequired()
                .HasMaxLength(260);

            entity.Property(x => x.RelativePath)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(x => x.OriginalFullPath)
                .HasMaxLength(1000);

            entity.Property(x => x.FileHash)
                .HasMaxLength(128);

            entity.Property(x => x.SchemaVersion)
                .HasMaxLength(50);

            entity.Property(x => x.BuildingName)
                .HasMaxLength(250);

            entity.Property(x => x.FileSizeBytes)
                .IsRequired();

            entity.Property(x => x.IsActive)
                .IsRequired();

            entity.Property(x => x.IsCacheValid)
                .IsRequired();

            entity.Property(x => x.ImportedAtUtc)
                .IsRequired();

            entity.HasIndex(x => x.ProjectInfoId);

            entity.HasIndex(x => x.RelativePath);

            entity.HasIndex(x => x.FileHash);

            entity.HasOne(x => x.ProjectInfo)
                .WithMany(x => x.IfcFiles)
                .HasForeignKey(x => x.ProjectInfoId)
                .OnDelete(DeleteBehavior.Cascade);

            // Deferred until geometry cache service exists.
            entity.Ignore(x => x.GeometryCache);
        }
    }
}
