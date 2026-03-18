using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WPF_IFC_test1.Database;

#nullable disable

namespace WPF_IFC_Helix.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260311010000_CraneCategory")]
    public partial class CraneCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CraneCategory",
                table: "CraneLoadCharts",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Mobile");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CraneCategory",
                table: "CraneLoadCharts");
        }
    }
}
