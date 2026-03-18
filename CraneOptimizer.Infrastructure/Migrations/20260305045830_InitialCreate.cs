using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WPF_IFC_Helix.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Country = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ConstructionSiteSize = table.Column<double>(type: "REAL", nullable: false),
                    ProjectDescription = table.Column<string>(type: "TEXT", nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    UnitSystem = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    LengthUnit = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    MassUnit = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    AreaUnit = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    VolumeUnit = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    PressureUnit = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    ForceUnit = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    DecimalSeparator = table.Column<string>(type: "TEXT", nullable: false),
                    ShowAdvancedParameters = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cranes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    CraneType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PositionX = table.Column<double>(type: "REAL", nullable: false),
                    PositionY = table.Column<double>(type: "REAL", nullable: false),
                    PositionZ = table.Column<double>(type: "REAL", nullable: false),
                    SlewAngle = table.Column<double>(type: "REAL", nullable: false),
                    CableLength = table.Column<double>(type: "REAL", nullable: false),
                    IsVisible = table.Column<bool>(type: "INTEGER", nullable: false),
                    ShowSwingRadius = table.Column<bool>(type: "INTEGER", nullable: false),
                    CarrierLength = table.Column<double>(type: "REAL", nullable: true),
                    CarrierWidth = table.Column<double>(type: "REAL", nullable: true),
                    CarrierHeight = table.Column<double>(type: "REAL", nullable: true),
                    BoomLength = table.Column<double>(type: "REAL", nullable: true),
                    BoomAngle = table.Column<double>(type: "REAL", nullable: true),
                    BoomSections = table.Column<int>(type: "INTEGER", nullable: true),
                    BoomBaseWidth = table.Column<double>(type: "REAL", nullable: true),
                    OutriggerSpread = table.Column<double>(type: "REAL", nullable: true),
                    OutriggerPadSize = table.Column<double>(type: "REAL", nullable: true),
                    ShowOutriggerPads = table.Column<bool>(type: "INTEGER", nullable: true),
                    TowerHeight = table.Column<double>(type: "REAL", nullable: true),
                    TowerCraneEntity_BoomLength = table.Column<double>(type: "REAL", nullable: true),
                    TowerCraneEntity_BoomAngle = table.Column<double>(type: "REAL", nullable: true),
                    CounterJibLength = table.Column<double>(type: "REAL", nullable: true),
                    BaseRadius = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cranes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cranes_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IfcElements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    GlobalId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Mark = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Length = table.Column<double>(type: "REAL", nullable: true),
                    Width = table.Column<double>(type: "REAL", nullable: true),
                    Height = table.Column<double>(type: "REAL", nullable: true),
                    Volume = table.Column<double>(type: "REAL", nullable: true),
                    Weight = table.Column<double>(type: "REAL", nullable: true),
                    DisplayWeight = table.Column<double>(type: "REAL", nullable: true),
                    WeightUnit = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Material = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    COG_X = table.Column<double>(type: "REAL", nullable: true),
                    COG_Y = table.Column<double>(type: "REAL", nullable: true),
                    COG_Z = table.Column<double>(type: "REAL", nullable: true),
                    ElementColor = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsSelected = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IfcElements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IfcElements_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Zones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ZoneType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    MinHeight = table.Column<double>(type: "REAL", nullable: false),
                    MaxHeight = table.Column<double>(type: "REAL", nullable: false),
                    IsVisible = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ColorHex = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Zones_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ZoneVertices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ZoneId = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    X = table.Column<double>(type: "REAL", nullable: false),
                    Y = table.Column<double>(type: "REAL", nullable: false),
                    Z = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZoneVertices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZoneVertices_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cranes_ProjectId",
                table: "Cranes",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_IfcElements_ProjectId_GlobalId",
                table: "IfcElements",
                columns: new[] { "ProjectId", "GlobalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Zones_ProjectId",
                table: "Zones",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ZoneVertices_ZoneId_Order",
                table: "ZoneVertices",
                columns: new[] { "ZoneId", "Order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Cranes");

            migrationBuilder.DropTable(
                name: "IfcElements");

            migrationBuilder.DropTable(
                name: "ZoneVertices");

            migrationBuilder.DropTable(
                name: "Zones");

            migrationBuilder.DropTable(
                name: "Projects");
        }
    }
}
