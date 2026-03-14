using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CraneOptimizer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cranes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Manufacturer = table.Column<string>(type: "TEXT", nullable: false),
                    CraneType = table.Column<int>(type: "INTEGER", nullable: false),
                    VehicleLength = table.Column<double>(type: "REAL", nullable: false),
                    VehicleWidth = table.Column<double>(type: "REAL", nullable: false),
                    VehicleHeight = table.Column<double>(type: "REAL", nullable: false),
                    OutriggerLength = table.Column<double>(type: "REAL", nullable: true),
                    OutriggerWidth = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cranes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CraneSpecifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MaxCapacity = table.Column<double>(type: "REAL", nullable: false),
                    MaxRadius = table.Column<double>(type: "REAL", nullable: false),
                    MinRadius = table.Column<double>(type: "REAL", nullable: false),
                    MaxBoomLength = table.Column<double>(type: "REAL", nullable: false),
                    MinBoomLength = table.Column<double>(type: "REAL", nullable: false),
                    CounterWeight = table.Column<double>(type: "REAL", nullable: false),
                    MachineRotation = table.Column<double>(type: "REAL", nullable: true),
                    CraneId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CraneSpecifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CraneSpecifications_Cranes_CraneId",
                        column: x => x.CraneId,
                        principalTable: "Cranes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TargetPoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Radius = table.Column<double>(type: "REAL", nullable: false),
                    BoomLength = table.Column<double>(type: "REAL", nullable: false),
                    Capacity = table.Column<double>(type: "REAL", nullable: false),
                    CraneSpecificationId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TargetPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TargetPoints_CraneSpecifications_CraneSpecificationId",
                        column: x => x.CraneSpecificationId,
                        principalTable: "CraneSpecifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CraneSpecifications_CraneId",
                table: "CraneSpecifications",
                column: "CraneId");

            migrationBuilder.CreateIndex(
                name: "IX_TargetPoints_CraneSpecificationId",
                table: "TargetPoints",
                column: "CraneSpecificationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TargetPoints");

            migrationBuilder.DropTable(
                name: "CraneSpecifications");

            migrationBuilder.DropTable(
                name: "Cranes");
        }
    }
}
