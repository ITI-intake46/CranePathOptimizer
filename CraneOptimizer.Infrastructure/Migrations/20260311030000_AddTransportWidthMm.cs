using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WPF_IFC_test1.Database;

#nullable disable

namespace WPF_IFC_Helix.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260311030000_AddTransportWidthMm")]
    public partial class AddTransportWidthMm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "TransportWidthMm",
                table: "CraneLoadCharts",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TransportWidthMm",
                table: "CraneLoadCharts");
        }
    }
}
