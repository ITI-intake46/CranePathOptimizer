using CraneOptimizer.Domain.CreatedProject.CraneFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

            entity.Property(x => x.Notes)
                .HasMaxLength(500);

            entity.HasIndex(x => x.CraneSnapshotId);

            entity.HasIndex(x => new { x.CraneSnapshotId, x.RadiusM, x.BoomLengthM })
                .IsUnique();

            entity.HasOne(x => x.CraneSnapshot)
                .WithMany(x => x.LoadChartRows)
                .HasForeignKey(x => x.CraneSnapshotId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
