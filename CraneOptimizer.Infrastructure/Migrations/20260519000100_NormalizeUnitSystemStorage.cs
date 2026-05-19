using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CraneOptimizer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeUnitSystemStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DisplayAreaUnit",
                table: "Projects",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DisplayLengthUnit",
                table: "Projects",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "DisplayMassUnit",
                table: "Projects",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "UnitDecimalPlaces",
                table: "Projects",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.Sql("UPDATE Cranes SET VehicleLength = VehicleLength / 1000.0 WHERE VehicleLength > 100.0;");
            migrationBuilder.Sql("UPDATE Cranes SET VehicleWidth = VehicleWidth / 1000.0 WHERE VehicleWidth > 100.0;");
            migrationBuilder.Sql("UPDATE Cranes SET VehicleHeight = VehicleHeight / 1000.0 WHERE VehicleHeight > 100.0;");
            migrationBuilder.Sql("UPDATE Cranes SET OutriggerLength = OutriggerLength / 1000.0 WHERE OutriggerLength > 100.0;");
            migrationBuilder.Sql("UPDATE Cranes SET OutriggerWidth = OutriggerWidth / 1000.0 WHERE OutriggerWidth > 100.0;");
            migrationBuilder.Sql("UPDATE CraneSpecifications SET MaxCapacity = MaxCapacity / 1000.0 WHERE MaxCapacity > 100.0;");
            migrationBuilder.Sql("UPDATE CraneSpecifications SET CounterWeight = CounterWeight / 1000.0 WHERE CounterWeight > 100.0;");
            migrationBuilder.Sql("UPDATE TargetPoints SET Capacity = Capacity / 1000.0 WHERE Capacity > 100.0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayAreaUnit",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DisplayLengthUnit",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DisplayMassUnit",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "UnitDecimalPlaces",
                table: "Projects");
        }
    }
}
