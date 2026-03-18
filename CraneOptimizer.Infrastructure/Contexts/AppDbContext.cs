using Microsoft.EntityFrameworkCore;
using WPF_IFC_test1.Database.Entities;

namespace WPF_IFC_test1.Database
{
    public class AppDbContext : DbContext
    {
        // ── Original tables ───────────────────────────────────────────────────
        public DbSet<ProjectEntity> Projects { get; set; }
        public DbSet<IfcElementEntity> IfcElements { get; set; }
        public DbSet<CraneEntity> Cranes { get; set; }
        public DbSet<ZoneEntity> Zones { get; set; }
        public DbSet<ZoneVertexEntity> ZoneVertices { get; set; }

        // ── File tracking ─────────────────────────────────────────────────────
        public DbSet<IfcFileEntity> IfcFiles { get; set; }
        public DbSet<DxfFileEntity> DxfFiles { get; set; }

        // ── Project management ────────────────────────────────────────────────
        public DbSet<RecentProjectEntity> RecentProjects { get; set; }

        // ── Analysis ──────────────────────────────────────────────────────────
        public DbSet<BuildingAnalysisResultEntity> BuildingAnalysisResults { get; set; }

        // ── Lift planning ─────────────────────────────────────────────────────
        public DbSet<LiftPlanEntity> LiftPlans { get; set; }
        public DbSet<LiftChecklistItemEntity> LiftChecklistItems { get; set; }

        // ── Safety & conflicts ────────────────────────────────────────────────
        public DbSet<CraneZoneConflictEntity> CraneZoneConflicts { get; set; }

        // ── Site ──────────────────────────────────────────────────────────────
        public DbSet<SiteObstacleEntity> SiteObstacles { get; set; }

        // ── Reference catalogs ────────────────────────────────────────────────
        public DbSet<MaterialCatalogEntity> MaterialCatalog { get; set; }
        public DbSet<CraneCatalogEntity> CraneCatalog { get; set; }

        // ── Element grouping ──────────────────────────────────────────────────
        public DbSet<ElementGroupEntity> ElementGroups { get; set; }
        public DbSet<ElementGroupItemEntity> ElementGroupItems { get; set; }

        // ── Annotations & notes ───────────────────────────────────────────────
        public DbSet<DimensionAnnotationEntity> DimensionAnnotations { get; set; }
        public DbSet<ProjectNoteEntity> ProjectNotes { get; set; }

        // ── Reports ───────────────────────────────────────────────────────────
        public DbSet<ReportEntity> Reports { get; set; }

        // ── Load Charts ─────────────────────────────────────────────────────────
        public DbSet<CraneLoadChartEntity> CraneLoadCharts { get; set; }
        public DbSet<CraneLoadChartPointEntity> CraneLoadChartPoints { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ── Project ───────────────────────────────────────────────────────
            modelBuilder.Entity<ProjectEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.ProjectName).IsRequired().HasMaxLength(200);
                e.Property(x => x.Country).HasMaxLength(100).HasDefaultValue("");
                e.Property(x => x.Location).HasMaxLength(300).HasDefaultValue("");
                e.Property(x => x.UnitSystem).HasMaxLength(20).HasDefaultValue("Metric");
                e.Property(x => x.LengthUnit).HasMaxLength(10).HasDefaultValue("m");
                e.Property(x => x.MassUnit).HasMaxLength(10).HasDefaultValue("kg");
                e.Property(x => x.AreaUnit).HasMaxLength(10).HasDefaultValue("m2");
                e.Property(x => x.VolumeUnit).HasMaxLength(10).HasDefaultValue("m3");
                e.Property(x => x.PressureUnit).HasMaxLength(10).HasDefaultValue("kPa");
                e.Property(x => x.ForceUnit).HasMaxLength(10).HasDefaultValue("kN");
                e.Property(x => x.ProjectDescription).HasDefaultValue("");
                e.Property(x => x.DecimalSeparator).HasMaxLength(5).HasDefaultValue(".");

