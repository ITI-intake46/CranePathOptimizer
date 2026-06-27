using CraneOptimizer.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CraneOptimizer.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(CraneDbContext))]
    [Migration("20260609011000_AddLoadChartOperatingConditionLabel")]
    public partial class AddLoadChartOperatingConditionLabel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OperatingConditionLabel",
                table: "TargetPoints",
                type: "TEXT",
                maxLength: 240,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OperatingConditionLabel",
                table: "TargetPoints");
        }
    }
}