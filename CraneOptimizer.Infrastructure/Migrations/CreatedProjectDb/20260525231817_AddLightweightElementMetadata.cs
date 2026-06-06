using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CraneOptimizer.Infrastructure.Migrations.CreatedProjectDb
{
    /// <inheritdoc />
    public partial class AddLightweightElementMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChildGlobalIdsJson",
                table: "ProjectIfcElements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractionWarningsJson",
                table: "ProjectIfcElements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HiddenInternalGeometryOwnerGlobalIdsJson",
                table: "ProjectIfcElements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeInElementTree",
                table: "ProjectIfcElements",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeInGa",
                table: "ProjectIfcElements",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeInRender",
                table: "ProjectIfcElements",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NormalizationDecisionReason",
                table: "ProjectIfcElements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentGlobalId",
                table: "ProjectIfcElements",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawIfcType",
                table: "ProjectIfcElements",
                type: "TEXT",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolvedCategory",
                table: "ProjectIfcElements",
                type: "TEXT",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "ProjectIfcElements",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VisibleGeometryOwnerGlobalIdsJson",
                table: "ProjectIfcElements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeightSource",
                table: "ProjectIfcElements",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeightTrustLevel",
                table: "ProjectIfcElements",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChildGlobalIdsJson",
                table: "ProjectIfcElements");

            migrationBuilder.DropColumn(
                name: "ExtractionWarningsJson",
                table: "ProjectIfcElements");

            migrationBuilder.DropColumn(
                name: "HiddenInternalGeometryOwnerGlobalIdsJson",
                table: "ProjectIfcElements");

            migrationBuilder.DropColumn(
                name: "IncludeInElementTree",
                table: "ProjectIfcElements");

            migrationBuilder.DropColumn(
                name: "IncludeInGa",
                table: "ProjectIfcElements");

            migrationBuilder.DropColumn(
                name: "IncludeInRender",
                table: "ProjectIfcElements");

            migrationBuilder.DropColumn(
                name: "NormalizationDecisionReason",
                table: "ProjectIfcElements");

            migrationBuilder.DropColumn(
                name: "ParentGlobalId",
                table: "ProjectIfcElements");

            migrationBuilder.DropColumn(
                name: "RawIfcType",
                table: "ProjectIfcElements");

            migrationBuilder.DropColumn(
                name: "ResolvedCategory",
                table: "ProjectIfcElements");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "ProjectIfcElements");

            migrationBuilder.DropColumn(
                name: "VisibleGeometryOwnerGlobalIdsJson",
                table: "ProjectIfcElements");

            migrationBuilder.DropColumn(
                name: "WeightSource",
                table: "ProjectIfcElements");

            migrationBuilder.DropColumn(
                name: "WeightTrustLevel",
                table: "ProjectIfcElements");
        }
    }
}
