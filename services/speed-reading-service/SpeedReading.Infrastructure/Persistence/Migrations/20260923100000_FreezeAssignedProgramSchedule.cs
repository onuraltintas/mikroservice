using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260923100000_FreezeAssignedProgramSchedule")]
public sealed class FreezeAssignedProgramSchedule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(
            name: "ScheduleJson",
            schema: "speed_reading",
            table: "student_program_progress",
            type: "jsonb",
            nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(
            name: "ScheduleJson",
            schema: "speed_reading",
            table: "student_program_progress");
}
