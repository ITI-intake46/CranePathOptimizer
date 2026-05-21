using CraneOptimizer.Domain.CreatedProject.OptimizationFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CraneOptimizer.Infrastructure.Contexts.CreatedProject.CreatedProjectConfigurations
{
    public sealed class ProjectOptimizationRunConfiguration : IEntityTypeConfiguration<ProjectOptimizationRunEntity>
    {
        public void Configure(EntityTypeBuilder<ProjectOptimizationRunEntity> entity)
        {
            entity.ToTable("ProjectOptimizationRuns");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.RunName)
                .HasMaxLength(250);

            entity.Property(x => x.Status)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.StartedAtUtc)
                .IsRequired();

            entity.Property(x => x.SettingsJson)
                .HasColumnType("TEXT");

            entity.Property(x => x.ResultSummaryJson)
                .HasColumnType("TEXT");

            entity.Property(x => x.CranePositionsJson)
                .HasColumnType("TEXT");

            entity.HasIndex(x => x.ProjectInfoId);

            entity.HasIndex(x => x.CraneSnapshotId);

            entity.HasIndex(x => x.StartedAtUtc);

            entity.HasIndex(x => x.Status);

            entity.HasOne(x => x.ProjectInfo)
                .WithMany(x => x.OptimizationRuns)
                .HasForeignKey(x => x.ProjectInfoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.CraneSnapshot)
                .WithMany(x => x.OptimizationRuns)
                .HasForeignKey(x => x.CraneSnapshotId)
                .OnDelete(DeleteBehavior.SetNull);

            // Deferred until detailed per-element result persistence is required.
            entity.Ignore(x => x.ElementResults);
        }
    }
}
