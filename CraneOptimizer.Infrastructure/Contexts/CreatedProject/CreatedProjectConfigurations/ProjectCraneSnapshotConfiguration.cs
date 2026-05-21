using CraneOptimizer.Domain.CreatedProject.CraneFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CraneOptimizer.Infrastructure.Contexts.CreatedProject.CreatedProjectConfigurations
{
    public sealed class ProjectCraneSnapshotConfiguration : IEntityTypeConfiguration<ProjectCraneSnapshotEntity>
    {
        public void Configure(EntityTypeBuilder<ProjectCraneSnapshotEntity> entity)
        {
            entity.ToTable("ProjectCraneSnapshots");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.SourceCraneId)
                .HasMaxLength(100);

            entity.Property(x => x.CraneName)
                .IsRequired()
                .HasMaxLength(250);

            entity.Property(x => x.Manufacturer)
                .HasMaxLength(250);

            entity.Property(x => x.Model)
                .HasMaxLength(250);

            entity.Property(x => x.CraneType)
                .HasMaxLength(100);

            entity.Property(x => x.OutriggerConfig)
                .HasMaxLength(250);

            entity.Property(x => x.VehicleLengthM);

            entity.Property(x => x.VehicleWidthM);

            entity.Property(x => x.VehicleHeightM);

            entity.Property(x => x.OutriggerLengthM);

            entity.Property(x => x.OutriggerWidthM);

            entity.Property(x => x.MinRadiusM);

            entity.Property(x => x.MinBoomLengthM);

            entity.Property(x => x.MaxBoomLengthM);

            entity.Property(x => x.CounterWeightTons);

            entity.Property(x => x.MachineRotation);

            entity.Property(x => x.DeratingRatio);

            entity.Property(x => x.ConfigurationJson)
                .HasColumnType("TEXT");

            entity.Property(x => x.SnapshottedAtUtc)
                .IsRequired();

            entity.HasIndex(x => x.ProjectInfoId);

            entity.HasIndex(x => x.SourceCraneId);

            entity.HasOne(x => x.ProjectInfo)
                .WithMany(x => x.CraneSnapshots)
                .HasForeignKey(x => x.ProjectInfoId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
