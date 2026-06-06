using CraneOptimizer.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CraneOptimizer.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(CraneDbContext))]
    [Migration("20260603120000_AddProjectConcreteDensity")]
    public partial class AddProjectConcreteDensity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ConcreteDensityKgPerM3",
                table: "Projects",
                type: "REAL",
                nullable: false,
                defaultValue: 2500.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConcreteDensityKgPerM3",
                table: "Projects");
        }
    }
}
