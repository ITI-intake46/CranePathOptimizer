using CraneOptimizer.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CraneOptimizer.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(CraneDbContext))]
    [Migration("20260528181000_AddFullFidelityLoadChartMetadata")]
    public partial class AddFullFidelityLoadChartMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExactLoadChartRawGridJson",
                table: "CraneSpecifications",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoadChartCapacityPolicy",
                table: "CraneSpecifications",
                type: "TEXT",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoadChartSourceDescription",
                table: "CraneSpecifications",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoadChartStorageMode",
                table: "CraneSpecifications",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VisualColumnIndex",
                table: "TargetPoints",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VisualColumnHeader",
                table: "TargetPoints",
                type: "TEXT",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExactLoadChartRawGridJson",
                table: "CraneSpecifications");

            migrationBuilder.DropColumn(
                name: "LoadChartCapacityPolicy",
                table: "CraneSpecifications");

            migrationBuilder.DropColumn(
                name: "LoadChartSourceDescription",
                table: "CraneSpecifications");

            migrationBuilder.DropColumn(
                name: "LoadChartStorageMode",
                table: "CraneSpecifications");

            migrationBuilder.DropColumn(
                name: "VisualColumnIndex",
                table: "TargetPoints");

            migrationBuilder.DropColumn(
                name: "VisualColumnHeader",
                table: "TargetPoints");
        }
    }
}
