using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260916103000_AddReadingSessionMeasurementStatus")]
public partial class AddReadingSessionMeasurementStatus : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "is_measured",
            schema: "speed_reading",
            table: "reading_sessions",
            type: "boolean",
            nullable: false,
            // Existing rows have no server-validated measurement marker. Keep
            // them out of measured analytics until a new completion writes it.
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "is_measured",
            schema: "speed_reading",
            table: "reading_sessions");
    }
}
