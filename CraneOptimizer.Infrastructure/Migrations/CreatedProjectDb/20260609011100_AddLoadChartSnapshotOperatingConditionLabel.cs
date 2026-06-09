using CraneOptimizer.Infrastructure.Contexts.CreatedProject;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CraneOptimizer.Infrastructure.Migrations.CreatedProjectDb
{
    /// <inheritdoc />
    [DbContext(typeof(CreatedProjectDbContext))]
    [Migration("20260609011100_AddLoadChartSnapshotOperatingConditionLabel")]
    public partial class AddLoadChartSnapshotOperatingConditionLabel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OperatingConditionLabel",
                table: "ProjectCraneLoadChartSnapshots",
                type: "TEXT",
                maxLength: 240,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OperatingConditionLabel",
                table: "ProjectCraneLoadChartSnapshots");
        }
    }
}