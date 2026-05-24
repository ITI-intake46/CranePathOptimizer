using CraneOptimizer.Domain.CreatedProject.ProjectState;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CraneOptimizer.Infrastructure.Contexts.CreatedProject.CreatedProjectConfigurations
{
    public sealed class ProjectViewportStateConfiguration : IEntityTypeConfiguration<ProjectViewportStateEntity>
    {
        public void Configure(EntityTypeBuilder<ProjectViewportStateEntity> entity)
        {
            entity.ToTable("ProjectViewportStates");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.StateKey)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.RenderQualityLabel)
                .HasMaxLength(50);

            entity.Property(x => x.CameraJson)
                .HasColumnType("TEXT");

            entity.Property(x => x.UpdatedAtUtc)
                .IsRequired();

            entity.HasIndex(x => new { x.ProjectInfoId, x.StateKey })
                .IsUnique();

            entity.HasOne(x => x.ProjectInfo)
                .WithMany(x => x.ViewportStates)
                .HasForeignKey(x => x.ProjectInfoId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
