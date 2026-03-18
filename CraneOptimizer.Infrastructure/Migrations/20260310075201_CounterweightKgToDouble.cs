using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WPF_IFC_Helix.Migrations
{
    /// <inheritdoc />
    public partial class CounterweightKgToDouble : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CraneLoadCharts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CraneModel = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, defaultValue: ""),
                    Manufacturer = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, defaultValue: ""),
                    ConfigName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, defaultValue: ""),
                    ChartType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "360"),
                    OutriggerConfig = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, defaultValue: ""),
                    CounterweightKg = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.0),
                    Notes = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    SourceImagePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false, defaultValue: ""),
                    ImportMethod = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "CSV"),
                    ImportedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CatalogId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CraneLoadCharts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CraneLoadCharts_CraneCatalog_CatalogId",
                        column: x => x.CatalogId,
                        principalTable: "CraneCatalog",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CraneLoadChartPoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChartId = table.Column<int>(type: "INTEGER", nullable: false),
                    RadiusM = table.Column<double>(type: "REAL", nullable: false),
                    BoomLengthM = table.Column<double>(type: "REAL", nullable: false),
                    CapacityTonne = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CraneLoadChartPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CraneLoadChartPoints_CraneLoadCharts_ChartId",
                        column: x => x.ChartId,
                        principalTable: "CraneLoadCharts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CraneLoadChartPoints_ChartId_RadiusM_BoomLengthM",
                table: "CraneLoadChartPoints",
                columns: new[] { "ChartId", "RadiusM", "BoomLengthM" });

            migrationBuilder.CreateIndex(
                name: "IX_CraneLoadCharts_CatalogId",
                table: "CraneLoadCharts",
                column: "CatalogId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CraneLoadChartPoints");

            migrationBuilder.DropTable(
                name: "CraneLoadCharts");
        }
    }
}
