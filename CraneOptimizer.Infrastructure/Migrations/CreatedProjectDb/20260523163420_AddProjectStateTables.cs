using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CraneOptimizer.Infrastructure.Migrations.CreatedProjectDb
{
    /// <inheritdoc />
    public partial class AddProjectStateTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                table: "ProjectIfcFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "ProjectIfcFiles",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TransformJson",
                table: "ProjectIfcFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CraneSetups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectInfoId = table.Column<int>(type: "INTEGER", nullable: false),
                    SceneCraneGuid = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    SourceCraneId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SourceCraneSpecificationId = table.Column<int>(type: "INTEGER", nullable: true),
                    CraneName = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    CraneType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PositionX = table.Column<double>(type: "REAL", nullable: false),
                    PositionY = table.Column<double>(type: "REAL", nullable: false),
                    PositionZ = table.Column<double>(type: "REAL", nullable: false),
                    Rotation = table.Column<double>(type: "REAL", nullable: false),
                    SlewAngle = table.Column<double>(type: "REAL", nullable: false),
                    PivotOffsetX = table.Column<double>(type: "REAL", nullable: false),
                    PivotOffsetY = table.Column<double>(type: "REAL", nullable: false),
                    PivotOffsetZ = table.Column<double>(type: "REAL", nullable: false),
                    BoomAngle = table.Column<double>(type: "REAL", nullable: false),
                    BoomLength = table.Column<double>(type: "REAL", nullable: false),
                    IsSelected = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsValidPlacement = table.Column<bool>(type: "INTEGER", nullable: false),
                    CurrentRadius = table.Column<double>(type: "REAL", nullable: true),
                    CurrentCapacity = table.Column<double>(type: "REAL", nullable: true),
                    ConfigurationJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CraneSetups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CraneSetups_ProjectInfo_ProjectInfoId",
                        column: x => x.ProjectInfoId,
                        principalTable: "ProjectInfo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectModelFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectInfoId = table.Column<int>(type: "INTEGER", nullable: false),
                    FileType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    OriginalFullPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    LastWriteUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TransformJson = table.Column<string>(type: "TEXT", nullable: true),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectModelFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectModelFiles_ProjectInfo_ProjectInfoId",
                        column: x => x.ProjectInfoId,
                        principalTable: "ProjectInfo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectViewportStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectInfoId = table.Column<int>(type: "INTEGER", nullable: false),
                    StateKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OriginX = table.Column<double>(type: "REAL", nullable: true),
                    OriginY = table.Column<double>(type: "REAL", nullable: true),
                    OriginZ = table.Column<double>(type: "REAL", nullable: true),
                    ShowCraneRadius = table.Column<bool>(type: "INTEGER", nullable: false),
                    RenderQualityLabel = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CameraJson = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectViewportStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectViewportStates_ProjectInfo_ProjectInfoId",
                        column: x => x.ProjectInfoId,
                        principalTable: "ProjectInfo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SiteZones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectInfoId = table.Column<int>(type: "INTEGER", nullable: false),
                    ZoneGuid = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    ZoneType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Area = table.Column<double>(type: "REAL", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    VerticesJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteZones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiteZones_ProjectInfo_ProjectInfoId",
                        column: x => x.ProjectInfoId,
                        principalTable: "ProjectInfo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CraneSetups_ProjectInfoId",
                table: "CraneSetups",
                column: "ProjectInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_CraneSetups_SourceCraneId",
                table: "CraneSetups",
                column: "SourceCraneId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectModelFiles_ProjectInfoId",
                table: "ProjectModelFiles",
                column: "ProjectInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectModelFiles_ProjectInfoId_FileType_Role",
                table: "ProjectModelFiles",
                columns: new[] { "ProjectInfoId", "FileType", "Role" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectModelFiles_RelativePath",
                table: "ProjectModelFiles",
                column: "RelativePath");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectViewportStates_ProjectInfoId_StateKey",
                table: "ProjectViewportStates",
                columns: new[] { "ProjectInfoId", "StateKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteZones_ProjectInfoId",
                table: "SiteZones",
                column: "ProjectInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteZones_ZoneGuid",
                table: "SiteZones",
                column: "ZoneGuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CraneSetups");

            migrationBuilder.DropTable(
                name: "ProjectModelFiles");

            migrationBuilder.DropTable(
                name: "ProjectViewportStates");

            migrationBuilder.DropTable(
                name: "SiteZones");

            migrationBuilder.DropColumn(
                name: "MetadataJson",
                table: "ProjectIfcFiles");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "ProjectIfcFiles");

            migrationBuilder.DropColumn(
                name: "TransformJson",
                table: "ProjectIfcFiles");
        }
    }
}
