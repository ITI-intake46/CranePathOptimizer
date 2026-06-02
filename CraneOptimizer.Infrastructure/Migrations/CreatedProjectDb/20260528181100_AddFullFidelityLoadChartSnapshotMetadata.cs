using CraneOptimizer.Infrastructure.Contexts.CreatedProject;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CraneOptimizer.Infrastructure.Migrations.CreatedProjectDb
{
    /// <inheritdoc />
    [DbContext(typeof(CreatedProjectDbContext))]
    [Migration("20260528181100_AddFullFidelityLoadChartSnapshotMetadata")]
    public partial class AddFullFidelityLoadChartSnapshotMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectCraneLoadChartSnapshots_CraneSnapshotId_RadiusM_BoomLengthM",
                table: "ProjectCraneLoadChartSnapshots");

            migrationBuilder.AddColumn<int>(
                name: "VisualColumnIndex",
                table: "ProjectCraneLoadChartSnapshots",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VisualColumnHeader",
                table: "ProjectCraneLoadChartSnapshots",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCraneLoadChartSnapshots_CraneSnapshotId_RadiusM_BoomLengthM_VisualColumnIndex",
                table: "ProjectCraneLoadChartSnapshots",
                columns: new[] { "CraneSnapshotId", "RadiusM", "BoomLengthM", "VisualColumnIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectCraneLoadChartSnapshots_CraneSnapshotId_RadiusM_BoomLengthM_VisualColumnIndex",
                table: "ProjectCraneLoadChartSnapshots");

            migrationBuilder.DropColumn(
                name: "VisualColumnIndex",
                table: "ProjectCraneLoadChartSnapshots");

            migrationBuilder.DropColumn(
                name: "VisualColumnHeader",
                table: "ProjectCraneLoadChartSnapshots");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCraneLoadChartSnapshots_CraneSnapshotId_RadiusM_BoomLengthM",
                table: "ProjectCraneLoadChartSnapshots",
                columns: new[] { "CraneSnapshotId", "RadiusM", "BoomLengthM" },
                unique: true);
        }
    }
}
