using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WPF_IFC_test1.Database;

#nullable disable

namespace WPF_IFC_Helix.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260311020000_AddDeratingPct")]
    public partial class AddDeratingPct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeratingPct",
                table: "CraneLoadCharts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 100);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeratingPct",
                table: "CraneLoadCharts");
        }
    }
}
