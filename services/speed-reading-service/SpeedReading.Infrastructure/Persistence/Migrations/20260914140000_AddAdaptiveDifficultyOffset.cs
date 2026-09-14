using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260914140000_AddAdaptiveDifficultyOffset")]
public partial class AddAdaptiveDifficultyOffset : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "AdaptiveDifficultyOffset",
            schema: "speed_reading",
            table: "student_program_progress",
            type: "integer",
            nullable: false,
            defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AdaptiveDifficultyOffset",
            schema: "speed_reading",
            table: "student_program_progress");
    }
}
