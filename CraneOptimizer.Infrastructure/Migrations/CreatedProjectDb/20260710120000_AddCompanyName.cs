using CraneOptimizer.Infrastructure.Contexts.CreatedProject;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CraneOptimizer.Infrastructure.Migrations.CreatedProjectDb
{
    /// <inheritdoc />
    [DbContext(typeof(CreatedProjectDbContext))]
    [Migration("20260710120000_AddCompanyName")]
    public partial class AddCompanyName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "ProjectInfo",
                type: "TEXT",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "ProjectInfo");
        }
    }
}
