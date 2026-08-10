using CraneOptimizer.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CraneOptimizer.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(CraneDbContext))]
    [Migration("20260710120000_AddCompanyNameToProject")]
    public partial class AddCompanyNameToProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "Projects",
                type: "TEXT",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "Projects");
        }
    }
}
