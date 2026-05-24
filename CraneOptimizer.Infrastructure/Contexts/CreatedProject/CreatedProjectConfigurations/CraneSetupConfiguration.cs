using CraneOptimizer.Domain.CreatedProject.ProjectState;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CraneOptimizer.Infrastructure.Contexts.CreatedProject.CreatedProjectConfigurations
{
    public sealed class CraneSetupConfiguration : IEntityTypeConfiguration<CraneSetupEntity>
    {
        public void Configure(EntityTypeBuilder<CraneSetupEntity> entity)
        {
            entity.ToTable("CraneSetups");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.SceneCraneGuid)
                .IsRequired()
                .HasMaxLength(32);

            entity.Property(x => x.SourceCraneId)
                .HasMaxLength(100);

            entity.Property(x => x.CraneName)
                .IsRequired()
                .HasMaxLength(250);

            entity.Property(x => x.CraneType)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.ConfigurationJson)
                .HasColumnType("TEXT");

            entity.HasIndex(x => x.ProjectInfoId);
            entity.HasIndex(x => x.SourceCraneId);

            entity.HasOne(x => x.ProjectInfo)
                .WithMany(x => x.CraneSetups)
                .HasForeignKey(x => x.ProjectInfoId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
