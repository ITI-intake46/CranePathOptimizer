using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WPF_IFC_test1.Database;

#nullable disable

namespace WPF_IFC_Helix.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260311000000_CraneDimensions")]
    public partial class CraneDimensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "OutriggerSpanMm",
                table: "CraneLoadCharts",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "TotalLengthMm",
                table: "CraneLoadCharts",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "HeightMm",
                table: "CraneLoadCharts",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "SpanToSpanMm",
                table: "CraneLoadCharts",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "BoundingRadiusM",
                table: "CraneLoadCharts",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OutriggerSpanMm",
                table: "CraneLoadCharts");

            migrationBuilder.DropColumn(
                name: "TotalLengthMm",
                table: "CraneLoadCharts");

            migrationBuilder.DropColumn(
                name: "HeightMm",
                table: "CraneLoadCharts");

            migrationBuilder.DropColumn(
                name: "SpanToSpanMm",
                table: "CraneLoadCharts");

            migrationBuilder.DropColumn(
                name: "BoundingRadiusM",
                table: "CraneLoadCharts");
        }
    }
}
