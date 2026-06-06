using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CraneOptimizer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectFolderpathtoProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProjectFolderPath",
                table: "Projects",
                type: "TEXT",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProjectFolderPath",
                table: "Projects");
        }
    }
}
