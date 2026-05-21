using CraneOptimizer.Domain.CreatedProject.IfcFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CraneOptimizer.Infrastructure.Contexts.CreatedProject.CreatedProjectConfigurations
{
    public sealed class ProjectIfcElementConfiguration : IEntityTypeConfiguration<ProjectIfcElementEntity>
    {
        public void Configure(EntityTypeBuilder<ProjectIfcElementEntity> entity)
        {
            entity.ToTable("ProjectIfcElements");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.GlobalId)
                .IsRequired()
                .HasMaxLength(128);

            entity.Property(x => x.Name)
                .HasMaxLength(500);

            entity.Property(x => x.DisplayName)
                .HasMaxLength(500);

            entity.Property(x => x.Mark)
                .HasMaxLength(250);

            entity.Property(x => x.ElementType)
                .HasMaxLength(100);

            entity.Property(x => x.StoreyName)
                .HasMaxLength(250);

            entity.Property(x => x.Material)
                .HasMaxLength(250);

            entity.Property(x => x.CachedAtUtc)
                .IsRequired();

            entity.HasIndex(x => x.ProjectInfoId);

            entity.HasIndex(x => x.IfcFileId);

            entity.HasIndex(x => new { x.IfcFileId, x.GlobalId })
                .IsUnique();

            entity.HasIndex(x => x.ElementType);

            entity.HasIndex(x => x.StoreyName);

            entity.HasIndex(x => x.Material);

            entity.HasIndex(x => x.WeightTons);

            entity.HasIndex(x => new { x.COG_X, x.COG_Y, x.COG_Z });

            entity.HasOne(x => x.ProjectInfo)
                .WithMany(x => x.IfcElements)
                .HasForeignKey(x => x.ProjectInfoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.IfcFile)
                .WithMany(x => x.Elements)
                .HasForeignKey(x => x.IfcFileId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
