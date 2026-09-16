using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpeedReading.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OwnedSpeedReadingDbContext))]
[Migration("20260916104000_AddReadingWordCountSnapshots")]
public partial class AddReadingWordCountSnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "word_count_snapshot",
            schema: "speed_reading",
            table: "student_reading_attempts",
            type: "integer",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "word_count_snapshot",
            schema: "speed_reading",
            table: "student_reading_attempts");
    }
}
