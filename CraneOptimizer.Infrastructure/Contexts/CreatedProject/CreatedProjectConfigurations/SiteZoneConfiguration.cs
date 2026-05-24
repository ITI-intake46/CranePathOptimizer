using CraneOptimizer.Domain.CreatedProject.ProjectState;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CraneOptimizer.Infrastructure.Contexts.CreatedProject.CreatedProjectConfigurations
{
    public sealed class SiteZoneConfiguration : IEntityTypeConfiguration<SiteZoneEntity>
    {
        public void Configure(EntityTypeBuilder<SiteZoneEntity> entity)
        {
            entity.ToTable("SiteZones");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.ZoneGuid)
                .IsRequired()
                .HasMaxLength(32);

            entity.Property(x => x.Name)
                .HasMaxLength(250);

            entity.Property(x => x.ZoneType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.VerticesJson)
                .IsRequired()
                .HasColumnType("TEXT");

            entity.HasIndex(x => x.ProjectInfoId);
            entity.HasIndex(x => x.ZoneGuid);

            entity.HasOne(x => x.ProjectInfo)
                .WithMany(x => x.SiteZones)
                .HasForeignKey(x => x.ProjectInfoId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