                e.HasMany(x => x.IfcElements).WithOne(x => x.Project).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.Cranes).WithOne(x => x.Project).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.Zones).WithOne(x => x.Project).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.IfcFiles).WithOne(x => x.Project).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.DxfFiles).WithOne(x => x.Project).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.LiftPlans).WithOne(x => x.Project).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.SiteObstacles).WithOne(x => x.Project).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.ElementGroups).WithOne(x => x.Project).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.DimensionAnnotations).WithOne(x => x.Project).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.ProjectNotes).WithOne(x => x.Project).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.Reports).WithOne(x => x.Project).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.CraneZoneConflicts).WithOne(x => x.Project).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.BuildingAnalysisResults).WithOne(x => x.Project).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            });

            // ── IFC Element ───────────────────────────────────────────────────
            modelBuilder.Entity<IfcElementEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.GlobalId).IsRequired().HasMaxLength(50);
                e.HasIndex(x => new { x.ProjectId, x.GlobalId }).IsUnique();
                e.Property(x => x.Name).HasMaxLength(300).HasDefaultValue("");
                e.Property(x => x.DisplayName).HasMaxLength(300).HasDefaultValue("");
                e.Property(x => x.Mark).HasMaxLength(100).HasDefaultValue("");
                e.Property(x => x.Type).HasMaxLength(100).HasDefaultValue("");
                e.Property(x => x.Material).HasMaxLength(500).HasDefaultValue("");
                e.Property(x => x.ElementColor).HasMaxLength(20).HasDefaultValue("#FFFFFF");
                e.Property(x => x.WeightUnit).HasMaxLength(10).HasDefaultValue("kg");
            });

            // ── Crane (TPH) ───────────────────────────────────────────────────
            modelBuilder.Entity<CraneEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.CraneType).IsRequired().HasMaxLength(20);
                e.Property(x => x.DisplayName).HasMaxLength(200).HasDefaultValue("");
                e.Property(x => x.AssignedConfig).HasMaxLength(200).HasDefaultValue("");
                e.HasDiscriminator(x => x.CraneType)
                 .HasValue<TowerCraneEntity>("Tower")
                 .HasValue<MobileCraneEntity>("Mobile");
            });

            // ── Zone ──────────────────────────────────────────────────────────
            modelBuilder.Entity<ZoneEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Name).HasMaxLength(200).HasDefaultValue("");
                e.Property(x => x.ZoneType).IsRequired().HasMaxLength(20);
                e.Property(x => x.Notes).HasMaxLength(1000).HasDefaultValue("");
                e.Property(x => x.ColorHex).HasMaxLength(20).HasDefaultValue("");
                e.HasMany(x => x.Vertices).WithOne(x => x.Zone).HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Cascade);
            });

            // ── Zone Vertex ───────────────────────────────────────────────────
            modelBuilder.Entity<ZoneVertexEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.ZoneId, x.Order });
            });

            // ── IFC File ──────────────────────────────────────────────────────
            modelBuilder.Entity<IfcFileEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.FilePath).HasMaxLength(500).HasDefaultValue("");
                e.Property(x => x.FileName).HasMaxLength(260).HasDefaultValue("");
                e.Property(x => x.Role).HasMaxLength(20).HasDefaultValue("Building");
                e.Property(x => x.BuildingName).HasDefaultValue("");
            });

            // ── DXF File ──────────────────────────────────────────────────────
            modelBuilder.Entity<DxfFileEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.FilePath).HasMaxLength(500).HasDefaultValue("");
                e.Property(x => x.FileName).HasMaxLength(260).HasDefaultValue("");
                e.Property(x => x.AlignmentMatrixJson).HasDefaultValue("");
            });

            // ── Recent Projects ───────────────────────────────────────────────
            modelBuilder.Entity<RecentProjectEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.ProjectName).HasMaxLength(200).HasDefaultValue("");
                e.Property(x => x.Location).HasMaxLength(500).HasDefaultValue("");
                e.Property(x => x.IfcFileName).HasMaxLength(260).HasDefaultValue("");
                e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Active");
                e.Property(x => x.StatusColor).HasMaxLength(20).HasDefaultValue("#4CAF50");
                e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            });

            // ── Building Analysis Results ─────────────────────────────────────
            modelBuilder.Entity<BuildingAnalysisResultEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.BuildingName).HasMaxLength(300).HasDefaultValue("");
                e.HasOne(x => x.IfcFile).WithMany().HasForeignKey(x => x.IfcFileId).OnDelete(DeleteBehavior.SetNull);
            });

            // ── Lift Plan ─────────────────────────────────────────────────────
            modelBuilder.Entity<LiftPlanEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Planned");
                e.Property(x => x.Phase).HasMaxLength(100).HasDefaultValue("");
                e.Property(x => x.LiftName).HasMaxLength(200).HasDefaultValue("");
                e.Property(x => x.Notes).HasDefaultValue("");
                e.HasOne(x => x.Crane).WithMany().HasForeignKey(x => x.CraneId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Element).WithMany().HasForeignKey(x => x.ElementId).OnDelete(DeleteBehavior.Restrict);
                e.HasMany(x => x.ChecklistItems).WithOne(x => x.LiftPlan).HasForeignKey(x => x.LiftPlanId).OnDelete(DeleteBehavior.Cascade);
            });

            // ── Lift Checklist ────────────────────────────────────────────────
            modelBuilder.Entity<LiftChecklistItemEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Item).HasMaxLength(500).HasDefaultValue("");
                e.Property(x => x.CheckedBy).HasMaxLength(100).HasDefaultValue("");
            });

            // ── Crane Zone Conflict ───────────────────────────────────────────
            modelBuilder.Entity<CraneZoneConflictEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Severity).HasMaxLength(20).HasDefaultValue("Warning");
                e.Property(x => x.Description).HasDefaultValue("");
                e.Property(x => x.Resolution).HasDefaultValue("");
                e.HasOne(x => x.Crane).WithMany().HasForeignKey(x => x.CraneId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Zone).WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict);
            });

            // ── Site Obstacle ─────────────────────────────────────────────────
            modelBuilder.Entity<SiteObstacleEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Name).HasMaxLength(200).HasDefaultValue("");
                e.Property(x => x.ObstacleType).HasMaxLength(50).HasDefaultValue("Other");
                e.Property(x => x.Description).HasDefaultValue("");
                e.Property(x => x.ColorHex).HasMaxLength(20).HasDefaultValue("#FF6600");
            });

            // ── Material Catalog ──────────────────────────────────────────────
            modelBuilder.Entity<MaterialCatalogEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.MaterialName).IsRequired().HasMaxLength(100);
                e.Property(x => x.Keywords).HasMaxLength(500).HasDefaultValue("");
                e.Property(x => x.Category).HasMaxLength(50).HasDefaultValue("");
                e.Property(x => x.Description).HasDefaultValue("");
                e.HasIndex(x => x.MaterialName).IsUnique();
            });

            // ── Crane Catalog ─────────────────────────────────────────────────
            modelBuilder.Entity<CraneCatalogEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.CraneType).IsRequired().HasMaxLength(20);
                e.Property(x => x.Manufacturer).HasMaxLength(100).HasDefaultValue("");
                e.Property(x => x.Model).HasMaxLength(100).HasDefaultValue("");
                e.Property(x => x.Description).HasDefaultValue("");
            });

            // ── Element Group ─────────────────────────────────────────────────
            modelBuilder.Entity<ElementGroupEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Name).HasMaxLength(200);
                e.Property(x => x.Description).HasDefaultValue("");
                e.Property(x => x.ColorHex).HasMaxLength(20).HasDefaultValue("#3498DB");
            });

            // ── Element Group Item (junction) ─────────────────────────────────
            modelBuilder.Entity<ElementGroupItemEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.GroupId, x.ElementId }).IsUnique();
                e.HasOne(x => x.Group).WithMany(x => x.Items).HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Element).WithMany().HasForeignKey(x => x.ElementId).OnDelete(DeleteBehavior.Cascade);
            });

            // ── Dimension Annotation ──────────────────────────────────────────
            modelBuilder.Entity<DimensionAnnotationEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.AnnotationType).HasMaxLength(20).HasDefaultValue("Linear");
                e.Property(x => x.Label).HasMaxLength(200).HasDefaultValue("");
            });

            // ── Project Note ──────────────────────────────────────────────────
            modelBuilder.Entity<ProjectNoteEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Title).HasMaxLength(200).HasDefaultValue("");
                e.Property(x => x.Content).HasDefaultValue("");
                e.Property(x => x.TargetType).HasMaxLength(30).HasDefaultValue("Project");
                e.Property(x => x.Author).HasMaxLength(100).HasDefaultValue("");
            });

            // ── Report ────────────────────────────────────────────────────────
            modelBuilder.Entity<ReportEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Title).HasMaxLength(200).HasDefaultValue("");
                e.Property(x => x.ReportType).HasMaxLength(50).HasDefaultValue("");
                e.Property(x => x.ContentHtml).HasDefaultValue("");
                e.Property(x => x.GeneratedBy).HasMaxLength(100).HasDefaultValue("System");
            });

            // ── Crane Load Chart ────────────────────────────────────────────────
            modelBuilder.Entity<CraneLoadChartEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.CraneModel).HasMaxLength(200).HasDefaultValue("");
                e.Property(x => x.Manufacturer).HasMaxLength(200).HasDefaultValue("");
                e.Property(x => x.ConfigName).HasMaxLength(200).HasDefaultValue("");
                e.Property(x => x.CraneCategory).HasMaxLength(20).HasDefaultValue("Mobile");
                e.Property(x => x.ChartType).HasMaxLength(50).HasDefaultValue("360");
                e.Property(x => x.OutriggerConfig).HasMaxLength(100).HasDefaultValue("");
                e.Property(x => x.CounterweightKg).HasDefaultValue(0.0);
                e.Property(x => x.OutriggerSpanMm).HasDefaultValue(0.0);
                e.Property(x => x.TotalLengthMm).HasDefaultValue(0.0);
                e.Property(x => x.HeightMm).HasDefaultValue(0.0);
                e.Property(x => x.SpanToSpanMm).HasDefaultValue(0.0);
                e.Property(x => x.BoundingRadiusM).HasDefaultValue(0.0);
                e.Property(x => x.Notes).HasDefaultValue("");
                e.Property(x => x.SourceImagePath).HasMaxLength(500).HasDefaultValue("");
                e.Property(x => x.ImportMethod).HasMaxLength(20).HasDefaultValue("CSV");
                e.HasOne(x => x.Catalog).WithMany().HasForeignKey(x => x.CatalogId).OnDelete(DeleteBehavior.SetNull);
                e.HasMany(x => x.Points).WithOne(x => x.Chart).HasForeignKey(x => x.ChartId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CraneLoadChartPointEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.ChartId, x.RadiusM, x.BoomLengthM });
            });

            // ── Seed Material Catalog ─────────────────────────────────────────
            modelBuilder.Entity<MaterialCatalogEntity>().HasData(
                new MaterialCatalogEntity { Id = 1, MaterialName = "Concrete", Keywords = "concrete,beton", DensityKgPerM3 = 2400, Category = "Concrete", Description = "", IsSystem = true },
                new MaterialCatalogEntity { Id = 2, MaterialName = "Steel", Keywords = "steel,metal", DensityKgPerM3 = 7850, Category = "Steel", Description = "", IsSystem = true },
                new MaterialCatalogEntity { Id = 3, MaterialName = "Timber", Keywords = "wood,timber", DensityKgPerM3 = 600, Category = "Wood", Description = "", IsSystem = true },
                new MaterialCatalogEntity { Id = 4, MaterialName = "Brick", Keywords = "brick", DensityKgPerM3 = 1800, Category = "Masonry", Description = "", IsSystem = true },
                new MaterialCatalogEntity { Id = 5, MaterialName = "Glass", Keywords = "glass,glazing", DensityKgPerM3 = 2500, Category = "Other", Description = "", IsSystem = true },
                new MaterialCatalogEntity { Id = 6, MaterialName = "Aluminium", Keywords = "aluminium,aluminum", DensityKgPerM3 = 2700, Category = "Steel", Description = "", IsSystem = true },
                new MaterialCatalogEntity { Id = 7, MaterialName = "Reinforced Concrete", Keywords = "reinforced,rc,rcc", DensityKgPerM3 = 2500, Category = "Concrete", Description = "", IsSystem = true }
            );
        }
    }
}