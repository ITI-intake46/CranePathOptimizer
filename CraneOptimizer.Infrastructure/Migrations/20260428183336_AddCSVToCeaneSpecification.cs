using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CraneOptimizer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCSVToCeaneSpecification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CraneDerating",
                table: "CraneSpecifications",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutriggerConfig",
                table: "CraneSpecifications",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CraneDerating",
                table: "CraneSpecifications");

            migrationBuilder.DropColumn(
                name: "OutriggerConfig",
                table: "CraneSpecifications");
        }
    }
}
