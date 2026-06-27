using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CraneOptimizer.Infrastructure.Migrations.CreatedProjectDb
{
    /// <inheritdoc />
    public partial class AddCanonicalAssemblyFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssemblyMark",
                table: "ProjectIfcElements",
                type: "TEXT",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssemblyMarkConfidence",
                table: "ProjectIfcElements",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AssemblyMarkIsExplicit",
                table: "ProjectIfcElements",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AssemblyMarkSource",
                table: "ProjectIfcElements",
                type: "TEXT",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExporterProfile",
                table: "ProjectIfcElements",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceSystem",
                table: "ProjectIfcElements",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidationStatus",
                table: "ProjectIfcElements",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidationWarningsJson",
                table: "ProjectIfcElements",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssemblyMark",
                table: "ProjectIfcElements");

            migrationBuilder.DropColumn(
                name: "AssemblyMarkConfidence",
                table: "ProjectIfcElements");

            migrationBuilder.DropColumn(
                name: "AssemblyMarkIsExplicit",
                table: "ProjectIfcElements");

            migrationBuilder.DropColumn(
                name: "AssemblyMarkSource",
                table: "ProjectIfcElements");

            migrationBuilder.DropColumn(
                name: "ExporterProfile",
                table: "ProjectIfcElements");

            migrationBuilder.DropColumn(
                name: "SourceSystem",
                table: "ProjectIfcElements");

            migrationBuilder.DropColumn(
                name: "ValidationStatus",
                table: "ProjectIfcElements");

            migrationBuilder.DropColumn(
                name: "ValidationWarningsJson",
                table: "ProjectIfcElements");
        }
    }
}
