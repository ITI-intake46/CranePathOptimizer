using CraneOptimizer.Domain.CreatedProject.CraneFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CraneOptimizer.Infrastructure.Contexts.CreatedProject.CreatedProjectConfigurations
{
    public sealed class ProjectCraneLoadChartSnapshotConfiguration : IEntityTypeConfiguration<ProjectCraneLoadChartSnapshotEntity>
    {
        public void Configure(EntityTypeBuilder<ProjectCraneLoadChartSnapshotEntity> entity)
        {
            entity.ToTable("ProjectCraneLoadChartSnapshots");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.RadiusM)
                .IsRequired();

            entity.Property(x => x.BoomLengthM)
                .IsRequired();

            entity.Property(x => x.CapacityTons)
                .IsRequired();

            entity.Property(x => x.VisualColumnHeader)
                .HasMaxLength(120);

            entity.Property(x => x.Notes)
                .HasMaxLength(500);

            entity.HasIndex(x => x.CraneSnapshotId);

            entity.HasIndex(x => new { x.CraneSnapshotId, x.RadiusM, x.BoomLengthM, x.VisualColumnIndex })
                .IsUnique();

            entity.HasOne(x => x.CraneSnapshot)
                .WithMany(x => x.LoadChartRows)
                .HasForeignKey(x => x.CraneSnapshotId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
