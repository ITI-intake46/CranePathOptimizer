using CraneOptimizer.Domain.CreatedProject.ProjectState;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CraneOptimizer.Infrastructure.Contexts.CreatedProject.CreatedProjectConfigurations
{
    public sealed class ProjectModelFileConfiguration : IEntityTypeConfiguration<ProjectModelFileEntity>
    {
        public void Configure(EntityTypeBuilder<ProjectModelFileEntity> entity)
        {
            entity.ToTable("ProjectModelFiles");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.FileType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Role)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.FileName)
                .IsRequired()
                .HasMaxLength(260);

            entity.Property(x => x.RelativePath)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(x => x.OriginalFullPath)
                .HasMaxLength(1000);

            entity.Property(x => x.TransformJson)
                .HasColumnType("TEXT");

            entity.Property(x => x.MetadataJson)
                .HasColumnType("TEXT");

            entity.Property(x => x.IsActive)
                .IsRequired();

            entity.Property(x => x.ImportedAtUtc)
                .IsRequired();

            entity.HasIndex(x => x.ProjectInfoId);
            entity.HasIndex(x => x.RelativePath);
            entity.HasIndex(x => new { x.ProjectInfoId, x.FileType, x.Role });

            entity.HasOne(x => x.ProjectInfo)
                .WithMany(x => x.ModelFiles)
                .HasForeignKey(x => x.ProjectInfoId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
