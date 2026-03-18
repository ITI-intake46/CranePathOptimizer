using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WPF_IFC_test1.Migrations
{
    /// <inheritdoc />
    public partial class AddLoadChartFkToCrane : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LoadChartId",
                table: "Cranes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedConfig",
                table: "Cranes",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LoadChartId",
                table: "Cranes");

            migrationBuilder.DropColumn(
                name: "AssignedConfig",
                table: "Cranes");
        }
    }
}
