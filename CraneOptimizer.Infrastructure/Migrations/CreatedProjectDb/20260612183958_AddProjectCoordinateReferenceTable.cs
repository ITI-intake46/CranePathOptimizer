using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CraneOptimizer.Infrastructure.Migrations.CreatedProjectDb
{
    /// <inheritdoc />
    public partial class AddProjectCoordinateReferenceTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectCoordinateReferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectInfoId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    CoordinateSystemName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    EPSGCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    LocalReferenceX = table.Column<double>(type: "REAL", nullable: false),
                    LocalReferenceY = table.Column<double>(type: "REAL", nullable: false),
                    LocalReferenceZ = table.Column<double>(type: "REAL", nullable: false),
                    SurveyEasting = table.Column<double>(type: "REAL", nullable: false),
                    SurveyNorthing = table.Column<double>(type: "REAL", nullable: false),
                    SurveyElevation = table.Column<double>(type: "REAL", nullable: false),
                    RotationToNorthDegrees = table.Column<double>(type: "REAL", nullable: false),
                    Scale = table.Column<double>(type: "REAL", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectCoordinateReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectCoordinateReferences_ProjectInfo_ProjectInfoId",
                        column: x => x.ProjectInfoId,
                        principalTable: "ProjectInfo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCoordinateReferences_ProjectInfoId",
                table: "ProjectCoordinateReferences",
                column: "ProjectInfoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectCoordinateReferences");
        }
    }
}
