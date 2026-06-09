using CraneOptimizer.Infrastructure.Contexts.CreatedProject;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CraneOptimizer.Infrastructure.Migrations.CreatedProjectDb
{
    /// <inheritdoc />
    [DbContext(typeof(CreatedProjectDbContext))]
    [Migration("20260608165100_AddLoadChartNumberOfLinesSnapshotMetadata")]
    public partial class AddLoadChartNumberOfLinesSnapshotMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NumberOfLines",
                table: "ProjectCraneLoadChartSnapshots",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NumberOfLines",
                table: "ProjectCraneLoadChartSnapshots");
        }
    }
}