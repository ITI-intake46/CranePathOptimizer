using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WPF_IFC_Helix.Migrations
{
    /// <inheritdoc />
    public partial class FullDatabaseSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "Zones",
                type: "TEXT",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Zones",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "ColorHex",
                table: "Zones",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "VolumeUnit",
                table: "Projects",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                defaultValue: "m3",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "UnitSystem",
                table: "Projects",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Metric",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "ProjectDescription",
                table: "Projects",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "PressureUnit",
                table: "Projects",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                defaultValue: "kPa",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "MassUnit",
                table: "Projects",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                defaultValue: "kg",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "Location",
                table: "Projects",
                type: "TEXT",
                maxLength: 300,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<string>(
                name: "LengthUnit",
                table: "Projects",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                defaultValue: "m",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "ForceUnit",
                table: "Projects",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                defaultValue: "kN",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "DecimalSeparator",
                table: "Projects",
                type: "TEXT",
                maxLength: 5,
                nullable: false,
                defaultValue: ".",
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Country",
                table: "Projects",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "AreaUnit",
                table: "Projects",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                defaultValue: "m2",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "WeightUnit",
                table: "IfcElements",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                defaultValue: "kg",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "IfcElements",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "IfcElements",
                type: "TEXT",
                maxLength: 300,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<string>(
                name: "Material",
                table: "IfcElements",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "Mark",
                table: "IfcElements",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "ElementColor",
                table: "IfcElements",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "#FFFFFF",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "IfcElements",
                type: "TEXT",
                maxLength: 300,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "Cranes",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 200);

            migrationBuilder.CreateTable(
                name: "CraneCatalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CraneType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Manufacturer = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, defaultValue: ""),
                    Model = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, defaultValue: ""),
                    Description = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    MaxCapacityKg = table.Column<double>(type: "REAL", nullable: false),
                    MaxReachM = table.Column<double>(type: "REAL", nullable: false),
                    DefaultTowerHeight = table.Column<double>(type: "REAL", nullable: true),
                    DefaultBoomLength = table.Column<double>(type: "REAL", nullable: true),
                    DefaultCounterJibLength = table.Column<double>(type: "REAL", nullable: true),
                    DefaultBaseRadius = table.Column<double>(type: "REAL", nullable: true),
                    DefaultCarrierLength = table.Column<double>(type: "REAL", nullable: true),
                    DefaultCarrierWidth = table.Column<double>(type: "REAL", nullable: true),
                    DefaultCarrierHeight = table.Column<double>(type: "REAL", nullable: true),
                    DefaultOutriggerSpread = table.Column<double>(type: "REAL", nullable: true),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CraneCatalog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CraneZoneConflicts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    CraneId = table.Column<int>(type: "INTEGER", nullable: false),
                    ZoneId = table.Column<int>(type: "INTEGER", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Warning"),
                    Description = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    CraneX = table.Column<double>(type: "REAL", nullable: false),
                    CraneY = table.Column<double>(type: "REAL", nullable: false),
                    CraneZ = table.Column<double>(type: "REAL", nullable: false),
                    SlewAngle = table.Column<double>(type: "REAL", nullable: false),
                    IsResolved = table.Column<bool>(type: "INTEGER", nullable: false),
                    Resolution = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    DetectedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CraneZoneConflicts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CraneZoneConflicts_Cranes_CraneId",
                        column: x => x.CraneId,
                        principalTable: "Cranes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CraneZoneConflicts_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CraneZoneConflicts_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DimensionAnnotations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    AnnotationType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Linear"),
                    Label = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, defaultValue: ""),
                    StartX = table.Column<double>(type: "REAL", nullable: false),
                    StartY = table.Column<double>(type: "REAL", nullable: false),
                    StartZ = table.Column<double>(type: "REAL", nullable: false),
                    EndX = table.Column<double>(type: "REAL", nullable: false),
                    EndY = table.Column<double>(type: "REAL", nullable: false),
                    EndZ = table.Column<double>(type: "REAL", nullable: false),
                    OffsetDistance = table.Column<double>(type: "REAL", nullable: false),
                    IsVisible = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DimensionAnnotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DimensionAnnotations_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DxfFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false, defaultValue: ""),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false, defaultValue: ""),
                    ImportedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    AlignmentMatrixJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    RefPoint1_X = table.Column<double>(type: "REAL", nullable: true),
                    RefPoint1_Y = table.Column<double>(type: "REAL", nullable: true),
                    RefPoint2_X = table.Column<double>(type: "REAL", nullable: true),
                    RefPoint2_Y = table.Column<double>(type: "REAL", nullable: true),
                    Scale = table.Column<double>(type: "REAL", nullable: true),
                    RotationDeg = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DxfFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DxfFiles_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ElementGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    ColorHex = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "#3498DB"),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElementGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElementGroups_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IfcFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false, defaultValue: ""),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false, defaultValue: ""),
                    Role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Building"),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    LoadedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BuildingName = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    NumberOfStoreys = table.Column<int>(type: "INTEGER", nullable: true),
                    BuildingArea = table.Column<double>(type: "REAL", nullable: true),
                    BuildingHeight = table.Column<double>(type: "REAL", nullable: true),
                    ElementCount = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IfcFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IfcFiles_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LiftPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    CraneId = table.Column<int>(type: "INTEGER", nullable: false),
                    ElementId = table.Column<int>(type: "INTEGER", nullable: false),
                    LiftName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, defaultValue: ""),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Planned"),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    Phase = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, defaultValue: ""),
                    PlannedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EstimatedDuration = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    ActualStartTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ActualEndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LiftWeightKg = table.Column<double>(type: "REAL", nullable: true),
                    LiftRadiusM = table.Column<double>(type: "REAL", nullable: true),
                    LiftHeightM = table.Column<double>(type: "REAL", nullable: true),
                    CraneCapacityKg = table.Column<double>(type: "REAL", nullable: true),
                    UtilizationPct = table.Column<double>(type: "REAL", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiftPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiftPlans_Cranes_CraneId",
                        column: x => x.CraneId,
                        principalTable: "Cranes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LiftPlans_IfcElements_ElementId",
                        column: x => x.ElementId,
                        principalTable: "IfcElements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LiftPlans_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaterialCatalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MaterialName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Keywords = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false, defaultValue: ""),
                    DensityKgPerM3 = table.Column<double>(type: "REAL", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: ""),
                    Description = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialCatalog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, defaultValue: ""),
                    Content = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    TargetType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false, defaultValue: "Project"),
                    TargetId = table.Column<int>(type: "INTEGER", nullable: true),
                    Author = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, defaultValue: ""),
                    IsPinned = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectNotes_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecentProjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProjectName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, defaultValue: ""),
                    Location = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false, defaultValue: ""),
                    IfcFileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false, defaultValue: ""),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "Active"),
                    StatusColor = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "#4CAF50"),
                    LastOpenedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecentProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecentProjects_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, defaultValue: ""),
                    ReportType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: ""),
                    ContentHtml = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    GeneratedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, defaultValue: "System"),
                    GeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reports_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SiteObstacles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, defaultValue: ""),
                    ObstacleType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "Other"),
                    Description = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    MinX = table.Column<double>(type: "REAL", nullable: false),
                    MinY = table.Column<double>(type: "REAL", nullable: false),
                    MinZ = table.Column<double>(type: "REAL", nullable: false),
                    MaxX = table.Column<double>(type: "REAL", nullable: false),
                    MaxY = table.Column<double>(type: "REAL", nullable: false),
                    MaxZ = table.Column<double>(type: "REAL", nullable: false),
                    IsVisible = table.Column<bool>(type: "INTEGER", nullable: false),
                    ColorHex = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "#FF6600")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteObstacles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiteObstacles_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ElementGroupItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    ElementId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElementGroupItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElementGroupItems_ElementGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "ElementGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ElementGroupItems_IfcElements_ElementId",
                        column: x => x.ElementId,
                        principalTable: "IfcElements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BuildingAnalysisResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    IfcFileId = table.Column<int>(type: "INTEGER", nullable: true),
                    BuildingName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false, defaultValue: ""),
                    BuildingArea = table.Column<double>(type: "REAL", nullable: false),
                    BuildingHeight = table.Column<double>(type: "REAL", nullable: false),
                    NumberOfStoreys = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalWeightKg = table.Column<double>(type: "REAL", nullable: true),
                    OverallCOG_X = table.Column<double>(type: "REAL", nullable: true),
                    OverallCOG_Y = table.Column<double>(type: "REAL", nullable: true),
                    OverallCOG_Z = table.Column<double>(type: "REAL", nullable: true),
                    IsAnalyzed = table.Column<bool>(type: "INTEGER", nullable: false),
                    AnalyzedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildingAnalysisResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuildingAnalysisResults_IfcFiles_IfcFileId",
                        column: x => x.IfcFileId,
                        principalTable: "IfcFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BuildingAnalysisResults_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LiftChecklistItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LiftPlanId = table.Column<int>(type: "INTEGER", nullable: false),
                    Item = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false, defaultValue: ""),
                    IsChecked = table.Column<bool>(type: "INTEGER", nullable: false),
                    CheckedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, defaultValue: ""),
                    CheckedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiftChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiftChecklistItems_LiftPlans_LiftPlanId",
                        column: x => x.LiftPlanId,
                        principalTable: "LiftPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "MaterialCatalog",
                columns: new[] { "Id", "Category", "DensityKgPerM3", "Description", "IsSystem", "Keywords", "MaterialName" },
                values: new object[,]
                {
                    { 1, "Concrete", 2400.0, "", true, "concrete,beton", "Concrete" },
                    { 2, "Steel", 7850.0, "", true, "steel,metal", "Steel" },
                    { 3, "Wood", 600.0, "", true, "wood,timber", "Timber" },
                    { 4, "Masonry", 1800.0, "", true, "brick", "Brick" },
                    { 5, "Other", 2500.0, "", true, "glass,glazing", "Glass" },
                    { 6, "Steel", 2700.0, "", true, "aluminium,aluminum", "Aluminium" },
                    { 7, "Concrete", 2500.0, "", true, "reinforced,rc,rcc", "Reinforced Concrete" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BuildingAnalysisResults_IfcFileId",
                table: "BuildingAnalysisResults",
                column: "IfcFileId");

            migrationBuilder.CreateIndex(
                name: "IX_BuildingAnalysisResults_ProjectId",
                table: "BuildingAnalysisResults",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CraneZoneConflicts_CraneId",
                table: "CraneZoneConflicts",
                column: "CraneId");

            migrationBuilder.CreateIndex(
                name: "IX_CraneZoneConflicts_ProjectId",
                table: "CraneZoneConflicts",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CraneZoneConflicts_ZoneId",
                table: "CraneZoneConflicts",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_DimensionAnnotations_ProjectId",
                table: "DimensionAnnotations",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_DxfFiles_ProjectId",
                table: "DxfFiles",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ElementGroupItems_ElementId",
                table: "ElementGroupItems",
                column: "ElementId");

            migrationBuilder.CreateIndex(
                name: "IX_ElementGroupItems_GroupId_ElementId",
                table: "ElementGroupItems",
                columns: new[] { "GroupId", "ElementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ElementGroups_ProjectId",
                table: "ElementGroups",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_IfcFiles_ProjectId",
                table: "IfcFiles",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_LiftChecklistItems_LiftPlanId",
                table: "LiftChecklistItems",
                column: "LiftPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_LiftPlans_CraneId",
                table: "LiftPlans",
                column: "CraneId");

            migrationBuilder.CreateIndex(
                name: "IX_LiftPlans_ElementId",
                table: "LiftPlans",
                column: "ElementId");

            migrationBuilder.CreateIndex(
                name: "IX_LiftPlans_ProjectId",
                table: "LiftPlans",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialCatalog_MaterialName",
                table: "MaterialCatalog",
                column: "MaterialName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectNotes_ProjectId",
                table: "ProjectNotes",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_RecentProjects_ProjectId",
                table: "RecentProjects",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ProjectId",
                table: "Reports",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteObstacles_ProjectId",
                table: "SiteObstacles",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BuildingAnalysisResults");

            migrationBuilder.DropTable(
                name: "CraneCatalog");

            migrationBuilder.DropTable(
                name: "CraneZoneConflicts");

            migrationBuilder.DropTable(
                name: "DimensionAnnotations");

            migrationBuilder.DropTable(
                name: "DxfFiles");

            migrationBuilder.DropTable(
                name: "ElementGroupItems");

            migrationBuilder.DropTable(
                name: "LiftChecklistItems");

            migrationBuilder.DropTable(
                name: "MaterialCatalog");

            migrationBuilder.DropTable(
                name: "ProjectNotes");

            migrationBuilder.DropTable(
                name: "RecentProjects");

            migrationBuilder.DropTable(
                name: "Reports");

            migrationBuilder.DropTable(
                name: "SiteObstacles");

            migrationBuilder.DropTable(
                name: "IfcFiles");

            migrationBuilder.DropTable(
                name: "ElementGroups");

            migrationBuilder.DropTable(
                name: "LiftPlans");

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "Zones",
                type: "TEXT",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 1000,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Zones",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 200,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "ColorHex",
                table: "Zones",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 20,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "VolumeUnit",
                table: "Projects",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 10,
                oldDefaultValue: "m3");

            migrationBuilder.AlterColumn<string>(
                name: "UnitSystem",
                table: "Projects",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 20,
                oldDefaultValue: "Metric");

            migrationBuilder.AlterColumn<string>(
                name: "ProjectDescription",
                table: "Projects",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "PressureUnit",
                table: "Projects",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 10,
                oldDefaultValue: "kPa");

            migrationBuilder.AlterColumn<string>(
                name: "MassUnit",
                table: "Projects",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 10,
                oldDefaultValue: "kg");

            migrationBuilder.AlterColumn<string>(
                name: "Location",
                table: "Projects",
                type: "TEXT",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 300,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "LengthUnit",
                table: "Projects",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 10,
                oldDefaultValue: "m");

            migrationBuilder.AlterColumn<string>(
                name: "ForceUnit",
                table: "Projects",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 10,
                oldDefaultValue: "kN");

            migrationBuilder.AlterColumn<string>(
                name: "DecimalSeparator",
                table: "Projects",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 5,
                oldDefaultValue: ".");

            migrationBuilder.AlterColumn<string>(
                name: "Country",
                table: "Projects",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "AreaUnit",
                table: "Projects",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 10,
                oldDefaultValue: "m2");

            migrationBuilder.AlterColumn<string>(
                name: "WeightUnit",
                table: "IfcElements",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 10,
                oldDefaultValue: "kg");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "IfcElements",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "IfcElements",
                type: "TEXT",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 300,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Material",
                table: "IfcElements",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 500,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Mark",
                table: "IfcElements",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "ElementColor",
                table: "IfcElements",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 20,
                oldDefaultValue: "#FFFFFF");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "IfcElements",
                type: "TEXT",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 300,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "Cranes",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 200,
                oldDefaultValue: "");
        }
    }
}
