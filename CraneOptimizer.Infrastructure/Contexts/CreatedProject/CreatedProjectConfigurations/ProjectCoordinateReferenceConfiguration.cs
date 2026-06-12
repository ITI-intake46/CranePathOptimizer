using CraneOptimizer.Domain.CreatedProject;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CraneOptimizer.Infrastructure.Contexts.CreatedProject.CreatedProjectConfigurations
{
    public sealed class ProjectCoordinateReferenceConfiguration : IEntityTypeConfiguration<ProjectCoordinateReferenceEntity>
    {
        public void Configure(EntityTypeBuilder<ProjectCoordinateReferenceEntity> entity)
        {
            entity.ToTable("ProjectCoordinateReferences");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(250);

            entity.Property(x => x.CoordinateSystemName)
                .HasMaxLength(100);

            entity.Property(x => x.EPSGCode)
                .HasMaxLength(20);

            entity.Property(x => x.Source)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(x => x.ProjectInfoId);

            entity.HasOne(x => x.ProjectInfo)
                .WithMany() // Or we can configure on ProjectInfoEntity
                .HasForeignKey(x => x.ProjectInfoId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
