using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CraneOptimizer.Infrastructure.Migrations.CreatedProjectDb
{
    /// <inheritdoc />
    public partial class InitialCreatedProjectDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectInfo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectGuid = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    UnitSystem = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LengthUnit = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AreaUnit = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AppVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    SchemaVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    CacheVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastOpenedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectInfo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectCraneSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectInfoId = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceCraneId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CraneName = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    CraneType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Manufacturer = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    Model = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    MaxCapacityTons = table.Column<double>(type: "REAL", nullable: true),
                    VehicleLengthM = table.Column<double>(type: "REAL", nullable: true),
                    VehicleWidthM = table.Column<double>(type: "REAL", nullable: true),
                    VehicleHeightM = table.Column<double>(type: "REAL", nullable: true),
                    OutriggerLengthM = table.Column<double>(type: "REAL", nullable: true),
                    OutriggerWidthM = table.Column<double>(type: "REAL", nullable: true),
                    MinRadiusM = table.Column<double>(type: "REAL", nullable: true),
                    MaxRadiusM = table.Column<double>(type: "REAL", nullable: true),
                    MinBoomLengthM = table.Column<double>(type: "REAL", nullable: true),
                    MaxBoomLengthM = table.Column<double>(type: "REAL", nullable: true),
                    MaxHookHeightM = table.Column<double>(type: "REAL", nullable: true),
                    CounterWeightTons = table.Column<double>(type: "REAL", nullable: true),
                    MachineRotation = table.Column<double>(type: "REAL", nullable: true),
                    DeratingRatio = table.Column<double>(type: "REAL", nullable: true),
                    ConfigurationJson = table.Column<string>(type: "TEXT", nullable: true),
                    OutriggerConfig = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    SnapshottedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectCraneSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectCraneSnapshots_ProjectInfo_ProjectInfoId",
                        column: x => x.ProjectInfoId,
                        principalTable: "ProjectInfo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectIfcFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectInfoId = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    OriginalFullPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    LastWriteUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FileHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    SchemaVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsCacheValid = table.Column<bool>(type: "INTEGER", nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BuildingName = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    NumberOfStoreys = table.Column<int>(type: "INTEGER", nullable: true),
                    BuildingAreaM2 = table.Column<double>(type: "REAL", nullable: true),
                    BuildingHeightM = table.Column<double>(type: "REAL", nullable: true),
                    ElementCount = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectIfcFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectIfcFiles_ProjectInfo_ProjectInfoId",
                        column: x => x.ProjectInfoId,
                        principalTable: "ProjectInfo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectCraneLoadChartSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CraneSnapshotId = table.Column<int>(type: "INTEGER", nullable: false),
                    RadiusM = table.Column<double>(type: "REAL", nullable: false),
                    BoomLengthM = table.Column<double>(type: "REAL", nullable: false),
                    CapacityTons = table.Column<double>(type: "REAL", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectCraneLoadChartSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectCraneLoadChartSnapshots_ProjectCraneSnapshots_CraneSnapshotId",
                        column: x => x.CraneSnapshotId,
                        principalTable: "ProjectCraneSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectOptimizationRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectInfoId = table.Column<int>(type: "INTEGER", nullable: false),
                    CraneSnapshotId = table.Column<int>(type: "INTEGER", nullable: true),
                    RunName = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TotalElements = table.Column<int>(type: "INTEGER", nullable: true),
                    SafeElements = table.Column<int>(type: "INTEGER", nullable: true),
                    UnsafeElements = table.Column<int>(type: "INTEGER", nullable: true),
                    CoverageRatio = table.Column<double>(type: "REAL", nullable: true),
                    AverageUtilizationPercent = table.Column<double>(type: "REAL", nullable: true),
                    SettingsJson = table.Column<string>(type: "TEXT", nullable: true),
                    ResultSummaryJson = table.Column<string>(type: "TEXT", nullable: true),
                    CranePositionsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectOptimizationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectOptimizationRuns_ProjectCraneSnapshots_CraneSnapshotId",
                        column: x => x.CraneSnapshotId,
                        principalTable: "ProjectCraneSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProjectOptimizationRuns_ProjectInfo_ProjectInfoId",
                        column: x => x.ProjectInfoId,
                        principalTable: "ProjectInfo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectIfcElements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectInfoId = table.Column<int>(type: "INTEGER", nullable: false),
                    IfcFileId = table.Column<int>(type: "INTEGER", nullable: false),
                    GlobalId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    EntityLabel = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Mark = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    ElementType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    StoreyName = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    Material = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    LengthM = table.Column<double>(type: "REAL", nullable: true),
                    WidthM = table.Column<double>(type: "REAL", nullable: true),
                    HeightM = table.Column<double>(type: "REAL", nullable: true),
                    VolumeM3 = table.Column<double>(type: "REAL", nullable: true),
                    WeightTons = table.Column<double>(type: "REAL", nullable: true),
                    COG_X = table.Column<double>(type: "REAL", nullable: true),
                    COG_Y = table.Column<double>(type: "REAL", nullable: true),
                    COG_Z = table.Column<double>(type: "REAL", nullable: true),
                    BBoxMinX = table.Column<double>(type: "REAL", nullable: true),
                    BBoxMinY = table.Column<double>(type: "REAL", nullable: true),
                    BBoxMinZ = table.Column<double>(type: "REAL", nullable: true),
                    BBoxMaxX = table.Column<double>(type: "REAL", nullable: true),
                    BBoxMaxY = table.Column<double>(type: "REAL", nullable: true),
                    BBoxMaxZ = table.Column<double>(type: "REAL", nullable: true),
                    IsLiftable = table.Column<bool>(type: "INTEGER", nullable: false),
                    CachedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectIfcElements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectIfcElements_ProjectIfcFiles_IfcFileId",
                        column: x => x.IfcFileId,
                        principalTable: "ProjectIfcFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectIfcElements_ProjectInfo_ProjectInfoId",
                        column: x => x.ProjectInfoId,
                        principalTable: "ProjectInfo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCraneLoadChartSnapshots_CraneSnapshotId",
                table: "ProjectCraneLoadChartSnapshots",
                column: "CraneSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCraneLoadChartSnapshots_CraneSnapshotId_RadiusM_BoomLengthM",
                table: "ProjectCraneLoadChartSnapshots",
                columns: new[] { "CraneSnapshotId", "RadiusM", "BoomLengthM" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCraneSnapshots_ProjectInfoId",
                table: "ProjectCraneSnapshots",
                column: "ProjectInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCraneSnapshots_SourceCraneId",
                table: "ProjectCraneSnapshots",
                column: "SourceCraneId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectIfcElements_COG_X_COG_Y_COG_Z",
                table: "ProjectIfcElements",
                columns: new[] { "COG_X", "COG_Y", "COG_Z" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectIfcElements_ElementType",
                table: "ProjectIfcElements",
                column: "ElementType");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectIfcElements_IfcFileId",
                table: "ProjectIfcElements",
                column: "IfcFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectIfcElements_IfcFileId_GlobalId",
                table: "ProjectIfcElements",
                columns: new[] { "IfcFileId", "GlobalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectIfcElements_Material",
                table: "ProjectIfcElements",
                column: "Material");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectIfcElements_ProjectInfoId",
                table: "ProjectIfcElements",
                column: "ProjectInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectIfcElements_StoreyName",
                table: "ProjectIfcElements",
                column: "StoreyName");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectIfcElements_WeightTons",
                table: "ProjectIfcElements",
                column: "WeightTons");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectIfcFiles_FileHash",
                table: "ProjectIfcFiles",
                column: "FileHash");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectIfcFiles_ProjectInfoId",
                table: "ProjectIfcFiles",
                column: "ProjectInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectIfcFiles_RelativePath",
                table: "ProjectIfcFiles",
                column: "RelativePath");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectInfo_ProjectGuid",
                table: "ProjectInfo",
                column: "ProjectGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectOptimizationRuns_CraneSnapshotId",
                table: "ProjectOptimizationRuns",
                column: "CraneSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectOptimizationRuns_ProjectInfoId",
                table: "ProjectOptimizationRuns",
                column: "ProjectInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectOptimizationRuns_StartedAtUtc",
                table: "ProjectOptimizationRuns",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectOptimizationRuns_Status",
                table: "ProjectOptimizationRuns",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectCraneLoadChartSnapshots");

            migrationBuilder.DropTable(
                name: "ProjectIfcElements");

            migrationBuilder.DropTable(
                name: "ProjectOptimizationRuns");

            migrationBuilder.DropTable(
                name: "ProjectIfcFiles");

            migrationBuilder.DropTable(
                name: "ProjectCraneSnapshots");

            migrationBuilder.DropTable(
                name: "ProjectInfo");
        }
    }
}
