using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CraneOptimizer.Infrastructure.Migrations.CreatedProjectDb
{
    /// <inheritdoc />
    public partial class AddProjectOptionalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "ProjectInfo",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SiteLocation",
                table: "ProjectInfo",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectImagePath",
                table: "ProjectInfo",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Country",
                table: "ProjectInfo");

            migrationBuilder.DropColumn(
                name: "SiteLocation",
                table: "ProjectInfo");

            migrationBuilder.DropColumn(
                name: "ProjectImagePath",
                table: "ProjectInfo");
        }
    }
}
